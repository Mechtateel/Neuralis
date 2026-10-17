namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Detects table-like geometry in the body band and emits one geometric row per baseline cluster.</summary>
public static class TableRowReconstructor
{
    private static readonly string[] HeaderTerms =
    {
        "component", "result", "norm", "comment", "performed", "approved",
        "компонент", "результат", "норма", "комментар", "выполн", "одобр",
    };

    public static TableRegion? TryReconstructTable(LabPageLayout page, IReadOnlyList<LayoutLine> bodyLines)
    {
        if (bodyLines.Count < 1)
            return null;

        var candidates = bodyLines.Where(l => l.Words.Count >= 2).ToList();
        if (candidates.Count < 1)
            return null;

        if (candidates.Count == 1 && !HasMultiColumnSignal(candidates[0], page.PageWidth <= 0 ? 1 : page.PageWidth))
            return null;

        var pw = page.PageWidth <= 0 ? 1 : page.PageWidth;
        var multiCell = 0d;
        foreach (var line in candidates)
        {
            var gaps = GapsBetweenWords(line.Words);
            var medW = MedianWordWidth(line.Words);
            var gapTh = Math.Max(medW * 2.2, pw * 0.018);
            if (gaps.Count(g => g >= gapTh) >= 1 || line.Words.Count >= 3)
                multiCell += 1d;
        }

        var meetsMultiCell = multiCell >= Math.Max(2, candidates.Count / 2.0);

        var (anchors, anchorConsistency) = EstimateColumnAnchors(candidates, pw);
        if (anchors == null || anchors.Count < 2)
        {
            anchors = EstimateWeakAnchors(candidates, pw);
            anchorConsistency = 0.28;
        }

        var spacingConsistency = ComputeRowSpacingConsistency(candidates);
        if (spacingConsistency < 0.15)
            spacingConsistency = 0.15;

        var geom = multiCell / Math.Max(4.0, candidates.Count);
        var confidence = Math.Clamp((geom * 0.45) + (anchorConsistency * 0.35) + (spacingConsistency * 0.20), 0.2, 0.95);
        if (!meetsMultiCell)
            confidence = Math.Min(confidence, 0.42);

        var rows = candidates
            .Select(l => new TableGeometricRow
            {
                Words = l.Words,
                RowY = l.MidY
            })
            .OrderByDescending(r => r.RowY)
            .ToList();

        return new TableRegion
        {
            Rows = rows,
            GeometryConfidence = confidence,
            ColumnAnchors = anchors
        };
    }

    public static TableRegion? BuildWeakTableFromLines(LabPageLayout page, IReadOnlyList<LayoutLine> bodyLines)
    {
        var candidates = bodyLines.Where(l => l.Words.Count >= 2).ToList();
        if (candidates.Count < 1)
            return null;
        if (candidates.Count == 1 && !HasMultiColumnSignal(candidates[0], page.PageWidth <= 0 ? 1 : page.PageWidth))
            return null;
        var rows = candidates
            .Select(l => new TableGeometricRow
            {
                Words = l.Words,
                RowY = l.MidY
            })
            .OrderByDescending(r => r.RowY)
            .ToList();
        var anchors = EstimateWeakAnchors(candidates, page.PageWidth <= 0 ? 1 : page.PageWidth);
        return new TableRegion
        {
            Rows = rows,
            GeometryConfidence = 0.25,
            ColumnAnchors = anchors
        };
    }

