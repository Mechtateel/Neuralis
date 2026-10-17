using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NeuralisAPI.Services.Lab;

/// <summary>
/// Layout-aware pipeline: collapsed TEXT → candidate ROWs → per-row parse → <see cref="LabValue"/>.
/// Format-agnostic: row = [text left][numeric value][text right]; no analyte or unit whitelist in logic.
/// </summary>
public static class LabTableReconstructionParser
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly Regex DateFragmentRx = new(
        @"\d{2}\.\d{2}\.\d{4}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TimeFragmentRx = new(
        @"\b\d{1,2}:\d{2}(?::\d{2})?\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Decimal or integer token (lab-style, not scientific).</summary>
    private static readonly Regex ValueCandidateRx = new(
        @"\d{1,5}(?:[.,]\d+)?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ReferenceRangeRx = new(
        @"(?<rmin>\d{1,5}(?:[.,]\d+)?)\s*[\u002D\u2013\u2014]\s*(?<rmax>\d{1,5}(?:[.,]\d+)?)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OcrFootnoteBeforeNumericValueRx = new(
        @"(?<=\p{L})(\d{1,2})(?=\s+\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>After digits or %, whitespace then a letter run ≥3 — common table row boundary (any script).</summary>
    private static readonly Regex GenericRowBoundaryRx = new(
        @"(?<=[\d%])\s+(?=[\p{L}]{3,})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmailLikeRx = new(
        @"\S+@\S+\.\S+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LongDigitRunRx = new(
        @"\d{9,}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PhoneLikeRx = new(
        @"(?:\+\d[\d\s\-().]{8,}\d|\b\d{3}[\s.\-]?\d{3}[\s.\-]?\d{4}\b)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Parse one normalized collapsed page into lab values using row reconstruction only.</summary>
    public static List<LabValue> ParsePage(string pageCollapsed, int pageIndex, ILogger? logger = null)
    {
        var page = CollapseInternalSpaces(pageCollapsed.Trim());
        if (page.Length == 0)
            return new List<LabValue>();

        var rows = ExtractCandidateRows(page);
        var results = new List<LabValue>(rows.Count);
        var seq = 0;

        foreach (var (startIndex, rowText) in rows)
        {
            if (IsNoiseRow(rowText))
                continue;

            var working = ApplyOcrFootnoteCleanupBeforeValue(rowText);
            var v = ParseSingleRowToLabValue(working, page, startIndex, pageIndex, ref seq, rowText, logger);
            if (v != null)
                results.Add(v);
        }

        if (results.Count == 0)
            return results;

        return results
            .OrderBy(x => LabResultClassifier.SortRank(LabResultClassifier.Classify(x)))
            .ThenBy(x => x.SourceOrder)
            .ToList();
    }

    /// <summary>TEXT → ROW segments using value-boundary heuristics only (no analyte dictionary).</summary>
    public static IReadOnlyList<(int StartIndex, string Text)> ExtractCandidateRows(string page)
    {
        var indices = new SortedSet<int> { 0, page.Length };

        foreach (Match m in GenericRowBoundaryRx.Matches(page))
        {
            var start = m.Index + m.Length;
            if (start < page.Length)
                indices.Add(start);
        }

        var arr = indices.ToArray();
        var rows = new List<(int, string)>(arr.Length);
        for (var i = 0; i < arr.Length - 1; i++)
        {
            var a = arr[i];
            var b = arr[i + 1];
            if (b <= a)
                continue;
            var slice = page.AsSpan(a, b - a).Trim();
            if (slice.Length == 0)
                continue;
            rows.Add((a, slice.ToString()));
        }

        return rows;
    }

    private static string CollapseInternalSpaces(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
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

        return sb.ToString();
    }

    private static string ApplyOcrFootnoteCleanupBeforeValue(string row) =>
        OcrFootnoteBeforeNumericValueRx.Replace(row, "");

    private static bool IsNoiseRow(string row)
    {
        if (string.IsNullOrWhiteSpace(row))
            return true;
        if (EmailLikeRx.IsMatch(row))
            return true;
        if (PhoneLikeRx.IsMatch(row))
            return true;
        if (LongDigitRunRx.IsMatch(row))
            return true;
        return false;
    }

    private static LabValue? ParseSingleRowToLabValue(
        string row,
        string fullPage,
        int rowStartInPage,
        int pageIndex,
        ref int seq,
        string originalRowText,
        ILogger? logger)
    {
        var corrections = new List<string>(4);
        if (!string.Equals(row, originalRowText, StringComparison.Ordinal))
            corrections.Add("FootnoteDigitsRemoved");

        if (!TryPickFirstValidValueMatch(row, out var valueMatch, out var rawNumeric))
            return null;

        var namePart = row[..valueMatch.Index].Trim();
        namePart = StripLeadingRowIndexGeneric(namePart);
        namePart = RemoveTrailingFootnoteDigits(namePart);
        namePart = CollapseInternalSpaces(namePart).Trim();
        var testName = LabTableRowExtractor.ExternalNormalizeNameDisplay(namePart);

        if (!ValidateNameAndLeftContext(testName, namePart))
            return null;

        if (!double.TryParse(rawNumeric.Replace(',', '.'), NumberStyles.Any, Inv, out var val))
            return null;

        val = MaybeNormalizeGluedLeadingDigit(val, rawNumeric, corrections);
        val = Math.Round(val, 2, MidpointRounding.AwayFromZero);

        var afterValue = row[(valueMatch.Index + valueMatch.Length)..].TrimStart();
        SplitRightSideGeneric(afterValue, out var unitRaw, out var afterUnit, corrections);

        var unit = string.IsNullOrWhiteSpace(unitRaw)
            ? string.Empty
            : LabTableRowExtractor.ExternalCanonicalizeUnit(unitRaw.Trim());
        if (string.IsNullOrWhiteSpace(unit) && !string.IsNullOrWhiteSpace(unitRaw))
            unit = unitRaw.Trim();

        var rangeMatch = ReferenceRangeRx.Match(afterUnit);
        double rmin = 0, rmax = 0;
        var hasRef = rangeMatch.Success
            && double.TryParse(rangeMatch.Groups["rmin"].Value.Replace(',', '.'), NumberStyles.Any, Inv, out rmin)
            && double.TryParse(rangeMatch.Groups["rmax"].Value.Replace(',', '.'), NumberStyles.Any, Inv, out rmax);

        if (hasRef)
        {
            if (rmin > rmax)
                (rmin, rmax) = (rmax, rmin);
            val = Math.Round(LabTableRowExtractor.ExternalRecoverMantissa(val, rmin, rmax, unit), 2, MidpointRounding.AwayFromZero);
            if (!IsGenericFinitePlausible(val, rmin, rmax, hasRef))
                return null;
        }
        else
        {
            corrections.Add("ReferenceMissing");
            if (!IsGenericFinitePlausible(val, 0, 0, false))
                return null;
        }

        var lowConfidence = !hasRef || string.IsNullOrWhiteSpace(unit);

        var absoluteValueIndex = rowStartInPage + valueMatch.Index;
        var section = LabTableRowExtractor.ExternalResolveSection(fullPage, absoluteValueIndex);
        var sourceOrder = pageIndex * 1_000_000_000 + rowStartInPage * 1_000 + seq++;

        LogParsedRow(logger, originalRowText, testName, val, corrections);

        return new LabValue(testName, val, unit, rmin, rmax, section, sourceOrder, lowConfidence, hasRef);
    }

    /// <summary>Generic bounds only (no analyte/unit rules).</summary>
    private static bool IsGenericFinitePlausible(double val, double rmin, double rmax, bool hasRef)
    {
        if (double.IsNaN(val) || double.IsInfinity(val))
            return false;
        if (val < -1e6 || val > 1e9)
            return false;
        if (!hasRef)
            return true;
        if (rmax <= rmin || rmin < -1e6 || rmax > 1e9)
            return false;
        return true;
    }

    /// <summary>
    /// If OCR glued a single leading digit before a two-digit integer part (e.g. 15.56 → 5.56), peel when pattern fits.
    /// Skips 10.x (valid tens) and multi-digit integers (e.g. 168.00).
    /// </summary>
    private static double MaybeNormalizeGluedLeadingDigit(double val, string rawNumeric, List<string> corrections)
    {
        var raw = rawNumeric.Trim();
        if (raw.Length < 4 || raw[0] is not (>= '0' and <= '9'))
            return val;
        var sep = raw.IndexOfAny([',', '.']);
        if (sep != 2)
            return val;
        if (raw[0] != '1' || raw[1] < '2' || raw[1] > '9')
            return val;

        var peeled = raw[1..];
        if (!double.TryParse(peeled.Replace(',', '.'), NumberStyles.Any, Inv, out var v2))
            return val;
        if (v2 >= val || v2 <= 0)
            return val;
        corrections.Add("SuspiciousMergedValueCorrected");
        return v2;
    }

    /// <summary>First token or empty; remainder after unit token. No unit whitelist.</summary>
    private static void SplitRightSideGeneric(string afterValue, out string unitRaw, out string afterUnit, List<string> corrections)
    {
        unitRaw = string.Empty;
        afterUnit = afterValue.TrimStart();
        var t = afterUnit;
        if (t.Length == 0)
        {
            corrections.Add("UnitMissing");
            return;
        }

        var rangeFirst = ReferenceRangeRx.Match(t);
        if (rangeFirst.Success && rangeFirst.Index == 0)
        {
            corrections.Add("UnitMissing");
            return;
        }

        var m = Regex.Match(t, @"^(\S+)");
        if (!m.Success)
        {
            corrections.Add("UnitMissing");
            return;
        }

        var first = m.Groups[1].Value;
        var rest = t[m.Length..].TrimStart();

        if (Regex.IsMatch(first, @"^\d{1,5}(?:[.,]\d+)?$"))
        {
            if (rest.Length > 0 && rest[0] is '-' or '\u2013' or '\u2014')
            {
                unitRaw = string.Empty;
                afterUnit = t;
                corrections.Add("UnitMissing");
                return;
            }
        }

        unitRaw = first;
        afterUnit = rest;
        if (string.IsNullOrWhiteSpace(unitRaw))
            corrections.Add("UnitMissing");
    }

    private static void LogParsedRow(ILogger? logger, string originalRow, string parsedName, double value, List<string> corrections)
    {
        if (logger == null)
            return;

        var corr = corrections.Count == 0 ? "(none)" : string.Join(", ", corrections);
        logger.LogDebug(
            "LabTableRow: OriginalRow={OriginalRow}; ParsedName={ParsedName}; Value={Value}; Corrections={Corrections}",
            originalRow, parsedName, value, corr);
        if (corrections.Contains("SuspiciousMergedValueCorrected"))
        {
            logger.LogInformation(
                "SuspiciousMergedValueCorrected: OriginalRow={OriginalRow}; ParsedName={ParsedName}; Value={Value}",
                originalRow, parsedName, value);
        }
    }

    private static bool ValidateNameAndLeftContext(string testName, string namePartRaw)
    {
        if (testName.Length < 3)
            return false;
        var letters = namePartRaw.Count(char.IsLetter);
        var digits = namePartRaw.Count(char.IsDigit);
        if (letters + digits == 0)
            return false;
        if (digits > (letters + digits) * 0.5)
            return false;

        var words = namePartRaw.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (words > 10)
            return false;
        if (namePartRaw.Length > 180)
            return false;

        return true;
    }

    private static string StripLeadingRowIndexGeneric(string namePart)
    {
        var t = namePart.TrimStart();
        t = Regex.Replace(t, @"^(\d{1,3})\s+(?=\p{L})", "", RegexOptions.CultureInvariant);
        return t.TrimStart();
    }

    private static string RemoveTrailingFootnoteDigits(string namePart)
    {
        var t = namePart.TrimEnd();
        return Regex.Replace(t, @"(?:\s+\d{1,2})+$", "", RegexOptions.CultureInvariant).TrimEnd();
    }

    private static bool TryPickFirstValidValueMatch(string row, out Match valueMatch, out string rawNumeric)
    {
        valueMatch = null!;
        rawNumeric = string.Empty;

        foreach (Match m in ValueCandidateRx.Matches(row))
        {
            if (!IsValidValueOccurrence(row, m))
                continue;
            valueMatch = m;
            rawNumeric = m.Value;
            return true;
        }

        return false;
    }

    private static bool IsValidValueOccurrence(string row, Match m)
    {
        if (OverlapsAny(row, m, DateFragmentRx))
            return false;
        if (OverlapsAny(row, m, TimeFragmentRx))
            return false;

        var v = m.Value;
        if (v.Length == 4 && int.TryParse(v, Inv, out var y) && y is >= 1900 and <= 2099)
            return false;

        if (IsPartOfLongDigitId(row, m.Index, m.Length))
            return false;

        return true;
    }

    private static bool IsPartOfLongDigitId(string row, int index, int length)
    {
        var start = index;
        while (start > 0 && char.IsDigit(row[start - 1]))
            start--;
        var end = index + length;
        while (end < row.Length && char.IsDigit(row[end]))
            end++;
        return end - start >= 8;
    }

    private static bool OverlapsAny(string row, Match m, Regex rx)
    {
        var end = m.Index + m.Length;
        foreach (Match d in rx.Matches(row))
        {
            if (m.Index < d.Index + d.Length && end > d.Index)
                return true;
        }

        return false;
    }

    /// <summary>Public wrapper returning <see cref="LabResult"/> for API use.</summary>
    public static List<LabResult> ParsePageToLabResults(string pageCollapsed, int pageIndex = 0)
    {
        return ParsePage(pageCollapsed, pageIndex)
            .Select(v =>
            {
                var s = LabResultClassifier.Classify(v);
                if (s == "CRITICAL_LOW")
                    s = "LOW";
                return new LabResult
                {
                    TestName = v.Name,
                    Value = v.Value,
                    Unit = v.Unit,
                    RefMin = v.RefMin,
                    RefMax = v.RefMax,
                    Status = s
                };
            })
            .ToList();
    }
}
