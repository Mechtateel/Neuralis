using NeuralisAPP_1.Models.Auth;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NeuralisAPP_1.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;

    public static string? CurrentToken { get; private set; }
    public static MeResponse? CurrentUser { get; private set; }

    public AuthService()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiConstants.BaseUrl)
        };
    }


    private HttpClient CreateClient(string? token = null)
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(ApiConstants.BaseUrl)
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }


    // LOGIN
    public async Task<string> Login(string email, string password)
    {
        var request = new
        {
            email,
            password
        };

        Console.WriteLine($"BASE URL: {ApiConstants.BaseUrl}");
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
        Console.WriteLine($"STATUS: {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"ERROR: {error}");
            throw new Exception(error);
        }

        var data = await response.Content.ReadFromJsonAsync<LoginResponse>();

        if (data == null || string.IsNullOrWhiteSpace(data.AccessToken))
            throw new Exception("Invalid login response");

        CurrentToken = data.AccessToken;

        return data.AccessToken;
    }


    public static void SetCurrentUser(MeResponse user)
    {
        CurrentUser = user;
    }


    // ME
    public async Task<MeResponse?> GetMe(string token)
    {
        try
        {
            var client = CreateClient(token);

            var response = await client.GetAsync("api/auth/me");

            // ❌ если API недоступен / 401 / 500
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[GetMe] API error: {error}");
                return null; // ⬅ НЕ падаем
            }

            var me = await response.Content.ReadFromJsonAsync<MeResponse>();

            return me;
        }
        catch (HttpRequestException ex)
        {
            // ❌ API не запущен / порт закрыт
            System.Diagnostics.Debug.WriteLine($"[GetMe] HTTP error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GetMe] Unexpected error: {ex}");
            return null;
        }
    }

    public static void SetToken(string? token)
    {
        CurrentToken = token;
    }


    public static void Logout()
    {
        CurrentToken = null;
    }


}
