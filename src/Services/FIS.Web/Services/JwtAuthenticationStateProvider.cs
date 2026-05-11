using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace FIS.Web.Services;

/// <summary>
/// Provides legacy auth state from circuit-scoped in-memory auth context.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenService _tokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthSessionTokenCache _sessionTokenCache;
    private readonly ILogger<JwtAuthenticationStateProvider> _logger;
    private const string AccessCookie = "FIS_Access_Token";
    private static readonly TimeSpan BootstrapAccessLifetime = TimeSpan.FromMinutes(14);

    public JwtAuthenticationStateProvider(
        TokenService tokenService,
        IHttpContextAccessor httpContextAccessor,
        AuthSessionTokenCache sessionTokenCache,
        ILogger<JwtAuthenticationStateProvider> logger)
    {
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
        _sessionTokenCache = sessionTokenCache;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _tokenService.InitializeAsync();
        await TryRestoreTokenFromRequestContextAsync();

        if (_tokenService.IsTokenValid)
        {
            var claims = new List<Claim>();

            if (_tokenService.UserAccessCode > 0)
            {
                claims.Add(new Claim("user_access_code", _tokenService.UserAccessCode.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(_tokenService.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, _tokenService.Email));
                claims.Add(new Claim(ClaimTypes.Name, _tokenService.Email));
            }

            var identity = new ClaimsIdentity(claims, "legacy-session");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        _logger.LogDebug("No valid legacy session in circuit state.");
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    private async Task TryRestoreTokenFromRequestContextAsync()
    {
        if (_tokenService.IsTokenValid)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        if (httpContext.Request.Cookies.TryGetValue(AccessCookie, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            await _tokenService.SetTokenAsync(cookieToken, DateTime.UtcNow.Add(BootstrapAccessLifetime));
            return;
        }

        var sessionKey = httpContext.Session?.Id;
        if (!string.IsNullOrWhiteSpace(sessionKey) &&
            _sessionTokenCache.TryGetAccessToken(sessionKey, out var cachedToken, out var expiresAtUtc))
        {
            await _tokenService.SetTokenAsync(cachedToken, expiresAtUtc);
        }
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
