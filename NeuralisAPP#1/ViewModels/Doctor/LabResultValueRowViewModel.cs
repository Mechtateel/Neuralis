using System.Globalization;
using NeuralisAPP_1.Models.Lab;

namespace NeuralisAPP_1.ViewModels.Doctor;

/// <summary>One structured lab row from API values[] (name, value, unit, status).</summary>
public sealed class LabResultValueRowViewModel
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Numeric value + unit for the middle column (e.g. "24.7 ng/mL").</summary>
    public string ValueFormatted { get; init; } = string.Empty;

    /// <summary>LOW | HIGH | NORMAL for display.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Status plus visual flag for abnormal rows (doctor scan).</summary>
    public string StatusWithGlyph
    {
        get
        {
            var s = Status.Trim();
            return s is "LOW" or "HIGH" or "CRITICAL-LOW" or "CRITICAL_LOW" ? $"{s} 🔴" : s;
        }
    }

    public Color StatusColor { get; init; }

    public static LabResultValueRowViewModel FromDto(LabResultValueDto d)
    {
        var s = (d.Status ?? string.Empty).Trim().ToLowerInvariant();
        var color = s switch
        {
            "critical" or "critical-low" or "critical_low" or "high" => Color.FromArgb("#ef4444"),
            "low" => Color.FromArgb("#f59e0b"),
            "borderline high" or "borderline low" or "borderline" => Color.FromArgb("#38bdf8"),
            "normal" => Color.FromArgb("#22c55e"),
            "unknown" => Color.FromArgb("#94a3b8"),
            "uninterpreted" => Color.FromArgb("#94a3b8"),
            _ => Color.FromArgb("#94a3b8")
        };

        var valueStr = ResolveDisplayValue(d);
        var unitPart = (d.Unit ?? string.Empty).Trim();
        var valueFormatted = string.IsNullOrEmpty(unitPart) || !string.IsNullOrWhiteSpace(d.TextValue)
            ? valueStr
            : $"{valueStr} {unitPart}";

        var statusLabel = string.IsNullOrWhiteSpace(d.Status)
            ? "—"
            : d.Status.Trim().ToUpperInvariant();

        return new LabResultValueRowViewModel
        {
            Name = d.Name ?? string.Empty,
            ValueFormatted = valueFormatted,
            Status = statusLabel,
            StatusColor = color
        };
    }

    private static string ResolveDisplayValue(LabResultValueDto d)
    {
        if (!string.IsNullOrWhiteSpace(d.TextValue))
            return d.TextValue.Trim();
        if (d.NumericValue.HasValue)
            return d.NumericValue.Value.ToString(CultureInfo.InvariantCulture);
        if (d.Value.ValueKind == System.Text.Json.JsonValueKind.Number && d.Value.TryGetDouble(out var n))
            return n.ToString(CultureInfo.InvariantCulture);
        if (d.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            return d.Value.GetString() ?? string.Empty;
        return string.Empty;
    }
}
