using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using NeuralisAPP_1.Models.Appointments;

namespace NeuralisAPP_1.Services;

public class AppointmentService
{
    private readonly HttpClient _client;

    public static string FormatApiErrorBody(string? body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
            return fallback;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            foreach (var name in new[] { "message", "detail", "title", "error" })
            {
                if (!root.TryGetProperty(name, out var el))
                    continue;
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s!;
                }
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        var t = body.Trim();
        if (t.Length > 0 && t.Length < 400 && !t.StartsWith('{'))
            return t;

        return fallback;
    }

    public AppointmentService(string token)
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

    public async Task<List<AvailableDoctorDto>> GetAvailableDoctorsAsync(string? specialization = null)
    {
        var url = "/api/doctors/available";
        if (!string.IsNullOrWhiteSpace(specialization))
            url += "?specialization=" + Uri.EscapeDataString(specialization.Trim());

        var response = await _client.GetAsync(url);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<AvailableDoctorDto>>()
                   ?? new List<AvailableDoctorDto>();

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(FormatApiErrorBody(body, "Unable to load doctors. Please try again."));
    }

    public async Task CreateAppointmentAsync(Guid doctorProfileId, DateTime scheduledAtUtc)
    {
        var response = await _client.PostAsJsonAsync("/api/appointments", new
        {
            doctorProfileId,
            scheduledAtUtc
        });

        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            FormatApiErrorBody(body, "Unable to complete booking. Please try again."));
    }

    public async Task<List<MyAppointmentDto>> GetMyAppointmentsAsync()
    {
        var response = await _client.GetAsync("/api/appointments/my");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<MyAppointmentDto>>()
               ?? new List<MyAppointmentDto>();
    }

    public async Task CancelAppointmentAsync(Guid appointmentId)
    {
        var response = await _client.PostAsync($"/api/appointments/{appointmentId}/cancel", null);

        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            FormatApiErrorBody(body, "Unable to cancel the appointment. Please try again."));
    }

    public async Task<List<DoctorAppointmentDto>> GetDoctorAppointmentsAsync()
    {
        var response = await _client.GetAsync("/api/appointments/doctor");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<DoctorAppointmentDto>>()
               ?? new List<DoctorAppointmentDto>();
    }

    public async Task<List<DoctorAppointmentDto>> GetAppointmentsAsync()
    {
        var response = await _client.GetAsync("/api/appointments");
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<DoctorAppointmentDto>>()
                   ?? new List<DoctorAppointmentDto>();

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.MethodNotAllowed)
            return await GetDoctorAppointmentsAsync();

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(FormatApiErrorBody(body, "Unable to load appointments. Please try again."));
    }
}
