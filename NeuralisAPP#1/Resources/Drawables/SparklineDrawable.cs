using Microsoft.Maui.Graphics;

namespace NeuralisAPP_1.Resources.Drawables;

/// <summary>
/// A compact sparkline used inside KPI cards. Draws a smooth accent line over
/// a soft area fill plus a glowing endpoint marker.
/// Values can be any range — they are normalized to the available height.
/// </summary>
public sealed class SparklineDrawable : IDrawable
{
    public IReadOnlyList<double> Values { get; set; } = Array.Empty<double>();
    public Color Stroke { get; set; } = Color.FromArgb("#2FE3C9");
    public Color FillStart { get; set; } = Color.FromArgb("#502FE3C9");
    public Color FillEnd { get; set; } = Color.FromArgb("#002FE3C9");
    public float StrokeThickness { get; set; } = 2f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Values.Count < 2 || dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        var w = dirtyRect.Width;
        var h = dirtyRect.Height;
        var topPad = 4f;
        var bottomPad = 4f;
        var usable = Math.Max(1f, h - topPad - bottomPad);

        var min = Values.Min();
        var max = Values.Max();
        var range = Math.Max(1e-6, max - min);

        var points = new PointF[Values.Count];
        for (var i = 0; i < Values.Count; i++)
        {
            var x = (float)(i / (double)(Values.Count - 1) * w);
            var norm = (Values[i] - min) / range;
            var y = topPad + usable - (float)(norm * usable);
            points[i] = new PointF(x, y);
        }

        var path = BuildSmoothPath(points);
        var fillPath = ClonePath(path);
        fillPath.LineTo(new PointF(points[^1].X, h));
        fillPath.LineTo(new PointF(points[0].X, h));
        fillPath.Close();

        canvas.SaveState();
        var gradient = new LinearGradientPaint
        {
            StartColor = FillStart,
            EndColor = FillEnd,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        canvas.SetFillPaint(gradient, dirtyRect);
        canvas.FillPath(fillPath);
        canvas.RestoreState();

        canvas.StrokeColor = Stroke;
        canvas.StrokeSize = StrokeThickness;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(path);

        // Endpoint dot
        var last = points[^1];
        canvas.FillColor = Color.FromArgb("#FFFFFF");
        canvas.FillCircle(last.X, last.Y, 3.5f);
        canvas.FillColor = Stroke;
        canvas.FillCircle(last.X, last.Y, 2f);
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
