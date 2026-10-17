using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab;

public static class LabTextNormalizer
{
    /// <summary>
    /// Normalizes lab report text: NBSP → space, comma decimals → dot, collapse spaces, lowercase.
    /// Does not invent content — only structural cleanup for matching.
    /// </summary>
    public static string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\u00A0' || ch == '\u202F' || ch == '\u2007' || ch == '\u2060')
                sb.Append(' ');
            else if (ch == ',')
                sb.Append('.');
            else
                sb.Append(char.ToLowerInvariant(ch));
        }

        var s = sb.ToString();
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);

        return s.Trim();
    }

    /// <summary>
    /// Inserts a space before <c>dd.mm.yyyy</c> when glued to a trailing digit (e.g. <c>400.024.09.2024</c> → <c>400.0 24.09.2024</c>).
    /// </summary>
    public static string InsertSpaceBeforeDayMonthYearDates(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return Regex.Replace(text, @"(?<=\d)(?=\d{2}\.\d{2}\.\d{4})", " ", RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Normalizes OCR variants of scientific count units (mantissa stays separate; exponent belongs in the unit).
    /// </summary>
    public static string NormalizeScientificUnitNotation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = Regex.Replace(text, @"10\s+(\d{1,2})\s*/\s*[лl]", "10^$1/л", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"10\s*\*\s*(\d{1,2})\s*/\s*[лl]", "10^$1/л", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"1012\s*/\s*[лl]", "10^12/л", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"109\s*/\s*[лl]", "10^9/л", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return text;
    }

    /// <summary>
    /// Full scientific OCR normalization for lab text. Run <b>before</b> <see cref="CleanSuperscripts"/>.
    /// Preserves <c>10^12</c> style notation; converts spaced forms like <c>10 12</c> to <c>10^12</c>.
    /// </summary>
    public static string NormalizeScientificNotation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = NormalizeScientificUnitNotation(text);

        // Bare "10 12" / "10 * 12" (no slash yet) — not part of a larger number; exponent belongs to unit.
        text = Regex.Replace(
            text,
            @"(^|\s)10\s+(\d{1,2})(?=\s|$|/|[лl]|\s*/\s*[лl])",
            m => $"{m.Groups[1].Value}10^{m.Groups[2].Value}",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        text = Regex.Replace(
            text,
            @"(^|\s)10\s*\*\s*(\d{1,2})(?=\s|$|/|[лl]|\s*/\s*[лl])",
            m => $"{m.Groups[1].Value}10^{m.Groups[2].Value}",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        return text;
    }

    /// <summary>
    /// Removes footnote-style superscripts (digits or Unicode superscripts after word letters).
    /// Does not alter <c>10^12</c> (caret is not a letter) or decimals like <c>5.56</c>.
    /// Run after <see cref="NormalizeScientificNotation"/> and ideally after one <see cref="FixBrokenSpacing"/> pass
    /// so values like <c>г/л 130</c> are not glued to the unit suffix.
    /// </summary>
    public static string CleanSuperscripts(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // TYPE 2: Unicode superscript digits (¹²³, ⁰–⁹) immediately after a letter.
        text = Regex.Replace(
            text,
            @"(?<=[\p{L}])[\u00B2\u00B3\u00B9\u2070-\u2079\u2080-\u2089]+",
            "",
            RegexOptions.CultureInvariant);

        // TYPE 2: ASCII 1–2 digit footnote only if preceded by three letters (Эритроциты¹, RBC²).
        // Avoids stripping "130" in "г/л130" ( '/' breaks the letter run before the digit block).
        text = Regex.Replace(
            text,
            @"(?<=[\p{L}][\p{L}][\p{L}])([1-9]\d?)(?!\d)",
            "",
            RegexOptions.CultureInvariant);

        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);

        return text.Trim();
    }

    /// <summary>
    /// Removes short row indices glued before Cyrillic analyte names (e.g. <c>1ферритин</c>, <c> 2лейкоциты</c>).
    /// </summary>
    public static string StripGluedRowIndices(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = Regex.Replace(text, @"^(\d{1,2})(?=[\p{IsCyrillic}]{4,})", "", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"(?<=[\s;:|*#_])(\d{1,2})(?=[\p{IsCyrillic}]{4,})", "", RegexOptions.CultureInvariant);
        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text;
    }

    /// <summary>Removes stray markers before Cyrillic tokens (e.g. leading <c>*</c> on analyte names).</summary>
    public static string StripAnalyteMarkerNoise(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = Regex.Replace(text, @"\*+(?=[\p{IsCyrillic}])", "", RegexOptions.CultureInvariant);
        return text;
    }

    /// <summary>
    /// Inserts spaces between letters and digits (OCR glue like <c>ферритин24.7нг/мл30-400</c>), then analyte-specific fixes.
    /// Safe to run on raw PDF text before <see cref="NormalizeText"/>; run again after normalize for lowercase passes.
    /// </summary>
    public static string FixBrokenSpacing(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // OCR placeholder ? glued to value: ферритин?51 → ферритин? 51
        text = Regex.Replace(text, @"(ферритин\?)(\d)", "$1 $2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Glued single-digit row index + 2–3 digit value + optional decimal (ферритин151.0 → ферритин 1 51.0)
        text = Regex.Replace(
            text,
            @"(ферритин\?|ферритин|ferritin)(\d)(\d{2,3}(?:[.,]\d+)?)(?!\d)",
            "$1 $2 $3",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Ferritin + digit run
        text = Regex.Replace(text, @"(ферритин\?|ферритин|ferritin)(\d+)", "$1 $2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Iterative letter↔digit (chains: …нг/мл30-400 → …нг/мл 30-400)
        for (var pass = 0; pass < 8; pass++)
        {
            var next = Regex.Replace(text, @"([\p{L}])(\d)", "$1 $2", RegexOptions.CultureInvariant);
            next = Regex.Replace(next, @"(\d)([\p{L}])", "$1 $2", RegexOptions.CultureInvariant);
            if (string.Equals(next, text, StringComparison.Ordinal))
                break;
            text = next;
        }

        // Kazakh / Latin ASCII edge cases not matched by \p{L} in some runtimes
        text = Regex.Replace(text, @"([а-яё])(\d)", "$1 $2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"(\d)([а-яё])", "$1 $2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);

        return text.Trim();
    }

    /// <summary>Normalize a captured numeric token for <see cref="double.TryParse"/> (InvariantCulture).</summary>
    public static string NormalizeNumberToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return raw.Trim().Replace(",", ".", StringComparison.Ordinal);
    }

    public static bool TryParseInvariant(string normalizedNumberToken, out double value) =>
        double.TryParse(normalizedNumberToken, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
