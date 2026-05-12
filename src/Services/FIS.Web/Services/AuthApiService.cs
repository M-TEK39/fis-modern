using System.Net;
using System.Net.Http.Json;
using FIS.Web.Models;
using Microsoft.JSInterop;

namespace FIS.Web.Services;

/// <summary>
/// Authentication API service
/// Calls the FIS.Web AuthProxyController which handles cookie forwarding
/// </summary>
public class AuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly AuthSessionTokenCache _sessionTokenCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IJSRuntime _jsRuntime;
    private readonly SessionAuthenticationStateProvider _authStateProvider;
    private readonly UserAccessContextService _userAccessContextService;
    private readonly ILogger<AuthApiService> _logger;

    public AuthApiService(
        HttpClient httpClient,
        TokenService tokenService,
        AuthSessionTokenCache sessionTokenCache,
        IHttpContextAccessor httpContextAccessor,
        IJSRuntime jsRuntime,
        SessionAuthenticationStateProvider authStateProvider,
        UserAccessContextService userAccessContextService,
        ILogger<AuthApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _sessionTokenCache = sessionTokenCache;
        _httpContextAccessor = httpContextAccessor;
        _jsRuntime = jsRuntime;
        _authStateProvider = authStateProvider;
        _userAccessContextService = userAccessContextService;
        _logger = logger;
    }

    public async Task<LegacyLoginResponse> LoginAsync(LegacyLoginRequest request)
    {
        // Browser-side fetch is required so HttpOnly Set-Cookie headers are stored by the browser.
        var loginResponse = await _jsRuntime.InvokeAsync<LegacyLoginResponse>("fisAuth.login", request)
            ?? throw new InvalidOperationException("Login response was empty.");

        await _tokenService.SetTokenAsync(loginResponse.Token, loginResponse.ExpiresAt);
        await _tokenService.SetUserContextAsync(loginResponse.UserAccessCode, loginResponse.Email);
        await _userAccessContextService.PrimeFromLoginAsync(
            loginResponse.UserAccessCode,
            request.FirstName);
        var sessionId = _httpContextAccessor.HttpContext?.Session.Id;
        if (!string.IsNullOrWhiteSpace(sessionId) && !string.IsNullOrWhiteSpace(loginResponse.Token))
        {
            _sessionTokenCache.SetAccessToken(sessionId, loginResponse.Token, loginResponse.ExpiresAt);
        }

        // Notify Blazor that user is now authenticated
        _authStateProvider.NotifyAuthenticationStateChanged();

        _logger.LogInformation("Login successful. FirstName: {FirstName}, User: {UserAccessCode}, Expires: {ExpiresAt}",
            request.FirstName, loginResponse.UserAccessCode, loginResponse.ExpiresAt);

        return loginResponse;
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("fisAuth.logout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call logout endpoint");
        }

        // Clear token from session storage
        await _tokenService.ClearTokenAsync();
        var sessionId = _httpContextAccessor.HttpContext?.Session.Id;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            _sessionTokenCache.RemoveAccessToken(sessionId);
        }
        _userAccessContextService.Clear();
        _authStateProvider.NotifyAuthenticationStateChanged();

        _logger.LogInformation("User logged out");
    }

    public async Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/change-password", request);
        return await HandleChangePasswordResponse(response);
    }

    public async Task<ChangePasswordResponse> ChangePasswordQuestionAsync(ChangePasswordQuestionRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/change-password-question", request);
        return await HandleChangePasswordResponse(response);
    }

    public async Task<UserAdminResponse> ResetLoginAsync(ResetLoginRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/reset-login", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ForcePasswordAsync(ForcePasswordRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/force-password", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ForgotPasswordStartAsync(ForgotPasswordStartRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/forgot-password/start", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ForgotPasswordConfirmAsync(ForgotPasswordConfirmRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/forgot-password/confirm", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> ActivateUserAsync(ActivateUserRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/activate-user", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> DeactivateUserAsync(DeactivateExpiredPasswordRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/deactivate-user", request);
        return await HandleUserAdminResponse(response);
    }

    public async Task<UserAdminResponse> DeactivateExpiredPasswordAsync(DeactivateExpiredPasswordRequest request)
    {
        var response = await PostAsJsonWithAuthRetryAsync("api/auth/deactivate-expired", request);
        return await HandleUserAdminResponse(response);
    }

    private async Task<HttpResponseMessage> PostAsJsonWithAuthRetryAsync<TRequest>(string path, TRequest request)
    {
        await AddAuthorizationHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync(path, request);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!await _tokenService.RefreshAccessTokenAsync())
        {
            return response;
        }

        response.Dispose();
        await AddAuthorizationHeaderAsync();
        return await _httpClient.PostAsJsonAsync(path, request);
    }

    private async Task AddAuthorizationHeaderAsync()
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={token}");
        }
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
    public string FirstName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LegacyLoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int UserAccessCode { get; set; }
    public bool PasswordExpired { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
}

internal class LegacyErrorResponse
{
    public string? error { get; set; }
}
