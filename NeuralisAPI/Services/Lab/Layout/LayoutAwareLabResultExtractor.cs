using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Parses value / optional unit / optional ref from role-assigned row text only (no page-wide number hunt).</summary>
public static class LayoutAwareLabResultExtractor
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly Regex ValueRx = new(@"\d{1,5}(?:[.,]\d+)?", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DateRx = new(@"\b\d{2}[.]\d{2}[.]\d{2,4}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TimeRx = new(@"\b\d{2}:\d{2}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex YearRx = new(@"\b(?:19|20)\d{2}\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex RefRx = new(
        @"(?<rmin>\d{1,5}(?:[.,]\d+)?)\s*[\u002D\u2013\u2014]\s*(?<rmax>\d{1,5}(?:[.,]\d+)?)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FootnoteOnWordRx = new(@"(?<=\p{L})(\d{1,2})(?=\s|$)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] QualitativeTokens =
    {
        "отрицательно", "положительно", "обнаружено", "не обнаружено"
    };

    public static LabValue? TryExtract(
        string cleanedLabel,
        RowRoleAssignment roles,
        int pageIndex,
        int rowOrder,
        double tableConfidence,
        ILogger? logger = null)
    {
        var resultCell = roles.ResultClusterText.Trim();
        if (string.IsNullOrEmpty(resultCell))
            return null;

        var name = CleanLabel(cleanedLabel);
        if (name.Length < 2)
        {
            name = TryExtractLabelPrefixFromResultCell(resultCell);
            if (name.Length < 2)
                return null;
        }

        var rejectedCandidates = new List<string>(4);
        var vm = ValueRx.Match(resultCell);
        var isQualitative = false;
        var qualitativeText = string.Empty;
        double val = 0;
        var unit = string.Empty;
        if (TryParseQualitative(resultCell, out qualitativeText))
        {
            isQualitative = true;
        }
        else
        {
            if (!TryPickNumericCandidate(resultCell, out vm, rejectedCandidates))
                return null;

            if (!double.TryParse(vm.Value.Replace(',', '.'), NumberStyles.Any, Inv, out val))
                return null;

            val = Math.Round(val, 2, MidpointRounding.AwayFromZero);
            var afterVal = resultCell.Substring(vm.Index + vm.Length).TrimStart();
            if (afterVal.Length > 0)
            {
                var unitTok = Regex.Match(afterVal, @"^\S+");
                if (unitTok.Success)
                    unit = LabTableRowExtractor.ExternalCanonicalizeUnit(unitTok.Value);
            }
        }

        double rmin = 0, rmax = 0;
        var refText = roles.ReferenceClusterText;
        if (string.IsNullOrWhiteSpace(refText))
            refText = roles.FarRightText;
        var hasRef = false;
        var rm = RefRx.Match(refText.Trim());
        if (rm.Success
            && double.TryParse(rm.Groups["rmin"].Value.Replace(',', '.'), NumberStyles.Any, Inv, out rmin)
            && double.TryParse(rm.Groups["rmax"].Value.Replace(',', '.'), NumberStyles.Any, Inv, out rmax))
        {
            hasRef = true;
            if (rmin > rmax)
                (rmin, rmax) = (rmax, rmin);
            rmin = Math.Round(rmin, 2, MidpointRounding.AwayFromZero);
            rmax = Math.Round(rmax, 2, MidpointRounding.AwayFromZero);
        }

        var lowConfidence = !isQualitative && (!hasRef || string.IsNullOrWhiteSpace(unit) || tableConfidence < 0.55);
        var sourceOrder = pageIndex * 1_000_000_000 + rowOrder * 1_000;
        var conf = tableConfidence >= 0.68 ? "HIGH" : tableConfidence >= 0.42 ? "MEDIUM" : "LOW";
        var detected = isQualitative ? qualitativeText : vm.Value;
        logger?.LogDebug(
            "Value extraction: detected_value={Detected}; rejected_candidates={Rejected}; final_value_source={Source}",
            detected,
            rejectedCandidates.Count == 0 ? "(none)" : string.Join(", ", rejectedCandidates),
            "result_col");

        return new LabValue(
            name,
            val,
            isQualitative ? string.Empty : unit,
            rmin,
            rmax,
            "Unknown",
            sourceOrder,
            lowConfidence,
            hasRef,
            conf,
            pageIndex,
            rowOrder,
            isQualitative ? "qualitative" : "numeric",
            isQualitative ? qualitativeText : null,
            isQualitative ? null : val,
            isQualitative ? MapQualitativeToBoolean(qualitativeText) : null);
    }

    private static string CleanLabel(string label)
    {
        var t = label.Trim();
        t = Regex.Replace(t, @"^\d{1,3}\s+", "", RegexOptions.CultureInvariant);
        t = FootnoteOnWordRx.Replace(t, "");
        t = Regex.Replace(t, @"\s+", " ");
        return LabTableRowExtractor.ExternalNormalizeNameDisplay(t);
    }

    private static string TryExtractLabelPrefixFromResultCell(string resultCell)
    {
        var m = Regex.Match(resultCell, @"^(?<name>[\p{L}\s\-\(\)]+?)\s+\d");
        if (!m.Success)
            return string.Empty;
        return CleanLabel(m.Groups["name"].Value);
    }

    private static bool TryParseQualitative(string resultCell, out string value)
    {
        value = string.Empty;
        var lower = resultCell.Trim().ToLowerInvariant();
        foreach (var token in QualitativeTokens.OrderByDescending(x => x.Length))
        {
            if (lower.Contains(token, StringComparison.Ordinal))
            {
                value = token;
                return true;
            }
        }

        return false;
    }

    private static bool? MapQualitativeToBoolean(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        if (t is "отрицательно" or "не обнаружено" or "negative" or "not detected")
            return false;
        if (t is "положительно" or "обнаружено" or "positive" or "detected")
            return true;
        return null;
    }

    private static bool TryPickNumericCandidate(string resultCell, out Match selected, List<string> rejected)
    {
        selected = null!;
        var dateRanges = DateRx.Matches(resultCell).Cast<Match>().Select(m => (m.Index, End: m.Index + m.Length)).ToList();
        var timeRanges = TimeRx.Matches(resultCell).Cast<Match>().Select(m => (m.Index, End: m.Index + m.Length)).ToList();
        var yearRanges = YearRx.Matches(resultCell).Cast<Match>().Select(m => (m.Index, End: m.Index + m.Length)).ToList();

        foreach (Match m in ValueRx.Matches(resultCell))
        {
            var s = m.Index;
            var e = m.Index + m.Length;
            if (OverlapsAny((s, e), dateRanges))
            {
                rejected.Add($"{m.Value}:date");
                continue;
            }

            if (OverlapsAny((s, e), timeRanges))
            {
                rejected.Add($"{m.Value}:time");
                continue;
            }

            if (m.Length == 4 && OverlapsAny((s, e), yearRanges))
            {
                rejected.Add($"{m.Value}:year");
                continue;
            }

            selected = m;
            return true;
        }

        return false;
    }

    private static bool OverlapsAny((int S, int E) r, List<(int S, int E)> ranges)
    {
        foreach (var x in ranges)
        {
            if (r.S < x.E && r.E > x.S)
                return true;
        }

        return false;
    }
}
