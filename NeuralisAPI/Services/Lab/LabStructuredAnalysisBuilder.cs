using System.Text.Json.Serialization;

namespace NeuralisAPI.Services.Lab;

public sealed class LabStructuredDocumentDto
{
    [JsonPropertyName("analyses")]
    public required IReadOnlyList<LabStructuredAnalysisDto> Analyses { get; init; }
}

public sealed class LabStructuredAnalysisDto
{
    [JsonPropertyName("analysis_type")]
    public required string AnalysisType { get; init; }

    [JsonPropertyName("results")]
    public required IReadOnlyList<LabStructuredResultDto> Results { get; init; }
}

public sealed class LabStructuredResultDto
{
    [JsonPropertyName("test_name")]
    public required string TestName { get; init; }

    [JsonPropertyName("value")]
    public required double Value { get; init; }

    [JsonPropertyName("unit")]
    public required string Unit { get; init; }

    [JsonPropertyName("reference_range")]
    public required LabStructuredRefRangeDto ReferenceRange { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}

public sealed class LabStructuredRefRangeDto
{
    [JsonPropertyName("min")]
    public double? Min { get; init; }

    [JsonPropertyName("max")]
    public double? Max { get; init; }
}

/// <summary>Builds strict JSON-oriented DTOs: groups by section, abnormal-first ordering, English labels where mapped.</summary>
public static class LabStructuredAnalysisBuilder
{
    private static readonly (string Key, string En)[] RuToEnNames =
    {
        ("средняя концентрация hb", "MCHC"),
        ("среднее содержание hb в эритроците", "MCH"),
        ("среднее содержание hb", "MCH"),
        ("средний объем эритроцита", "MCV"),
        ("средний объем тромбоцита", "MPV"),
        ("распределение эритроцитов по объему", "RDW"),
        ("распределение тромбоцитов по объему", "PDW"),
        ("нейтрофилы (абс", "Neutrophils (absolute)"),
        ("нейтрофил", "Neutrophils"),
        ("эозинофилы (абс", "Eosinophils (absolute)"),
        ("эозинофил", "Eosinophils"),
        ("базофилы (абс", "Basophils (absolute)"),
        ("базофил", "Basophils"),
        ("моноциты (абс", "Monocytes (absolute)"),
        ("моноцит", "Monocytes"),
        ("лимфоциты (абс", "Lymphocytes (absolute)"),
        ("лимфоцит", "Lymphocytes"),
        ("цветной показатель", "Color index"),
        ("тромбокрит", "Plateletcrit"),
        ("соэ (по вестергрену)", "ESR (Westergren)"),
        ("соэ (по панченкову)", "ESR (Panchenkov)"),
        ("соэ", "ESR"),
        ("ферритин", "Ferritin"),
        ("гемоглобин", "Hemoglobin"),
        ("эритроцит", "Erythrocytes"),
        ("гематокрит", "Hematocrit"),
        ("тромбоцит", "Platelets"),
        ("лейкоцит", "Leukocytes"),
    };

    public static LabStructuredDocumentDto Build(IReadOnlyList<LabValue> values)
    {
        if (values == null || values.Count == 0)
        {
            return new LabStructuredDocumentDto
            {
                Analyses = new List<LabStructuredAnalysisDto>
                {
                    new()
                    {
                        AnalysisType = "Unknown",
                        Results = Array.Empty<LabStructuredResultDto>()
                    }
                }
            };
        }

        var groups = values
            .GroupBy(v => string.IsNullOrWhiteSpace(v.Section) ? "Unknown" : v.Section, StringComparer.Ordinal)
            .OrderBy(g => g.Min(v => v.SourceOrder))
            .ToList();

        var analyses = new List<LabStructuredAnalysisDto>(groups.Count);
        foreach (var g in groups)
        {
            var ordered = g
                .OrderBy(v => LabResultClassifier.SortRank(LabResultClassifier.Classify(v)))
                .ThenBy(v => v.SourceOrder)
                .ToList();

            analyses.Add(new LabStructuredAnalysisDto
            {
                AnalysisType = g.Key,
                Results = ordered.Select(MapResult).ToList()
            });
        }

        return new LabStructuredDocumentDto { Analyses = analyses };
    }

    private static LabStructuredResultDto MapResult(LabValue v)
    {
        var status = LabResultClassifier.Classify(v);
        if (status == "CRITICAL_LOW")
            status = "LOW";

        return new LabStructuredResultDto
        {
            TestName = MapTestName(v.Name),
            Value = v.Value,
            Unit = MapUnit(v.Unit),
            ReferenceRange = v.HasReferenceRange
                ? new LabStructuredRefRangeDto { Min = v.RefMin, Max = v.RefMax }
                : new LabStructuredRefRangeDto { Min = null, Max = null },
            Status = status
        };
    }

    private static string MapTestName(string name)
    {
        var n = name.Trim().ToLowerInvariant();
        foreach (var (key, en) in RuToEnNames)
        {
            if (n.Contains(key, StringComparison.Ordinal))
                return en;
        }

        return name.Trim();
    }

    private static string MapUnit(string unit)
    {
        var u = unit.Trim().ToLowerInvariant();
        if (u.Contains("нг/мл", StringComparison.Ordinal))
            return "ng/ml";
        if (u.Contains("10^12", StringComparison.Ordinal))
            return "10^12/L";
        if (u.Contains("10^9", StringComparison.Ordinal))
            return "10^9/L";
        if (u.Contains("мкмоль/л", StringComparison.Ordinal))
            return "μmol/L";
        if (u.Contains("ммоль/л", StringComparison.Ordinal))
            return "mmol/L";
        if (u.Contains("мг/л", StringComparison.Ordinal))
            return "mg/L";
        if (u.Contains("мкг/мл", StringComparison.Ordinal))
            return "μg/mL";
        if (u.Contains("мм/час", StringComparison.Ordinal))
            return "mm/h";
        if (u.Contains("г/дл", StringComparison.Ordinal))
            return "g/dL";
        if (u.Contains("г/л", StringComparison.Ordinal))
            return "g/L";
        if (u.Contains("пг", StringComparison.Ordinal))
            return "pg";
        if (u.Contains("фл", StringComparison.Ordinal))
            return "fL";
        if (u == "%")
            return "%";

        return unit.Trim();
    }
}
