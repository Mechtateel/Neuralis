namespace NeuralisAPI.Options;

public sealed class LabExtractionOptions
{
    public const string SectionName = "LabExtraction";

    /// <summary>Text shorter than this (after trim) triggers OCR fallback for PDFs.</summary>
    public int MinReliableTextChars { get; set; } = 50;

    /// <summary>Folder containing eng.traineddata, rus.traineddata, etc. Relative paths are resolved under <see cref="AppContext.BaseDirectory"/>.</summary>
    public string TessdataPath { get; set; } = "tessdata";

    public int OcrMaxPages { get; set; } = 10;

    public int OcrDpi { get; set; } = 200;

    /// <summary>Max characters of extracted text to log at Information level (remainder truncated).</summary>
    public int LogTextMaxChars { get; set; } = 16000;

    /// <summary>Reserved; OCR is forced when PdfPig output contains any '?' (see <c>LabPdfTextExtractorService</c>).</summary>
    public int QuestionMarkForceOcrThreshold { get; set; } = 20;
}
