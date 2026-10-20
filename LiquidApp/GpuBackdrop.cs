using System;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using SoundCloud.Controls;
using XamlTemplates.FX;

// Render directly to a DirectComposition HWND. The previous WPF D3DImage
// presenter introduced another asynchronously updated surface during movement.
internal sealed class BackdropBlur : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { internal int Left, Top, Right, Bottom; }
    delegate IntPtr Procedure(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct WindowClass
    {
        internal uint Style; internal Procedure Proc; internal int ClassExtra, WindowExtra;
        internal IntPtr Instance, Icon, Cursor, Background; internal string Menu, Name;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClass(ref WindowClass cls);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(int ex, string cls, string text, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll")] static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool ValidateRect(IntPtr hwnd, IntPtr rect);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint value);
    [DllImport("user32.dll")] static extern IntPtr BeginDeferWindowPos(int count);
    [DllImport("user32.dll")] static extern IntPtr DeferWindowPos(IntPtr batch, IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern bool EndDeferWindowPos(IntPtr batch);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    static readonly Procedure proc = delegate(IntPtr hwnd, uint message, IntPtr w, IntPtr l) {
        if (message == 0x84) return new IntPtr(-1);
        if (message == 0x14) return new IntPtr(1);
        if (message == 0x0F) { ValidateRect(hwnd, IntPtr.Zero); return IntPtr.Zero; }
        return DefWindowProc(hwnd, message, w, l);
    };
    internal static IntPtr CaptureSourceOverride;
    readonly Window window;
    readonly IntPtr owner;
    IntPtr panel;
    GlassContext context;
    GlassOptions options;
    GlslShaderEffectParameters effect;
    readonly GlassButtonChrome[] buttons = new GlassButtonChrome[4];
    readonly Vector4[] lensBounds = new Vector4[4];
    bool geometryQueued;
    RecordableBackdropSource captureSource;
    uint fps;
    bool disposed, positioning;
    int width, height;
    Exception error;
    internal bool IsApplied { get { return context != null && !context.GetDiagnostics().UsingFallback; } }

    internal BackdropBlur(Window window)
    {
        this.window = window;
        owner = new WindowInteropHelper(window).Handle;
        fps = FrameSynchronizedDrag.RefreshRate(window);
        try
        {
            var module = GetModuleHandle(null);
            var cls = new WindowClass { Name = "SoundCloudDirectGlass", Proc = proc, Instance = module };
            RegisterClass(ref cls);
            panel = CreateWindowEx(0x082000A0, cls.Name, "", unchecked((int)0x80000000), 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, module, IntPtr.Zero);
            if (panel == IntPtr.Zero) throw new InvalidOperationException("Could not create the composition surface.");
            if (CaptureSourceOverride == IntPtr.Zero) captureSource = new RecordableBackdropSource(owner);
            Rect bounds;
            if (!GetWindowRect(owner, out bounds)) throw new InvalidOperationException("Could not read the application bounds.");
            width = bounds.Right - bounds.Left; height = bounds.Bottom - bounds.Top;
            SetWindowPos(panel, owner, bounds.Left, bounds.Top, width, height, 0x0010);
            options = GlassOptions.CreateDefault();
            effect = new GlslShaderEffectParameters(Path.Combine(AppContext.BaseDirectory, "Shaders", "LiquidControls.glsl"));
            effect.TintStrength = 0;
            string[] names = { "Download", "Browse", "Minimize", "CloseWindow" };
            for (int i = 0; i < names.Length; i++) {
                var button = (Button)window.FindName(names[i]);
                button.ApplyTemplate();
                buttons[i] = (GlassButtonChrome)button.Template.FindName("Chrome", button);
                buttons[i].OpticalBoundsChanged += OnOpticalBounds;
                buttons[i].SizeChanged += OnSize;
            }
            SyncLensGeometry();
            options.Effect = effect;
            options.CornerRadius = 32;
            options.FpsCap = fps;
            options.CaptureSourceWindow = captureSource == null ? CaptureSourceOverride : captureSource.Handle;
            context = GlassContext.CreateForHwnd(panel, options);
            context.Resize((uint)width, (uint)height, Scale());
            context.Start();
            Align();
        }
        catch (Exception failure)
        {
            error = failure;
            ReleaseNative();
        }
        window.LocationChanged += OnPosition;
        window.SizeChanged += OnSize;
        window.StateChanged += OnPosition;
        window.Activated += OnPosition;
        window.IsVisibleChanged += OnVisibility;
        window.Closed += OnClosed;
    }
    float Scale()
    {
        var source = PresentationSource.FromVisual(window);
        return source == null ? 1 : (float)source.CompositionTarget.TransformToDevice.M11;
    }
    void OnPosition(object sender, EventArgs e) { Align(); }
    void OnSize(object sender, SizeChangedEventArgs e) { Align(); QueueLensGeometry(); }
    void OnOpticalBounds(object sender, EventArgs e) { QueueLensGeometry(); }
    void QueueLensGeometry() {
        if (disposed || geometryQueued) return;
        geometryQueued = true;
        window.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => {
            geometryQueued = false;
            if (!disposed && SyncLensGeometry() && context != null) context.UpdateOptions(options, false);
        }));
    }
    bool SyncLensGeometry() {
        bool changed = false;
        float scale = Scale();
        effect.Set("dpiScale", scale);
        for (int i = 0; i < buttons.Length; i++) {
            var button = buttons[i];
            if (button == null || button.ActualWidth < 4 || button.ActualHeight < 4) continue;
            var local = new System.Windows.Rect(1.4, 1.4, button.ActualWidth - 2.8, button.ActualHeight - 3.8);
            var rect = button.TransformToAncestor(window).TransformBounds(local);
            var value = new Vector4((float)rect.X * scale, (float)rect.Y * scale, (float)rect.Width * scale, (float)rect.Height * scale);
            if (value != lensBounds[i]) { lensBounds[i] = value; changed = true; }
            effect.Set("button" + i, value);
        }
        return changed;
    }
    void OnVisibility(object sender, DependencyPropertyChangedEventArgs e) { Align(); }
    void OnClosed(object sender, EventArgs e) { Dispose(); }
    void Align()
    {
        if (disposed || positioning || panel == IntPtr.Zero || context == null) return;
        if (window.WindowState == WindowState.Minimized || !window.IsVisible) { ShowWindow(panel, 0); return; }
        Rect bounds;
        if (!GetWindowRect(owner, out bounds)) return;
        int w = bounds.Right - bounds.Left, h = bounds.Bottom - bounds.Top;
        SetWindowPos(panel, owner, bounds.Left, bounds.Top, w, h, 0x0050);
        if (width != w || height != h) { width = w; height = h; context.Resize((uint)w, (uint)h, Scale()); }
        context.NotifyWindowMoved();
    }
    internal bool MoveTogether(int x, int y)
    {
        if (disposed || panel == IntPtr.Zero || context == null) return SetWindowPos(owner, IntPtr.Zero, x, y, 0, 0, 0x0015);
        positioning = true;
        bool moved = false;
        try
        {
            IntPtr batch = BeginDeferWindowPos(2);
            if (batch != IntPtr.Zero) batch = DeferWindowPos(batch, owner, IntPtr.Zero, x, y, 0, 0, 0x0015);
            if (batch != IntPtr.Zero) batch = DeferWindowPos(batch, panel, owner, x, y, 0, 0, 0x0011);
            if (batch != IntPtr.Zero) moved = EndDeferWindowPos(batch);
        }
        finally { positioning = false; }
        if (!moved) { moved = SetWindowPos(owner, IntPtr.Zero, x, y, 0, 0, 0x0015); Align(); }
        else context.NotifyWindowMoved();
        return moved;
    }
    internal string Diagnostics()
    {
        var report = new StringBuilder();
        report.AppendLine("Presenter: DirectComposition (no WPF backbuffer)");
        report.AppendLine("FrameCap: " + fps);
        report.AppendLine("Error: " + error);
        report.AppendLine("DirectPanelAttached: " + (panel != IntPtr.Zero));
        GetWindowDisplayAffinity(owner, out uint ownerAffinity);
        GetWindowDisplayAffinity(panel, out uint panelAffinity);
        report.AppendLine("CaptureAffinity: UI=" + ownerAffinity + ", Glass=" + panelAffinity);
        for (int i = 0; i < lensBounds.Length; i++) report.AppendLine("ButtonLens" + i + ": " + lensBounds[i]);
        if (context != null)
        {
            var value = context.GetDiagnostics();
            foreach (var property in value.GetType().GetProperties()) report.AppendLine(property.Name + ": " + property.GetValue(value));
            report.AppendLine("ShaderError: " + context.GetLastShaderError());
        }
        return report.ToString();
    }
    void ReleaseNative()
    {
        if (context != null) { context.Dispose(); context = null; }
        if (panel != IntPtr.Zero) { DestroyWindow(panel); panel = IntPtr.Zero; }
        if (captureSource != null) { captureSource.Dispose(); captureSource = null; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        window.LocationChanged -= OnPosition; window.SizeChanged -= OnSize;
        window.StateChanged -= OnPosition; window.Activated -= OnPosition;
        window.IsVisibleChanged -= OnVisibility; window.Closed -= OnClosed;
        foreach (var button in buttons) if (button != null) { button.OpticalBoundsChanged -= OnOpticalBounds; button.SizeChanged -= OnSize; }
        ReleaseNative();
    }
}
