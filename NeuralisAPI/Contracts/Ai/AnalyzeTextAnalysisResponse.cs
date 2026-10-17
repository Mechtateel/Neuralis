using System.Text.Json.Serialization;

namespace NeuralisAPI.Contracts.Ai;

/// <summary>
/// Matches FastAPI <c>AnalyzeTextResponse</c> (snake_case JSON).
/// </summary>
public class AnalyzeTextAnalysisResponse
{
    [JsonPropertyName("symptoms")]
    public List<string> Symptoms { get; set; } = new();

    [JsonPropertyName("possible_conditions")]
    public List<string> PossibleConditions { get; set; } = new();

    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [JsonPropertyName("risk_level")]
    public string RiskLevel { get; set; } = string.Empty;

    /// <summary>Optional 0–100 heuristic score from AI service; may be absent for older deployments.</summary>
    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    /// <summary>Optional GPT-enhanced educational interpretation; must not be diagnostic.</summary>
    [JsonPropertyName("ai_interpretation")]
    public string AiInterpretation { get; set; } = "AI interpretation unavailable.";
}
