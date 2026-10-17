using System.Text;
using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab;

/// <summary>
/// Splits flattened OCR lab text into logical rows using known analyte / abbreviation boundaries
/// before value+unit+reference parsing.
/// </summary>
public static class LabRowReconstructor
{
    /// <summary>Start index in the collapsed page string and the segment text.</summary>
    public readonly record struct RowSegment(int StartIndex, string Text);

    private static readonly string RowStartAlternation = BuildAlternation();

    private static readonly Regex RowBoundaryRx = new(
        $@"(?<=^|[\s.;:,*#()])(?=(?:{RowStartAlternation}))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Indices where a known analyte/phrase begins (for table row reconstruction).</summary>
    internal static void CollectMedicalAnchorIndices(string page, ISet<int> indices)
    {
        foreach (Match m in RowBoundaryRx.Matches(page))
            indices.Add(m.Index);
    }

    private static readonly Regex HasValueUnitRefRx = new(
        @"\d+(?:\.\d+)?\s*(?:нг/|г/|10\s*\^|10\^|%|фл|пг|мм/час)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingDateTimeRx = new(
        @"\s+\d{2}\.\d{2}\.\d{4}(?:\s+\d{1,2}:\d{2})?.*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TrailingFooterRx = new(
        @"\s+(?:одобрил|выполнено|approved|врач|doctor|dr\.)\b.*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const int MaxRowLength = 168;

    private static string BuildAlternation()
    {
        var phrases = new[]
        {
            "распределение эритроцитов по объему",
            "распределение тромбоцитов по объему",
            "средний объем эритроцита",
            "среднее содержание hb в эритроците",
            "среднее содержание hb",
            "средняя концентрация hb в эритроците",
            "средний объем тромбоцита",
            "нейтрофилы (абс",
            "лимфоциты (абс",
            "моноциты (абс",
            "эозинофилы (абс",
            "базофилы (абс",
            "соэ (по вестергрену)",
            "соэ (по панченкову)",
            "цветной показатель",
            "гемоглобин",
            "эритроциты",
            "лейкоциты",
            "гематокрит",
            "тромбоциты",
            "тромбокрит",
            "ферритин",
            "лимфоциты",
            "моноциты",
            "нейтрофилы",
            "эозинофилы",
            "базофилы",
            "соэ",
            "глюкоза",
            "креатинин",
            "мочевина",
            "билирубин",
            "общий белок",
            "альбумин",
            "алт",
            "аст",
            "щелочная фосфатаза",
            "ггт",
            "железо",
            "трансферрин",
            "кальций",
            "магний",
            "калий",
            "натрий",
            "холестерин",
            "триглицериды",
            "mcv",
            "mch",
            "mchc",
            "rdw",
            "mpv",
            "pdw",
            "pct",
            "plt",
            "wbc",
            "rbc",
            "hgb",
            "hct",
            "neu",
            "eos",
            "bas",
            "mon",
            "lym",
            "crp",
            "ldl",
            "hdl",
        };

        return string.Join("|", phrases.OrderByDescending(static p => p.Length).Select(Regex.Escape));
    }

    /// <summary>Builds non-overlapping row segments from normalized, collapsed page text.</summary>
    public static IReadOnlyList<RowSegment> BuildSegments(string collapsedPageText)
    {
        if (string.IsNullOrWhiteSpace(collapsedPageText))
            return Array.Empty<RowSegment>();

        var page = collapsedPageText.Trim();
        var matches = RowBoundaryRx.Matches(page);
        if (matches.Count == 0)
            return new[] { new RowSegment(0, page) };

        var list = new List<RowSegment>(matches.Count + 1);

        var firstStart = matches[0].Index;
        if (firstStart > 0)
        {
            var preamble = page[..firstStart].Trim();
            if (preamble.Length >= 12 && HasValueUnitRefRx.IsMatch(preamble))
                list.Add(new RowSegment(0, preamble));
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : page.Length;
            if (end <= start)
                continue;
            var seg = page.AsSpan(start, end - start).Trim().ToString();
            if (seg.Length > 0)
                list.Add(new RowSegment(start, seg));
        }

        return list.Count > 0 ? list : new[] { new RowSegment(0, page) };
    }

    /// <summary>Trims footer noise and caps row length so one analyte line fits the parser window.</summary>
    public static string CleanRowSegment(string rowText)
    {
        if (string.IsNullOrWhiteSpace(rowText))
            return string.Empty;

        var t = rowText.Trim();
        t = TrailingDateTimeRx.Replace(t, string.Empty);
        t = TrailingFooterRx.Replace(t, string.Empty);
        t = CollapseSpaces(t);

        if (t.Length > MaxRowLength)
            t = SmartTruncate(t, MaxRowLength);

        return t.Trim();
    }

    /// <summary>False if the row cannot contain a measurable lab line (filters "к." garbage).</summary>
    public static bool IsValidRowForParsing(string rowText)
    {
        if (string.IsNullOrWhiteSpace(rowText) || rowText.Length < 8)
            return false;

        if (!rowText.Any(char.IsDigit))
            return false;

        var letters = rowText.Count(char.IsLetter);
        if (letters < 3)
            return false;

        var junk = rowText.Count(c => !IsAllowedRowChar(c));
        if (junk > rowText.Length * 0.38)
            return false;

        if (rowText.Trim().Length <= 2)
            return false;

        return true;
    }

    /// <summary>
    /// Parses a normalized page into <see cref="LabResult"/> using row reconstruction then per-row regex extract.
    /// </summary>
    public static List<LabResult> ParseNormalizedPageToLabResults(string normalizedCollapsedPage, int pageIndex = 0)
    {
        var page = LabTableRowExtractor.CollapseLabText(normalizedCollapsedPage);
        var segments = BuildSegments(page);
        var values = new List<LabValue>(32);

        if (segments.Count >= 2)
        {
            foreach (var seg in segments)
            {
                if (!IsValidRowForParsing(seg.Text))
                    continue;
                var leadingTrim = seg.Text.Length - seg.Text.TrimStart().Length;
                var cleaned = CleanRowSegment(seg.Text);
                if (cleaned.Length < 8)
                    continue;
                values.AddRange(LabTableRowExtractor.ExtractFromSegment(
                    cleaned,
                    page,
                    seg.StartIndex + leadingTrim,
                    pageIndex));
            }

            if (values.Count == 0)
                values.AddRange(LabTableRowExtractor.ExtractFromPages(new[] { page }));
        }
        else
            values.AddRange(LabTableRowExtractor.ExtractFromPages(new[] { page }));

        values = values
            .OrderBy(v => LabResultClassifier.SortRank(LabResultClassifier.Classify(v)))
            .ThenBy(v => v.SourceOrder)
            .ToList();

        return values.Select(ToLabResult).ToList();
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

    private static bool IsAllowedRowChar(char c)
    {
        if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            return true;
        return "()#%-^/.,№;:+*?'\u2011\u2013\u2014".Contains(c, StringComparison.Ordinal);
    }

    private static string SmartTruncate(string t, int maxLen)
    {
        if (t.Length <= maxLen)
            return t;

        var cut = t[..maxLen];
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > maxLen / 2 ? cut[..lastSpace].TrimEnd() : cut.TrimEnd();
    }

    private static string CollapseSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
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

        return sb.ToString().Trim();
    }
}
