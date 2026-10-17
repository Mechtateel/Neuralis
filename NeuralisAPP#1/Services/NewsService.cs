using System.Net.Http.Headers;
using System.Net.Http.Json;
using NeuralisAPP_1.Models.News;

namespace NeuralisAPP_1.Services;

/// <summary>
/// Talks to <c>/api/news</c>. Public endpoints don't need a token. Admin endpoints
/// (admin list, create, update, delete, publish/unpublish/archive) require a Bearer
/// token from <see cref="AuthService.CurrentToken"/>.
/// </summary>
public class NewsService
{
    private readonly HttpClient _client;

    public NewsService(string? token = null)
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

        if (!string.IsNullOrWhiteSpace(token))
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ===============================
    // PUBLIC
    // ===============================
    public async Task<List<NewsItemDto>> GetPublishedNewsAsync(string? category = null)
    {
        var url = string.IsNullOrWhiteSpace(category)
            ? "/api/news"
            : $"/api/news?category={Uri.EscapeDataString(category)}";

        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<NewsItemDto>>()
               ?? new List<NewsItemDto>();
    }

    public async Task<NewsItemDto?> GetByIdAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/news/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<NewsItemDto>();
    }

    public async Task RegisterViewAsync(Guid id)
    {
        try
        {
            await _client.PostAsync($"/api/news/{id}/view", null);
        }
        catch
        {
            // Best-effort — view count is non-essential.
        }
    }

    // ===============================
    // ADMIN
    // ===============================
    public async Task<List<NewsItemDto>> GetAllForAdminAsync(
        string? status = null,
        string? search = null,
        string? category = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(status))
            query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(search))
            query.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(category))
            query.Add($"category={Uri.EscapeDataString(category)}");

        var url = "/api/news/admin" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        var response = await _client.GetAsync(url);
        await EnsureSuccess(response);

        return await response.Content.ReadFromJsonAsync<List<NewsItemDto>>()
               ?? new List<NewsItemDto>();
    }

    public async Task<NewsItemDto?> GetByIdForAdminAsync(Guid id)
    {
        var response = await _client.GetAsync($"/api/news/admin/{id}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<NewsItemDto>();
    }

    public async Task<NewsItemDto?> CreateAsync(NewsEditorPayload payload)
    {
        var response = await _client.PostAsJsonAsync("/api/news", payload);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<NewsItemDto>();
    }

    public async Task<NewsItemDto?> UpdateAsync(Guid id, NewsEditorPayload payload)
    {
        var response = await _client.PutAsJsonAsync($"/api/news/{id}", payload);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<NewsItemDto>();
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await _client.DeleteAsync($"/api/news/{id}");
        await EnsureSuccess(response);
    }

    public async Task<NewsItemDto?> PublishAsync(Guid id) =>
        await StateTransition(id, "publish");

    public async Task<NewsItemDto?> UnpublishAsync(Guid id) =>
        await StateTransition(id, "unpublish");

    public async Task<NewsItemDto?> ArchiveAsync(Guid id) =>
        await StateTransition(id, "archive");

    private async Task<NewsItemDto?> StateTransition(Guid id, string action)
    {
        var response = await _client.PostAsync($"/api/news/{id}/{action}", null);
        await EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<NewsItemDto>();
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var body = await response.Content.ReadAsStringAsync();
        throw new Exception(string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body);
    }
}

/// <summary>
/// Payload sent to POST/PUT <c>/api/news</c>. Matches <c>NewsRequest</c> on the server.
/// </summary>
public sealed class NewsEditorPayload
{
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "general";
    public string Tags { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public int? Status { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; } = true;
}
