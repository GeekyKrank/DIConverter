using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

internal static class DesktopGlassProgram
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool SetThreadPreferredUILanguages(uint flags, string languages, out uint count);
    [STAThread]
    static void Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        uint languageCount;
        SetThreadPreferredUILanguages(8, "en-US\0\0", out languageCount);
        try
        {
            if (args.Length == 2 && args[0] == "--backdrop-source") { RecordableBackdropSource.Run(new IntPtr(long.Parse(args[1]))); return; }
            if (args.Length == 1 && args[0] == "--motion-scene") { MotionTestScene.Run(); return; }
            if (args.Length >= 2 && args[0] == "--test-source") BackdropBlur.CaptureSourceOverride = new IntPtr(long.Parse(args[1]));
            if (args.Length == 1 && args[0] == "--renderer-api")
            {
                var api = new StringBuilder();
                foreach (string name in new[] { "XamlTemplates.FX.Wpf", "XamlTemplates.FX" }) {
                    var assembly = Assembly.Load(name);
                    foreach (var type in assembly.GetExportedTypes()) {
                        if (type.Name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) < 0 && type.Name.IndexOf("Diagnostic", StringComparison.OrdinalIgnoreCase) < 0 && type.Name.IndexOf("Shader", StringComparison.OrdinalIgnoreCase) < 0 && type.Name.IndexOf("Glsl", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        api.AppendLine(type.FullName);
                        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) api.AppendLine("  " + property.PropertyType.Name + " " + property.Name + " Set=" + property.SetMethod);
                        foreach (var constructor in type.GetConstructors()) api.AppendLine("  " + constructor);
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) api.AppendLine("  " + method);
                        if (type.Name.IndexOf("Context", StringComparison.OrdinalIgnoreCase) >= 0 || type.Name == "GlslShaderEffectParameters") {
                            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) api.AppendLine("  " + method + " " + string.Join(", ", method.GetParameters().Select(p => p.Name)));
                            foreach (var member in type.GetEvents()) api.AppendLine("  event " + member.EventHandlerType + " " + member.Name);
                        }
                    }
                }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "renderer-api.txt"), api.ToString());
                return;
            }
            if (args.Length == 1 && args[0] == "--style-preview")
            {
                var previewApp = new Application();
                Window preview;
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GlassWindow.xaml")) preview = (Window)XamlReader.Load(resource);
                preview.Background = new SolidColorBrush(Color.FromRgb(25, 28, 34));
                foreach (string name in new[] { "MediaFormat", "MediaQuality" }) {
                    var combo = (ComboBox)preview.FindName(name);
                    combo.ItemsSource = name == "MediaFormat" ? new[] { "MP3", "M4A", "WAV" } : new[] { "Best audio" };
                    combo.SelectedIndex = 0;
                    if (name == "MediaQuality") combo.IsEnabled = false;
                }
                preview.Show();
                preview.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)preview.Width, (int)preview.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(preview);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "style-preview.png"))) encoder.Save(stream);
                var choices = (ComboBox)preview.FindName("MediaFormat");
                choices.IsDropDownOpen = true;
                choices.UpdateLayout();
                var popup = (System.Windows.Controls.Primitives.Popup)choices.Template.FindName("PART_Popup", choices);
                popup.Child.UpdateLayout();
                var menu = (FrameworkElement)popup.Child;
                if (menu.ActualWidth < 1 || menu.ActualHeight < 1) throw new Exception("Dropdown did not open.");
                bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth), (int)Math.Ceiling(menu.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(menu);
                encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using (var stream = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dropdown-preview.png"))) encoder.Save(stream);
                choices.IsDropDownOpen = false;
                var modeChoices = (ComboBox)preview.FindName("MediaMode");
                modeChoices.IsDropDownOpen = true;
                modeChoices.UpdateLayout();
                var videoItem = (ComboBoxItem)modeChoices.ItemContainerGenerator.ContainerFromIndex(1);
                videoItem.ApplyTemplate();
                videoItem.UpdateLayout();
                var hit = videoItem.InputHitTest(new Point(videoItem.ActualWidth / 2, videoItem.ActualHeight / 2)) as DependencyObject;
                if (hit == null || !FrameSynchronizedDrag.Interactive(hit)) throw new Exception("Video item click would be intercepted by window dragging.");
                using (var inputCheck = new FrameSynchronizedDrag(preview, moving => { if (moving) throw new Exception("Dropdown started window dragging."); }, (x, y) => true)) {
                    videoItem.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                }
                videoItem.IsSelected = true;
                if (modeChoices.SelectedIndex != 1) throw new Exception("Video selection failed.");
                modeChoices.IsDropDownOpen = false;
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "interaction-check.txt"), "Video popup hit target bypasses window dragging. Routed mouse input preserved. Video selection passed.");
                preview.Close();
                return;
            }
            if (args.Length == 1 && args[0] == "--self-test")
            {
                if (FrameSynchronizedDrag.NormalizeRefreshRate(59) != 60 || FrameSynchronizedDrag.NormalizeRefreshRate(119) != 120 || FrameSynchronizedDrag.NormalizeRefreshRate(144) != 144 || FrameSynchronizedDrag.NormalizeRefreshRate(0) != 60) Environment.Exit(4);
                if (!MediaUrl.ValidUrl("https://soundcloud.com/artist/track") || MediaUrl.ValidUrl("https://example.com/track")) Environment.Exit(1);
                if (!MediaUrl.ValidUrl("https://www.youtube.com/watch?v=abcdefghijk") || !MediaUrl.ValidUrl("https://youtu.be/abcdefghijk") || !MediaUrl.ValidUrl("https://youtube.com/shorts/abcdefghijk") || MediaUrl.ValidUrl("https://youtube.com/playlist?list=test") || MediaUrl.ValidUrl("https://youtube.com.evil.test/watch?v=test")) Environment.Exit(5);
                using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GlassWindow.xaml"))
                    if (!(XamlReader.Load(resource) is Window)) Environment.Exit(2);
                return;
            }
            var app = new Application();
            var controller = new DesktopGlassController(app);
            if (args.Contains("--material-check"))
            {
                var check = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                check.Tick += delegate {
                    check.Stop();
                    var report = new StringBuilder();
                    report.AppendLine(controller.RendererDiagnostics());
                    var brand = (TextBlock)app.MainWindow.FindName("BrandText");
                    GlyphTypeface glyph;
                    bool loaded = new Typeface(brand.FontFamily, brand.FontStyle, brand.FontWeight, brand.FontStretch).TryGetGlyphTypeface(out glyph);
                    report.AppendLine("SpecialGothicExpandedOneLoaded: " + (loaded && glyph.FamilyNames.Values.Contains("Special Gothic Expanded One")));
                    report.AppendLine("CyrillicSupported: " + (loaded && glyph.CharacterToGlyphMap.ContainsKey(0x0410)));
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "material-check.txt"), report.ToString());
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)app.MainWindow.ActualWidth, (int)app.MainWindow.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(app.MainWindow);
                    var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "material-preview.png"))) png.Save(stream);
                };
                check.Start();
            }
            app.Run();
        }
        catch (Exception error) {
            if (args.Length == 1 && (args[0] == "--self-test" || args[0] == "--style-preview")) { Console.Error.WriteLine(error); Environment.Exit(3); }
            MessageBox.Show(error.ToString(), "Startup error");
        }
    }
}

