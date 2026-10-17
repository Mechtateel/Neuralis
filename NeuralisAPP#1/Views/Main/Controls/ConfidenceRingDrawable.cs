using Microsoft.Maui.Graphics;

namespace NeuralisAPP_1.Views.Main.Controls;

/// <summary>Draws a circular progress ring for confidence / completion (0–1).</summary>
public sealed class ConfidenceRingDrawable : IDrawable
{
    public double Progress { get; set; } = 0.86;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        const float stroke = 10f;
        var r = (size - stroke) * 0.5f;
        var cx = dirtyRect.Center.X;
        var cy = dirtyRect.Center.Y;
        var track = Color.FromArgb("#1B6270");
        var fill = Color.FromArgb("#2FE3C9");

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = stroke;

        canvas.StrokeColor = track;
        canvas.DrawEllipse(cx - r, cy - r, r * 2, r * 2);

        var clamped = Math.Clamp(Progress, 0, 1);
        if (clamped <= 0)
            return;

        canvas.StrokeColor = fill;
        var start = -90f;
        var sweep = (float)(360 * clamped);
        canvas.DrawArc(cx - r, cy - r, r * 2, r * 2, start, start + sweep, false, false);
    }
}
