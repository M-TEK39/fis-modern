using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FIS.Api.Services;

public class InMemorySessionTokenStore : ISessionTokenStore
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _standardRefreshLifetime;
    private readonly TimeSpan _rememberedRefreshLifetime;

    private readonly ConcurrentDictionary<string, SessionRecord> _accessSessions = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<string, SessionRecord> _refreshSessions = new(
        StringComparer.Ordinal
    );

    public InMemorySessionTokenStore(
        TimeSpan? standardRefreshLifetime = null,
        TimeSpan? rememberedRefreshLifetime = null
    )
    {
        _standardRefreshLifetime = standardRefreshLifetime ?? TimeSpan.FromHours(8);
        _rememberedRefreshLifetime = rememberedRefreshLifetime ?? TimeSpan.FromDays(7);
    }

    public (
        string AccessToken,
        DateTimeOffset AccessExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt
    ) IssueTokens(IEnumerable<Claim> claims, bool rememberMe = false)
    {
        var claimList = claims.ToList().AsReadOnly();
        var now = DateTimeOffset.UtcNow;
        var accessToken = GenerateToken();
        var refreshToken = GenerateToken();

        var record = new SessionRecord(
            claimList,
            now.Add(AccessLifetime),
            now.Add(rememberMe ? _rememberedRefreshLifetime : _standardRefreshLifetime),
            rememberMe
        );

        _accessSessions[accessToken] = record;
        _refreshSessions[refreshToken] = record;

        return (accessToken, record.AccessExpiresAt, refreshToken, record.RefreshExpiresAt);
    }

    public bool TryValidateAccessToken(string accessToken, out IReadOnlyCollection<Claim> claims)
    {
        claims = Array.Empty<Claim>();

        if (!_accessSessions.TryGetValue(accessToken, out var record))
        {
            return false;
        }

        if (record.AccessExpiresAt <= DateTimeOffset.UtcNow)
        {
            _accessSessions.TryRemove(accessToken, out _);
            return false;
        }

        claims = record.Claims;
        return true;
    }

    public bool TryRefresh(
        string refreshToken,
        out (
            string AccessToken,
            DateTimeOffset AccessExpiresAt,
            string RefreshToken,
            DateTimeOffset RefreshExpiresAt
        ) refreshedTokens,
        out IReadOnlyCollection<Claim> claims
    )
    {
        refreshedTokens = default;
        claims = Array.Empty<Claim>();

        if (!_refreshSessions.TryRemove(refreshToken, out var record))
        {
            return false;
        }

        if (record.RefreshExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        claims = record.Claims;
        refreshedTokens = IssueTokens(record.Claims, record.RememberMe);
        return true;
    }

    public void RevokeByAccessToken(string accessToken)
    {
        _accessSessions.TryRemove(accessToken, out _);
    }

    public void RevokeByRefreshToken(string refreshToken)
    {
        _refreshSessions.TryRemove(refreshToken, out _);
    }

    public void RevokeByUserAccessCode(int userAccessCode)
    {
        if (userAccessCode <= 0)
        {
            return;
        }

        foreach (var pair in _accessSessions)
        {
            if (HasUserAccessCode(pair.Value.Claims, userAccessCode))
            {
                _accessSessions.TryRemove(pair.Key, out _);
            }
        }

        foreach (var pair in _refreshSessions)
        {
            if (HasUserAccessCode(pair.Value.Claims, userAccessCode))
            {
                _refreshSessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool HasUserAccessCode(
        IReadOnlyCollection<Claim> claims,
        int userAccessCode
    ) => claims.Any(claim =>
        string.Equals(claim.Type, "user_access_code", StringComparison.OrdinalIgnoreCase)
        && int.TryParse(claim.Value, out var value)
        && value == userAccessCode
    );

    private sealed record SessionRecord(
        IReadOnlyCollection<Claim> Claims,
        DateTimeOffset AccessExpiresAt,
        DateTimeOffset RefreshExpiresAt,
        bool RememberMe
    );
}
