using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace FIS.Web.Services;

/// <summary>
/// HTTP Message Handler that automatically adds JWT token to outgoing requests
/// Resolves TokenService from current scope to get the correct instance
/// </summary>
public class AuthorizationHeaderHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuthorizationHeaderHandler> _logger;

    public AuthorizationHeaderHandler(IServiceProvider serviceProvider, ILogger<AuthorizationHeaderHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? token = null;

        // Resolve TokenService from the current scope (Blazor circuit scope)
        // This ensures we get the SAME instance that was used during login
        var tokenService = _serviceProvider.GetService<TokenService>();

        if (tokenService != null)
        {
            // Read JWT token from TokenService (loaded from ProtectedSessionStorage)
            if (tokenService.IsTokenValid && !string.IsNullOrEmpty(tokenService.Token))
            {
                token = tokenService.Token;
                _logger.LogInformation("Using JWT token from TokenService for request method {Method}.",
                    request.Method);
            }
            else
            {
                _logger.LogWarning("No valid token in TokenService for request method {Method}. IsValid: {IsValid}, HasToken: {HasToken}",
                    request.Method, tokenService.IsTokenValid, !string.IsNullOrEmpty(tokenService.Token));
            }
        }
        else
        {
            _logger.LogError("TokenService not found in service provider.");
        }

        // Add token to Authorization header
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
