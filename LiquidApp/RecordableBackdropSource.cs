using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// A separate, non-activating DWM scene supplies live windows behind the app.
// It never includes the app itself, so no display-affinity restriction is needed.
internal sealed class RecordableBackdropSource : IDisposable
{
    readonly Process process;
    internal IntPtr Handle { get; }
    internal RecordableBackdropSource(IntPtr owner)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add("--backdrop-source");
        start.ArgumentList.Add(owner.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
        process = Process.Start(start);
        var ready = process.StandardOutput.ReadLineAsync();
        if (!ready.Wait(4000) || !long.TryParse(ready.Result, out long handle) || handle == 0) {
            Dispose();
            throw new InvalidOperationException("The recordable backdrop source did not start.");
        }
        Handle = new IntPtr(handle);
    }
    public void Dispose() {
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
        process.Dispose();
    }
    internal static void Run(IntPtr owner) {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using (var scene = new Scene(owner)) Application.Run(scene);
    }

    sealed class Scene : Form
    {
        [StructLayout(LayoutKind.Sequential)] struct Rect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct ThumbnailProperties {
            internal uint Flags; internal Rect Destination, Source; internal byte Opacity;
            [MarshalAs(UnmanagedType.Bool)] internal bool Visible;
            [MarshalAs(UnmanagedType.Bool)] internal bool ClientOnly;
        }
        delegate bool EnumProc(IntPtr hwnd, IntPtr param);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
        [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attr, out int value, int size);
        [DllImport("dwmapi.dll")] static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
        [DllImport("dwmapi.dll")] static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")] static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref ThumbnailProperties properties);
        readonly IntPtr owner;
        readonly uint ownerProcess;
        readonly Timer timer = new Timer { Interval = 80 };
        readonly Dictionary<IntPtr, IntPtr> thumbnails = new Dictionary<IntPtr, IntPtr>();
        List<IntPtr> order = new List<IntPtr>();
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x080000A0; return cp; }
        }
        internal Scene(IntPtr owner) {
            this.owner = owner;
            GetWindowThreadProcessId(owner, out ownerProcess);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            BackColor = Color.FromArgb(32, 36, 42);
            Text = "SoundCloud backdrop source";
            timer.Tick += delegate { if (!IsWindow(owner)) Close(); else Synchronize(); };
        }
        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            SetWindowPos(Handle, new IntPtr(1), Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, 0x0010);
            Synchronize();
            Console.Out.WriteLine(Handle.ToInt64());
            Console.Out.Flush();
            timer.Start();
        }
        void Synchronize() {
            var desktop = new List<IntPtr>();
            var behind = new List<IntPtr>();
            bool below = false;
            EnumWindows((hwnd, _) => {
                if (hwnd == owner) { below = true; return true; }
                if (hwnd == Handle || !IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == ownerProcess || pid == Environment.ProcessId) return true;
                if (GetWindowDisplayAffinity(hwnd, out uint affinity) && affinity != 0) return true;
                DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4);
                if (cloaked != 0 || !GetWindowRect(hwnd, out Rect rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top) return true;
                var cls = new StringBuilder(80); GetClassName(hwnd, cls, cls.Capacity);
                if (cls.ToString() == "Progman" || cls.ToString() == "WorkerW") desktop.Add(hwnd);
                else if (below) behind.Add(hwnd);
                return true;
            }, IntPtr.Zero);
            // DWM draws thumbnail relationships in registration order, bottom first.
            desktop.Reverse(); behind.Reverse();
            var next = desktop.Concat(behind).ToList();
            if (!next.SequenceEqual(order)) {
                foreach (var item in thumbnails.Values) DwmUnregisterThumbnail(item);
                thumbnails.Clear(); order = next;
                foreach (var hwnd in order) if (DwmRegisterThumbnail(Handle, hwnd, out IntPtr item) == 0) thumbnails[hwnd] = item;
            }
            foreach (var item in thumbnails) {
                if (!GetWindowRect(item.Key, out Rect rect)) continue;
                rect.Left -= Left; rect.Right -= Left; rect.Top -= Top; rect.Bottom -= Top;
                var properties = new ThumbnailProperties { Flags = 1 | 4 | 8 | 16, Destination = rect, Opacity = 255, Visible = true, ClientOnly = false };
                DwmUpdateThumbnailProperties(item.Value, ref properties);
            }
            // Stay beneath real windows and never take keyboard or mouse focus.
            SetWindowPos(Handle, new IntPtr(1), 0, 0, 0, 0, 0x0013);
        }
        protected override void WndProc(ref Message message) {
            if (message.Msg == 0x84) { message.Result = new IntPtr(-1); return; }
            if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; }
            base.WndProc(ref message);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) {
                timer.Dispose();
                foreach (var item in thumbnails.Values) DwmUnregisterThumbnail(item);
                thumbnails.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
