using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

/// <summary>GET /api/lab-results/{id}/analysis</summary>
public class LabResultAnalysisApiDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<string>? Findings { get; set; }

    [JsonPropertyName("recommendations")]
    public List<string>? Recommendations { get; set; }

    [JsonPropertyName("ai_laboratory_interpretation")]
    public string AiLaboratoryInterpretation { get; set; } = "AI laboratory interpretation unavailable.";
}
