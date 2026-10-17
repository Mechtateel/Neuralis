using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Rejects header-like, metadata, and non–lab-result rows using generic text shape only.</summary>
public static class LabRowValidator
{
    private static readonly Regex ManyDigitsRx = new(@"\d{8,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DateOnlyRx = new(@"^\s*\d{1,2}[./]\d{1,2}[./]\d{2,4}\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EmailRx = new(@"\S+@\S+\.\S+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhoneRx = new(@"(?:\+\d[\d\s\-().]{8,}\d|\b\d{10,}\b)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] HeaderTerms =
    {
        "component", "result", "norm", "comment", "performed", "approved",
        "компонент", "результат", "норма", "комментар", "выполн", "одобр",
    };
    private static readonly string[] QualitativeTerms =
    {
        "отрицательно", "положительно", "обнаружено", "не обнаружено",
        "теріс", "оң",
        "negative", "positive", "detected", "not detected"
    };

    private static readonly Regex DateTimeLikeRx = new(
        @"^\s*\d{1,2}[./]\d{1,2}[./]\d{2,4}(?:\s+\d{1,2}:\d{2})?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsPlausibleDataRow(
        TableGeometricRow geo,
        RowRoleAssignment roles,
        int rowIndexInTable,
        out string? rejectReason)
    {
        rejectReason = null;
        var label = roles.LabelText.Trim();
        var full = string.Join(' ', geo.Words.Select(w => w.Text));

        if (label.Length == 0)
        {
            rejectReason = "empty_label";
            return false;
        }

        var letterCount = label.Count(char.IsLetter);
        var digitCount = label.Count(char.IsDigit);
        if (letterCount == 0 && digitCount > 2)
        {
            rejectReason = "label_mostly_digits";
            return false;
        }

        if (full.Length > 280 && full.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 35)
            return Reject("long_paragraph", out rejectReason);

        if (EmailRx.IsMatch(full))
            return Reject("email_like_row", out rejectReason);
        if (PhoneRx.IsMatch(full))
            return Reject("phone_like_row", out rejectReason);

        if (ManyDigitsRx.IsMatch(full))
        {
            rejectReason = "long_digit_run";
            return false;
        }

        if (DateOnlyRx.IsMatch(label))
        {
            rejectReason = "date_only_label";
            return false;
        }

        if (rowIndexInTable == 0 && LooksLikeGenericHeader(geo, roles))
        {
            rejectReason = "generic_table_header";
            return false;
        }
        if (LooksLikeHeaderVocabulary(full, roles))
        {
            rejectReason = "header_vocabulary_row";
            return false;
        }

        AnalyzeRowResultSignals(roles, out var hasNumericValue, out var hasQualitativeValue);
        if (!hasNumericValue && !hasQualitativeValue)
        {
            rejectReason = "no_numeric_or_qualitative_token_in_row";
            return false;
        }

        if (string.IsNullOrWhiteSpace(roles.ResultClusterText) && !hasNumericValue && !hasQualitativeValue)
        {
            rejectReason = "no_result_cluster";
            return false;
        }

        return true;
    }

    public static void AnalyzeRowResultSignals(RowRoleAssignment roles, out bool hasNumericValue, out bool hasQualitativeValue)
    {
        hasNumericValue =
            ContainsDigit(roles.ResultClusterText)
            || ContainsDigit(roles.ReferenceClusterText)
            || ContainsDigit(roles.FarRightText);

        var allRight = string.Join(' ', new[] { roles.ResultClusterText, roles.ReferenceClusterText, roles.FarRightText });
        hasQualitativeValue =
            ContainsQualitative(allRight)
            || IsQualitativeFallbackCandidate(roles.ResultClusterText);
    }

    private static bool ContainsDigit(string s)
    {
        foreach (var c in s)
        {
            if (char.IsDigit(c))
                return true;
        }

        return false;
    }

    private static bool LooksLikeGenericHeader(TableGeometricRow geo, RowRoleAssignment roles)
    {
        if (geo.Words.Count > 6)
            return false;
        if (roles.ResultClusterText.Any(char.IsDigit))
            return false;
        var t = roles.LabelText;
        if (t.Length > 48)
            return false;
        return !string.IsNullOrEmpty(t) && t == t.ToUpperInvariant() && t.Any(char.IsLetter);
    }

    private static bool LooksLikeHeaderVocabulary(string full, RowRoleAssignment roles)
    {
        var lower = full.ToLowerInvariant();
        var hit = HeaderTerms.Count(t => lower.Contains(t, StringComparison.Ordinal));
        if (hit < 2)
            return false;
        var numericSignal = ContainsDigit(roles.ResultClusterText) || ContainsDigit(roles.ReferenceClusterText);
        return !numericSignal;
    }

    private static bool ContainsQualitative(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        foreach (var q in QualitativeTerms)
        {
            if (t.Contains(q, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool IsQualitativeFallbackCandidate(string resultCell)
    {
        var t = resultCell.Trim();
        if (t.Length == 0)
            return false;
        if (ContainsDigit(t))
            return false;
        if (DateTimeLikeRx.IsMatch(t))
            return false;
        if (EmailRx.IsMatch(t) || PhoneRx.IsMatch(t))
            return false;

        var lower = t.ToLowerInvariant();
        if (HeaderTerms.Any(h => lower.Contains(h, StringComparison.Ordinal)))
            return false;

        var words = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 5)
            return false;
        return t.Any(char.IsLetter);
    }

    private static bool Reject(string reason, out string rejectReason)
    {
        rejectReason = reason;
        return false;
    }
}
