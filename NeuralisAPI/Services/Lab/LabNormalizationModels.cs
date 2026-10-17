namespace NeuralisAPI.Services.Lab;

/// <summary>Outcome of <see cref="LabNormalizationPipeline.NormalizeWithReport"/> with a full audit trail.</summary>
public sealed class LabNormalizationResult
{
    public required string OriginalText { get; init; }

    public required string NormalizedText { get; init; }

    public List<NormalizationChange> Changes { get; init; } = new();
}

/// <summary>One logical OCR fix or skipped safeguard (medical traceability).</summary>
public sealed class NormalizationChange
{
    public required string Type { get; init; }

    public required string Before { get; init; }

    public required string After { get; init; }

    /// <summary>Start index in the working string at the moment this step was applied (stable for descending application).</summary>
    public int Position { get; init; }

    public required string Reason { get; init; }

    /// <summary>False when an anomaly was detected but not auto-fixed (safeguard).</summary>
    public bool Applied { get; init; } = true;
}
