using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

// Coalesce mouse input to one atomic HWND move per presentation frame. Unlike
// DragMove, this does not enter the native modal move loop or starve the WPF
// presenter that supplies the refracted desktop image.
internal sealed class FrameSynchronizedDrag : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct DisplayMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device;
        internal ushort Spec, Driver, Size, Extra;
        internal uint Fields;
        internal int X, Y;
        internal uint Orientation, FixedOutput;
        internal short Color, Duplex, Resolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Form;
        internal ushort LogPixels;
        internal uint Bits, Width, Height, Flags, Frequency, IcmMethod, IcmIntent, Media, Dither, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string device, int modeNumber, ref DisplayMode mode);
    readonly Window window;
    readonly Action<bool> dragState;
    readonly Func<int, int, bool> moveTogether;
    IntPtr hwnd;
    NativePoint cursorOrigin;
    NativeRect origin;
    bool dragging, disposed;
    int previousX, previousY;
    TimeSpan previousFrame = TimeSpan.MinValue;
    internal FrameSynchronizedDrag(Window window, Action<bool> dragState, Func<int, int, bool> moveTogether)
    {
        this.window = window;
        this.dragState = dragState;
        this.moveTogether = moveTogether;
        window.PreviewMouseLeftButtonDown += OnDown;
        window.PreviewMouseLeftButtonUp += OnUp;
        window.LostMouseCapture += OnCaptureLost;
        window.PreviewKeyDown += OnKey;
        window.Deactivated += OnDeactivated;
        window.Closed += OnClosed;
    }
    internal static uint RefreshRate(Window window)
    {
        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle);
        var mode = new DisplayMode { Size = (ushort)Marshal.SizeOf(typeof(DisplayMode)) };
        if (!EnumDisplaySettings(screen.DeviceName, -1, ref mode) || mode.Frequency < 30) return 60;
        return NormalizeRefreshRate(mode.Frequency);
    }
    internal static uint NormalizeRefreshRate(uint frequency)
    {
        if (frequency < 30) return 60;
        // DEVMODE rounds common fractional modes down (59.94 -> 59). Using
        // that integer as a strict cap introduces a periodic missed frame.
        foreach (uint nominal in new uint[] { 60, 75, 90, 100, 120, 144, 165, 180, 200, 240 })
            if (frequency == nominal - 1) return nominal;
        return Math.Min(240u, frequency);
    }
    internal static bool Interactive(DependencyObject value)
    {
        while (value != null)
        {
            // Popup items route input back through their owning ComboBox. Do not
            // capture that click for window dragging before selection can run.
            if (value is ButtonBase || value is TextBoxBase || value is ScrollBar || value is Selector || value is System.Windows.Controls.ComboBoxItem) return true;
            if (value is Visual || value is System.Windows.Media.Media3D.Visual3D) value = VisualTreeHelper.GetParent(value);
            else if (value is FrameworkContentElement) value = ((FrameworkContentElement)value).Parent;
            else break;
        }
        return false;
    }
    void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (disposed || dragging || e.ChangedButton != MouseButton.Left || Interactive(e.OriginalSource as DependencyObject)) return;
        hwnd = new WindowInteropHelper(window).Handle;
        if (!GetCursorPos(out cursorOrigin) || !GetWindowRect(hwnd, out origin)) return;
        if (!window.CaptureMouse()) return;
        previousX = origin.Left; previousY = origin.Top;
        previousFrame = TimeSpan.MinValue;
        dragging = true;
        dragState(true);
        CompositionTarget.Rendering += OnFrame;
        e.Handled = true;
    }
    void OnFrame(object sender, EventArgs e)
    {
        if (!dragging) return;
        var frame = e as RenderingEventArgs;
        if (frame != null && frame.RenderingTime == previousFrame) return;
        if (frame != null) previousFrame = frame.RenderingTime;
        if (Mouse.LeftButton != MouseButtonState.Pressed) { End(false); return; }
        UpdatePosition();
    }
    void UpdatePosition()
    {
        NativePoint cursor;
        if (!GetCursorPos(out cursor)) return;
        int x = origin.Left + cursor.X - cursorOrigin.X;
        int y = origin.Top + cursor.Y - cursorOrigin.Y;
        if (x == previousX && y == previousY) return;
        // A single native call changes both coordinates and emits one position
        // notification to the capture renderer, including on diagonal moves.
        if (moveTogether(x, y)) { previousX = x; previousY = y; }
    }
    void OnUp(object sender, MouseButtonEventArgs e) { if (dragging && e.ChangedButton == MouseButton.Left) { End(false); e.Handled = true; } }
    void OnCaptureLost(object sender, MouseEventArgs e) { End(false); }
    void OnDeactivated(object sender, EventArgs e) { End(false); }
    void OnKey(object sender, KeyEventArgs e) { if (dragging && e.Key == Key.Escape) { End(true); e.Handled = true; } }
    void OnClosed(object sender, EventArgs e) { Dispose(); }
    void End(bool cancel)
    {
        if (!dragging) return;
        if (!cancel) UpdatePosition();
        dragging = false;
        CompositionTarget.Rendering -= OnFrame;
        if (cancel) moveTogether(origin.Left, origin.Top);
        if (window.IsMouseCaptured) window.ReleaseMouseCapture();
        dragState(false);
    }
    public void Dispose()
    {
        if (disposed) return;
        End(false);
        disposed = true;
        window.PreviewMouseLeftButtonDown -= OnDown;
        window.PreviewMouseLeftButtonUp -= OnUp;
        window.LostMouseCapture -= OnCaptureLost;
        window.PreviewKeyDown -= OnKey;
        window.Deactivated -= OnDeactivated;
        window.Closed -= OnClosed;
    }
}
