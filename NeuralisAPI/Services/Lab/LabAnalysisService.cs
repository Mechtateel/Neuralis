using NeuralisAPI.Services.Lab.Layout;

namespace NeuralisAPI.Services.Lab;

/// <summary>Structured parse result (no raw document text exposed).</summary>
public sealed class LabDocumentParseResult
{
    public bool LowConfidenceText { get; init; }

    public required IReadOnlyList<LabValue> Values { get; init; }

    public required IReadOnlyList<InterpretedLabRow> Rows { get; init; }

    /// <summary>Populated when the layout-first pipeline produced values (regions, row trace, skip reasons).</summary>
    public LabLayoutExtractionDebug? LayoutDiagnostics { get; init; }
}

/// <summary>PdfPig + optional OCR, universal value/unit/ref extraction, deterministic interpretation (no AI).</summary>
public sealed class LabAnalysisService
{
    private readonly LabPdfTextExtractorService _pdfText;
    private readonly LabMarkerExtractorService _markers;
    private readonly LabInterpretationService _interpretation;
    private readonly ILogger<LabAnalysisService> _logger;

    public LabAnalysisService(
        LabPdfTextExtractorService pdfText,
        LabMarkerExtractorService markers,
        LabInterpretationService interpretation,
        ILogger<LabAnalysisService> logger)
    {
        _pdfText = pdfText;
        _markers = markers;
        _interpretation = interpretation;
        _logger = logger;
    }

    public LabDocumentParseResult Parse(byte[] fileBytes, string? contentType, string? fileName)
    {
        var extracted = _pdfText.Extract(fileBytes, contentType, fileName);
        var values = _markers.Extract(extracted, out var layoutDiag);
        _logger.LogInformation("Extracted values count: {Count}", values.Count);
        var rows = _interpretation.InterpretRows(values, LabReferenceRanges.SexHint.Unknown);
        return new LabDocumentParseResult
        {
            LowConfidenceText = extracted.LowConfidenceText,
            Values = values,
            Rows = rows,
            LayoutDiagnostics = layoutDiag
        };
    }

    /// <summary>Structured JSON DTO: analyses by section, abnormal-first, English names/units where mapped.</summary>
    public LabStructuredDocumentDto BuildStructuredDocument(byte[] fileBytes, string? contentType, string? fileName)
    {
        var parse = Parse(fileBytes, contentType, fileName);
        return LabStructuredAnalysisBuilder.Build(parse.Values);
    }

    /// <summary>Rows shown in doctor detail UI: abnormal-only when any abnormality exists.</summary>
    public (IReadOnlyList<LabValue> Values, IReadOnlyList<InterpretedLabRow> Rows) PrepareDoctorFacing(IReadOnlyList<LabValue> values) =>
        _interpretation.PrepareDoctorFacing(values);

    /// <summary>Rule-based narrative plus parsed values/rows for optional GPT lab interpretation.</summary>
    public LabDocumentAnalysisResult AnalyzeDocument(
        byte[] fileBytes,
        string? contentType,
        string? fileName,
        Guid? documentId = null)
    {
        var parse = Parse(fileBytes, contentType, fileName);
        var numeric = parse.Values.Count(v => string.Equals(v.ValueType, "numeric", StringComparison.OrdinalIgnoreCase));
        var qual = parse.Values.Count(v => string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase));
        var reportType = numeric > 0 && qual > 0 ? "mixed" : numeric > 0 ? "numeric" : qual > 0 ? "qualitative" : "empty";
        if (parse.Values.Count == 0)
        {
            _logger.LogWarning(
                "Report generation skipped: documentId={DocumentId}; extractedRows={Extracted}; usedRows=0; reportType={Type}; reason={Reason}",
                documentId,
                parse.Values.Count,
                reportType,
                "no_lab_values_extracted");
        }
        else
        {
            _logger.LogInformation(
                "Report generation: documentId={DocumentId}; extractedRows={Extracted}; usedRows={Used}; reportType={Type}",
                documentId,
                parse.Values.Count,
                parse.Values.Count,
                reportType);
        }

        return new LabDocumentAnalysisResult
        {
            Narrative = _interpretation.BuildNarrative(parse.Values, LabReferenceRanges.SexHint.Unknown),
            Values = parse.Values,
            Rows = parse.Rows,
        };
    }

    public Task<LabAnalysisNarrative> AnalyzeAsync(
        byte[] fileBytes,
        string? contentType,
        string? fileName,
        Guid? documentId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AnalyzeDocument(fileBytes, contentType, fileName, documentId).Narrative);
    }
}

/// <summary>Rule-based lab narrative with extracted markers for GPT overlay.</summary>
public sealed class LabDocumentAnalysisResult
{
    public required LabAnalysisNarrative Narrative { get; init; }

    public required IReadOnlyList<LabValue> Values { get; init; }

    public required IReadOnlyList<InterpretedLabRow> Rows { get; init; }
}
