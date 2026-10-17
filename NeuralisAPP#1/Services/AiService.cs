using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NeuralisAPP_1.Models.Ai;

namespace NeuralisAPP_1.Services;

/// <summary>
/// Calls Neuralis API <c>POST /api/ai/analyze</c>, which proxies to FastAPI <c>POST /analyze-text</c>.
/// </summary>
public class AiService
{
    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public AiService(string token)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiConstants.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>POST /api/ai/analyze → upstream FastAPI POST /analyze-text.</summary>
    public Task<(bool Ok, AiAnalyzeResponse? Data, string? ErrorMessage)> AnalyzeTextAsync(string input) =>
        AnalyzeTextAsyncCore(input);

    private async Task<(bool Ok, AiAnalyzeResponse? Data, string? ErrorMessage)> AnalyzeTextAsyncCore(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text))
            return (false, null, "Enter clinical text before running analysis.");

#if DEBUG
        Debug.WriteLine($"[AI] POST {_client.BaseAddress}api/ai/analyze payload chars={text.Length}");
#endif

        try
        {
            using var response = await _client.PostAsJsonAsync("api/ai/analyze", new { text });

            var raw = await response.Content.ReadAsStringAsync();
#if DEBUG
            Debug.WriteLine($"[AI] Response HTTP {(int)response.StatusCode} len={raw?.Length ?? 0}");
#endif

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<AiAnalyzeResponse>(raw, JsonReadOptions);
                if (data == null)
                    return (false, null, "Analysis returned no data. Try again.");

#if DEBUG
                var interpretationActive = !string.IsNullOrWhiteSpace(data.AiInterpretation)
                    && !data.AiInterpretation.Trim().Equals("AI interpretation unavailable.", StringComparison.OrdinalIgnoreCase);
                Debug.WriteLine(
                    $"[AI] Parsed OK symptoms={data.Symptoms.Count} risk={data.RiskLevel} confidence={data.Confidence} openaiActive={interpretationActive}");
#endif
                return (true, data, null);
            }

            var serverMsg = TryParseErrorMessage(raw);
            var msg = MapHttpFailure(response.StatusCode, serverMsg);
            return (false, null, msg);
        }
        catch (HttpRequestException ex)
        {
#if DEBUG
            Debug.WriteLine($"[AI] HttpRequestException: {ex.Message}");
#endif
            return (false, null,
                "Connection issue — could not reach the API. Ensure NeuralisAPI is running and BaseUrl is correct (see ApiConstants).");
        }
        catch (TaskCanceledException ex)
        {
#if DEBUG
            Debug.WriteLine($"[AI] TaskCanceledException: {ex.Message}");
#endif
            return (false, null, "Connection issue — request timed out. Try again.");
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[AI] Exception: {ex}");
#endif
            return (false, null, "Analysis failed, try again.");
        }
    }

    private static string? TryParseErrorMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var dto = JsonSerializer.Deserialize<ApiErrorMessageDto>(json, JsonReadOptions);
            return string.IsNullOrWhiteSpace(dto?.Message) ? null : dto.Message.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MapHttpFailure(HttpStatusCode status, string? serverMessage)
    {
        if (!string.IsNullOrWhiteSpace(serverMessage))
            return serverMessage!;

        return status switch
        {
            HttpStatusCode.BadRequest => "Invalid request. Check your input and try again.",
            HttpStatusCode.Unauthorized => "Sign in again, then retry analysis.",
            HttpStatusCode.Forbidden => "You are not allowed to run analysis.",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable =>
                "Analysis failed — the AI engine may be offline. Start the ai-service (FastAPI on port 8000) and retry.",
            _ => $"Analysis failed (HTTP {(int)status}). Try again."
        };
    }
}
