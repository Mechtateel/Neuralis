namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Layout-first: regions → table geometry → rows → roles → validation → <see cref="LabValue"/>.</summary>
public sealed class LabLayoutPipelineOrchestrator
{
    private readonly ILogger<LabLayoutPipelineOrchestrator> _logger;

    public LabLayoutPipelineOrchestrator(ILogger<LabLayoutPipelineOrchestrator> logger) => _logger = logger;

    public LabLayoutPipelineResult Run(IReadOnlyList<LabPageLayout> pages, string? fullTextForLog = null)
    {
        var values = new List<LabValue>();
        var trace = new List<LabLayoutReconstructedRow>();
        var messages = new List<string>();

        if (pages.Count == 0)
        {
            messages.Add("no_page_layouts");
            return Finish(values, trace, messages);
        }

        messages.Add($"pages={pages.Count}, source={pages[0].Source}");

        for (var pi = 0; pi < pages.Count; pi++)
        {
            var page = pages[pi];
            var lines = LabPdfPigGeometryExtractor.BuildLines(page);
            var regions = LayoutRegionDetector.Partition(page, lines);
            var body = regions.FirstOrDefault(r => r.Kind == LayoutRegionKind.Body);
            if (body == null || body.Lines.Count == 0)
            {
                messages.Add($"page{page.PageNumber}:no_body_region");
                continue;
            }

            var table = TableRowReconstructor.TryReconstructTable(page, body.Lines);
            if (table == null)
            {
                messages.Add($"page{page.PageNumber}:no_table_geometry_trying_small_table_rescue");
                table = TableRowReconstructor.TryPromoteSmallTableFromHeader(page, body.Lines, out var smallReason);
                if (table != null)
                {
                    var anchors = table.ColumnAnchors == null
                        ? "[]"
                        : $"[{string.Join(", ", table.ColumnAnchors.Select(a => a.ToString("F3")))}]";
                    var bodyRows = Math.Max(0, table.Rows.Count - 1);
                    _logger.LogInformation(
                        "Small table promoted: page={Page}; headerRowDetected=true; bodyRows={BodyRows}; anchors={Anchors}; reason={Reason}",
                        pi,
                        bodyRows,
                        anchors,
                        smallReason);
                }
                else
                {
                    _logger.LogInformation(
                        "Small table rejected: page={Page}; reason={Reason}",
                        pi,
                        smallReason);
                }

                if (table == null)
                    table = TableRowReconstructor.BuildWeakTableFromLines(page, body.Lines);
                if (table == null)
                {
                    foreach (var line in body.Lines.Take(40))
                    {
                        trace.Add(new LabLayoutReconstructedRow
                        {
                            PageIndex = pi,
                            Region = LayoutRegionKind.Body,
                            CleanedRowText = string.Join(' ', line.Words.Select(w => w.Text)),
                            GeometryConfidence = 0,
                            ConfidenceLevel = "LOW",
                            RowBoundsDescription = $"y≈{line.MidY:F1}",
                            SkippedReason = "no_stable_table"
                        });
                    }

                    continue;
                }
            }

            messages.Add($"page{page.PageNumber}:table_rows={table.Rows.Count},conf={table.GeometryConfidence:F2}");
            _logger.LogInformation(
                "Layout table detected: page={Page}; confidence={Conf:F2}; rowCount={Rows}; mode={Mode}",
                pi,
                table.GeometryConfidence,
                table.Rows.Count,
                table.Rows.Count == 1 ? "single-row" : "multi-row");

            for (var ri = 0; ri < table.Rows.Count; ri++)
            {
                var row = table.Rows[ri];
                var roles = RowRoleAssigner.Assign(row, page.PageWidth, table.ColumnAnchors);
                var cleaned = string.Join(' ', row.Words.Select(w => w.Text));
                var rowLevel = ToConfidenceLevel(table.GeometryConfidence, roles.Confidence);
                LabRowValidator.AnalyzeRowResultSignals(roles, out var hasNumericValue, out var hasQualitativeValue);

                if (!LabRowValidator.IsPlausibleDataRow(row, roles, ri, out var reject))
                {
                    _logger.LogDebug(
                        "Layout row rejected: rowText={RowText}; hasNumericValue={HasNumeric}; hasQualitativeValue={HasQualitative}; reason={Reason}",
                        cleaned,
                        hasNumericValue,
                        hasQualitativeValue,
                        reject ?? "unknown");
                    trace.Add(new LabLayoutReconstructedRow
                    {
                        PageIndex = pi,
                        RowIndexInTable = ri,
                        Region = LayoutRegionKind.Body,
                        CleanedRowText = cleaned,
                        GeometryConfidence = table.GeometryConfidence,
                        ConfidenceLevel = rowLevel,
                        Column1 = roles.LabelText,
                        Column2 = roles.ResultClusterText,
                        Column3 = roles.ReferenceClusterText,
                        ColumnRest = roles.FarRightText,
                        RowBoundsDescription = $"y≈{row.RowY:F1}",
                        SkippedReason = reject
                    });
                    continue;
                }

                var lv = LayoutAwareLabResultExtractor.TryExtract(roles.LabelText, roles, pi, ri, table.GeometryConfidence, _logger);
                if (lv == null)
                {
                    // Salvage pass: same row geometry, but cluster-based roles without column anchors.
                    var roles2 = RowRoleAssigner.Assign(row, page.PageWidth, null);
                    var rowLevel2 = ToConfidenceLevel(Math.Min(table.GeometryConfidence, 0.45), roles2.Confidence);
                    if (LabRowValidator.IsPlausibleDataRow(row, roles2, ri, out var reject2))
                    {
                        var lv2 = LayoutAwareLabResultExtractor.TryExtract(roles2.LabelText, roles2, pi, ri, Math.Min(table.GeometryConfidence, 0.45), _logger);
                        if (lv2 != null)
                        {
                            values.Add(lv2);
                            _logger.LogDebug(
                                "Layout row accepted: rowText={RowText}; hasNumericValue={HasNumeric}; hasQualitativeValue={HasQualitative}; cleanedName={Name}; valueType={ValueType}; value={Value}; unit={Unit}; keepReason={Reason}",
                                cleaned,
                                hasNumericValue,
                                hasQualitativeValue,
                                lv2.Name,
                                lv2.ValueType,
                                lv2.Value,
                                lv2.Unit,
                                $"accepted_salvage_{rowLevel2.ToLowerInvariant()}");
                            trace.Add(new LabLayoutReconstructedRow
                            {
                                PageIndex = pi,
                                RowIndexInTable = ri,
                                Region = LayoutRegionKind.Body,
                                CleanedRowText = cleaned,
                                GeometryConfidence = table.GeometryConfidence,
                                ConfidenceLevel = rowLevel2,
                                Column1 = roles2.LabelText,
                                Column2 = roles2.ResultClusterText,
                                Column3 = roles2.ReferenceClusterText,
                                ColumnRest = roles2.FarRightText,
                                RowBoundsDescription = $"y≈{row.RowY:F1}; mode=salvage",
                                SkippedReason = null
                            });
                            continue;
                        }
                    }

                    _logger.LogDebug(
                        "Layout row rejected: rowText={RowText}; hasNumericValue={HasNumeric}; hasQualitativeValue={HasQualitative}; reason={Reason}",
                        cleaned,
                        hasNumericValue,
                        hasQualitativeValue,
                        "value_parse_failed");
                    trace.Add(new LabLayoutReconstructedRow
                    {
                        PageIndex = pi,
                        RowIndexInTable = ri,
                        Region = LayoutRegionKind.Body,
                        CleanedRowText = cleaned,
                        GeometryConfidence = table.GeometryConfidence,
                        ConfidenceLevel = rowLevel,
                        Column1 = roles.LabelText,
                        Column2 = roles.ResultClusterText,
                        Column3 = roles.ReferenceClusterText,
                        ColumnRest = roles.FarRightText,
                        RowBoundsDescription = $"y≈{row.RowY:F1}",
                        SkippedReason = "value_parse_failed"
                    });
                    continue;
                }

                values.Add(lv);
                _logger.LogDebug(
                    "Layout row accepted: rowText={RowText}; hasNumericValue={HasNumeric}; hasQualitativeValue={HasQualitative}; cleanedName={Name}; valueType={ValueType}; value={Value}; unit={Unit}; keepReason={Reason}",
                    cleaned,
                    hasNumericValue,
                    hasQualitativeValue,
                    lv.Name,
                    lv.ValueType,
                    lv.Value,
                    lv.Unit,
                    $"accepted_{rowLevel.ToLowerInvariant()}");
                var lbl = roles.LabelText;
                var lblPrev = lbl.Length <= 40 ? lbl : lbl[..40] + "…";
                trace.Add(new LabLayoutReconstructedRow
                {
                    PageIndex = pi,
                    RowIndexInTable = ri,
                    Region = LayoutRegionKind.Body,
                    CleanedRowText = cleaned,
                    GeometryConfidence = table.GeometryConfidence,
                    ConfidenceLevel = rowLevel,
                    Column1 = roles.LabelText,
                    Column2 = roles.ResultClusterText,
                    Column3 = roles.ReferenceClusterText,
                    ColumnRest = roles.FarRightText,
                    RowBoundsDescription = $"y≈{row.RowY:F1}; label≈{lblPrev}",
                    SkippedReason = null
                });
            }
        }

        _logger.LogDebug("Layout pipeline: values={Count}, trace_rows={Trace}", values.Count, trace.Count);
        return Finish(values, trace, messages);
    }

    private static string ToConfidenceLevel(double tableConfidence, double roleConfidence)
    {
        var s = (tableConfidence * 0.6) + (roleConfidence * 0.4);
        if (s >= 0.68)
            return "HIGH";
        if (s >= 0.42)
            return "MEDIUM";
        return "LOW";
    }

    private static LabLayoutPipelineResult Finish(List<LabValue> values, List<LabLayoutReconstructedRow> trace, List<string> messages) =>
        new()
        {
            Values = values,
            ReconstructedRows = trace,
            Debug = new LabLayoutExtractionDebug
            {
                Messages = messages,
                RowTrace = trace
            }
        };
}
