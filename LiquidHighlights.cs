using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

internal sealed class LiquidHighlights : FrameworkElement
{
    Point light = new Point(.25, .1), target = new Point(.25, .1);
    readonly DispatcherTimer motion;
    bool suspended;
    internal LiquidHighlights(Window window)
    {
        IsHitTestVisible = false;
        motion = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        motion.Tick += delegate {
            light = new Point(light.X + (target.X - light.X) * .16, light.Y + (target.Y - light.Y) * .16);
            InvalidateVisual();
            if (Math.Abs(light.X - target.X) + Math.Abs(light.Y - target.Y) < .002) motion.Stop();
        };
        window.PreviewMouseMove += delegate(object sender, MouseEventArgs e) {
            if (suspended) return;
            Point p = e.GetPosition(window);
            target = new Point(p.X / Math.Max(1, window.ActualWidth), p.Y / Math.Max(1, window.ActualHeight));
            motion.Start();
        };
        window.MouseLeave += delegate { target = new Point(.25, .1); motion.Start(); };
        window.Closed += delegate { motion.Stop(); };
    }
    internal void Suspend(bool value) { suspended = value; if (value) motion.Stop(); }
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 4 || h < 4) return;
        var bounds = new Rect(.8, .8, w - 1.6, h - 1.6);
        var outline = new RectangleGeometry(bounds, 31, 31);
        dc.PushClip(outline);
        // Broad, continuous specular reflections; never a diagonal opaque pane.
        var halo = new RadialGradientBrush(Color.FromArgb(24, 255, 255, 255), Colors.Transparent) { Center = light, GradientOrigin = light, RadiusX = .7, RadiusY = .8 };
        dc.DrawRectangle(halo, null, bounds);
        var rim = new LinearGradientBrush { StartPoint = new Point(light.X, light.Y), EndPoint = new Point(1 - light.X * .4, 1) };
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 255, 255), 0));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(30, 235, 244, 255), .27));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(10, 255, 255, 255), .5));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(125, 255, 255, 255), .83));
        rim.GradientStops.Add(new GradientStop(Color.FromArgb(205, 255, 255, 255), 1));
        dc.DrawGeometry(null, new Pen(rim, 1.5), outline);
        var bevel = new LinearGradientBrush(Color.FromArgb(50, 255, 255, 255), Color.FromArgb(5, 255, 255, 255), 65);
        dc.DrawRoundedRectangle(null, new Pen(bevel, 3.5), new Rect(3.4, 3.4, w - 6.8, h - 6.8), 28, 28);
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)), 1), new Rect(8, 8, w - 16, h - 16), 24, 24);
        dc.Pop();
    }
}