    /// <summary>
    /// Header-led fallback for small tables: promotes "header + >=1 aligned body row" to a valid table.
    /// </summary>
    public static TableRegion? TryPromoteSmallTableFromHeader(
        LabPageLayout page,
        IReadOnlyList<LayoutLine> bodyLines,
        out string decisionReason)
    {
        decisionReason = "no_header_row";
        if (bodyLines.Count < 2)
        {
            decisionReason = "insufficient_lines_for_small_table";
            return null;
        }

        var ordered = bodyLines.OrderByDescending(l => l.MidY).ToList();
        var pw = page.PageWidth <= 0 ? 1 : page.PageWidth;

        for (var hi = 0; hi < ordered.Count - 1; hi++)
        {
            var header = ordered[hi];
            if (!LooksHeaderLike(header))
                continue;

            var anchors = BuildAnchorsFromHeader(header, pw);
            if (anchors.Count < 2)
            {
                decisionReason = "header_without_column_bands";
                continue;
            }

            var body = new List<LayoutLine>();
            for (var i = hi + 1; i < ordered.Count; i++)
            {
                var line = ordered[i];
                if (line.Words.Count < 2)
                    continue;
                if (!HasMultiColumnSignal(line, pw))
                    continue;
                if (!IsAlignedToAnchors(line, anchors, pw))
                    continue;
                body.Add(line);
            }

            if (body.Count >= 1)
            {
                decisionReason = "header_plus_aligned_body";
                var rows = new List<TableGeometricRow>(1 + body.Count)
                {
                    new()
                    {
                        Words = header.Words,
                        RowY = header.MidY
                    }
                };
                rows.AddRange(body.Select(l => new TableGeometricRow
                {
                    Words = l.Words,
                    RowY = l.MidY
                }));

                return new TableRegion
                {
                    Rows = rows.OrderByDescending(r => r.RowY).ToList(),
                    GeometryConfidence = 0.52,
                    ColumnAnchors = anchors
                };
            }

            decisionReason = "no_aligned_body_rows";
        }

        return null;
    }

    private static List<double> GapsBetweenWords(IReadOnlyList<LayoutWord> words)
    {
        var g = new List<double>();
        for (var i = 1; i < words.Count; i++)
            g.Add(words[i].X0 - words[i - 1].X1);
        return g;
    }

    private static double MedianWordWidth(IReadOnlyList<LayoutWord> words)
    {
        var w = words.Select(x => x.Width).Where(x => x > 0.01).OrderBy(x => x).ToList();
        return w.Count == 0 ? 10 : w[w.Count / 2];
    }

