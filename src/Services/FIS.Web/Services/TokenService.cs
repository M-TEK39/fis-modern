using System.Net.Http.Json;
using Microsoft.Net.Http.Headers;

namespace FIS.Web.Services;

/// <summary>
/// Circuit-scoped auth session state. Does not persist tokens in browser storage.
/// </summary>
public class TokenService
{
    private readonly object _lock = new();
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthSessionTokenCache _sessionTokenCache;
    private readonly ILogger<TokenService> _logger;
    private readonly Uri _apiBaseUri;
    private const string AccessCookie = "FIS_Access_Token";
    private const string RefreshCookie = "FIS_Refresh_Token";
    private static readonly TimeSpan BootstrapAccessLifetime = TimeSpan.FromMinutes(14);
    private static readonly TimeSpan BootstrapRefreshLifetime = TimeSpan.FromHours(8);

    private string? _accessToken;
    private DateTime _accessExpiresAtUtc;
    private int _userAccessCode;
    private string? _email;
    private string? _sessionKey;

    public TokenService(
        IHttpContextAccessor httpContextAccessor,
        AuthSessionTokenCache sessionTokenCache,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _sessionTokenCache = sessionTokenCache;
        _logger = logger;
        var apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5010";
        _apiBaseUri = new Uri($"{apiBaseUrl.TrimEnd('/')}/");
    }

    public string? Token
    {
        get
        {
            lock (_lock)
            {
                if (string.IsNullOrWhiteSpace(_accessToken) || _accessExpiresAtUtc <= DateTime.UtcNow)
                {
                    TryBootstrapTokenUnsafe();
                }
                return _accessToken;
            }
        }
    }

