using System.Security.Claims;

namespace FIS.Api.Services;

public interface ISessionTokenStore
{
    (string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt) IssueTokens(IEnumerable<Claim> claims);
    bool TryValidateAccessToken(string accessToken, out IReadOnlyCollection<Claim> claims);
    bool TryRefresh(string refreshToken, out (string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt) refreshedTokens, out IReadOnlyCollection<Claim> claims);
    void RevokeByAccessToken(string accessToken);
    void RevokeByRefreshToken(string refreshToken);
}
