using Microsoft.Extensions.Options;
using NeuralisAPI.Options;
using PDFtoImage;
using Tesseract;

#pragma warning disable CA1416 // PDFtoImage uses PDFium — supported on Windows/Linux/macOS server deployments.

namespace NeuralisAPI.Services.Lab;

/// <summary>Renders PDF pages to images and runs Tesseract (eng+rus when available). PDFium is not thread-safe — uses a process-wide lock.</summary>
public sealed class LabPdfOcrService
{
    private static readonly object PdfiumLock = new();

    private readonly LabExtractionOptions _options;
    private readonly ILogger<LabPdfOcrService> _logger;

    public LabPdfOcrService(IOptions<LabExtractionOptions> options, ILogger<LabPdfOcrService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string TryOcrPdf(byte[] pdfBytes) =>
        string.Join(Environment.NewLine, TryOcrPdfPages(pdfBytes));

    /// <summary>One OCR text blob per PDF page (same order as the file).</summary>
    public IReadOnlyList<string> TryOcrPdfPages(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0)
            return Array.Empty<string>();

        var tessDir = ResolveTessdataDirectory();
        if (tessDir == null)
        {
            _logger.LogWarning("OCR skipped: tessdata folder or eng.traineddata not found at {Path}", GetConfiguredTessdataPath());
            return Array.Empty<string>();
        }

        var langs = ResolveLanguages(tessDir);
        var maxPages = Math.Clamp(_options.OcrMaxPages, 1, 50);
        var dpi = Math.Clamp(_options.OcrDpi, 72, 400);

        var renderOptions = new RenderOptions(
            dpi,
            null,
            null,
            true,
            true,
            false,
            PdfRotation.Rotate0,
            PdfAntiAliasing.Text,
            null,
            null,
            false,
            false,
            false);

        try
        {
            int pageCount;
            lock (PdfiumLock)
                pageCount = Conversion.GetPageCount(pdfBytes, null);

            var pagesToRead = Math.Min(pageCount, maxPages);
            _logger.LogInformation("OCR: PDF has {Total} page(s); processing {N} at {Dpi} DPI; languages={Langs}",
                pageCount, pagesToRead, dpi, langs);

            var pages = new List<string>(pagesToRead);
            using var engine = new TesseractEngine(tessDir, langs, EngineMode.Default);

            for (var i = 0; i < pagesToRead; i++)
            {
                byte[] pngBytes;
                lock (PdfiumLock)
                {
                    using var pngStream = new MemoryStream();
                    Conversion.SavePng(pngStream, pdfBytes, Index.FromStart(i), null, renderOptions);
                    pngBytes = pngStream.ToArray();
                }

                using var pix = Pix.LoadFromMemory(pngBytes);
                using var page = engine.Process(pix);
                var text = page.GetText();
                pages.Add(text);
                _logger.LogDebug("OCR page {Page}: {Chars} characters", i + 1, text.Length);
            }

            return pages;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR failed for PDF ({Bytes} bytes)", pdfBytes.Length);
            return Array.Empty<string>();
        }
    }

    private string GetConfiguredTessdataPath()
    {
        var p = _options.TessdataPath?.Trim() ?? "tessdata";
        return Path.IsPathRooted(p)
            ? p
            : Path.Combine(AppContext.BaseDirectory, p);
    }

    private string? ResolveTessdataDirectory()
    {
        var dir = GetConfiguredTessdataPath();
        var eng = Path.Combine(dir, "eng.traineddata");
        return Directory.Exists(dir) && File.Exists(eng) ? dir : null;
    }

    private static string ResolveLanguages(string tessDir)
    {
        var rus = Path.Combine(tessDir, "rus.traineddata");
        return File.Exists(rus) ? "eng+rus" : "eng";
    }
}

#pragma warning restore CA1416
