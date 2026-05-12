using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace FIS.Web.Services;

/// <summary>
/// Single source of truth for Blazor auth state. Asks the API who the current
/// cookie holder is via /api/auth/validate, then constructs a ClaimsPrincipal
/// from the claims the API returns — including access_level and role claims.
///
/// Replaces the legacy JwtAuthenticationStateProvider + DualAuthStateProvider pair.
/// Session cookies (FIS_Access_Token) are automatically sent by the browser AND
/// re-attached for server-side API calls by SessionCookieAuthHandler.
/// </summary>
public class SessionAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SessionAuthenticationStateProvider> _logger;
    private readonly UserAccessContextService _accessContext;

    private AuthenticationState _cachedState = Anonymous;
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;

    public SessionAuthenticationStateProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<SessionAuthenticationStateProvider> logger,
        UserAccessContextService accessContext)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _accessContext = accessContext;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (DateTimeOffset.UtcNow < _cacheExpiresAt && _cachedState.User.Identity?.IsAuthenticated == true)
        {
            return _cachedState;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(SessionAuthenticationStateProvider));
            var response = await client.GetAsync("api/auth/validate");

            if (!response.IsSuccessStatusCode)
            {
                _cachedState = Anonymous;
                _cacheExpiresAt = DateTimeOffset.MinValue;
                return Anonymous;
            }

            var payload = await response.Content.ReadFromJsonAsync<ValidateResponse>();
            if (payload?.Valid != true || payload.Claims is null || payload.Claims.Length == 0)
            {
                _cachedState = Anonymous;
                _cacheExpiresAt = DateTimeOffset.MinValue;
                return Anonymous;
            }

            var claims = payload.Claims.Select(c => new Claim(c.Type, c.Value)).ToList();
            var identity = new ClaimsIdentity(claims, payload.AuthType ?? "Session", ClaimTypes.Name, ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);

            // Keep UserAccessContextService primed with the access_level so legacy bit-checks work
            var accessLevelClaim = claims.FirstOrDefault(c => c.Type == "access_level")?.Value;
            var userAccessCodeClaim = claims.FirstOrDefault(c => c.Type == "user_access_code")?.Value;
            if (int.TryParse(userAccessCodeClaim, out var uac))
            {
                await _accessContext.EnsureAccessLevelAsync(uac);
            }

            _cachedState = new AuthenticationState(principal);
            _cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheLifetime);
            return _cachedState;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate session via API");
            _cachedState = Anonymous;
            _cacheExpiresAt = DateTimeOffset.MinValue;
            return Anonymous;
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        _cacheExpiresAt = DateTimeOffset.MinValue;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private sealed class ValidateResponse
    {
        public bool Valid { get; set; }
        public string? AuthType { get; set; }
        public ClaimPayload[]? Claims { get; set; }
    }

    private sealed class ClaimPayload
    {
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
