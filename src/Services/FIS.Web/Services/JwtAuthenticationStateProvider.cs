using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace FIS.Web.Services;

/// <summary>
/// Provides legacy auth state from circuit-scoped in-memory auth context.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenService _tokenService;
    private readonly ILogger<JwtAuthenticationStateProvider> _logger;

    public JwtAuthenticationStateProvider(
        TokenService tokenService,
        ILogger<JwtAuthenticationStateProvider> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await _tokenService.InitializeAsync();

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

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
