using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

/// <summary>
/// HTTP Message Handler that forwards auth cookies from the incoming browser request
/// to API calls made by the Blazor Server host.
/// </summary>
public class AuthorizationHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthorizationHeaderHandler> _logger;

    private const string AccessCookie = "FIS_Access_Token";
    private const string RefreshCookie = "FIS_Refresh_Token";

    public AuthorizationHeaderHandler(IHttpContextAccessor httpContextAccessor, ILogger<AuthorizationHeaderHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var requestCookies = _httpContextAccessor.HttpContext?.Request.Cookies;
        var accessToken = string.Empty;
        var refreshToken = string.Empty;
        var hasAccess = requestCookies?.TryGetValue(AccessCookie, out accessToken) == true && !string.IsNullOrWhiteSpace(accessToken);
        var hasRefresh = requestCookies?.TryGetValue(RefreshCookie, out refreshToken) == true && !string.IsNullOrWhiteSpace(refreshToken);
        if (hasAccess || hasRefresh)
        {
            var cookieParts = new List<string>();
            if (hasAccess) cookieParts.Add($"{AccessCookie}={accessToken}");
            if (hasRefresh) cookieParts.Add($"{RefreshCookie}={refreshToken}");
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookieParts));
            _logger.LogDebug("Forwarded auth cookies for {Method} {Uri}.", request.Method, request.RequestUri?.PathAndQuery);
        }
        else
        {
            _logger.LogWarning("No auth cookies found for {Method} {Uri}.", request.Method, request.RequestUri?.PathAndQuery);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