    private static (IReadOnlyList<double>? Anchors, double Consistency) EstimateColumnAnchors(List<LayoutLine> lines, double pageWidth)
    {
        // Use per-row cluster start X values; then cluster X across rows to find vertical alignments.
        var starts = new List<double>();
        var rowContrib = 0;
        foreach (var line in lines)
        {
            var sorted = line.Words.OrderBy(w => w.X0).ToList();
            if (sorted.Count < 2)
                continue;
            var medW = MedianWordWidth(sorted);
            var gapTh = Math.Max(medW * 2.2, pageWidth * 0.018);
            var xStarts = new List<double> { sorted[0].X0 / pageWidth };
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].X0 - sorted[i - 1].X1 > gapTh)
                    xStarts.Add(sorted[i].X0 / pageWidth);
            }

            if (xStarts.Count >= 2)
            {
                rowContrib++;
                starts.AddRange(xStarts);
            }
        }

        if (rowContrib < 3 || starts.Count < 8)
            return (null, 0);

        starts.Sort();
        var tol = 0.03; // normalized X tolerance
        var groups = new List<List<double>>();
        foreach (var x in starts)
        {
            if (groups.Count == 0)
            {
                groups.Add(new List<double> { x });
                continue;
            }

            var last = groups[^1];
            var mean = last.Average();
            if (Math.Abs(x - mean) <= tol)
                last.Add(x);
            else
                groups.Add(new List<double> { x });
        }

        var stable = groups
            .Where(g => g.Count >= Math.Max(2, rowContrib / 3))
            .Select(g => g.Average())
            .OrderBy(x => x)
            .ToList();

        if (stable.Count < 2)
            return (null, 0);

        // Keep up to first 5 columns to avoid overfitting from noisy right metadata.
        if (stable.Count > 5)
            stable = stable.Take(5).ToList();

        var consistency = Math.Clamp(stable.Count / 4.0, 0.25, 1.0);
        return (stable, consistency);
    }

    private static IReadOnlyList<double> EstimateWeakAnchors(List<LayoutLine> lines, double pageWidth)
    {
        var pw = pageWidth <= 0 ? 1 : pageWidth;
        var xs = new List<double>();
        foreach (var line in lines)
        {
            foreach (var w in line.Words)
                xs.Add(w.CenterX / pw);
        }

        if (xs.Count == 0)
            return new[] { 0.18, 0.46, 0.70 };
        xs.Sort();
        return new[] { xs[xs.Count / 6], xs[xs.Count / 2], xs[(xs.Count * 5) / 6] };
    }

    private static double ComputeRowSpacingConsistency(List<LayoutLine> rows)
    {
        var ys = rows.Select(r => r.MidY).OrderByDescending(y => y).ToList();
        if (ys.Count < 3)
            return 0;
        var gaps = new List<double>();
        for (var i = 1; i < ys.Count; i++)
        {
            var d = ys[i - 1] - ys[i];
            if (d > 0.1)
                gaps.Add(d);
        }

        if (gaps.Count < 2)
            return 0.25;
        var mean = gaps.Average();
        if (mean <= 0.01)
            return 0;
        var variance = gaps.Select(g => (g - mean) * (g - mean)).Average();
        var std = Math.Sqrt(variance);
        var cv = std / mean;
        return Math.Clamp(1.0 - cv, 0, 1);
    }

    private static bool HasMultiColumnSignal(LayoutLine line, double pageWidth)
    {
        var words = line.Words.OrderBy(w => w.X0).ToList();
        if (words.Count < 2)
            return false;
        var medW = MedianWordWidth(words);
        var gapTh = Math.Max(medW * 2.2, pageWidth * 0.018);
        var gaps = GapsBetweenWords(words);
        return gaps.Any(g => g >= gapTh);
    }

    private static bool LooksHeaderLike(LayoutLine line)
    {
        var full = string.Join(' ', line.Words.Select(w => w.Text)).ToLowerInvariant();
        var hit = HeaderTerms.Count(t => full.Contains(t, StringComparison.Ordinal));
        if (hit >= 2)
            return true;
        // Fallback "framing-like" header: mostly words and no numeric payload.
        var hasDigits = line.Words.Any(w => w.Text.Any(char.IsDigit));
        return !hasDigits && line.Words.Count >= 3;
    }

    private static IReadOnlyList<double> BuildAnchorsFromHeader(LayoutLine header, double pageWidth)
    {
        var pw = pageWidth <= 0 ? 1 : pageWidth;
        var sorted = header.Words.OrderBy(w => w.X0).ToList();
        if (sorted.Count < 2)
            return Array.Empty<double>();
        var medW = MedianWordWidth(sorted);
        var gapTh = Math.Max(medW * 2.2, pw * 0.018);
        var anchors = new List<double> { sorted[0].X0 / pw };
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].X0 - sorted[i - 1].X1 > gapTh)
                anchors.Add(sorted[i].X0 / pw);
        }

        return anchors;
    }

    private static bool IsAlignedToAnchors(LayoutLine line, IReadOnlyList<double> anchorsNorm, double pageWidth)
    {
        if (anchorsNorm.Count < 2)
            return false;

        var pw = pageWidth <= 0 ? 1 : pageWidth;
        var sorted = line.Words.OrderBy(w => w.X0).ToList();
        var medW = MedianWordWidth(sorted);
        var gapTh = Math.Max(medW * 2.2, pw * 0.018);
        var starts = new List<double> { sorted[0].X0 / pw };
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].X0 - sorted[i - 1].X1 > gapTh)
                starts.Add(sorted[i].X0 / pw);
        }

        var tol = 0.05;
        var aligned = 0;
        foreach (var s in starts)
        {
            if (anchorsNorm.Any(a => Math.Abs(a - s) <= tol))
                aligned++;
        }

        return aligned >= 2 || (anchorsNorm.Count == 2 && aligned >= 1);
    }
}