    public bool IsTokenValid
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_accessToken) && _accessExpiresAtUtc > DateTime.UtcNow;
            }
        }
    }

    public int UserAccessCode
    {
        get
        {
            lock (_lock)
            {
                return _userAccessCode;
            }
        }
    }

    public string? Email
    {
        get
        {
            lock (_lock)
            {
                return _email;
            }
        }
    }

    public Task InitializeAsync()
    {
        if (IsTokenValid)
        {
            return Task.CompletedTask;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return Task.CompletedTask;
        }

        var sessionKey = httpContext.Session?.Id;
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            _sessionKey = sessionKey;
        }

        var key = GetSessionKey();
        if (!string.IsNullOrWhiteSpace(key) &&
            _sessionTokenCache.TryGetAccessToken(key, out var cachedToken, out var cachedExpiresAtUtc))
        {
            lock (_lock)
            {
                _accessToken = cachedToken;
                _accessExpiresAtUtc = cachedExpiresAtUtc;
            }
            return Task.CompletedTask;
        }

        if (httpContext.Request.Cookies.TryGetValue(AccessCookie, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            var expiresAtUtc = DateTime.UtcNow.Add(BootstrapAccessLifetime);
            lock (_lock)
            {
                _accessToken = cookieToken;
                _accessExpiresAtUtc = expiresAtUtc;
            }

            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                _sessionTokenCache.SetAccessToken(sessionKey, cookieToken, expiresAtUtc);
                if (httpContext.Request.Cookies.TryGetValue(RefreshCookie, out var refreshCookie) &&
                    !string.IsNullOrWhiteSpace(refreshCookie))
                {
                    _sessionTokenCache.SetRefreshToken(sessionKey, refreshCookie);
                }
            }

            return Task.CompletedTask;
        }

        _logger.LogDebug("TokenService could not bootstrap token from request cookie or session cache.");
        return Task.CompletedTask;
    }

    private void TryBootstrapTokenUnsafe()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var key = GetSessionKey();
        if (httpContext == null)
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                _sessionTokenCache.TryGetAccessToken(key, out var cachedAccessToken, out var cachedAccessExpiresAtUtc))
            {
                _accessToken = cachedAccessToken;
                _accessExpiresAtUtc = cachedAccessExpiresAtUtc;
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(key) &&
            _sessionTokenCache.TryGetAccessToken(key, out var cachedToken, out var cachedExpiresAtUtc))
        {
            _accessToken = cachedToken;
            _accessExpiresAtUtc = cachedExpiresAtUtc;
            return;
        }

        if (httpContext.Request.Cookies.TryGetValue(AccessCookie, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            var expiresAtUtc = DateTime.UtcNow.Add(BootstrapAccessLifetime);
            _accessToken = cookieToken;
            _accessExpiresAtUtc = expiresAtUtc;

            var sessionKey = GetSessionKey();
            if (!string.IsNullOrWhiteSpace(sessionKey))
            {
                _sessionTokenCache.SetAccessToken(sessionKey, cookieToken, expiresAtUtc);
                if (httpContext.Request.Cookies.TryGetValue(RefreshCookie, out var refreshCookie) &&
                    !string.IsNullOrWhiteSpace(refreshCookie))
                {
                    _sessionTokenCache.SetRefreshToken(sessionKey, refreshCookie);
                }
            }
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        if (IsTokenValid)
        {
            return Token;
        }

        if (await RefreshAccessTokenAsync())
        {
            return Token;
        }

        return Token;
    }

    public async Task<bool> RefreshAccessTokenAsync()
    {
        var sessionKey = GetSessionKey();
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        if (!_sessionTokenCache.TryGetRefreshToken(sessionKey, out var refreshToken))
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Request.Cookies.TryGetValue(RefreshCookie, out var cookieToken) == true &&
                !string.IsNullOrWhiteSpace(cookieToken))
            {
                refreshToken = cookieToken;
                _sessionTokenCache.SetRefreshToken(sessionKey, cookieToken);
            }
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_apiBaseUri, "api/auth/refresh"));
            request.Headers.TryAddWithoutValidation("Cookie", $"{RefreshCookie}={refreshToken}");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API refresh failed with status {StatusCode}.", response.StatusCode);
                _sessionTokenCache.RemoveTokens(sessionKey);
                await ClearTokenAsync();
                return false;
            }

            var loginResponse = await response.Content.ReadFromJsonAsync<RefreshLoginResponse>();
            var cookies = ExtractAuthCookies(response);
            var accessToken = cookies.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                accessToken = loginResponse?.Token;
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return false;
            }

            var accessExpiresAtUtc = cookies.AccessExpiresAt
                ?? loginResponse?.ExpiresAt
                ?? DateTime.UtcNow.Add(BootstrapAccessLifetime);
            var refreshExpiresAtUtc = cookies.RefreshExpiresAt ?? DateTime.UtcNow.Add(BootstrapRefreshLifetime);

            await SetTokenAsync(accessToken, accessExpiresAtUtc);
            _sessionTokenCache.SetTokens(
                sessionKey,
                accessToken,
                accessExpiresAtUtc,
                cookies.RefreshToken,
                refreshExpiresAtUtc);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh API access token.");
            return false;
        }
    }

    public Task SetTokenAsync(string token, DateTime expiresAt)
    {
        var expiresAtUtc = expiresAt.ToUniversalTime();
        lock (_lock)
        {
            _accessToken = token;
            _accessExpiresAtUtc = expiresAtUtc;
        }

        var sessionKey = GetSessionKey();
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            _sessionTokenCache.SetAccessToken(sessionKey, token, expiresAtUtc);
        }

        return Task.CompletedTask;
    }

    public Task SetUserContextAsync(int userAccessCode, string? email)
    {
        lock (_lock)
        {
            _userAccessCode = userAccessCode;
            _email = email;
        }

        return Task.CompletedTask;
    }

    public Task ClearTokenAsync()
    {
        lock (_lock)
        {
            _accessToken = null;
            _accessExpiresAtUtc = DateTime.MinValue;
            _userAccessCode = 0;
            _email = null;
            _sessionKey = null;
        }

        return Task.CompletedTask;
    }

    public void SetToken(string token, DateTime expiresAt)
    {
        _ = SetTokenAsync(token, expiresAt);
    }

    public void ClearToken()
    {
        _ = ClearTokenAsync();
    }

    private string? GetSessionKey()
    {
        if (!string.IsNullOrWhiteSpace(_sessionKey))
        {
            return _sessionKey;
        }

        var sessionKey = _httpContextAccessor.HttpContext?.Session?.Id;
        if (!string.IsNullOrWhiteSpace(sessionKey))
        {
            _sessionKey = sessionKey;
        }

        return _sessionKey;
    }

    private static AuthCookies ExtractAuthCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(HeaderNames.SetCookie, out var setCookieValues))
        {
            return new AuthCookies(null, null, null, null);
        }

        string? accessToken = null;
        DateTime? accessExpiresAt = null;
        string? refreshToken = null;
        DateTime? refreshExpiresAt = null;

        foreach (var cookie in SetCookieHeaderValue.ParseList(setCookieValues.ToList()))
        {
            var name = cookie.Name.ToString();
            if (string.Equals(name, AccessCookie, StringComparison.Ordinal))
            {
                accessToken = cookie.Value.ToString();
                accessExpiresAt = cookie.Expires?.UtcDateTime;
            }
            else if (string.Equals(name, RefreshCookie, StringComparison.Ordinal))
            {
                refreshToken = cookie.Value.ToString();
                refreshExpiresAt = cookie.Expires?.UtcDateTime;
            }
        }

        return new AuthCookies(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);
    }

    private sealed record AuthCookies(
        string? AccessToken,
        DateTime? AccessExpiresAt,
        string? RefreshToken,
        DateTime? RefreshExpiresAt);

    private sealed record RefreshLoginResponse
    {
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
    }
}
