using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeuralisAPI.Contracts.Ai;
using NeuralisAPI.Services;

namespace NeuralisAPI.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly AIService _aiService;

    public AiController(AIService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Proxies to FastAPI text analysis (possible conditions only — not a diagnosis).
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeTextRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Text))
            return BadRequest(new { message = "Text is required." });

        var outcome = await _aiService.AnalyzeTextAsync(request.Text, cancellationToken);

        if (!outcome.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new { message = outcome.ErrorMessage ?? "Analysis failed, try again." });

        return Ok(outcome.Data);
    }
}
