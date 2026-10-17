using System.Globalization;
using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab;

/// <summary>
/// Per-page global regex scan (value + unit + ref range). No forced line splitting or block merging before parse.
/// Exponent units (10^/л): numeric value is mantissa vs same-scale ref (no scaling).
/// </summary>
internal static class LabTableRowExtractor
{
    /// <summary>Value + unit + ref min–max; allows Latin/Cyrillic <c>л</c> in units and ESR <c>мм/час</c>.</summary>
    private static readonly Regex ValueUnitRangeRx = new(
        @"(?<val>\d{1,5}(?:[.,]\d+)?)\s*(?<unit>нг/[мm][лl]|г/[лl]|г/дл|ммоль/[лl]|мкмоль/[лl]|мг/[лl]|мкг/мл|мкг/[мm][лl]|%|фл|пг|мм/час|mm/час|Ед/[лl]|ед/[лl]|МЕ/мл|мЕд/[лl]|10\s*\^\s*\d+\s*/\s*[лl]|10\^\d+\s*/\s*[лl])\s*(?<rmin>\d{1,5}(?:[.,]\d+)?)\s*[\u002D\u2013\u2014]\s*(?<rmax>\d{1,5}(?:[.,]\d+)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<LabValue> Extract(string normalizedText) =>
        ExtractFromPages(new[] { normalizedText ?? string.Empty });

    /// <summary>Parse each page string independently; merge and sort at end.</summary>
    public static IReadOnlyList<LabValue> ExtractFromPages(IReadOnlyList<string> normalizedPages)
    {
        if (normalizedPages == null || normalizedPages.Count == 0)
            return Array.Empty<LabValue>();

        var components = new List<LabComponent>(64);

        for (var pageIndex = 0; pageIndex < normalizedPages.Count; pageIndex++)
        {
            var page = normalizedPages[pageIndex] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(page))
                continue;

            var text = CollapseSpaces(page.Trim());
            CollectComponentsFromSegment(text, text, 0, pageIndex, components);
        }

        return SortComponentsToLabValues(components);
    }

    /// <summary>Parse one reconstructed row; section resolved from <paramref name="fullPageCollapsed"/> + segment offset.</summary>
    internal static IReadOnlyList<LabValue> ExtractFromSegment(
        string segmentCollapsed,
        string fullPageCollapsed,
        int segmentStartInFullPage,
        int pageIndex)
    {
        var components = new List<LabComponent>(8);
        CollectComponentsFromSegment(segmentCollapsed, fullPageCollapsed, segmentStartInFullPage, pageIndex, components);
        return SortComponentsToLabValues(components);
    }

    private static void CollectComponentsFromSegment(
        string segmentCollapsed,
        string sectionSourceCollapsed,
        int segmentStartInSectionSource,
        int pageIndex,
        List<LabComponent> components)
    {
        var text = segmentCollapsed;
        if (text.Length == 0)
            return;

        var matches = ValueUnitRangeRx.Matches(text);
        var lastEnd = 0;

        foreach (Match m in matches)
        {
            if (!m.Success || m.Index < lastEnd)
                continue;

            if (!TryParseToken(m.Groups["val"].Value, out var val)
                || !TryParseToken(m.Groups["rmin"].Value, out var rmin)
                || !TryParseToken(m.Groups["rmax"].Value, out var rmax))
                continue;

            if (rmin > rmax)
                (rmin, rmax) = (rmax, rmin);

            var unit = CanonicalizeLabUnit(m.Groups["unit"].Value.Trim());
            if (unit.Length == 0)
                continue;

            val = MaybeRecoverExponentMantissa(val, rmin, rmax, unit);

            var rawPrefix = ExtractRawLetterPrefixBeforeValue(text, m.Index);
            var name = LastAnalyteTokenFromLabel(rawPrefix);
            if (!IsPlausibleName(name))
                continue;

            if (!IsPlausibleMeasurement(val, rmin, rmax, unit))
                continue;

            var section = ResolveAnalysisSection(sectionSourceCollapsed, segmentStartInSectionSource + m.Index);
            var sourceIndex = pageIndex * 1_000_000_000 + segmentStartInSectionSource + m.Index;
            components.Add(new LabComponent(name, val, unit, rmin, rmax, section, sourceIndex));
            lastEnd = m.Index + m.Length;
        }
    }

    private static IReadOnlyList<LabValue> SortComponentsToLabValues(List<LabComponent> components)
    {
        var indexed = new List<(LabComponent C, LabValue V)>(components.Count);
        foreach (var c in components)
        {
            var v = new LabValue(
                NormalizeNameDisplay(c.Name),
                c.Value,
                c.Unit,
                c.RefMin,
                c.RefMax,
                c.AnalysisType,
                c.SourceIndex);
            indexed.Add((c, v));
        }

        indexed.Sort((x, y) =>
        {
            var sx = LabResultClassifier.Classify(x.V.Value, x.V.RefMin, x.V.RefMax);
            var sy = LabResultClassifier.Classify(y.V.Value, y.V.RefMin, y.V.RefMax);
            var rx = LabResultClassifier.SortRank(sx);
            var ry = LabResultClassifier.SortRank(sy);
            if (rx != ry)
                return rx.CompareTo(ry);
            return x.C.SourceIndex.CompareTo(y.C.SourceIndex);
        });

        return indexed.ConvertAll(static t => t.V);
    }

