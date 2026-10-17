using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Extract page text + word geometry from PdfPig in a single document pass.</summary>
public static class LabPdfPigGeometryExtractor
{
    public static bool TryExtractTextAndLayouts(
        byte[] fileBytes,
        out IReadOnlyList<string> pageTexts,
        out string combinedText,
        out IReadOnlyList<LabPageLayout> layouts,
        ILogger? logger = null)
    {
        pageTexts = Array.Empty<string>();
        combinedText = string.Empty;
        layouts = Array.Empty<LabPageLayout>();
        try
        {
            using var ms = new MemoryStream(fileBytes, writable: false);
            using var document = PdfDocument.Open(ms);
            var pages = new List<string>();
            var ly = new List<LabPageLayout>();
            var sb = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                var t = page.Text ?? string.Empty;
                pages.Add(t);
                sb.AppendLine(t);

                var words = new List<LayoutWord>();
                foreach (var w in page.GetWords())
                {
                    var bb = w.BoundingBox;
                    var wt = (w.Text ?? string.Empty).Trim();
                    if (wt.Length == 0)
                        continue;
                    words.Add(new LayoutWord
                    {
                        Text = wt,
                        X0 = bb.BottomLeft.X,
                        Y0 = bb.BottomLeft.Y,
                        X1 = bb.TopRight.X,
                        Y1 = bb.TopRight.Y
                    });
                }

                ly.Add(new LabPageLayout
                {
                    PageNumber = page.Number,
                    PageWidth = page.Width,
                    PageHeight = page.Height,
                    Source = LabLayoutCoordinateSource.PdfPigGeometry,
                    Words = words
                });
            }

            pageTexts = pages;
            combinedText = sb.ToString();
            layouts = ly;
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "PdfPig text+layout extraction failed");
            return false;
        }
    }

    public static IReadOnlyList<LayoutLine> BuildLines(LabPageLayout page)
    {
        if (page.Words.Count == 0)
            return Array.Empty<LayoutLine>();

        var heights = page.Words.Select(w => w.Height).Where(h => h > 0.01).OrderBy(h => h).ToList();
        var medianH = heights.Count == 0 ? 8d : heights[heights.Count / 2];
        var rowTol = Math.Max(2.0, medianH * 0.55);

        var sorted = page.Words.OrderByDescending(w => w.CenterY).ToList();
        var buckets = new List<List<LayoutWord>>();

        foreach (var w in sorted)
        {
            List<LayoutWord>? best = null;
            var bestDy = double.MaxValue;
            foreach (var b in buckets)
            {
                var my = b.Average(x => x.CenterY);
                var dy = Math.Abs(w.CenterY - my);
                if (dy <= rowTol && dy < bestDy)
                {
                    bestDy = dy;
                    best = b;
                }
            }

            if (best == null)
                buckets.Add(new List<LayoutWord> { w });
            else
                best.Add(w);
        }

        return buckets
            .Select(lw =>
            {
                var ordered = lw.OrderBy(w => w.X0).ToList();
                return new LayoutLine
                {
                    Words = ordered,
                    MinY = ordered.Min(w => w.Y0),
                    MaxY = ordered.Max(w => w.Y1)
                };
            })
            .OrderByDescending(l => l.MidY)
            .ToList();
    }
}

/// <summary>When only flat text exists: synthetic boxes from line / word order (Y from line index, X from word index).</summary>
public static class PseudoLayoutFromTextBuilder
{
    public static LabPageLayout BuildPage(int pageNumber, string pageText, double pseudoWidth = 520, double lineHeight = 14)
    {
        var words = new List<LayoutWord>();
        var lines = (pageText ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var y = 0d;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                y += lineHeight;
                continue;
            }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var x = 0d;
            const double wordStep = 72d;
            foreach (var p in parts)
            {
                words.Add(new LayoutWord
                {
                    Text = p,
                    X0 = x,
                    Y0 = y,
                    X1 = x + Math.Max(24, p.Length * 6.5),
                    Y1 = y + lineHeight
                });
                x += wordStep;
            }

            y += lineHeight;
        }

        var h = Math.Max(400, y + lineHeight * 2);
        return new LabPageLayout
        {
            PageNumber = pageNumber,
            PageWidth = pseudoWidth,
            PageHeight = h,
            Source = LabLayoutCoordinateSource.PseudoFromTextLines,
            Words = words
        };
    }
}
