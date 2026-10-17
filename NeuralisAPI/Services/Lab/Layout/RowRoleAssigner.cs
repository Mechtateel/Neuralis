namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Infer label / result / reference / far-right text from horizontal word clusters only.</summary>
public static class RowRoleAssigner
{
    public static RowRoleAssignment Assign(TableGeometricRow row, double pageWidth, IReadOnlyList<double>? columnAnchors = null)
    {
        var words = row.Words.OrderBy(w => w.X0).ToList();
        if (words.Count == 0)
            return new RowRoleAssignment { LabelText = string.Empty, ResultClusterText = string.Empty, Confidence = 0 };

        if (columnAnchors is { Count: >= 2 })
            return AssignByAnchors(words, pageWidth, columnAnchors);

        var medW = MedianWidth(words);
        var gapTh = Math.Max(medW * 2.4, pageWidth <= 0 ? 12 : pageWidth * 0.02);
        var clusters = ClusterByGap(words, gapTh);
        if (clusters.Count == 0)
            return new RowRoleAssignment { LabelText = string.Empty, ResultClusterText = string.Empty, Confidence = 0 };

        var confidence = 0.5 + 0.1 * Math.Min(4, clusters.Count);
        var start = 0;
        if (clusters.Count >= 2 && IsRowIndexCluster(clusters[0]) && HasLetter(Join(clusters[1])))
            start = 1;

        if (clusters.Count - start <= 0)
            return new RowRoleAssignment { LabelText = string.Empty, ResultClusterText = string.Empty, Confidence = 0 };

        var resultIdx = -1;
        for (var i = start; i < clusters.Count; i++)
        {
            if (ContainsDigit(Join(clusters[i])))
            {
                resultIdx = i;
                break;
            }
        }

        if (resultIdx < 0)
        {
            var labelOnly = string.Join(" ", clusters.Skip(start).Select(Join)).Trim();
            return new RowRoleAssignment { LabelText = labelOnly, ResultClusterText = string.Empty, Confidence = 0.35 };
        }

        var label = resultIdx > start
            ? string.Join(" ", clusters.Skip(start).Take(resultIdx - start).Select(Join)).Trim()
            : string.Empty;
        var result = Join(clusters[resultIdx]);
        string reference = string.Empty;
        string farRight = string.Empty;
        var right = clusters.Skip(resultIdx + 1).ToList();
        if (right.Count >= 1)
        {
            reference = Join(right[0]);
            if (right.Count >= 2)
                farRight = string.Join(" ", right.Skip(1).Select(Join));
            if (LooksLikeNumericRange(farRight) || LooksMostlyNumeric(farRight))
            {
                reference = string.IsNullOrEmpty(reference) ? farRight : $"{reference} {farRight}".Trim();
                farRight = string.Empty;
            }
        }

        return new RowRoleAssignment
        {
            LabelText = label.Trim(),
            ResultClusterText = result.Trim(),
            ReferenceClusterText = reference.Trim(),
            FarRightText = farRight.Trim(),
            Confidence = Math.Clamp(confidence, 0, 1)
        };
    }

    private static RowRoleAssignment AssignByAnchors(List<LayoutWord> words, double pageWidth, IReadOnlyList<double> anchorsNorm)
    {
        var pw = pageWidth <= 0 ? 1 : pageWidth;
        var anchors = anchorsNorm.Select(a => a * pw).OrderBy(a => a).ToList();
        var buckets = new Dictionary<int, List<LayoutWord>>();
        for (var i = 0; i < anchors.Count; i++)
            buckets[i] = new List<LayoutWord>();

        foreach (var w in words)
        {
            var best = 0;
            var bestD = double.MaxValue;
            for (var i = 0; i < anchors.Count; i++)
            {
                var d = Math.Abs(w.CenterX - anchors[i]);
                if (d < bestD)
                {
                    best = i;
                    bestD = d;
                }
            }

            buckets[best].Add(w);
        }

        if (buckets[0].Count > 0 && IsRowIndexCluster(buckets[0]))
            buckets[0].Clear();

        var label = Join(buckets[0].OrderBy(w => w.X0).ToList()).Trim();
        var resultBucket = anchors.Count >= 2 ? 1 : -1;
        if (resultBucket >= 0 && !ContainsDigit(Join(buckets[resultBucket])))
        {
            for (var i = 1; i < anchors.Count; i++)
            {
                if (ContainsDigit(Join(buckets[i])))
                {
                    resultBucket = i;
                    break;
                }
            }
        }

        var result = resultBucket >= 0 ? Join(buckets[resultBucket].OrderBy(w => w.X0).ToList()).Trim() : string.Empty;
        var referenceBucket = resultBucket >= 0 ? resultBucket + 1 : 2;
        var reference = (referenceBucket >= 0 && referenceBucket < anchors.Count)
            ? Join(buckets[referenceBucket].OrderBy(w => w.X0).ToList()).Trim()
            : string.Empty;
        var farRightStart = Math.Max(referenceBucket + 1, 3);
        var farRight = farRightStart < anchors.Count
            ? string.Join(" ", Enumerable.Range(farRightStart, anchors.Count - farRightStart).Select(i => Join(buckets[i].OrderBy(w => w.X0).ToList())).Where(x => !string.IsNullOrWhiteSpace(x)))
            : string.Empty;

        var nonEmpty = buckets.Values.Count(v => v.Count > 0);
        var conf = Math.Clamp((nonEmpty / (double)Math.Max(2, anchors.Count)) * 0.9, 0.3, 1.0);

        return new RowRoleAssignment
        {
            LabelText = label,
            ResultClusterText = result,
            ReferenceClusterText = reference,
            FarRightText = farRight.Trim(),
            Confidence = conf
        };
    }

    private static bool IsRowIndexCluster(List<LayoutWord> cluster)
    {
        var t = Join(cluster).Trim();
        if (t.Length is < 1 or > 3)
            return false;
        return t.All(char.IsDigit);
    }

    private static List<List<LayoutWord>> ClusterByGap(List<LayoutWord> sorted, double gapTh)
    {
        var clusters = new List<List<LayoutWord>>();
        List<LayoutWord>? cur = null;
        double lastEnd = double.NegativeInfinity;
        foreach (var w in sorted)
        {
            if (cur == null || w.X0 - lastEnd > gapTh)
            {
                cur = new List<LayoutWord> { w };
                clusters.Add(cur);
            }
            else
                cur.Add(w);
            lastEnd = w.X1;
        }

        return clusters;
    }

    private static double MedianWidth(List<LayoutWord> words)
    {
        var w = words.Select(x => x.Width).Where(x => x > 0.01).OrderBy(x => x).ToList();
        return w.Count == 0 ? 8 : w[w.Count / 2];
    }

    private static string Join(List<LayoutWord> c) => string.Join(' ', c.Select(w => w.Text));

    private static bool ContainsDigit(string s) => s.Any(char.IsDigit);
    private static bool HasLetter(string s) => s.Any(char.IsLetter);

    private static bool LooksMostlyNumeric(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        var digit = s.Count(char.IsDigit);
        return digit >= s.Length * 0.45;
    }

    private static bool LooksLikeNumericRange(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        return System.Text.RegularExpressions.Regex.IsMatch(s,
            @"\d{1,5}(?:[.,]\d+)?\s*[\u002D\u2013\u2014]\s*\d{1,5}(?:[.,]\d+)?");
    }
}