internal sealed class DesktopGlassController
{
    readonly Window window;
    readonly TextBox link;
    readonly TextBlock folder, status;
    readonly Button browse, download;
    readonly ProgressBar progress;
    readonly ComboBox mode, format, quality;
    readonly string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundCloudDownloader", "folder.txt");
    bool busy;
    BackdropBlur backdrop;
    LiquidHighlights highlights;
    FrameSynchronizedDrag drag;
    internal DesktopGlassController(Application app)
    {
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("GlassWindow.xaml")) window = (Window)XamlReader.Load(resource);
        // Always use per-pixel alpha. Selecting the opaque HWND path based on
        // a Windows preference made the desktop disappear on some systems.
        window.AllowsTransparency = true;
        app.MainWindow = window;
        link = (TextBox)window.FindName("Link");
        folder = (TextBlock)window.FindName("Folder");
        status = (TextBlock)window.FindName("Status");
        browse = (Button)window.FindName("Browse");
        download = (Button)window.FindName("Download");
        progress = (ProgressBar)window.FindName("Progress");
        mode = (ComboBox)window.FindName("MediaMode");
        format = (ComboBox)window.FindName("MediaFormat");
        quality = (ComboBox)window.FindName("MediaQuality");
        mode.SelectionChanged += delegate { UpdateOptions(); };
        UpdateOptions();
        folder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        try { if (File.Exists(settings) && Directory.Exists(File.ReadAllText(settings))) folder.Text = File.ReadAllText(settings); } catch { }
        folder.ToolTip = folder.Text;
        link.TextChanged += delegate { ((TextBlock)window.FindName("Placeholder")).Visibility = link.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
        var linkSurface = (Border)window.FindName("LinkSurface");
        link.GotKeyboardFocus += delegate { linkSurface.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 235, 239, 250)); };
        link.LostKeyboardFocus += delegate { linkSurface.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)); };
        browse.Click += delegate {
            using (var picker = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = folder.Text, Description = "Choose a folder for your tracks", UseDescriptionForTitle = true })
                if (picker.ShowDialog(new NativeOwner(new WindowInteropHelper(window).Handle)) == System.Windows.Forms.DialogResult.OK)
                {
                    folder.Text = picker.SelectedPath;
                    folder.ToolTip = folder.Text;
                    try { Directory.CreateDirectory(Path.GetDirectoryName(settings)); File.WriteAllText(settings, folder.Text); } catch { }
                }
        };
        download.Click += async delegate { await Download(); };
        ((Button)window.FindName("CloseWindow")).Click += delegate { window.Close(); };
        ((Button)window.FindName("Minimize")).Click += delegate { window.WindowState = WindowState.Minimized; };
        window.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (busy) { e.Cancel = true; status.Text = "Please wait for the download to finish."; } };
        var material = (Grid)window.FindName("MaterialRoot");
        highlights = new LiquidHighlights(window);
        material.Children.Add(highlights);
        drag = new FrameSynchronizedDrag(window, delegate(bool moving) {
            highlights.Suspend(moving);
            material.CacheMode = moving ? new BitmapCache() : null;
        }, delegate(int x, int y) { return backdrop != null && backdrop.MoveTogether(x, y); });
        Action clip = delegate { material.Clip = new RectangleGeometry(new Rect(0, 0, window.ActualWidth, window.ActualHeight), 32, 32); };
        window.SizeChanged += delegate { clip(); };
        window.Loaded += delegate { clip(); backdrop = new BackdropBlur(window); };
        window.Show();
    }
    internal string RendererDiagnostics() { return backdrop == null ? "Renderer not attached" : backdrop.Diagnostics(); }

    async Task Download()
    {
        if (busy) return;
        string url = link.Text.Trim();
        if (!MediaUrl.ValidUrl(url)) { status.Text = "Enter a YouTube video or SoundCloud track link."; return; }
        string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe");
        if (!File.Exists(exe)) { status.Text = "Missing tools\\yt-dlp.exe. Restore the bundled downloader."; return; }
        string toolDirectory = Path.GetDirectoryName(exe);
        if (!File.Exists(Path.Combine(toolDirectory, "ffmpeg.exe"))) { status.Text = "Missing bundled FFmpeg converter."; return; }
        bool video = mode.SelectedIndex == 1;
        string mediaArguments = video
            ? "-f " + Quote("bv[height<=" + ((string)quality.SelectedItem).Replace("p", "") + "]+ba/b[height<=" + ((string)quality.SelectedItem).Replace("p", "") + "]") + " --merge-output-format mp4 --recode-video mp4"
            : "-f bestaudio/best -x --audio-format " + ((string)format.SelectedItem).ToLowerInvariant() + " --audio-quality 0";
        busy = true;
        mode.IsEnabled = format.IsEnabled = quality.IsEnabled = false;
        link.IsEnabled = browse.IsEnabled = download.IsEnabled = false;
        progress.Visibility = Visibility.Visible;
        status.Text = "Downloading…";
        string destination = folder.Text;
        bool succeeded = false;
        try
        {
            Directory.CreateDirectory(destination);
            string result = await Task.Run(delegate {
                var info = new ProcessStartInfo(exe) {
                    Arguments = "--ignore-config --no-playlist --max-downloads 1 --no-progress --windows-filenames --no-overwrites --ffmpeg-location " + Quote(toolDirectory) + " " + mediaArguments + " -P " + Quote(destination) + " -o " + Quote("%(title).180B [%(id)s].%(ext)s") + " --print after_move:filepath -- " + Quote(url),
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (var process = new Process { StartInfo = info })
                {
                    process.Start();
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> errors = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    Task.WaitAll(output, errors);
                    if (process.ExitCode != 0 && process.ExitCode != 101) throw new Exception(errors.Result);
                    string path = output.Result.Trim();
                    if (path.Length == 0 || !File.Exists(path)) throw new Exception(errors.Result.Length > 0 ? errors.Result : "The service did not return a media file.");
                    return path;
                }
            });
            status.Text = "Успешно сохранено: " + Path.GetFileName(result);
            status.ToolTip = result;
            link.Clear();
            succeeded = true;
        }
        catch (Exception error) { status.Text = "Could not download this media."; MessageBox.Show(window, "Check the link and your internet connection.\n\n" + error.Message, "Download error", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { busy = false; mode.IsEnabled = format.IsEnabled = true; quality.IsEnabled = mode.SelectedIndex == 1; link.IsEnabled = browse.IsEnabled = download.IsEnabled = true; progress.Visibility = Visibility.Hidden; if (succeeded) link.Focus(); }
    }
    void UpdateOptions()
    {
        bool video = mode.SelectedIndex == 1;
        format.ItemsSource = video ? new[] { "MP4" } : new[] { "MP3", "M4A", "WAV" };
        format.SelectedIndex = 0;
        quality.ItemsSource = video ? new[] { "720p", "1080p", "1440p" } : new[] { "Best audio" };
        quality.SelectedIndex = video ? 1 : 0;
        quality.IsEnabled = video;
        status.Text = "homerick";
    }
    static string Quote(string value) { return "\"" + value.Replace("\"", "") + "\""; }
    sealed class NativeOwner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; private set; }
        internal NativeOwner(IntPtr handle) { Handle = handle; }
    }
}
