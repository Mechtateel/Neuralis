namespace NeuralisAPI.Services.Lab;

/// <summary>One detected measurement from free-form lab text (no fixed analyte list).</summary>
internal sealed record LabComponent(
    string Name,
    double Value,
    string Unit,
    double RefMin,
    double RefMax,
    string AnalysisType,
    int SourceIndex);
