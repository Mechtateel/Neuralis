namespace NeuralisAPI.Services.Lab;

/// <summary>One analyte parsed from a table row. Reference interval may be absent (see <see cref="HasReferenceRange"/>).</summary>
public sealed record LabValue(
    string Name,
    double Value,
    string Unit,
    double RefMin,
    double RefMax,
    string Section,
    /// <summary>Stable ordering key from extraction (for grouping / structured export).</summary>
    int SourceOrder = 0,
    /// <summary>True when unit or reference could not be read reliably from the row.</summary>
    bool LowConfidence = false,
    /// <summary>When false, <see cref="RefMin"/>/<see cref="RefMax"/> are not from the report and must not be used for status.</summary>
    bool HasReferenceRange = true,
    /// <summary>HIGH | MEDIUM | LOW confidence assigned by layout parser (if available).</summary>
    string ParseConfidence = "LOW",
    /// <summary>Source page index (0-based) when known.</summary>
    int PageIndex = -1,
    /// <summary>Row index within table on the page (0-based) when known.</summary>
    int TableRowIndex = -1,
    /// <summary>"numeric" or "qualitative".</summary>
    string ValueType = "numeric",
    /// <summary>String payload for qualitative rows (e.g. "отрицательно").</summary>
    string? TextValue = null,
    /// <summary>Numeric payload when <see cref="ValueType"/> is numeric.</summary>
    double? NumericValue = null,
    /// <summary>Boolean mapping for qualitative values: false=negative, true=positive.</summary>
    bool? BooleanValue = null);
