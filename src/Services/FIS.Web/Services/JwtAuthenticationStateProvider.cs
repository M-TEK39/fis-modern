using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace FIS.Web.Services;

/// <summary>
/// Custom AuthenticationStateProvider for JWT token authentication in Blazor Server
/// Uses circuit-scoped TokenService to provide authentication state
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenService _tokenService;
    private readonly ILogger<JwtAuthenticationStateProvider> _logger;

    public JwtAuthenticationStateProvider(TokenService tokenService, ILogger<JwtAuthenticationStateProvider> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsIdentity identity;

        if (_tokenService.IsTokenValid && !string.IsNullOrEmpty(_tokenService.Token))
        {
            try
            {
                // Parse JWT token to get claims
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(_tokenService.Token);

                // Create authenticated identity with claims from token
                identity = new ClaimsIdentity(token.Claims, "jwt");
                
                _logger.LogInformation("User authenticated with JWT token. User: {User}", 
                    identity.FindFirst("user_access_code")?.Value ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JWT token - treating as anonymous");
                // Invalid token - return anonymous
                identity = new ClaimsIdentity();
            }
        }
        else
        {
            _logger.LogDebug("No valid token - user is anonymous");
            identity = new ClaimsIdentity();
        }

        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }

    /// <summary>
    /// Notify Blazor that authentication state has changed (after login/logout)
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        _logger.LogInformation("Authentication state changed - notifying Blazor");
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
