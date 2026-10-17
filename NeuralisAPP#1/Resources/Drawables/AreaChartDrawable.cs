using Microsoft.Maui.Graphics;

namespace NeuralisAPP_1.Resources.Drawables;

/// <summary>
/// Smooth area chart with horizontal grid, axis labels and value markers.
/// Designed for the Admin "User Growth" / "AI Activity" panels.
/// </summary>
public sealed class AreaChartDrawable : IDrawable
{
    public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();
    public IReadOnlyList<string> Labels { get; set; } = Array.Empty<string>();
    public Color Stroke { get; set; } = Color.FromArgb("#2FE3C9");
    public Color FillStart { get; set; } = Color.FromArgb("#552FE3C9");
    public Color FillEnd { get; set; } = Color.FromArgb("#002FE3C9");
    public Color GridColor { get; set; } = Color.FromArgb("#143C44");
    public Color AxisLabelColor { get; set; } = Color.FromArgb("#6E8C96");
    public Color MarkerFill { get; set; } = Color.FromArgb("#0C2530");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Values.Count < 2 || dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            DrawEmpty(canvas, dirtyRect);
            return;
        }

        const float leftPad = 36f;
        const float rightPad = 10f;
        const float topPad = 12f;
        const float bottomPad = 26f;

        var plot = new RectF(
            dirtyRect.X + leftPad,
            dirtyRect.Y + topPad,
            Math.Max(1f, dirtyRect.Width - leftPad - rightPad),
            Math.Max(1f, dirtyRect.Height - topPad - bottomPad));

        var min = 0.0;
        var max = Math.Max(1.0, Values.Max());
        var range = Math.Max(1e-6, max - min);

        // Grid + Y-axis labels (4 ticks)
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1;
        canvas.FontColor = AxisLabelColor;
        canvas.FontSize = 9;
        for (var i = 0; i <= 3; i++)
        {
            var t = i / 3f;
            var y = plot.Bottom - t * plot.Height;
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            var value = min + t * range;
            canvas.DrawString(
                FormatTick(value),
                dirtyRect.X,
                y - 6,
                leftPad - 6,
                12,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        // Build points
        var points = new PointF[Values.Count];
        for (var i = 0; i < Values.Count; i++)
        {
            var x = plot.Left + (float)(i / (double)(Values.Count - 1) * plot.Width);
            var norm = (Values[i] - min) / range;
            var y = plot.Bottom - (float)(norm * plot.Height);
            points[i] = new PointF(x, y);
        }

        // Fill
        var linePath = BuildSmoothPath(points);
        var fillPath = ClonePath(linePath);
        fillPath.LineTo(new PointF(points[^1].X, plot.Bottom));
        fillPath.LineTo(new PointF(points[0].X, plot.Bottom));
        fillPath.Close();

        var gradient = new LinearGradientPaint
        {
            StartColor = FillStart,
            EndColor = FillEnd,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        canvas.SaveState();
        canvas.SetFillPaint(gradient, plot);
        canvas.FillPath(fillPath);
        canvas.RestoreState();

        // Line
        canvas.StrokeColor = Stroke;
        canvas.StrokeSize = 2.4f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(linePath);

        // Value markers
        foreach (var p in points)
        {
            canvas.FillColor = MarkerFill;
            canvas.FillCircle(p.X, p.Y, 4.5f);
            canvas.FillColor = Stroke;
            canvas.FillCircle(p.X, p.Y, 2.6f);
        }

        // X-axis labels
        if (Labels.Count > 0)
        {
            canvas.FontColor = AxisLabelColor;
            canvas.FontSize = 9;
            for (var i = 0; i < Labels.Count && i < points.Length; i++)
            {
                var x = points[i].X;
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

    private static PathF BuildSmoothPath(IReadOnlyList<PointF> pts)
    {
        var path = new PathF();
        path.MoveTo(pts[0]);
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var p0 = i == 0 ? pts[0] : pts[i - 1];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i + 2 < pts.Count ? pts[i + 2] : p2;

            var c1 = new PointF(p1.X + (p2.X - p0.X) / 6f, p1.Y + (p2.Y - p0.Y) / 6f);
            var c2 = new PointF(p2.X - (p3.X - p1.X) / 6f, p2.Y - (p3.Y - p1.Y) / 6f);
            path.CurveTo(c1, c2, p2);
        }
        return path;
    }

    private static PathF ClonePath(PathF source) => new(source);
}
