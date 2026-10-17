using System.Text.Json.Serialization;

namespace NeuralisAPI.Contracts.Ai;

/// <summary>Request for FastAPI POST /analyze-lab (rule-based context + markers).</summary>
public class AnalyzeLabRequest
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<string> Findings { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();

    [JsonPropertyName("laboratory_values")]
    public List<LabMarkerInputDto> LaboratoryValues { get; set; } = new();
}

public class LabMarkerInputDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value_display")]
    public string ValueDisplay { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "NORMAL";
}
