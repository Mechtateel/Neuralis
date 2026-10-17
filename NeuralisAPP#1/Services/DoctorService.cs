using System.Net.Http.Headers;
using System.Net.Http.Json;
using NeuralisAPP_1.Models.Doctor;

namespace NeuralisAPP_1.Services;

public class DoctorService
{
    private static string BaseUrl => ApiConstants.BaseUrl;
    private readonly HttpClient _client;

    public DoctorService(string token)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl)
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // GET api/doctor/me
    public async Task<DoctorMeResponse?> GetMe()
    {
        var response = await _client.GetAsync("/api/doctor/me");

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<DoctorMeResponse>();
    }

    // GET api/doctor/dashboard
    public async Task<bool> CheckDashboardAccess()
    {
        var response = await _client.GetAsync("/api/doctor/dashboard");
        return response.IsSuccessStatusCode;
    }
}
