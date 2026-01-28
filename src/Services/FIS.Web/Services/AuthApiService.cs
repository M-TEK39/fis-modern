using System.Net.Http.Json;

namespace FIS.Web.Services;

/// <summary>
/// Authentication API service
/// Calls the FIS.Web AuthProxyController which handles cookie forwarding
/// </summary>
public class AuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly DualAuthStateProvider _authStateProvider;
    private readonly ILogger<AuthApiService> _logger;

    public AuthApiService(
        HttpClient httpClient,
        TokenService tokenService,
        DualAuthStateProvider authStateProvider,
        ILogger<AuthApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    public async Task<LegacyLoginResponse> LoginAsync(LegacyLoginRequest request)
    {
        // Call the proxy controller which handles cookie forwarding from API to browser
        var response = await _httpClient.PostAsJsonAsync("/AuthProxy/login", request);

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessage(response);
            throw new InvalidOperationException(message);
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LegacyLoginResponse>()
            ?? throw new InvalidOperationException("Login response was empty.");

        // Store the JWT token in ProtectedSessionStorage (persists across SignalR reconnections)
        await _tokenService.SetTokenAsync(loginResponse.Token, loginResponse.ExpiresAt);

        // Notify Blazor that user is now authenticated
        _authStateProvider.NotifyAuthenticationStateChanged();

        _logger.LogInformation("Login successful. User: {UserAccessCode}, Expires: {ExpiresAt}",
            loginResponse.UserAccessCode, loginResponse.ExpiresAt);

        return loginResponse;
    }

    public async Task LogoutAsync()
    {
        // Call proxy controller logout
        try
        {
            await _httpClient.PostAsync("/AuthProxy/logout", null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call logout endpoint");
        }

        // Clear token from session storage
        await _tokenService.ClearTokenAsync();
        _authStateProvider.NotifyAuthenticationStateChanged();

        _logger.LogInformation("User logged out");
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
