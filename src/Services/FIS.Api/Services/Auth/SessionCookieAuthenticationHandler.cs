using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FIS.Api.Services;

public class SessionCookieAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SessionCookie";
    public const string AccessCookieName = "FIS_Access_Token";

    private readonly ISessionTokenStore _tokenStore;

    public SessionCookieAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionTokenStore tokenStore
    )
        : base(options, logger, encoder)
    {
        _tokenStore = tokenStore;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (
            !Request.Cookies.TryGetValue(AccessCookieName, out var accessToken)
            || string.IsNullOrWhiteSpace(accessToken)
        )
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!_tokenStore.TryValidateAccessToken(accessToken, out var claims))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired access token."));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
