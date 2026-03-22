using System.Drawing.Drawing2D;

namespace SpaceHog;

public sealed class SizeBarRenderer
{
    private static readonly Color[] BarColors = {
        Color.FromArgb(86, 156, 214),   // Blue
        Color.FromArgb(78, 201, 176),   // Teal
        Color.FromArgb(220, 220, 170),  // Yellow
        Color.FromArgb(206, 145, 120),  // Orange
        Color.FromArgb(197, 134, 192),  // Purple
        Color.FromArgb(156, 220, 254),  // Light blue
    };

    public static Color GetColor(int index) => BarColors[index % BarColors.Length];

    public static void DrawBar(Graphics g, Rectangle bounds, double fraction, Color color)
    {
        if (fraction <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;

        var barWidth = (int)(bounds.Width * Math.Min(fraction, 1.0));
        if (barWidth < 2) barWidth = 2;

        // Background
        using var bgBrush = new SolidBrush(Color.FromArgb(40, 40, 50));
        g.FillRectangle(bgBrush, bounds);

        // Fill
        var barRect = new Rectangle(bounds.X, bounds.Y, barWidth, bounds.Height);
        using var brush = new LinearGradientBrush(barRect, color, Color.FromArgb(180, color), LinearGradientMode.Vertical);
        g.FillRectangle(brush, barRect);
    }
}