    /// <summary>Shared collapse for column parser and row regex (single spaces).</summary>
    internal static string CollapseLabText(string s) => CollapseSpaces(s);

    internal static string ExternalCanonicalizeUnit(string raw) => CanonicalizeLabUnit(raw.Trim());

    internal static double ExternalRecoverMantissa(double v, double rmin, double rmax, string unit) =>
        MaybeRecoverExponentMantissa(v, rmin, rmax, unit);

    internal static bool ExternalIsPlausibleMeasurement(double v, double rmin, double rmax, string unit) =>
        IsPlausibleMeasurement(v, rmin, rmax, unit);

    /// <summary>Bounds check when the row has no reference interval (table reconstruction relaxed mode).</summary>
    internal static bool ExternalIsPlausibleValueWithoutReference(double val, string unit)
    {
        if (double.IsNaN(val) || double.IsInfinity(val))
            return false;
        if (val < 0 || val > 1_000_000_000d)
            return false;

        if (string.IsNullOrWhiteSpace(unit))
            return val is <= 1_000_000d;

        var u = unit.ToLowerInvariant();
        var hasExponent = u.Contains('^', StringComparison.Ordinal);
        if (!hasExponent)
        {
            if (u.Contains("г/л", StringComparison.Ordinal) && val > 600)
                return false;
            if (u.Contains("нг/мл", StringComparison.Ordinal) && val > 200_000)
                return false;
            if (u == "%" && val > 100)
                return false;
        }

        return true;
    }

    internal static string ExternalResolveSection(string fullPage, int indexInPage) =>
        ResolveAnalysisSection(fullPage, Math.Max(0, indexInPage));

    internal static string ExternalNormalizeNameDisplay(string name) => NormalizeNameDisplay(name);

    /// <summary>Validates name/plausibility and builds <see cref="LabValue"/> from a linked position (column parser).</summary>
    /// <param name="precomputedTestName">If set, used instead of prefix scan (e.g. 50-char window name from column parser).</param>
    internal static LabValue? TryBuildLinkedMeasurement(
        string collapsedText,
        int valueStartIndex,
        double value,
        string unitRaw,
        double rmin,
        double rmax,
        int sourceOrder,
        string? precomputedTestName = null)
    {
        var unit = CanonicalizeLabUnit(unitRaw.Trim());
        if (unit.Length == 0)
            return null;

        value = MaybeRecoverExponentMantissa(value, rmin, rmax, unit);

        string name;
        if (!string.IsNullOrWhiteSpace(precomputedTestName))
            name = NormalizeNameDisplay(precomputedTestName.Trim());
        else
        {
            var rawPrefix = ExtractRawLetterPrefixBeforeValue(collapsedText, valueStartIndex);
            name = NormalizeNameDisplay(LastAnalyteTokenFromLabel(rawPrefix));
        }

        if (!IsPlausibleName(name))
            return null;

        if (!IsPlausibleMeasurement(value, rmin, rmax, unit))
            return null;

        var section = ResolveAnalysisSection(collapsedText, valueStartIndex);
        return new LabValue(name, value, unit, rmin, rmax, section, sourceOrder);
    }

    private static string NormalizeNameDisplay(string name)
    {
        var t = name.Trim();
        while (t.Length > 0 && (char.IsPunctuation(t[0]) || char.IsSeparator(t[0])))
            t = t[1..].TrimStart();
        return t.Length > 0 ? t : name.Trim();
    }

    private static string ExtractRawLetterPrefixBeforeValue(string text, int valueStart)
    {
        if (valueStart <= 0)
            return string.Empty;

        const int maxWindow = 120;
        var len = Math.Min(maxWindow, valueStart);
        var start = valueStart - len;
        var window = text.AsSpan(start, len);

        var j = window.Length - 1;
        while (j >= 0 && char.IsWhiteSpace(window[j]))
            j--;
        if (j < 0)
            return string.Empty;

        var endExclusive = j + 1;
        while (j >= 0)
        {
            var c = window[j];
            if (char.IsLetter(c) || c is '-' or '\u2011' or '\'')
            {
                j--;
                continue;
            }

            if (c == ' ' && j > 0 && char.IsLetter(window[j - 1]))
            {
                j--;
                continue;
            }

            break;
        }

        var nameStart = j + 1;
        if (endExclusive - nameStart < 1)
            return string.Empty;

        var raw = window.Slice(nameStart, endExclusive - nameStart).ToString().Trim();
        while (raw.Length > 0 && !char.IsLetter(raw[0]))
            raw = raw[1..].TrimStart();

        return raw;
    }

