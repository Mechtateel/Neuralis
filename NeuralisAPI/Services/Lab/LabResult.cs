namespace NeuralisAPI.Services.Lab;

/// <summary>One analyte from column-proximity parsing (status vs on-row reference only).</summary>
public sealed class LabResult
{
    public required string TestName { get; init; }

    public required double Value { get; init; }

    public required string Unit { get; init; }

    public required double RefMin { get; init; }

    public required double RefMax { get; init; }

    /// <summary>LOW, NORMAL, or HIGH (CRITICAL_LOW mapped to LOW).</summary>
    public required string Status { get; init; }
}
