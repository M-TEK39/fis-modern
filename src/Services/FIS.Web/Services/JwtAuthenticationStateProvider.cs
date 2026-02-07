using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace FIS.Web.Services;

/// <summary>
/// Custom AuthenticationStateProvider for JWT token authentication in Blazor Server
/// Uses circuit-scoped TokenService to provide authentication state
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly TokenService _tokenService;
    private readonly UserAccessContextService _userAccessContextService;
    private readonly ILogger<JwtAuthenticationStateProvider> _logger;

    public JwtAuthenticationStateProvider(
        TokenService tokenService,
        UserAccessContextService userAccessContextService,
        ILogger<JwtAuthenticationStateProvider> logger)
    {
        _tokenService = tokenService;
        _userAccessContextService = userAccessContextService;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsIdentity identity;

        if (_tokenService.IsTokenValid && !string.IsNullOrEmpty(_tokenService.Token))
        {
            try
            {
                // Parse JWT token to get claims
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(_tokenService.Token);
                var claims = token.Claims.ToList();

                // Backfill legacy access level claim from user profile API if token does not include it.
                var hasAccessLevelClaim = claims.Any(c => c.Type == "access_level");
                if (!hasAccessLevelClaim)
                {
                    var userAccessCodeClaim = claims.FirstOrDefault(c => c.Type == "user_access_code")?.Value;
                    if (int.TryParse(userAccessCodeClaim, out var userAccessCode))
                    {
                        var accessLevel = await _userAccessContextService.EnsureAccessLevelAsync(userAccessCode);
                        if (accessLevel > 0)
                        {
                            claims.Add(new Claim("access_level", accessLevel.ToString()));
                        }
                    }
                }

                // Create authenticated identity with claims from token
                identity = new ClaimsIdentity(claims, "jwt");
                
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
        return new AuthenticationState(user);
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
