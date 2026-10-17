using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Ai;

public class AiAnalyzeResponse
{
    [JsonPropertyName("symptoms")]
    public List<string> Symptoms { get; set; } = new();

    [JsonPropertyName("possible_conditions")]
    public List<string> PossibleConditions { get; set; } = new();

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [JsonPropertyName("risk_level")]
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>Optional 0–100 heuristic score from the analyzer.</summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    /// <summary>Optional GPT-enhanced educational interpretation; never diagnostic.</summary>
    [JsonPropertyName("ai_interpretation")]
    public string AiInterpretation { get; set; } = "AI interpretation unavailable.";
}
