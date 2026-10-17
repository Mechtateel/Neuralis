using NeuralisAPI.Services.Lab.Layout;

namespace NeuralisAPI.Services.Lab;

public sealed class LabPdfExtractionResult
{
    /// <summary>Combined text from PdfPig and/or OCR.</summary>
    public required string Text { get; init; }

    /// <summary>One string per PDF page (same order as document). Empty when only <see cref="Text"/> is available.</summary>
    public IReadOnlyList<string> PageTexts { get; init; } = Array.Empty<string>();

    /// <summary>True when initial PdfPig text was null/empty or shorter than configured threshold (OCR may have been used).</summary>
    public bool LowConfidenceText { get; init; }

    /// <summary>Per-page words and boxes when available; otherwise pseudo-layout from line breaks.</summary>
    public IReadOnlyList<LabPageLayout> PageLayouts { get; init; } = Array.Empty<LabPageLayout>();

    public LabLayoutCoordinateSource LayoutSource { get; init; } = LabLayoutCoordinateSource.None;
}
