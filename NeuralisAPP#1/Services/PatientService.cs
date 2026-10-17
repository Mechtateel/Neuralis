using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NeuralisAPP_1.Models.Patients;

namespace NeuralisAPP_1.Services;

public sealed class PatientService
{
    private readonly HttpClient _client;

    public PatientService(string token)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiConstants.BaseUrl)
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<PatientDirectoryDto>> GetPatientsAsync()
    {
        var primary = await _client.GetAsync("/patients");
        if (!primary.IsSuccessStatusCode)
        {
            var fallback = await _client.GetAsync("/api/patients");
            if (!fallback.IsSuccessStatusCode)
            {
                var body = await fallback.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    AppointmentService.FormatApiErrorBody(body, "Unable to load patients. Please try again."));
            }

            primary = fallback;
        }

        var raw = await primary.Content.ReadAsStringAsync();
        System.Diagnostics.Debug.WriteLine($"PATIENTS RESPONSE RAW: {raw}");

        if (string.IsNullOrWhiteSpace(raw))
            return new List<PatientDirectoryDto>();

        try
        {
            var direct = JsonSerializer.Deserialize<List<PatientDirectoryDto>>(raw);
            if (direct is { Count: > 0 })
                return direct;
        }
        catch (JsonException)
        {
            // Try wrapped response shape below.
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Array)
            {
                var wrapped = JsonSerializer.Deserialize<List<PatientDirectoryDto>>(dataEl.GetRawText());
                return wrapped ?? new List<PatientDirectoryDto>();
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var fromArray = JsonSerializer.Deserialize<List<PatientDirectoryDto>>(doc.RootElement.GetRawText());
                return fromArray ?? new List<PatientDirectoryDto>();
            }
        }
        catch (JsonException)
        {
            // Return empty if payload is non-standard but request succeeded.
        }

        return new List<PatientDirectoryDto>();
    }
}
