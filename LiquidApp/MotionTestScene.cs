using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Controlled background for visual movement tests. It contains no user data.
internal static class MotionTestScene
{
    internal static void Run()
    {
        var app = new Application();
        var window = new Window { Title = "SoundCloud motion test background", Width = 1120, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Brushes.White };
        var canvas = new Canvas();
        window.Content = canvas;
        var background = new System.Windows.Shapes.Rectangle { Width = 1600, Height = 1200, Fill = new LinearGradientBrush(Color.FromRgb(103, 174, 238), Color.FromRgb(247, 179, 159), 35) };
        canvas.Children.Add(background);
        for (int x = 0; x < 1600; x += 40)
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = x, X2 = x, Y1 = 0, Y2 = 1200, Stroke = new SolidColorBrush(Color.FromArgb(70, 25, 40, 65)), StrokeThickness = 1 });
        for (int y = 0; y < 1200; y += 40)
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = 0, X2 = 1600, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromArgb(70, 25, 40, 65)), StrokeThickness = 1 });
        for (int y = 80; y < 800; y += 160)
        {
            var text = new TextBlock { Text = "LIVE DESKTOP / GLASS MOTION TEST", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(23, 39, 64)) };
            Canvas.SetLeft(text, 40); Canvas.SetTop(text, y); canvas.Children.Add(text);
        }
        app.Run(window);
    }
}
