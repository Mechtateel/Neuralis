using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab;

/// <summary>
/// Self-healing OCR normalization with mandatory audit records. Does not apply risky numeric edits;
/// suspicious patterns are logged as warnings with <see cref="NormalizationChange.Applied"/> false.
/// </summary>
public static class LabNormalizationPipeline
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int SnippetMax = 120;

    private static readonly Regex AnalyteStarRx = new(
        @"\*+(?=[\p{IsCyrillic}])",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SciSlashRx = new(
        @"10\s+(\d{1,2})\s*/\s*[лl]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SciStarSlashRx = new(
        @"10\s*\*\s*(\d{1,2})\s*/\s*[лl]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Sci1012Rx = new(
        @"1012\s*/\s*[лl]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Sci109Rx = new(
        @"109\s*/\s*[лl]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SciBareSpaceRx = new(
        @"(^|\s)10\s+(\d{1,2})(?=\s|$|/|[лl]|\s*/\s*[лl])",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SciBareAsteriskRx = new(
        @"(^|\s)10\s*\*\s*(\d{1,2})(?=\s|$|/|[лl]|\s*/\s*[лl])",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GluedUnitRx = new(
        @"(г/л|нг/мл|мкг/мл|мг/л|г/дл)(\d{1,8})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Only 1–2 digit row index when followed by space + Cyrillic word (avoids eating values like 168 г/л).</summary>
    private static readonly Regex RowIndexLineStartRx = new(
        @"^(\d{1,2})(?=\s+\p{IsCyrillic})",
        RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex RowIndexAfterBoundaryRx = new(
        @"(?<=[\s;:|*#_])(\d{1,2})(?=\s+\p{IsCyrillic})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex UnicodeSuperRx = new(
        @"(?<=[\p{L}])[\u00B2\u00B3\u00B9\u2070-\u2079\u2080-\u2089]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AsciiFootnoteRx = new(
        @"(?<=[\p{L}][\p{L}][\p{L}])([1-9]\d?)(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DateGlueRx = new(
        @"(?<=\d)(?=\d{2}\.\d{2}\.\d{4})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SuspiciousMantissaNearSciRx = new(
        @"\b(\d{1,2}\.\d{2})\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SciContextRx = new(
        @"10\s*[\^]\s*(?:9|12)|10\s+(?:9|12)\s*/\s*[лl]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Runs the full lab OCR normalization with a trace for every applied (or explicitly skipped) fix.</summary>
    public static LabNormalizationResult NormalizeWithReport(string? text, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new LabNormalizationResult
            {
                OriginalText = string.Empty,
                NormalizedText = string.Empty,
                Changes = new List<NormalizationChange>()
            };
        }

        var original = text;
        var changes = new List<NormalizationChange>(32);
        var current = text;

        current = ReplaceDescendingMatches(current, AnalyteStarRx, _ => string.Empty, changes,
            "AnalyteMarkerStripped",
            _ => "Leading * noise before Cyrillic analyte token");

        current = ReplaceDescendingMatches(current, SciSlashRx, m => $"10^{m.Groups[1].Value}/л", changes,
            "ScientificNotationNormalized",
            _ => "Spaced scientific unit normalized to caret form (exponent in unit, not value)");

        current = ReplaceDescendingMatches(current, SciStarSlashRx, m => $"10^{m.Groups[1].Value}/л", changes,
            "ScientificNotationNormalized",
            _ => "Asterisk-style scientific unit normalized to caret form");

        current = ReplaceDescendingMatches(current, Sci1012Rx, _ => "10^12/л", changes,
            "ScientificNotationNormalized",
            _ => "Glued 1012/л normalized to 10^12/л");

        current = ReplaceDescendingMatches(current, Sci109Rx, _ => "10^9/л", changes,
            "ScientificNotationNormalized",
            _ => "Glued 109/л normalized to 10^9/л");

        current = ReplaceDescendingMatches(current, SciBareSpaceRx,
            m => $"{m.Groups[1].Value}10^{m.Groups[2].Value}",
            changes,
            "ScientificNotationNormalized",
            _ => "Bare spaced exponent (10 12) merged into 10^n for unit binding");

        current = ReplaceDescendingMatches(current, SciBareAsteriskRx,
            m => $"{m.Groups[1].Value}10^{m.Groups[2].Value}",
            changes,
            "ScientificNotationNormalized",
            _ => "Bare 10*12 style exponent merged into 10^n");

        current = ReplaceDescendingMatches(current, GluedUnitRx, m =>
            {
                var digits = m.Groups[2].Value;
                return digits.Length > 6 ? m.Value : $"{m.Groups[1].Value} {digits}";
            },
            changes,
            "GluedUnitSplit",
            _ => "Inserted space between unit token and following digits (reference/value boundary)");

        current = ReplaceDescendingMatches(current, RowIndexLineStartRx, _ => string.Empty, changes,
            "RowIndexRemoved",
            _ => "Line-leading 1–2 digit index removed only when followed by space + Cyrillic word");

        current = ReplaceDescendingMatches(current, RowIndexAfterBoundaryRx, _ => string.Empty, changes,
            "RowIndexRemoved",
            _ => "Same row-index rule after punctuation/whitespace boundary");

        DetectSuspiciousMantissaNearScientific(current, changes, logger);

        current = ApplyFixBrokenSpacingTracked(current, changes, passLabel: "primary");

        current = ReplaceDescendingMatches(current, UnicodeSuperRx, _ => string.Empty, changes,
            "SuperscriptRemoved",
            _ => "Unicode superscript digits after letter treated as footnote artifact");

        current = ReplaceDescendingMatches(current, AsciiFootnoteRx, _ => string.Empty, changes,
            "SuperscriptRemoved",
            _ => "ASCII 1–2 digit footnote after ≥3 letters (avoids stripping г/л130-style refs)");

        current = CollapseSpacesTracked(current, changes);

        current = ApplyNormalizeTextTracked(current, changes);

        current = ReplaceDescendingMatches(current, DateGlueRx, _ => " ", changes,
            "DateBoundarySpaced",
            _ => "Inserted space before dd.mm.yyyy glued to trailing digit in reference");

        current = CollapseSpacesTracked(current, changes);

        current = ApplyFixBrokenSpacingTracked(current, changes, passLabel: "post-normalize");

        var result = new LabNormalizationResult
        {
            OriginalText = original,
            NormalizedText = current,
            Changes = changes
        };

        LogNormalizationReport(result, logger);
        return result;
    }

    /// <summary>Human-readable multi-line report for logs or UI.</summary>
    public static string BuildHumanReadableReport(LabNormalizationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Normalization Report]");
        if (result.Changes.Count == 0)
        {
            sb.AppendLine("• No transformations applied.");
            return sb.ToString();
        }

        foreach (var c in result.Changes)
        {
            var flag = c.Applied ? string.Empty : "(skipped — safeguard) ";
            sb.Append("• ").Append(flag).Append(c.Type).Append(": \"")
                .Append(Snippet(c.Before)).Append("\" → \"").Append(Snippet(c.After))
                .Append("\" @ ").Append(c.Position).Append(" — ").AppendLine(c.Reason);
        }

        return sb.ToString().TrimEnd();
    }

    private static void LogNormalizationReport(LabNormalizationResult result, ILogger? logger)
    {
        if (logger == null)
            return;

        if (result.Changes.Count == 0)
        {
            logger.LogDebug("Lab normalization: no changes (length {Len})", result.OriginalText.Length);
            return;
        }

        logger.LogInformation("{Report}", BuildHumanReadableReport(result));
    }

    private static string ReplaceDescendingMatches(
        string input,
        Regex rx,
        Func<Match, string> computeReplacement,
        List<NormalizationChange> changes,
        string type,
        Func<Match, string> reason)
    {
        var text = input;
        var matches = rx.Matches(text).Cast<Match>().Where(m => m.Success).OrderByDescending(m => m.Index).ToList();
        foreach (var m in matches)
        {
            var before = m.Value;
            var after = computeReplacement(m);
            if (string.Equals(before, after, StringComparison.Ordinal))
                continue;

            changes.Add(new NormalizationChange
            {
                Type = type,
                Before = Snippet(before),
                After = Snippet(after),
                Position = m.Index,
                Reason = reason(m),
                Applied = true
            });

            text = text.Remove(m.Index, m.Length).Insert(m.Index, after);
        }

        return text;
    }

    private static void DetectSuspiciousMantissaNearScientific(string text, List<NormalizationChange> changes, ILogger? logger)
    {
        foreach (Match m in SuspiciousMantissaNearSciRx.Matches(text))
        {
            if (!m.Success)
                continue;
            var start = Math.Max(0, m.Index - 48);
            var len = Math.Min(text.Length - start, m.Length + 96);
            if (len <= 0)
                continue;
            var window = text.AsSpan(start, len);
            if (!SciContextRx.IsMatch(window.ToString()))
                continue;
            if (!double.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, Inv, out var v))
                continue;
            if (v is < 8d or > 24d)
                continue;

            var change = new NormalizationChange
            {
                Type = "SuspiciousMergedNumber",
                Before = Snippet(m.Value),
                After = Snippet(m.Value),
                Position = m.Index,
                Reason =
                    "Numeric token near scientific unit resembles glued mantissa+exponent; not auto-modified (downstream mantissa recovery).",
                Applied = false
            };
            changes.Add(change);
            logger?.LogWarning(
                "OCR safeguard: {Reason} Context @ {Position}: \"{Snippet}\"",
                change.Reason,
                m.Index,
                Snippet(m.Value));
        }
    }

    private static string ApplyFixBrokenSpacingTracked(string input, List<NormalizationChange> changes, string passLabel)
    {
        var after = LabTextNormalizer.FixBrokenSpacing(input);
        if (string.Equals(input, after, StringComparison.Ordinal))
            return after;

        changes.Add(new NormalizationChange
        {
            Type = "LetterDigitSpacingHeuristic",
            Before = Snippet(input),
            After = Snippet(after),
            Position = 0,
            Reason = $"Iterative letter↔digit spacing ({passLabel} pass); full strings truncated in audit — compare hashes or originals in tests.",
            Applied = true
        });

        return after;
    }

    private static string ApplyNormalizeTextTracked(string input, List<NormalizationChange> changes)
    {
        var after = LabTextNormalizer.NormalizeText(input);
        if (string.Equals(input, after, StringComparison.Ordinal))
            return after;

        changes.Add(new NormalizationChange
        {
            Type = "TextFormatNormalized",
            Before = Snippet(input),
            After = Snippet(after),
            Position = 0,
            Reason = "NBSP→space, comma decimals→dot, lowercase, space collapse (structural only).",
            Applied = true
        });

        return after;
    }

    private static string CollapseSpacesTracked(string input, List<NormalizationChange> changes)
    {
        var s = input;
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        s = s.Trim();
        if (string.Equals(s, input, StringComparison.Ordinal))
            return s;

        changes.Add(new NormalizationChange
        {
            Type = "WhitespaceCollapsed",
            Before = Snippet(input),
            After = Snippet(s),
            Position = 0,
            Reason = "Collapsed repeated spaces after targeted replacements.",
            Applied = true
        });

        return s;
    }

    private static string Snippet(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;
        s = s.Replace('\r', ' ').Replace('\n', ' ');
        return s.Length <= SnippetMax ? s : s[..SnippetMax] + "…";
    }
}
