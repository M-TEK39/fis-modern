using FIS.Api.Services;
using FIS.Api.Services.Finance;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/auth/microsoft")]
public sealed class MicrosoftAuthenticationController : ControllerBase
{
    private const string DefaultFailurePath = "/login?error=microsoft-sign-in";

    private readonly IConfiguration _configuration;
    private readonly ILogger<MicrosoftAuthenticationController> _logger;
    private readonly ISessionTokenStore _sessionTokenStore;
    private readonly MicrosoftIdentityCompatibilityService _identityCompatibility;
    private readonly LegacyRoleCompatibilityService _legacyRoleCompatibility;
    private readonly LegacyFinanceAccessService _legacyFinanceAccess;

    public MicrosoftAuthenticationController(
        IConfiguration configuration,
        ILogger<MicrosoftAuthenticationController> logger,
        ISessionTokenStore sessionTokenStore,
        MicrosoftIdentityCompatibilityService identityCompatibility,
        LegacyRoleCompatibilityService legacyRoleCompatibility,
        LegacyFinanceAccessService legacyFinanceAccess
    )
    {
        _configuration = configuration;
        _logger = logger;
        _sessionTokenStore = sessionTokenStore;
        _identityCompatibility = identityCompatibility;
        _legacyRoleCompatibility = legacyRoleCompatibility;
        _legacyFinanceAccess = legacyFinanceAccess;
    }

    [HttpGet("sign-in")]
    [AllowAnonymous]
    public IActionResult SignIn([FromQuery] string? returnUrl = null)
    {
        if (!IsMicrosoftIdentityConfigured())
        {
            return NotFound(new { error = "Microsoft sign-in is not configured." });
        }

        var safeReturnUrl = GetSafeReturnUrl(returnUrl);
        var callbackUrl = Url.Action(
            nameof(Complete),
            "MicrosoftAuthentication",
            new { returnUrl = safeReturnUrl },
            Request.Scheme
        );

        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            return Problem("Microsoft sign-in could not create its callback URL.");
        }

        return Challenge(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            MicrosoftAuthenticationDefaults.OpenIdConnectScheme
        );
    }

    /// <summary>
    /// Public, non-secret capability check used by the login page. The
    /// frontend must not infer Microsoft availability from a browser-side
    /// environment flag because the API owns the Azure credential binding.
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new { enabled = IsMicrosoftIdentityConfigured() });

    [HttpGet("complete")]
    [Authorize(AuthenticationSchemes = MicrosoftAuthenticationDefaults.CookieScheme)]
    public async Task<IActionResult> Complete(
        [FromQuery] string? returnUrl = null,
        CancellationToken cancellationToken = default
    )
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        try
        {
            var resolvedUser = await _identityCompatibility.ResolveAsync(User, cancellationToken);
            if (
                resolvedUser is null
                || !resolvedUser.IsActive
                || string.IsNullOrWhiteSpace(resolvedUser.Email)
            )
            {
                _logger.LogWarning(
                    "Microsoft sign-in was rejected because no active FIS account could be resolved"
                );
                return await RejectSignInAsync();
            }

            var financeProfile = await _legacyFinanceAccess.ResolveProfileScopeAsync(
                resolvedUser.UserAccessCode,
                cancellationToken
            );
            var grantedRoles = await _legacyRoleCompatibility.ResolveRolesAsync(
                resolvedUser.Username,
                resolvedUser.AccessString,
                resolvedUser.AccessLevel,
                cancellationToken
            );
            var claims = AuthController.BuildAuthClaims(
                resolvedUser.UserAccessCode,
                resolvedUser.Email,
                resolvedUser.AccessLevel,
                additionalRoles: grantedRoles,
                departmentCode: financeProfile?.DepartmentCode,
                siteCode: financeProfile?.SiteCode,
                hasAllDepartmentFinanceDataRole: grantedRoles.Contains(
                    "Financial Data (All Departments)",
                    StringComparer.OrdinalIgnoreCase
                ),
                legacyUsername: resolvedUser.Username,
                hasProvinceWideVehicleListRole: grantedRoles.Contains(
                    "Vehicle List for All Departments in Province",
                    StringComparer.OrdinalIgnoreCase
                )
            );
            var tokens = _sessionTokenStore.IssueTokens(claims);
            WriteAuthCookies(tokens);

            await HttpContext.SignOutAsync(MicrosoftAuthenticationDefaults.CookieScheme);
            return Redirect(BuildFrontendRedirect(safeReturnUrl));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Microsoft sign-in completion failed");
            return await RejectSignInAsync();
        }
    }

    private async Task<IActionResult> RejectSignInAsync()
    {
        await HttpContext.SignOutAsync(MicrosoftAuthenticationDefaults.CookieScheme);
        return Redirect(BuildFrontendRedirect(DefaultFailurePath));
    }

    private bool IsMicrosoftIdentityConfigured()
    {
        return _configuration.GetValue<bool?>("SystemSettings:EntraEnabled") != false
            && !string.IsNullOrWhiteSpace(_configuration["AzureAd:ClientId"])
            && !string.IsNullOrWhiteSpace(_configuration["AzureAd:TenantId"])
            && !string.IsNullOrWhiteSpace(_configuration["AzureAd:ClientSecret"]);
    }

    private string BuildFrontendRedirect(string path)
    {
        var configuredBaseUrl = _configuration["ApiSettings:WebBaseUrl"]?.TrimEnd('/');
        if (
            Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri)
            && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps)
            && (
                !IsLoopbackHost(baseUri.Host)
                || IsLoopbackHost(Request.Host.Host)
            )
        )
        {
            return $"{configuredBaseUrl}{path}";
        }

        // Compose may intentionally leave SERVER_IP/SERVER_HOSTNAME unset in
        // a test deployment, which produces an https://localhost base URL.
        // Behind nginx, the forwarded request host is the public FIS origin;
        // use it instead of redirecting a client browser to its own localhost.
        if (Request.Host is { Host: var requestHost } && !string.IsNullOrWhiteSpace(requestHost))
        {
            var requestScheme = Request.Scheme is "http" or "https" ? Request.Scheme : "https";
            var requestOrigin = new UriBuilder(requestScheme, requestHost).Uri.GetLeftPart(
                UriPartial.Authority
            );
            return $"{requestOrigin}{path}";
        }

        return path;
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (
            string.IsNullOrWhiteSpace(returnUrl)
            || !returnUrl.StartsWith("/", StringComparison.Ordinal)
            || returnUrl.StartsWith("//", StringComparison.Ordinal)
        )
        {
            return "/home";
        }

        return returnUrl!;
    }

    private void WriteAuthCookies(
        (
            string AccessToken,
            DateTimeOffset AccessExpiresAt,
            string RefreshToken,
            DateTimeOffset RefreshExpiresAt
        ) tokens
    )
    {
        var isHttps = Request.IsHttps;
        Response.Cookies.Append(
            SessionCookieAuthenticationHandler.AccessCookieName,
            tokens.AccessToken,
            CreateCookieOptions(tokens.AccessExpiresAt, isHttps)
        );
        Response.Cookies.Append(
            "FIS_Refresh_Token",
            tokens.RefreshToken,
            CreateCookieOptions(tokens.RefreshExpiresAt, isHttps)
        );
    }

    private static CookieOptions CreateCookieOptions(DateTimeOffset expiresAt, bool isHttps)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt,
            Path = "/",
            IsEssential = true,
        };
    }
}
