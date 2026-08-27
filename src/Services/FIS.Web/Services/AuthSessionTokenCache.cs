using System.Collections.Concurrent;

namespace FIS.Web.Services;

public class AuthSessionTokenCache
{
    private static readonly TimeSpan BootstrapAccessLifetime = TimeSpan.FromMinutes(14);
    private static readonly TimeSpan BootstrapRefreshLifetime = TimeSpan.FromHours(8);
    private readonly ConcurrentDictionary<string, SessionTokenRecord> _sessionTokens = new(StringComparer.Ordinal);

    public void SetAccessToken(string sessionKey, string accessToken)
        => SetAccessToken(sessionKey, accessToken, DateTime.UtcNow.Add(BootstrapAccessLifetime));

    public void SetAccessToken(string sessionKey, string accessToken, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sessionKey) || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        var accessExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        _sessionTokens.AddOrUpdate(
            sessionKey,
            _ => new SessionTokenRecord(accessToken, accessExpiresAtUtc, string.Empty, DateTime.MinValue),
            (_, current) => current with
            {
                AccessToken = accessToken,
                AccessExpiresAtUtc = accessExpiresAtUtc
            });
    }

    public void SetRefreshToken(string sessionKey, string refreshToken)
        => SetRefreshToken(sessionKey, refreshToken, DateTime.UtcNow.Add(BootstrapRefreshLifetime));

    public void SetRefreshToken(string sessionKey, string refreshToken, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sessionKey) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var refreshExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        _sessionTokens.AddOrUpdate(
            sessionKey,
            _ => new SessionTokenRecord(string.Empty, DateTime.MinValue, refreshToken, refreshExpiresAtUtc),
            (_, current) => current with
            {
                RefreshToken = refreshToken,
                RefreshExpiresAtUtc = refreshExpiresAtUtc
            });
    }

    public void SetTokens(
        string sessionKey,
        string accessToken,
        DateTime accessExpiresAtUtc,
        string? refreshToken,
        DateTime? refreshExpiresAtUtc)
    {
        SetAccessToken(sessionKey, accessToken, accessExpiresAtUtc);

        if (!string.IsNullOrWhiteSpace(refreshToken) && refreshExpiresAtUtc.HasValue)
        {
            SetRefreshToken(sessionKey, refreshToken, refreshExpiresAtUtc.Value);
        }
    }

    public bool TryGetAccessToken(string sessionKey, out string accessToken)
        => TryGetAccessToken(sessionKey, out accessToken, out _);

    public bool TryGetAccessToken(string sessionKey, out string accessToken, out DateTime expiresAtUtc)
    {
        accessToken = string.Empty;
        expiresAtUtc = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        if (!_sessionTokens.TryGetValue(sessionKey, out var record) || string.IsNullOrWhiteSpace(record.AccessToken))
        {
            return false;
        }

        if (record.AccessExpiresAtUtc <= DateTime.UtcNow)
        {
            ClearExpiredAccessToken(sessionKey, record);
            return false;
        }

        accessToken = record.AccessToken;
        expiresAtUtc = record.AccessExpiresAtUtc;
        return true;
    }

    public bool TryGetRefreshToken(string sessionKey, out string refreshToken)
        => TryGetRefreshToken(sessionKey, out refreshToken, out _);

    public bool TryGetRefreshToken(string sessionKey, out string refreshToken, out DateTime expiresAtUtc)
    {
        refreshToken = string.Empty;
        expiresAtUtc = DateTime.MinValue;
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return false;
        }

        if (!_sessionTokens.TryGetValue(sessionKey, out var record) || string.IsNullOrWhiteSpace(record.RefreshToken))
        {
            return false;
        }

        if (record.RefreshExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessionTokens.TryRemove(sessionKey, out _);
            return false;
        }

        refreshToken = record.RefreshToken;
        expiresAtUtc = record.RefreshExpiresAtUtc;
        return true;
    }

    public void RemoveAccessToken(string sessionKey)
        => RemoveTokens(sessionKey);

    public void RemoveTokens(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        _sessionTokens.TryRemove(sessionKey, out _);
    }

    private void ClearExpiredAccessToken(string sessionKey, SessionTokenRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.RefreshToken) || record.RefreshExpiresAtUtc <= DateTime.UtcNow)
        {
            _sessionTokens.TryRemove(sessionKey, out _);
            return;
        }

        _sessionTokens.AddOrUpdate(
            sessionKey,
            record,
            (_, current) => current with
            {
                AccessToken = string.Empty,
                AccessExpiresAtUtc = DateTime.MinValue
            });
    }

    private sealed record SessionTokenRecord(
        string AccessToken,
        DateTime AccessExpiresAtUtc,
        string RefreshToken,
        DateTime RefreshExpiresAtUtc);
}
