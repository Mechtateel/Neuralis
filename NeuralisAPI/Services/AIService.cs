using System.Net.Http.Json;
using System.Text.Json;
using NeuralisAPI.Contracts.Ai;

namespace NeuralisAPI.Services;

public sealed class AiAnalyzeOutcome
{
    public bool Success { get; init; }
    public AnalyzeTextAnalysisResponse? Data { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Symptom NLP via PostgreSQL knowledge base; optional GPT overlay via FastAPI ai-service.
/// </summary>
public class AIService
{
    private readonly HttpClient _http;
    private readonly TextAnalysisService _textAnalysis;
    private readonly ILogger<AIService> _logger;

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AIService(HttpClient http, TextAnalysisService textAnalysis, ILogger<AIService> logger)
    {
        _http = http;
        _textAnalysis = textAnalysis;
        _logger = logger;
    }

    /// <summary>
    /// Rule-based analysis from PostgreSQL KB, then optional POST /interpret-text for GPT overlay.
    /// </summary>
    public async Task<AiAnalyzeOutcome> AnalyzeTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AiAnalyzeOutcome
            {
                Success = false,
                ErrorMessage = "Text is required."
            };
        }

        try
        {
            var trimmed = text.Trim();
            var data = await _textAnalysis.AnalyzeAsync(trimmed, cancellationToken);

            _logger.LogInformation(
                "Symptom KB analysis OK: {SymptomCount} symptoms, {CondCount} conditions, risk={Risk}, confidence={Conf}",
                data.Symptoms.Count, data.PossibleConditions.Count, data.RiskLevel, data.Confidence);

            data.AiInterpretation = await TryFetchInterpretationAsync(trimmed, data, cancellationToken);

            var interpretationActive = !string.IsNullOrWhiteSpace(data.AiInterpretation)
                && !data.AiInterpretation.Trim().Equals("AI interpretation unavailable.", StringComparison.OrdinalIgnoreCase);
            _logger.LogInformation(
                "AI analyze-text complete: openaiInterpretationActive={OpenAiActive}",
                interpretationActive);

            return new AiAnalyzeOutcome { Success = true, Data = data };
        }
        catch (OperationCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
                throw;

            _logger.LogWarning(ex, "AI request timed out");
            return new AiAnalyzeOutcome
            {
                Success = false,
                ErrorMessage = "The AI analysis timed out. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Symptom text analysis failed");
            return new AiAnalyzeOutcome
            {
                Success = false,
                ErrorMessage = "Analysis failed. Try again."
            };
        }
    }

    private async Task<string> TryFetchInterpretationAsync(
        string rawText,
        AnalyzeTextAnalysisResponse data,
        CancellationToken cancellationToken)
    {
        const string fallback = "AI interpretation unavailable.";
        try
        {
            var payload = new InterpretTextRequest
            {
                RawText = rawText,
                Symptoms = data.Symptoms,
                PossibleConditions = data.PossibleConditions,
                RiskLevel = data.RiskLevel,
                Confidence = data.Confidence ?? 50.0,
            };

            _logger.LogInformation("Proxying GPT overlay to POST {Base}interpret-text", _http.BaseAddress);
            using var response = await _http.PostAsJsonAsync("interpret-text", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("interpret-text HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return fallback;
            }

            var interp = JsonSerializer.Deserialize<InterpretTextResponse>(body, JsonReadOptions);
            return string.IsNullOrWhiteSpace(interp?.AiInterpretation) ? fallback : interp.AiInterpretation;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Cannot reach AI service for interpret-text at {Base}", _http.BaseAddress);
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "interpret-text request failed; using fallback");
            return fallback;
        }
    }

    /// <summary>
    /// Calls POST /analyze-lab on the configured AI base URL. Returns fallback text on failure (never throws).
    /// </summary>
    public async Task<string> AnalyzeLabAsync(AnalyzeLabRequest request, CancellationToken cancellationToken = default)
    {
        const string fallback = "AI laboratory interpretation unavailable.";
        try
        {
            _logger.LogInformation(
                "[LAB AI] OpenAI request sent → POST {Base}analyze-lab (markers={Count}, priority={Priority})",
                _http.BaseAddress,
                request.LaboratoryValues.Count,
                request.Priority);
            using var response = await _http.PostAsJsonAsync("analyze-lab", request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Lab AI service HTTP {Status}", (int)response.StatusCode);
                return fallback;
            }

            var data = JsonSerializer.Deserialize<AnalyzeLabResponse>(body, JsonReadOptions);
            if (data == null || string.IsNullOrWhiteSpace(data.AiLaboratoryInterpretation))
            {
                _logger.LogWarning("Lab AI service returned empty interpretation");
                return fallback;
            }

            var active = !data.AiLaboratoryInterpretation.Trim()
                .Equals(fallback, StringComparison.OrdinalIgnoreCase);
            _logger.LogInformation(
                "[LAB AI] OpenAI response received (len={Len}, active={Active})",
                data.AiLaboratoryInterpretation.Length,
                active);
            return data.AiLaboratoryInterpretation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lab AI request failed; using fallback interpretation");
            return fallback;
        }
    }

    private static string? TryParseFastApiDetail(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("detail", out var d))
            {
                if (d.ValueKind == JsonValueKind.String)
                    return d.GetString();
                if (d.ValueKind == JsonValueKind.Array && d.GetArrayLength() > 0)
                {
                    var first = d[0];
                    if (first.TryGetProperty("msg", out var msg))
                        return msg.GetString();
                }
            }
        }
        catch (JsonException)
        {
            /* ignore */
        }

        return null;
    }
}
