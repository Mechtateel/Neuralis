using System.Text.Json.Serialization;

namespace NeuralisAPI.Contracts.Ai;

/// <summary>Request body for ai-service POST /interpret-text (GPT overlay only).</summary>
public sealed class InterpretTextRequest
{
    [JsonPropertyName("raw_text")]
    public string RawText { get; set; } = string.Empty;

    [JsonPropertyName("symptoms")]
    public List<string> Symptoms { get; set; } = new();

    [JsonPropertyName("possible_conditions")]
    public List<string> PossibleConditions { get; set; } = new();

    [JsonPropertyName("risk_level")]
    public string RiskLevel { get; set; } = "low";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public sealed class InterpretTextResponse
{
    [JsonPropertyName("ai_interpretation")]
    public string AiInterpretation { get; set; } = "AI interpretation unavailable.";
}
