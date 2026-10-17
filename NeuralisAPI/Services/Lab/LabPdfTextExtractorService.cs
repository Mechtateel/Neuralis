using Microsoft.Extensions.Options;
using NeuralisAPI.Options;
using NeuralisAPI.Services.Lab.Layout;

namespace NeuralisAPI.Services.Lab;

/// <summary>PdfPig text extraction with OCR fallback; attaches per-page layout (geometry or pseudo from lines).</summary>
public sealed class LabPdfTextExtractorService
{
    private readonly LabPdfOcrService _ocr;
    private readonly LabExtractionOptions _options;
    private readonly ILogger<LabPdfTextExtractorService> _logger;

    public LabPdfTextExtractorService(
        LabPdfOcrService ocr,
        IOptions<LabExtractionOptions> options,
        ILogger<LabPdfTextExtractorService> logger)
    {
        _ocr = ocr;
        _options = options?.Value ?? new LabExtractionOptions();
        _logger = logger;
    }

    public LabPdfExtractionResult Extract(byte[] fileBytes, string? contentType, string? fileName)
    {
        if (fileBytes.Length == 0)
            return EmptyResult(true);

        var isPdf = (contentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) ?? false)
                    || fileName?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true;

        if (!isPdf)
            return EmptyResult(true);

        var pigOk = LabPdfPigGeometryExtractor.TryExtractTextAndLayouts(
            fileBytes, out var pdfPigPages, out var pig, out var pigLayouts, _logger);

        if (!pigOk)
        {
            pig = string.Empty;
            pdfPigPages = Array.Empty<string>();
            pigLayouts = Array.Empty<LabPageLayout>();
        }

        var (layouts, layoutSource) = ResolveLayouts(pdfPigPages, pigLayouts);

        if (pig.Contains('?'))
        {
            _logger.LogInformation("PdfPig text contains '?'; forcing OCR instead of PdfPig body text");

            var ocrPagesForced = _ocr.TryOcrPdfPages(fileBytes);
            var ocrForced = string.Join(Environment.NewLine, ocrPagesForced);
            var ocrTrim = ocrForced.Trim();
            if (ocrTrim.Length > 0)
            {
                LogExtractedText("OCR-forced", ocrForced, ocrTrim.Length < _options.MinReliableTextChars);
                var pseudo = BuildPseudoLayouts(ocrPagesForced);
                return new LabPdfExtractionResult
                {
                    Text = ocrForced,
                    PageTexts = ocrPagesForced,
                    LowConfidenceText = true,
                    PageLayouts = pseudo,
                    LayoutSource = LabLayoutCoordinateSource.PseudoFromTextLines
                };
            }

            _logger.LogWarning("OCR (forced due to '?') returned empty text; falling back to PdfPig output");
        }

        var threshold = Math.Max(0, _options.MinReliableTextChars);
        var trimmed = pig.Trim();
        var lowConfidence = string.IsNullOrEmpty(trimmed) || trimmed.Length < threshold;

        LogExtractedText("PdfPig", pig, lowConfidence);

        if (!lowConfidence)
        {
            return new LabPdfExtractionResult
            {
                Text = pig,
                PageTexts = pdfPigPages,
                LowConfidenceText = false,
                PageLayouts = layouts,
                LayoutSource = layoutSource
            };
        }

        _logger.LogInformation("LOW_CONFIDENCE_TEXT: PdfPig text length {Len} (threshold {Thr}); attempting OCR",
            trimmed.Length, threshold);

        var ocrPages = _ocr.TryOcrPdfPages(fileBytes);
        var ocrText = string.Join(Environment.NewLine, ocrPages);
        var ocrTrim2 = ocrText.Trim();
        LogExtractedText("OCR", ocrText, string.IsNullOrEmpty(ocrTrim2) || ocrTrim2.Length < threshold);

        if (ocrTrim2.Length >= threshold)
        {
            var pseudo = BuildPseudoLayouts(ocrPages);
            return new LabPdfExtractionResult
            {
                Text = ocrText,
                PageTexts = ocrPages,
                LowConfidenceText = true,
                PageLayouts = pseudo,
                LayoutSource = LabLayoutCoordinateSource.PseudoFromTextLines
            };
        }

        var useOcr = ocrTrim2.Length > trimmed.Length;
        return new LabPdfExtractionResult
        {
            Text = useOcr ? ocrText : pig,
            PageTexts = useOcr ? ocrPages : pdfPigPages,
            LowConfidenceText = true,
            PageLayouts = useOcr ? BuildPseudoLayouts(ocrPages) : layouts,
            LayoutSource = useOcr ? LabLayoutCoordinateSource.PseudoFromTextLines : layoutSource
        };
    }

    private static LabPdfExtractionResult EmptyResult(bool low) =>
        new()
        {
            Text = string.Empty,
            PageTexts = Array.Empty<string>(),
            LowConfidenceText = low,
            PageLayouts = Array.Empty<LabPageLayout>(),
            LayoutSource = LabLayoutCoordinateSource.None
        };

    private static (IReadOnlyList<LabPageLayout> layouts, LabLayoutCoordinateSource source) ResolveLayouts(
        IReadOnlyList<string> pageTexts,
        IReadOnlyList<LabPageLayout> pigLayouts)
    {
        if (pigLayouts.Count > 0 && pigLayouts.Any(p => p.Words.Count > 0))
            return (pigLayouts, LabLayoutCoordinateSource.PdfPigGeometry);

        if (pageTexts.Count > 0)
            return (BuildPseudoLayouts(pageTexts), LabLayoutCoordinateSource.PseudoFromTextLines);

        return (Array.Empty<LabPageLayout>(), LabLayoutCoordinateSource.None);
    }

    private static IReadOnlyList<LabPageLayout> BuildPseudoLayouts(IReadOnlyList<string> pages)
    {
        var list = new List<LabPageLayout>(pages.Count);
        for (var i = 0; i < pages.Count; i++)
            list.Add(PseudoLayoutFromTextBuilder.BuildPage(i + 1, pages[i] ?? string.Empty));
        return list;
    }

    private void LogExtractedText(string source, string text, bool stillLowConfidence)
    {
        var max = Math.Clamp(_options.LogTextMaxChars, 256, 200_000);
        var payload = text.Length <= max ? text : text[..max] + "\n…[truncated]";
        _logger.LogInformation(
            "Lab text extraction ({Source}): length={Len}, lowConfidenceAfterPass={Low}\n{Payload}",
            source,
            text.Length,
            stillLowConfidence,
            payload);
    }
}
