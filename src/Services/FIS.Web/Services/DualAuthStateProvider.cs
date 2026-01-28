using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace FIS.Web.Services;

/// <summary>
/// Dual authentication state provider that supports BOTH:
/// 1. Entra ID (Azure AD) authentication via HTTP context
/// 2. JWT token authentication via TokenService
/// </summary>
public class DualAuthStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JwtAuthenticationStateProvider _jwtAuthProvider;
    private readonly ILogger<DualAuthStateProvider> _logger;

    public DualAuthStateProvider(
        IHttpContextAccessor httpContextAccessor,
        JwtAuthenticationStateProvider jwtAuthProvider,
        ILogger<DualAuthStateProvider> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _jwtAuthProvider = jwtAuthProvider;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // First, check if user is authenticated via Entra ID (HTTP context)
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User authenticated via Entra ID: {User}", 
                httpContext.User.Identity.Name);
            return new AuthenticationState(httpContext.User);
        }

        // Otherwise, check JWT token authentication
        var jwtAuthState = await _jwtAuthProvider.GetAuthenticationStateAsync();
        if (jwtAuthState.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User authenticated via JWT token");
            return jwtAuthState;
        }

        _logger.LogDebug("No authentication found - user is anonymous");
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>
    /// Notify that authentication state changed (after JWT login/logout)
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        _logger.LogInformation("Authentication state changed - notifying Blazor");
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
