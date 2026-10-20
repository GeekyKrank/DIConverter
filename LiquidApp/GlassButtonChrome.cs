using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SoundCloud.Controls
{
    // Native WPF interpretation of Petr Knoll's Glass Button (CodePen QwWLZdx):
    // layered rims, a travelling sheen, a soft contact shadow and press depth.
    public sealed class GlassButtonChrome : Decorator
    {
        internal event EventHandler OpticalBoundsChanged;
        public static readonly DependencyProperty HoverProperty = DependencyProperty.Register("Hover", typeof(double), typeof(GlassButtonChrome), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, StateChanged));
        public static readonly DependencyProperty PressProperty = DependencyProperty.Register("Press", typeof(double), typeof(GlassButtonChrome), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, StateChanged));
        public double Hover { get { return (double)GetValue(HoverProperty); } set { SetValue(HoverProperty, value); } }
        public double Press { get { return (double)GetValue(PressProperty); } set { SetValue(PressProperty, value); } }
        static void StateChanged(DependencyObject value, DependencyPropertyChangedEventArgs e)
        {
            var chrome = (GlassButtonChrome)value;
            double x = 1 - .025 * chrome.Hover - .008 * chrome.Press;
            chrome.RenderTransformOrigin = new Point(.5, .5);
            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(x, x - .065 * chrome.Press));
            transform.Children.Add(new TranslateTransform(0, 1.7 * chrome.Press));
            chrome.RenderTransform = transform;
            chrome.OpticalBoundsChanged?.Invoke(chrome, EventArgs.Empty);
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth, h = ActualHeight;
            if (w < 8 || h < 8) return;
            var body = new Rect(1.4, 1.4, w - 2.8, h - 3.8);
            double radius = body.Height / 2;
            var shape = new RectangleGeometry(body, radius, radius);
            double shadowY = 3.5 - Hover * 1.5 + Press;
            dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(14, 0, 0, 0)), 4), new Rect(3, shadowY + 2, w - 6, h - 7), radius, radius);
            dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)), 1.5), new Rect(2, shadowY + 1, w - 4, h - 5), radius, radius);
            var bodyFill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(.8, 1) };
            bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(15, 255, 255, 255), 0));
            bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(49 + 15 * Hover), 255, 255, 255), .47));
            bodyFill.GradientStops.Add(new GradientStop(Color.FromArgb(12, 255, 255, 255), 1));
            dc.DrawGeometry(bodyFill, null, shape);
            dc.PushClip(shape);
            var inside = new LinearGradientBrush(Color.FromArgb(19, 0, 0, 0), Color.FromArgb((byte)(110 + 30 * Hover), 255, 255, 255), 90);
            dc.DrawRoundedRectangle(null, new Pen(inside, 2), new Rect(2.6, 2.6, w - 5.2, h - 6.2), radius - 1, radius - 1);
            var rim = new LinearGradientBrush { StartPoint = new Point(.15 + .3 * Hover, 0), EndPoint = new Point(.85 - .2 * Hover, 1) };
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(215, 255, 255, 255), 0));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(45, 0, 0, 0), .28));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(25, 255, 255, 255), .54));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 255, 255), .86));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(75, 0, 0, 0), 1));
            dc.DrawGeometry(null, new Pen(rim, 1.15), shape);
            double shift = -.65 + .7 * Hover + .12 * Press;
            var sheen = new LinearGradientBrush { StartPoint = new Point(shift, 0), EndPoint = new Point(shift + 1.7, 1) };
            sheen.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            sheen.GradientStops.Add(new GradientStop(Colors.Transparent, .38));
            sheen.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(65 + 30 * Press), 255, 255, 255), .5));
            sheen.GradientStops.Add(new GradientStop(Colors.Transparent, .57));
            sheen.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            dc.DrawRectangle(sheen, null, body);
            dc.Pop();
        }
    }
}
