using FIS.Api.Services;
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

    public MicrosoftAuthenticationController(
        IConfiguration configuration,
        ILogger<MicrosoftAuthenticationController> logger,
        ISessionTokenStore sessionTokenStore,
        MicrosoftIdentityCompatibilityService identityCompatibility)
    {
        _configuration = configuration;
        _logger = logger;
        _sessionTokenStore = sessionTokenStore;
        _identityCompatibility = identityCompatibility;
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
            Request.Scheme);

        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            return Problem("Microsoft sign-in could not create its callback URL.");
        }

        return Challenge(
            new AuthenticationProperties { RedirectUri = callbackUrl },
            MicrosoftAuthenticationDefaults.OpenIdConnectScheme);
    }

    [HttpGet("complete")]
    [Authorize(AuthenticationSchemes = MicrosoftAuthenticationDefaults.CookieScheme)]
    public async Task<IActionResult> Complete(
        [FromQuery] string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        try
        {
            var resolvedUser = await _identityCompatibility.ResolveAsync(User, cancellationToken);
            if (resolvedUser is null || !resolvedUser.IsActive || string.IsNullOrWhiteSpace(resolvedUser.Email))
            {
                _logger.LogWarning("Microsoft sign-in was rejected because no active FIS account could be resolved");
                return await RejectSignInAsync();
            }

            var claims = AuthController.BuildAuthClaims(
                resolvedUser.UserAccessCode,
                resolvedUser.Email,
                resolvedUser.AccessLevel);
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
        return !string.IsNullOrWhiteSpace(_configuration["AzureAd:ClientId"])
            && !string.IsNullOrWhiteSpace(_configuration["AzureAd:TenantId"])
            && !string.IsNullOrWhiteSpace(_configuration["AzureAd:ClientSecret"]);
    }

    private string BuildFrontendRedirect(string path)
    {
        var configuredBaseUrl = _configuration["ApiSettings:WebBaseUrl"]?.TrimEnd('/');
        if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri)
            && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps))
        {
            return $"{configuredBaseUrl}{path}";
        }

        return path;
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !returnUrl.StartsWith("/", StringComparison.Ordinal)
            || returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/home";
        }

        return returnUrl!;
    }

    private void WriteAuthCookies(
        (string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt) tokens)
    {
        var isHttps = Request.IsHttps;
        Response.Cookies.Append(
            SessionCookieAuthenticationHandler.AccessCookieName,
            tokens.AccessToken,
            CreateCookieOptions(tokens.AccessExpiresAt, isHttps));
        Response.Cookies.Append(
            "FIS_Refresh_Token",
            tokens.RefreshToken,
            CreateCookieOptions(tokens.RefreshExpiresAt, isHttps));
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
            IsEssential = true
        };
    }
}
