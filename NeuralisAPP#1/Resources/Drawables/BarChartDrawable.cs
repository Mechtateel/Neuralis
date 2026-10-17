using Microsoft.Maui.Graphics;

namespace NeuralisAPP_1.Resources.Drawables;

/// <summary>
/// Compact vertical bar chart with rounded tops. Designed for the Admin
/// "Weekly activity" panel (e.g. registrations / applications per weekday).
/// </summary>
public sealed class BarChartDrawable : IDrawable
{
    public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();
    public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
    public Color BarStart { get; set; } = Color.FromArgb("#2FE3C9");
    public Color BarEnd { get; set; } = Color.FromArgb("#1F8FA6");
    public Color HighlightStart { get; set; } = Color.FromArgb("#7FD0FF");
    public Color HighlightEnd { get; set; } = Color.FromArgb("#2C8DFF");
    public Color AxisLabelColor { get; set; } = Color.FromArgb("#6E8C96");
    public Color GridColor { get; set; } = Color.FromArgb("#143C44");
    public int HighlightIndex { get; set; } = -1;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Values.Count == 0 || dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            DrawEmpty(canvas, dirtyRect);
            return;
        }

        const float leftPad = 26f;
        const float rightPad = 8f;
        const float topPad = 10f;
        const float bottomPad = 26f;

        var plot = new RectF(
            dirtyRect.X + leftPad,
            dirtyRect.Y + topPad,
            Math.Max(1f, dirtyRect.Width - leftPad - rightPad),
            Math.Max(1f, dirtyRect.Height - topPad - bottomPad));

        var max = Math.Max(1.0, Values.Max());

        // Grid (3 lines)
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;
        canvas.FontColor = AxisLabelColor;
        canvas.FontSize = 9;
        for (var i = 0; i <= 2; i++)
        {
            var t = i / 2f;
            var y = plot.Bottom - t * plot.Height;
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            var value = t * max;
            canvas.DrawString(
                FormatTick(value),
                dirtyRect.X,
                y - 6,
                leftPad - 6,
                12,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        // Bars
        var gap = 10f;
        var slot = plot.Width / Values.Count;
        var barWidth = Math.Max(6f, slot - gap);
        for (var i = 0; i < Values.Count; i++)
        {
            var v = Values[i];
            var h = (float)(v / max * plot.Height);
            if (h <= 0)
                h = 2f;

            var x = plot.Left + i * slot + (slot - barWidth) / 2f;
            var y = plot.Bottom - h;
            var rect = new RectF(x, y, barWidth, h);

            var startColor = i == HighlightIndex ? HighlightStart : BarStart;
            var endColor = i == HighlightIndex ? HighlightEnd : BarEnd;

            var gradient = new LinearGradientPaint
            {
                StartColor = startColor,
                EndColor = endColor,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            canvas.SaveState();
            canvas.SetFillPaint(gradient, rect);
            canvas.FillRoundedRectangle(rect, 5);
            canvas.RestoreState();
        }

        // Labels
        if (Labels.Count > 0)
        {
            canvas.FontColor = AxisLabelColor;
            canvas.FontSize = 10;
            for (var i = 0; i < Labels.Count && i < Values.Count; i++)
            {
                var x = plot.Left + i * slot + slot / 2f;
                canvas.DrawString(
                    Labels[i],
                    x - 24,
                    plot.Bottom + 8,
                    48,
                    14,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Top);
            }
        }
    }

    private void DrawEmpty(ICanvas canvas, RectF rect)
    {
        canvas.FontColor = AxisLabelColor;
        canvas.FontSize = 11;
        canvas.DrawString(
            "No data",
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static string FormatTick(double v)
    {
        if (v >= 1000) return $"{(v / 1000):0.#}k";
        return ((int)Math.Round(v)).ToString();
    }
}
