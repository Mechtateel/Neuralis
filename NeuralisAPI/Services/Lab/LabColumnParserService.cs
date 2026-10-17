using System.Globalization;
using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab;

/// <summary>
/// Position-based lab OCR linker: every anchor is a <see cref="Match"/> with <see cref="Match.Index"/>.
/// For each result value, chooses the best unused (unit, range) pair strictly to the RIGHT within distance limits.
/// No parallel array indexing; ordering is always by text position.
/// </summary>
public sealed class LabColumnParserService
{
    private readonly ILogger<LabColumnParserService> _logger;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Result mantissa only (scientific part is never captured here).</summary>
    private static readonly Regex ValueTokenRx = new(
        @"\d+(?:\.\d+)?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReferenceRangeRx = new(
        @"(?<rmin>\d+(?:\.\d+)?)\s*[\u002D\u2013\u2014]\s*(?<rmax>\d+(?:\.\d+)?)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DateRx = new(
        @"\b\d{2}\.\d{2}\.\d{4}\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Exponent marker belongs to unit; digits here are never result values.</summary>
    private static readonly Regex ScientificLiteralRx = new(
        @"10\s*\^\s*\d+(?:\s*/\s*[лl])?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Units (user set + essentials); longest-first inside alternation.</summary>
    private static readonly Regex UnitTokenRx = new(
        @"(10\s*\^\s*\d+\s*/\s*[лl]|нг/[мm][лl]|г/дл|мкмоль/[лl]|ммоль/[лl]|мг/[лl]|мкг/мл|мкг/[мm][лl]|г/[лl]|мм/час|mm/час|Ед/[лl]|ед/[лl]|МЕ/мл|мЕд/[лl]|%|фл|пг)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Max distance from end of value token to start of unit (characters).</summary>
    private const int MaxCharsValueEndToUnitStart = 30;

    /// <summary>Max distance from end of value token to start of reference range (characters).</summary>
    private const int MaxCharsValueEndToRangeStart = 50;

    /// <summary>Max gap between unit end and range start (or vice versa) for same row.</summary>
    private const int MaxGapUnitRange = 40;

    private const double MaxResultValueSkip = 1000d;

    public LabColumnParserService(ILogger<LabColumnParserService> logger) => _logger = logger;

    public IReadOnlyList<LabResult> ParseRaw(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return Array.Empty<LabResult>();

        var normalized = RunTrackedNormalization(ocrText);
        return ParseNormalized(normalized);
    }

    public IReadOnlyList<LabResult> ParseNormalized(string normalizedText)
    {
        var labValues = TryExtractColumnLabValues(normalizedText, pageIndex: 0);
        return labValues.Select(ToLabResult).ToList();
    }

    public IReadOnlyList<LabValue> TryExtractColumnLabValues(string normalizedPageText, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(normalizedPageText))
            return Array.Empty<LabValue>();

        var text = LabTableRowExtractor.CollapseLabText(normalizedPageText.Trim());
        var excluded = BuildExcludedSpans(text);
        var rangeOccs = CollectRangeOccurrences(text);
        var unitOccs = CollectUnitOccurrences(text);

        var usedUnitAt = new HashSet<int>();
        var usedRangeAt = new HashSet<int>();
        var results = new List<LabValue>(32);
        var sourceSeq = 0;

        foreach (var vm in EnumerateValueMatches(text, excluded).OrderBy(m => m.Index))
        {
            if (!TryParseDouble(vm.Value, out var numeric))
                continue;
            if (numeric > MaxResultValueSkip)
                continue;

            var valueEnd = vm.Index + vm.Length;
            var sourceOrder = pageIndex * 1_000_000_000 + sourceSeq++;

            if (!TryPickPositionLinkedPair(
                    valueEnd,
                    rangeOccs,
                    unitOccs,
                    usedRangeAt,
                    usedUnitAt,
                    out var unitOcc,
                    out var rangeOcc))
                continue;

            var testName = ExtractMedicalTestName(text, vm.Index);
            var sliceAfter = valueEnd < text.Length
                ? text.Substring(valueEnd, Math.Min(96, text.Length - valueEnd))
                : string.Empty;
            var unitText = ApplyAnalyteUnitRules(testName, unitOcc.Text, sliceAfter);

            var built = LabTableRowExtractor.TryBuildLinkedMeasurement(
                text,
                vm.Index,
                numeric,
                unitText,
                rangeOcc.Min,
                rangeOcc.Max,
                sourceOrder,
                precomputedTestName: testName);

            if (built == null)
                continue;

            usedUnitAt.Add(unitOcc.Index);
            usedRangeAt.Add(rangeOcc.Index);
            results.Add(built);
        }

        _logger.LogDebug("Lab column (position-linked): {Count} row(s), page {Page}", results.Count, pageIndex);
        return SortClinicalFirst(results);
    }

    public static bool ShouldFallbackToLineParse(IReadOnlyList<LabValue> columnRows, IReadOnlyList<LabValue> lineRows)
    {
        if (columnRows.Count == 0)
            return true;
        if (lineRows.Count >= 5 && columnRows.Count * 2 < lineRows.Count)
            return true;
        return false;
    }

    /// <summary>
    /// Enumerate (unit, range) pairs using only <see cref="PosToken.Index"/> ordering and distance in the source string.
    /// Picks the tightest pair: minimal spread to the right of the value end.
    /// </summary>
    private static bool TryPickPositionLinkedPair(
        int valueEndExclusive,
        IReadOnlyList<RangeOcc> ranges,
        IReadOnlyList<UnitOcc> units,
        HashSet<int> usedRangeIndex,
        HashSet<int> usedUnitIndex,
        out UnitOcc bestUnit,
        out RangeOcc bestRange)
    {
        bestUnit = default!;
        bestRange = default!;

        var candidates = new List<(UnitOcc U, RangeOcc R, int Spread)>(16);

        foreach (var u in units)
        {
            if (usedUnitIndex.Contains(u.Index))
                continue;
            var du = u.Index - valueEndExclusive;
            if (du < 0 || du > MaxCharsValueEndToUnitStart)
                continue;

            foreach (var r in ranges)
            {
                if (usedRangeIndex.Contains(r.Index))
                    continue;
                var dr = r.Index - valueEndExclusive;
                if (dr < 0 || dr > MaxCharsValueEndToRangeStart)
                    continue;
                if (!AreSameRowByPosition(u, r))
                    continue;

                var spread = Math.Max(u.Index, r.Index) - valueEndExclusive;
                candidates.Add((u, r, spread));
            }
        }

        if (candidates.Count == 0)
            return false;

        var pick = candidates
            .OrderBy(c => c.Spread)
            .ThenBy(c => c.U.Index)
            .ThenBy(c => c.R.Index)
            .First();

        bestUnit = pick.U;
        bestRange = pick.R;
        return true;
    }

    /// <summary>Unit and range belong to one row if the gap between them is small (either order).</summary>
    private static bool AreSameRowByPosition(UnitOcc u, RangeOcc r)
    {
        var uEnd = u.Index + u.Length;
        var rEnd = r.Index + r.Length;

        if (u.Index < r.Index)
            return r.Index - uEnd <= MaxGapUnitRange;

        if (r.Index < u.Index)
            return u.Index - rEnd <= MaxGapUnitRange;

        return false;
    }

    private static IEnumerable<Match> EnumerateValueMatches(string text, List<(int Start, int End)> excluded)
    {
        foreach (Match m in ValueTokenRx.Matches(text))
        {
            if (!m.Success)
                continue;
            if (OverlapsExcluded(m.Index, m.Length, excluded))
                continue;
            if (IsLikelyYearNoise(m))
                continue;
            yield return m;
        }
    }

    private static List<(int Start, int End)> BuildExcludedSpans(string text)
    {
        var list = new List<(int Start, int End)>(64);
        foreach (Match m in DateRx.Matches(text))
            list.Add((m.Index, m.Index + m.Length));
        foreach (Match m in ReferenceRangeRx.Matches(text))
            list.Add((m.Index, m.Index + m.Length));
        foreach (Match m in ScientificLiteralRx.Matches(text))
            list.Add((m.Index, m.Index + m.Length));
        return list;
    }

    private static bool OverlapsExcluded(int index, int length, List<(int Start, int End)> intervals)
    {
        var end = index + length;
        foreach (var (s, e) in intervals)
        {
            if (end <= s || index >= e)
                continue;
            return true;
        }

        return false;
    }

    private static bool IsLikelyYearNoise(Match nm)
    {
        if (nm.Length != 4 || nm.Value.Contains('.', StringComparison.Ordinal))
            return false;
        if (!int.TryParse(nm.Value, Inv, out var y))
            return false;
        return y is >= 1900 and <= 2100;
    }

    private static List<RangeOcc> CollectRangeOccurrences(string text)
    {
        var list = new List<RangeOcc>();
        foreach (Match m in ReferenceRangeRx.Matches(text))
        {
            if (!TryParseDouble(m.Groups["rmin"].Value, out var a)
                || !TryParseDouble(m.Groups["rmax"].Value, out var b))
                continue;
            if (a > b)
                (a, b) = (b, a);
            list.Add(new RangeOcc(m.Index, m.Length, a, b));
        }

        return list;
    }

    private static List<UnitOcc> CollectUnitOccurrences(string text)
    {
        var list = new List<UnitOcc>();
        foreach (Match m in UnitTokenRx.Matches(text))
            list.Add(new UnitOcc(m.Index, m.Length, m.Value));
        return list;
    }

    /// <summary>Up to 50 chars before value; strip digits, dates, units; last meaningful token.</summary>
    private static string ExtractMedicalTestName(string text, int valueStartIndex)
    {
        if (valueStartIndex <= 0)
            return string.Empty;

        var start = Math.Max(0, valueStartIndex - 50);
        var len = valueStartIndex - start;
        var w = text.AsSpan(start, len).ToString();

        w = DateRx.Replace(w, " ");
        w = Regex.Replace(w, @"\d+(?:\.\d+)?", " ", RegexOptions.CultureInvariant);
        w = Regex.Replace(
            w,
            @"(10\s*\^\s*\d+\s*/\s*[лl]|нг/[мm][лl]|г/[лl]|г/дл|ммоль/[лl]|мкмоль/[лl]|мг/[лl]|мкг/мл|%|фл|пг|мм/час)",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        while (w.Contains("  ", StringComparison.Ordinal))
            w = w.Replace("  ", " ", StringComparison.Ordinal);

        var tokens = Regex.Split(w.Trim(), @"\s+")
            .Where(t => t.Length > 0)
            .ToArray();
        if (tokens.Length == 0)
            return string.Empty;

        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            var t = tokens[i].Trim();
            if (t.Length < 2)
                continue;
            if (IsLatinAnalyteCodeToken(t))
                continue;
            if (!Regex.IsMatch(t, @"\p{L}"))
                continue;
            return t;
        }

        return tokens[^1];
    }

    private static bool IsLatinAnalyteCodeToken(string token)
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

    /// <summary>Force canonical units for known analytes when OCR links the wrong token.</summary>
    private static string ApplyAnalyteUnitRules(string testName, string linkedUnitRaw, string textAfterValue)
    {
        var u = linkedUnitRaw.Trim();
        var n = testName.ToLowerInvariant();
        var after = textAfterValue;

        if (n.Contains("гематокрит", StringComparison.Ordinal) || n.Contains("hct", StringComparison.Ordinal))
            return "%";

        if (n.Contains("эритроцит", StringComparison.Ordinal) || n == "rbc")
            return PickOrDefaultSciUnit(after, u, exp: 12, fallback: "10^12/л");

        if (n.Contains("тромбоцит", StringComparison.Ordinal) || n.Contains("plt", StringComparison.Ordinal))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("лейкоцит", StringComparison.Ordinal) || n.Contains("wbc", StringComparison.Ordinal))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("нейтрофил", StringComparison.Ordinal) && (n.Contains("абс", StringComparison.Ordinal) || n.Contains("#", StringComparison.Ordinal)))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("лимфоцит", StringComparison.Ordinal) && (n.Contains("абс", StringComparison.Ordinal) || n.Contains("#", StringComparison.Ordinal)))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("моноцит", StringComparison.Ordinal) && (n.Contains("абс", StringComparison.Ordinal) || n.Contains("#", StringComparison.Ordinal)))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("эозинофил", StringComparison.Ordinal) && (n.Contains("абс", StringComparison.Ordinal) || n.Contains("#", StringComparison.Ordinal)))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("базофил", StringComparison.Ordinal) && (n.Contains("абс", StringComparison.Ordinal) || n.Contains("#", StringComparison.Ordinal)))
            return PickOrDefaultSciUnit(after, u, exp: 9, fallback: "10^9/л");

        if (n.Contains("гемоглобин", StringComparison.Ordinal) || n.Contains("hgb", StringComparison.Ordinal))
        {
            if (u.Contains("г/л", StringComparison.Ordinal))
                return u;
            return "г/л";
        }

        if (n.Contains("средний объем эритроцита", StringComparison.Ordinal) || n.Contains("mcv", StringComparison.Ordinal))
            return "фл";

        if (n.Contains("среднее содержание", StringComparison.Ordinal) && n.Contains("hb", StringComparison.Ordinal))
            return "пг";

        if (n.Contains("средняя концентрация", StringComparison.Ordinal) || n.Contains("mchc", StringComparison.Ordinal))
            return "г/л";

        if (n.Contains("rdw", StringComparison.Ordinal) || n.Contains("распределение эритроцитов", StringComparison.Ordinal))
            return "%";

        return u;
    }

    private static string PickOrDefaultSciUnit(string afterValue, string linkedUnit, int exp, string fallback)
    {
        var ul = linkedUnit.ToLowerInvariant();
        var expMarker = exp == 12 ? "^12" : "^9";
        if (ul.Contains(expMarker, StringComparison.Ordinal) && ul.Contains("/л", StringComparison.Ordinal))
            return linkedUnit;
        if (ul.Contains('^') && ul.Contains("/л", StringComparison.Ordinal))
            return linkedUnit;

        var pattern = exp == 12 ? @"10\s*\^\s*12\s*/\s*[лl]" : @"10\s*\^\s*9\s*/\s*[лl]";
        if (Regex.IsMatch(afterValue, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return $"10^{exp}/л";

        return fallback;
    }

    private string RunTrackedNormalization(string raw) =>
        LabNormalizationPipeline.NormalizeWithReport(raw.Trim(), _logger).NormalizedText;

    private static IReadOnlyList<LabValue> SortClinicalFirst(List<LabValue> rows)
    {
        return rows
            .OrderBy(v => LabResultClassifier.SortRank(LabResultClassifier.Classify(v)))
            .ThenBy(v => v.SourceOrder)
            .ToList();
    }

    private static LabResult ToLabResult(LabValue v)
    {
        var status = LabResultClassifier.Classify(v);
        if (status == "CRITICAL_LOW")
            status = "LOW";

        return new LabResult
        {
            TestName = v.Name,
            Value = v.Value,
            Unit = v.Unit,
            RefMin = v.RefMin,
            RefMax = v.RefMax,
            Status = status
        };
    }

    private static bool TryParseDouble(string token, out double v) =>
        double.TryParse(token.Replace(',', '.'), NumberStyles.Any, Inv, out v);

    private readonly struct UnitOcc
    {
        public UnitOcc(int index, int length, string text)
        {
            Index = index;
            Length = length;
            Text = text;
        }

        public int Index { get; }
        public int Length { get; }
        public string Text { get; }
    }

    private readonly struct RangeOcc
    {
        public RangeOcc(int index, int length, double min, double max)
        {
            Index = index;
            Length = length;
            Min = min;
            Max = max;
        }

        public int Index { get; }
        public int Length { get; }
        public double Min { get; }
        public double Max { get; }
    }
}
