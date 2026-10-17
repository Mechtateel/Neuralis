using NeuralisAPI.Services.Lab.Layout;

namespace NeuralisAPI.Services.Lab;

/// <summary>Layout-first (geometry/pseudo) → string table reconstruction → row-wise segments → column/line fallback.</summary>
public sealed class LabMarkerExtractorService
{
    private readonly LabColumnParserService _columns;
    private readonly LabLayoutPipelineOrchestrator _layoutPipeline;
    private readonly ILogger<LabMarkerExtractorService> _logger;

    public LabMarkerExtractorService(
        LabColumnParserService columns,
        LabLayoutPipelineOrchestrator layoutPipeline,
        ILogger<LabMarkerExtractorService> logger)
    {
        _columns = columns;
        _layoutPipeline = layoutPipeline;
        _logger = logger;
    }

    public IReadOnlyList<LabValue> Extract(LabPdfExtractionResult extracted) =>
        Extract(extracted, out _);

    public IReadOnlyList<LabValue> Extract(LabPdfExtractionResult extracted, out LabLayoutExtractionDebug? layoutPipelineDebug)
    {
        layoutPipelineDebug = null;
        if (extracted.PageLayouts is { Count: > 0 }
            && extracted.LayoutSource != LabLayoutCoordinateSource.None)
        {
            var layoutResult = _layoutPipeline.Run(extracted.PageLayouts, extracted.Text);
            layoutPipelineDebug = layoutResult.Debug;
            _logger.LogInformation(
                "Lab extract: layout pipeline (strict) → {Count} value(s); rows_traced={Trace}",
                layoutResult.Values.Count,
                layoutResult.ReconstructedRows.Count);
            var layoutOnly = SortClinicalFirst(Deduplicate(layoutResult.Values.ToList()));
            Console.WriteLine($"Rows extracted: {layoutOnly.Count}");
            _logger.LogInformation("Rows extracted: {Count}", layoutOnly.Count);
            return layoutOnly;
        }

        List<string> normPages;
        if (extracted.PageTexts is { Count: > 0 })
        {
            normPages = new List<string>(extracted.PageTexts.Count);
            foreach (var p in extracted.PageTexts)
                normPages.Add(NormalizePageText(p));
        }
        else
        {
            normPages = new List<string> { NormalizePageText(extracted.Text ?? string.Empty) };
        }

        var textRows = RunExtract(normPages, out _);
        var sorted = SortClinicalFirst(textRows.ToList());
        Console.WriteLine($"Rows extracted: {sorted.Count}");
        _logger.LogInformation("Rows extracted: {Count}", sorted.Count);
        return sorted;
    }

    public IReadOnlyList<LabValue> Extract(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            Console.WriteLine("ROLLBACK ACTIVE");
            Console.WriteLine("Rows extracted: 0");
            _logger.LogInformation("ROLLBACK ACTIVE; Rows extracted: 0 (empty input)");
            return Array.Empty<LabValue>();
        }

        var rows = RunExtract(new List<string> { NormalizePageText(rawText) }, out _);
        var sorted = SortClinicalFirst(rows.ToList());
        Console.WriteLine($"Rows extracted: {sorted.Count}");
        _logger.LogInformation("Rows extracted: {Count}", sorted.Count);
        return sorted;
    }

    private IReadOnlyList<LabValue> RunExtract(List<string> normPages, out LabLayoutExtractionDebug? layoutPipelineDebug)
    {
        layoutPipelineDebug = null;
        Console.WriteLine("ROLLBACK ACTIVE");
        _logger.LogInformation("Lab extract: string table reconstruction → row-wise segments → column/line fallback per page");

        var merged = new List<LabValue>(128);
        for (var pageIndex = 0; pageIndex < normPages.Count; pageIndex++)
        {
            var page = normPages[pageIndex];
            if (string.IsNullOrWhiteSpace(page))
                continue;

            var collapsed = LabTableRowExtractor.CollapseLabText(page);
            var tableRows = LabTableReconstructionParser.ParsePage(collapsed, pageIndex, _logger);
            if (tableRows.Count > 0)
            {
                merged.AddRange(tableRows);
                _logger.LogDebug("Lab page {Page}: table reconstruction → {Count} value(s)", pageIndex, tableRows.Count);
            }

            var rowWise = ExtractPageRowWise(collapsed, pageIndex);
            if (rowWise.Count > 0)
            {
                merged.AddRange(rowWise);
                _logger.LogDebug("Lab page {Page}: row-wise extract → {Count} value(s)", pageIndex, rowWise.Count);
            }

            var column = _columns.TryExtractColumnLabValues(page, pageIndex);
            var line = LabTableRowExtractor.ExtractFromPages(new[] { page });
            var pick = LabColumnParserService.ShouldFallbackToLineParse(column, line) ? line : column;
            merged.AddRange(pick);
        }

        return Deduplicate(merged);
    }

    private static List<LabValue> Deduplicate(List<LabValue> values)
    {
        return values
            .GroupBy(v => $"{v.Name.Trim().ToLowerInvariant()}|{v.Value:F2}|{v.Unit.Trim().ToLowerInvariant()}|{v.RefMin:F2}|{v.RefMax:F2}")
            .Select(g => g.OrderBy(x => x.SourceOrder).First())
            .ToList();
    }

    /// <summary>Reconstruct logical rows, parse each with regex extract; empty → caller uses column/line fallback.</summary>
    private static List<LabValue> ExtractPageRowWise(string pageCollapsed, int pageIndex)
    {
        var segments = LabRowReconstructor.BuildSegments(pageCollapsed);
        if (segments.Count < 2)
            return new List<LabValue>();

        var list = new List<LabValue>(32);
        foreach (var seg in segments)
        {
            if (!LabRowReconstructor.IsValidRowForParsing(seg.Text))
                continue;
            var leadingTrim = seg.Text.Length - seg.Text.TrimStart().Length;
            var cleaned = LabRowReconstructor.CleanRowSegment(seg.Text);
            if (cleaned.Length < 8)
                continue;
            list.AddRange(LabTableRowExtractor.ExtractFromSegment(
                cleaned,
                pageCollapsed,
                seg.StartIndex + leadingTrim,
                pageIndex));
        }

        return list.Count > 0 ? SortClinicalFirst(list).ToList() : new List<LabValue>();
    }

    private static IReadOnlyList<LabValue> SortClinicalFirst(List<LabValue> rows)
    {
        return rows
            .OrderBy(v => LabResultClassifier.SortRank(LabResultClassifier.Classify(v)))
            .ThenBy(v => v.SourceOrder)
            .ToList();
    }

    /// <summary>Traceable OCR normalization: every edit recorded in <see cref="LabNormalizationResult.Changes"/> and logged.</summary>
    private string NormalizePageText(string raw)
    {
        var result = LabNormalizationPipeline.NormalizeWithReport(raw.Trim(), _logger);
        return result.NormalizedText;
    }
}
