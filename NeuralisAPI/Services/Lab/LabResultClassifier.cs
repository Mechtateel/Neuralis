namespace NeuralisAPI.Services.Lab;

/// <summary>Value vs on-row reference only; shared by extraction sort and interpretation.</summary>
internal static class LabResultClassifier
{
    public static string Classify(LabValue v)
    {
        if (string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase))
        {
            if (v.BooleanValue.HasValue)
                return v.BooleanValue.Value ? "HIGH" : "NORMAL";
            return ClassifyQualitative(v.TextValue);
        }
        return Classify(v.Value, v.RefMin, v.RefMax, v.HasReferenceRange);
    }

    public static string ClassifyQualitative(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "UNKNOWN";
        var t = text.Trim().ToLowerInvariant();
        if (t.Contains("не обнаруж", StringComparison.Ordinal) || t.Contains("отриц", StringComparison.Ordinal))
            return "NORMAL";
        if (t.Contains("полож", StringComparison.Ordinal))
            return "HIGH";
        if (t.Contains("обнаруж", StringComparison.Ordinal))
            return "HIGH";
        return "UNKNOWN";
    }

    /// <summary>CRITICAL_LOW | LOW | HIGH | NORMAL | UNKNOWN (English, for API/UI). UNKNOWN when no on-row reference.</summary>
    public static string Classify(double value, double refMin, double refMax, bool hasReferenceRange = true)
    {
        if (!hasReferenceRange)
            return "UNKNOWN";

        if (value < refMin)
        {
            if (refMin > 0 && value <= refMin * 0.35)
                return "CRITICAL_LOW";
            return "LOW";
        }

        if (value > refMax)
            return "HIGH";
        return "NORMAL";
    }

    /// <summary>For sorting: CRITICAL_LOW, LOW, HIGH, NORMAL.</summary>
    public static int SortRank(string status) =>
        status switch
        {
            "CRITICAL_LOW" => 0,
            "LOW" => 1,
            "HIGH" => 2,
            "NORMAL" => 3,
            "UNKNOWN" => 4,
            _ => 3,
        };
}
