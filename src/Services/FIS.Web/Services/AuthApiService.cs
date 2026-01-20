using System.Net.Http.Json;

namespace FIS.Web.Services;

public class AuthApiService
{
    private readonly HttpClient _httpClient;

    public AuthApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LegacyLoginResponse> LoginAsync(LegacyLoginRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessage(response);
            throw new InvalidOperationException(message);
        }

        return await response.Content.ReadFromJsonAsync<LegacyLoginResponse>()
            ?? throw new InvalidOperationException("Login response was empty.");
    }

    private static async Task<string> ReadErrorMessage(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<LegacyErrorResponse>();
            if (!string.IsNullOrWhiteSpace(payload?.error))
            {
                return payload.error;
            }
        }
        catch
        {
            // Fall back to status text if payload isn't JSON.
        }

        return $"Login failed ({(int)response.StatusCode}).";
    }
}

public class LegacyLoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LegacyLoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int UserAccessCode { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
}

internal class LegacyErrorResponse
{
    public string? error { get; set; }
}