    private static string LastAnalyteTokenFromLabel(string label)
    {
        var t = label.Trim();
        if (t.Length == 0)
            return string.Empty;

        var tokens = Regex.Split(t, @"\s+").Where(x => x.Length > 0).ToArray();
        if (tokens.Length == 0)
            return string.Empty;

        var idx = tokens.Length - 1;
        while (idx >= 0 && IsLatinReportCodeToken(tokens[idx]))
            idx--;

        return idx < 0 ? tokens[^1] : tokens[idx];
    }

    private static bool IsLatinReportCodeToken(string token)
    {
        if (token.Length is < 2 or > 8)
            return false;
        foreach (var c in token)
        {
            if (c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z'))
                return false;
        }

        return true;
    }

    private static bool IsPlausibleName(string name)
    {
        if (name.Length is < 2 or > 72)
            return false;
        if (!Regex.IsMatch(name, @"\p{L}"))
            return false;

        var lower = name.ToLowerInvariant();
        if (lower.Contains("телефон", StringComparison.Ordinal)
            || lower.Contains("тел.", StringComparison.Ordinal)
            || lower.StartsWith("тоо", StringComparison.Ordinal)
            || lower.Contains("тоо ", StringComparison.Ordinal))
            return false;
        if (name.Contains('«') || name.Contains('»'))
            return false;

        var digitCount = name.Count(char.IsDigit);
        if (digitCount > name.Length * 0.35)
            return false;

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words <= 12;
    }

    private static string CanonicalizeLabUnit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return unit;
        var t = unit.Trim();
        t = Regex.Replace(t, @"10\s*\^\s*(\d+)\s*/\s*[lL]", "10^$1/л", RegexOptions.CultureInvariant);
        t = Regex.Replace(t, @"/\s*[lL](?=\s|\d)", "/л", RegexOptions.CultureInvariant);
        t = Regex.Replace(t, @"mm/час", "мм/час", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return t;
    }

    /// <summary>
    /// When OCR glues a row index into the mantissa (e.g. 15.56 vs ref 4–5 for 10^12/л), recover a value in range.
    /// </summary>
    private static double MaybeRecoverExponentMantissa(double val, double rmin, double rmax, string unit)
    {
        var u = unit.ToLowerInvariant();
        if (!u.Contains('^', StringComparison.Ordinal))
            return val;
        if (val >= rmin - 1e-9 && val <= rmax + 1e-9)
            return val;
        if (rmax <= 0 || double.IsNaN(val) || double.IsInfinity(val))
            return val;

        var tol = Math.Max(0.05, rmax * 0.03);

        var minusTen = val - 10d;
        if (minusTen >= rmin - tol && minusTen <= rmax + tol)
            return minusTen;

        var divTen = val / 10d;
        if (divTen >= rmin - tol && divTen <= rmax + tol)
            return divTen;

        return val;
    }

    private static string ResolveAnalysisSection(string normalizedPageLower, int matchStart)
    {
        if (matchStart <= 0 || string.IsNullOrEmpty(normalizedPageLower))
            return "Unknown";

        var head = normalizedPageLower[..matchStart];
        var lastBio = head.LastIndexOf("биохимическ", StringComparison.Ordinal);
        var lastCbc = Math.Max(
            head.LastIndexOf("общий анализ крови", StringComparison.Ordinal),
            head.LastIndexOf("қан талдауы", StringComparison.Ordinal));

        var bestIdx = -1;
        var best = "Unknown";
        if (lastBio >= 0 && lastBio > bestIdx)
        {
            bestIdx = lastBio;
            best = "Биохимический анализ крови";
        }

        if (lastCbc >= 0 && lastCbc > bestIdx)
        {
            bestIdx = lastCbc;
            best = "Общий анализ крови";
        }

        return best;
    }

    private static bool IsPlausibleMeasurement(double val, double rmin, double rmax, string unit)
    {
        if (double.IsNaN(val) || double.IsInfinity(val))
            return false;
        if (rmax <= rmin)
            return false;
        if (rmin < 0 || rmax > 1_000_000)
            return false;

        var u = unit.ToLowerInvariant();
        var hasExponent = u.Contains('^', StringComparison.Ordinal);

        if (!hasExponent)
        {
            if (u.Contains("г/л", StringComparison.Ordinal) && (val > 600 || rmax > 600))
                return false;
            if (u.Contains("нг/мл", StringComparison.Ordinal) && (val > 200_000 || rmax > 200_000))
                return false;
            if (u == "%" && (val > 100 || rmax > 100))
                return false;
        }

        return val is >= 0 and <= 1_000_000_000d;
    }

    private static string CollapseSpaces(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        var space = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space && sb.Length > 0)
                sb.Append(' ');
            space = false;
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool TryParseToken(string token, out double v) =>
        double.TryParse(token.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out v);
}
