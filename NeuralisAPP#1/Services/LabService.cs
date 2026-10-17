using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using NeuralisAPP_1.Models.Lab;

namespace NeuralisAPP_1.Services;

public class LabService
{
    private const string UploadErrorFallback = "Upload failed, try again";

    /// <summary>Outcome of POST /api/lab/upload — success is driven only by HTTP status, not response JSON.</summary>
    public readonly record struct LabUploadHttpResult(bool IsSuccess, string? ErrorMessage);

    private readonly HttpClient _client;

    public LabService(string token)
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

    public async Task<List<LabAssignedPatientDto>> GetAssignedPatientsAsync()
    {
        var response = await _client.GetAsync("/api/lab/assigned-patients");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<LabAssignedPatientDto>>()
               ?? new List<LabAssignedPatientDto>();
    }

    public async Task<LabUploadHttpResult> UploadAsync(
        Guid patientUserId,
        string description,
        IReadOnlyList<string>? tags,
        Stream fileStream,
        string fileName,
        string contentType)
    {
        var tagsCsv = tags is { Count: > 0 }
            ? string.Join(',', tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(patientUserId.ToString()), "patientUserId");
        content.Add(new StringContent(description ?? string.Empty), "description");
        content.Add(new StringContent(tagsCsv), "tags");
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);

        var response = await _client.PostAsync("/api/lab/upload", content);
        Debug.WriteLine($"Upload status: {response.StatusCode}");

        if (response.IsSuccessStatusCode)
            return new LabUploadHttpResult(true, null);

        var body = await response.Content.ReadAsStringAsync();
        var message = AppointmentService.FormatApiErrorBody(body, UploadErrorFallback);
        return new LabUploadHttpResult(false, message);
    }

    public async Task<List<LabUploadHistoryDto>> GetUploadsForPatientAsync(Guid patientUserId, int take = 50)
    {
        var response = await _client.GetAsync($"/api/lab/uploads?patientId={patientUserId}&take={take}");
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<List<LabUploadHistoryDto>>()
                   ?? new List<LabUploadHistoryDto>();

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            AppointmentService.FormatApiErrorBody(body, "Unable to load upload history."));
    }

    public async Task<List<LabRecentUploadDto>> GetRecentUploadsAsync(int take = 15)
    {
        var response = await _client.GetAsync($"/api/lab/recent-uploads?take={take}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<LabRecentUploadDto>>()
               ?? new List<LabRecentUploadDto>();
    }

    /// <summary>GET /api/lab/files — newest uploads for dashboard (default take 10).</summary>
    public async Task<List<LabRecentUploadDto>> GetLabFilesAsync(int take = 10)
    {
        var response = await _client.GetAsync($"/api/lab/files?take={take}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<LabRecentUploadDto>>()
               ?? new List<LabRecentUploadDto>();
    }

    public async Task<LabStatsDto> GetLabStatsAsync()
    {
        var response = await _client.GetAsync("/api/lab/stats");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<LabStatsDto>()
               ?? new LabStatsDto();
    }

    /// <summary>Downloads an attachment to cache and opens it with the system viewer.</summary>
    public async Task OpenAttachmentAsync(Guid attachmentId, string fileName)
    {
        var response = await _client.GetAsync($"/api/lab/file/{attachmentId}");
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "lab-result" : Path.GetFileName(fileName);
        var target = Path.Combine(FileSystem.CacheDirectory, $"lab_{attachmentId}_{safeName}");
        await File.WriteAllBytesAsync(target, bytes);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest(safeName, new ReadOnlyFile(target)));
        });
    }

    public async Task<List<LabResultListItemDto>> GetLabResultsAsync(int take = 150)
    {
        var response = await _client.GetAsync($"/api/lab-results?take={take}");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<LabResultListItemDto>>()
               ?? new List<LabResultListItemDto>();
    }

    public async Task<LabResultDetailDto?> GetLabResultDetailAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/lab-results/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<LabResultDetailDto>();
    }

    public async Task<LabResultAnalysisApiDto?> GetLabResultAnalysisAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/lab-results/{id}/analysis");
        if (!response.IsSuccessStatusCode)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LAB AI] MAUI HTTP failed: {(int)response.StatusCode}");
            return null;
        }

        var raw = await response.Content.ReadAsStringAsync();
        var hasField = raw.Contains("ai_laboratory_interpretation", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("aiLaboratoryInterpretation", StringComparison.Ordinal);
        System.Diagnostics.Debug.WriteLine(
            $"[LAB AI] MAUI raw JSON len={raw.Length} has_ai_field={hasField}");

        var dto = System.Text.Json.JsonSerializer.Deserialize<LabResultAnalysisApiDto>(raw);
        if (dto != null)
        {
            var active = !string.IsNullOrWhiteSpace(dto.AiLaboratoryInterpretation)
                && !dto.AiLaboratoryInterpretation.Trim().Equals(
                    "AI laboratory interpretation unavailable.",
                    StringComparison.OrdinalIgnoreCase);
            System.Diagnostics.Debug.WriteLine(
                $"[LAB AI] MAUI DTO deserialized: interpretation_len={dto.AiLaboratoryInterpretation?.Length ?? 0} active={active}");
        }

        return dto;
    }

    /// <summary>Windows → Downloads folder; other platforms → cache + OS share sheet.</summary>
    public readonly record struct LabDownloadOutcome(bool UsedOsShare, string? SavedPath);

    public async Task<LabDownloadOutcome> SaveLabFileForUserAsync(Guid attachmentId, string fileName)
    {
        var response = await _client.GetAsync($"/api/lab/file/{attachmentId}");
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var safeName = string.IsNullOrWhiteSpace(fileName) ? "lab-result" : Path.GetFileName(fileName);

        if (DeviceInfo.Current.Platform == DevicePlatform.WinUI)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeName}");
            await File.WriteAllBytesAsync(path, bytes);
            return new LabDownloadOutcome(false, path);
        }

        var cachePath = Path.Combine(FileSystem.CacheDirectory, $"dl_{attachmentId}_{safeName}");
        await File.WriteAllBytesAsync(cachePath, bytes);

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await Share.Default.RequestAsync(new ShareFileRequest(safeName, new ShareFile(cachePath)));
        });

        return new LabDownloadOutcome(true, null);
    }
}
