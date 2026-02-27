using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

/// <summary>
/// HTTP Message Handler that automatically adds JWT token to outgoing requests.
/// Reads from HttpContext.Items which is populated by the circuit-scoped TokenService.
/// IHttpContextAccessor uses AsyncLocal internally, so this correctly reads the token
/// for whichever Blazor circuit is currently executing the request.
/// </summary>
public class AuthorizationHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthorizationHeaderHandler> _logger;

    private const string ITEMS_KEY = "FIS_JWT_Token";

    public AuthorizationHeaderHandler(IHttpContextAccessor httpContextAccessor, ILogger<AuthorizationHeaderHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.Items[ITEMS_KEY] as string;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            _logger.LogInformation("JWT token attached for {Method} {Uri}.", request.Method, request.RequestUri?.PathAndQuery);
        }
        else
        {
            _logger.LogWarning("No JWT token in HttpContext.Items for {Method} {Uri}.", request.Method, request.RequestUri?.PathAndQuery);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
