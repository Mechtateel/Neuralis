namespace NeuralisAPI.Contracts.Ai;

/// <summary>
/// Request body for POST /api/ai/analyze (and for proxying to FastAPI /analyze-text).
/// </summary>
public class AnalyzeTextRequest
{
    public string Text { get; set; } = string.Empty;
}
