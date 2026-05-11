using System.Net;
using System.Net.Http.Json;
using Microsoft.Net.Http.Headers;

namespace FIS.Web.Services;

public sealed class SessionCookieAuthHandler : DelegatingHandler
{
    private const string AccessCookieName = "FIS_Access_Token";
    private const string RefreshCookieName = "FIS_Refresh_Token";
    private static readonly TimeSpan RefreshFallbackLifetime = TimeSpan.FromHours(8);

    private readonly Uri _apiBaseUri;
    private readonly AuthSessionTokenCache _sessionTokenCache;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SessionCookieAuthHandler> _logger;

    public SessionCookieAuthHandler(
        IConfiguration configuration,
        AuthSessionTokenCache sessionTokenCache,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionCookieAuthHandler> logger)
    {
        var apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5010";
        _apiBaseUri = new Uri($"{apiBaseUrl.TrimEnd('/')}/");
        _sessionTokenCache = sessionTokenCache;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!ShouldHandleRequest(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        AddAccessCookie(request);
        using var retryRequest = await CloneRequestAsync(request, cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        if (!await TryRefreshAccessTokenAsync(cancellationToken))
        {
            return response;
        }

        response.Dispose();
        AddAccessCookie(retryRequest);
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private bool ShouldHandleRequest(HttpRequestMessage request)
    {
        if (request.RequestUri is null)
        {
            return false;
        }

        var requestUri = request.RequestUri.IsAbsoluteUri
            ? request.RequestUri
            : new Uri(_apiBaseUri, request.RequestUri);

        return string.Equals(requestUri.Scheme, _apiBaseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(requestUri.Host, _apiBaseUri.Host, StringComparison.OrdinalIgnoreCase)
            && requestUri.Port == _apiBaseUri.Port;
    }

    private void AddAccessCookie(HttpRequestMessage request)
    {
        var sessionKey = _httpContextAccessor.HttpContext?.Session?.Id;
        if (string.IsNullOrWhiteSpace(sessionKey) ||
            !_sessionTokenCache.TryGetAccessToken(sessionKey, out var token))
        {
            return;
        }

        request.Headers.Remove("Cookie");
        request.Headers.TryAddWithoutValidation("Cookie", $"{AccessCookieName}={token}");
    }

    private async Task<bool> TryRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var sessionKey = _httpContextAccessor.HttpContext?.Session?.Id;
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        if (!TryGetRefreshToken(sessionKey, out var refreshToken))
        {
            _logger.LogDebug("No refresh token is available for the current web session.");
            return false;
        }

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(_apiBaseUri, "api/auth/refresh"));
        refreshRequest.Headers.TryAddWithoutValidation("Cookie", $"{RefreshCookieName}={refreshToken}");

        using var refreshClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        using var refreshResponse = await refreshClient.SendAsync(refreshRequest, cancellationToken);
        if (!refreshResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("API token refresh failed with status {StatusCode}.", refreshResponse.StatusCode);
            _sessionTokenCache.RemoveTokens(sessionKey);
            return false;
        }

        var loginResponse = await refreshResponse.Content.ReadFromJsonAsync<RefreshLoginResponse>(
            cancellationToken: cancellationToken);

        var authCookies = ExtractAuthCookies(refreshResponse);
        var accessToken = authCookies.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            accessToken = loginResponse?.Token;
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("API token refresh succeeded but no access token was returned.");
            return false;
        }

        var accessExpiresAt = authCookies.AccessExpiresAt
            ?? loginResponse?.ExpiresAt
            ?? DateTime.UtcNow.AddMinutes(14);
        var refreshExpiresAt = authCookies.RefreshExpiresAt ?? DateTime.UtcNow.Add(RefreshFallbackLifetime);

        _sessionTokenCache.SetTokens(
            sessionKey,
            accessToken,
            accessExpiresAt,
            authCookies.RefreshToken,
            refreshExpiresAt);

        return true;
    }

    private bool TryGetRefreshToken(string sessionKey, out string refreshToken)
    {
        if (_sessionTokenCache.TryGetRefreshToken(sessionKey, out refreshToken))
        {
            return true;
        }

        if (_httpContextAccessor.HttpContext?.Request.Cookies.TryGetValue(RefreshCookieName, out var cookieToken) == true
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            refreshToken = cookieToken;
            _sessionTokenCache.SetRefreshToken(sessionKey, cookieToken);
            return true;
        }

        refreshToken = string.Empty;
        return false;
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
            if (string.Equals(name, AccessCookieName, StringComparison.Ordinal))
            {
                accessToken = cookie.Value.ToString();
                accessExpiresAt = cookie.Expires?.UtcDateTime;
            }
            else if (string.Equals(name, RefreshCookieName, StringComparison.Ordinal))
            {
                refreshToken = cookie.Value.ToString();
                refreshExpiresAt = cookie.Expires?.UtcDateTime;
            }
        }

        return new AuthCookies(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
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
