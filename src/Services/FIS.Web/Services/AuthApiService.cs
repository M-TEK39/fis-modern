using System.Net.Http.Json;
using FIS.Web.Models;

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

    public async Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/change-password", request);
        return await HandleChangePasswordResponse(response);
    }

    public async Task<ChangePasswordResponse> ChangePasswordQuestionAsync(ChangePasswordQuestionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/change-password-question", request);
        return await HandleChangePasswordResponse(response);
    }

    public async Task<UserAdminResponse> ResetLoginAsync(ResetLoginRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/reset-login", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ForcePasswordAsync(ForcePasswordRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/force-password", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ForgotPasswordStartAsync(ForgotPasswordStartRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password/start", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ForgotPasswordConfirmAsync(ForgotPasswordConfirmRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/forgot-password/confirm", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ActivateUserAsync(ActivateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/activate-user", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> DeactivateExpiredPasswordAsync(DeactivateExpiredPasswordRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/deactivate-expired", request);
        return await HandleUserAdminResponse(response);
    }

    private async Task<ChangePasswordResponse> HandleChangePasswordResponse(HttpResponseMessage response)
    {
        var payload = await TryReadResponseAsync(response);
        if (response.IsSuccessStatusCode)
        {
            return payload ?? new ChangePasswordResponse { Success = true, Message = "Password updated." };
        }

        var message = await ReadErrorMessage(response);
        return new ChangePasswordResponse
        {
            Success = false,
            Message = payload?.Message ?? message
        };
    }

    private static async Task<ChangePasswordResponse?> TryReadResponseAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        }
        catch
        {
            return null;
        }
    }

    private async Task<UserAdminResponse> HandleUserAdminResponse(HttpResponseMessage response)
    {
        var payload = await TryReadUserAdminResponseAsync(response);
        if (response.IsSuccessStatusCode)
        {
            return payload ?? new UserAdminResponse { Success = true, Message = "Operation completed." };
        }

        var message = await ReadErrorMessage(response);
        return new UserAdminResponse
        {
            Success = false,
            Message = payload?.Message ?? message
        };
    }

    private static async Task<UserAdminResponse?> TryReadUserAdminResponseAsync(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<UserAdminResponse>();
        }
        catch
        {
            return null;
        }
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
