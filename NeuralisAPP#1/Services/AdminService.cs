using System.Net.Http.Headers;
using System.Net.Http.Json;
using NeuralisAPP_1.Models.Admin;

namespace NeuralisAPP_1.Services;

public class AdminService
{
    private static string BaseUrl => ApiConstants.BaseUrl;
    private readonly HttpClient _client;

    public AdminService(string token)
    {
        var handler = new HttpClientHandler
        {
            // ⚠️ Только для DEV
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl)
        };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ===============================
    // 🔒 COMMON
    // ===============================
    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());
    }

    // ===============================
    // 👤 USERS
    // ===============================

    public async Task<List<AdminUserModel>> GetUsers()
    {
        var response = await _client.GetAsync("/api/admin/users");
        await EnsureSuccess(response);

        return await response.Content
                   .ReadFromJsonAsync<List<AdminUserModel>>()
               ?? new List<AdminUserModel>();
    }

    public async Task BlockUser(Guid userId)
    {
        var response = await _client.PostAsync(
            $"/api/admin/users/{userId}/block", null);

        await EnsureSuccess(response);
    }

    public async Task UnblockUser(Guid userId)
    {
        var response = await _client.PostAsync(
            $"/api/admin/users/{userId}/unblock", null);

        await EnsureSuccess(response);
    }

    // ===============================
    // 🩺 DOCTOR APPLICATIONS
    // ===============================

    // ✅ Получить заявки
    public async Task<PagedResponse<DoctorApplicationModel>> GetDoctorApplications(
        string status,
        int page,
        string? search)
    {
        var url =
            $"/api/admin/doctor-applications" +
            $"?status={status}&page={page}&search={search}";

        var response = await _client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());

        return await response.Content
            .ReadFromJsonAsync<PagedResponse<DoctorApplicationModel>>()
            ?? new();
    }

    // ✅ Approve
    public async Task ApproveDoctor(Guid doctorId)
    {
        var response = await _client.PostAsync(
            $"/api/admin/verify-doctor/{doctorId}", null);

        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());
    }

    // ✅ Reject
    public async Task RejectDoctor(Guid doctorId, string reason)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/admin/reject-doctor/{doctorId}",
            new { reason });

        if (!response.IsSuccessStatusCode)
            throw new Exception(await response.Content.ReadAsStringAsync());
    }

}
