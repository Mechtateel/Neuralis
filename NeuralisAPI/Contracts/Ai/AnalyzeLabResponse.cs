using System.Text.Json.Serialization;

namespace NeuralisAPI.Contracts.Ai;

/// <summary>Matches FastAPI <c>AnalyzeLabResponse</c>.</summary>
public class AnalyzeLabResponse
{
    [JsonPropertyName("ai_laboratory_interpretation")]
    public string AiLaboratoryInterpretation { get; set; } = "AI laboratory interpretation unavailable.";
}
