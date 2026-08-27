using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

public class SqlSessionTokenStore : ISessionTokenStore
{
    private const string AccessType = "access";
    private const string RefreshType = "refresh";
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromHours(8);

    private readonly IServiceProvider _services;
    private readonly ILogger<SqlSessionTokenStore> _logger;

    public SqlSessionTokenStore(IServiceProvider services, ILogger<SqlSessionTokenStore> logger)
    {
        _services = services;
        _logger = logger;
    }

    public (string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt) IssueTokens(IEnumerable<Claim> claims)
    {
        var claimList = claims.ToList();
        var claimsJson = SerializeClaims(claimList);
        var now = DateTime.UtcNow;
        var sessionId = GenerateToken();
        var accessToken = GenerateToken();
        var refreshToken = GenerateToken();
        var accessExpiresAt = now.Add(AccessLifetime);
        var refreshExpiresAt = now.Add(RefreshLifetime);

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();

        db.SessionTokens.Add(new SessionToken
        {
            token_id = accessToken,
            token_type = AccessType,
            session_id = sessionId,
            claims_json = claimsJson,
            expires_at = accessExpiresAt,
            created_at = now
        });
        db.SessionTokens.Add(new SessionToken
        {
            token_id = refreshToken,
            token_type = RefreshType,
            session_id = sessionId,
            claims_json = claimsJson,
            expires_at = refreshExpiresAt,
            created_at = now
        });
        db.SaveChanges();

        return (accessToken, new DateTimeOffset(accessExpiresAt, TimeSpan.Zero),
                refreshToken, new DateTimeOffset(refreshExpiresAt, TimeSpan.Zero));
    }

    public bool TryValidateAccessToken(string accessToken, out IReadOnlyCollection<Claim> claims)
    {
        claims = Array.Empty<Claim>();
        if (string.IsNullOrWhiteSpace(accessToken)) return false;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();

        var record = db.SessionTokens
            .AsNoTracking()
            .FirstOrDefault(t => t.token_id == accessToken && t.token_type == AccessType);

        if (record is null) return false;

        if (record.expires_at <= DateTime.UtcNow)
        {
            var expired = db.SessionTokens.FirstOrDefault(t => t.token_id == accessToken);
            if (expired is not null)
            {
                db.SessionTokens.Remove(expired);
                db.SaveChanges();
            }
            return false;
        }

        claims = DeserializeClaims(record.claims_json);
        return true;
    }

    public bool TryRefresh(string refreshToken, out (string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTimeOffset RefreshExpiresAt) refreshedTokens, out IReadOnlyCollection<Claim> claims)
    {
        refreshedTokens = default;
        claims = Array.Empty<Claim>();
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();

        var record = db.SessionTokens
            .FirstOrDefault(t => t.token_id == refreshToken && t.token_type == RefreshType);

        if (record is null) return false;

        var sessionId = record.session_id;
        var claimsJson = record.claims_json;

        // Atomic rotate: delete entire old session, issue new pair
        var all = db.SessionTokens.Where(t => t.session_id == sessionId).ToList();
        db.SessionTokens.RemoveRange(all);
        db.SaveChanges();

        if (record.expires_at <= DateTime.UtcNow) return false;

        var deserialized = DeserializeClaims(claimsJson);
        claims = deserialized;
        refreshedTokens = IssueTokens(deserialized);
        return true;
    }

    public void RevokeByAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var rec = db.SessionTokens.FirstOrDefault(t => t.token_id == accessToken && t.token_type == AccessType);
        if (rec is null) return;
        var all = db.SessionTokens.Where(t => t.session_id == rec.session_id).ToList();
        db.SessionTokens.RemoveRange(all);
        db.SaveChanges();
    }

    public void RevokeByRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var rec = db.SessionTokens.FirstOrDefault(t => t.token_id == refreshToken && t.token_type == RefreshType);
        if (rec is null) return;
        var all = db.SessionTokens.Where(t => t.session_id == rec.session_id).ToList();
        db.SessionTokens.RemoveRange(all);
        db.SaveChanges();
    }

    private static string SerializeClaims(IEnumerable<Claim> claims)
    {
        var array = claims.Select(c => new SerializedClaim(c.Type, c.Value, c.ValueType, c.Issuer)).ToArray();
        return JsonSerializer.Serialize(array);
    }

    private static IReadOnlyCollection<Claim> DeserializeClaims(string json)
    {
        var arr = JsonSerializer.Deserialize<SerializedClaim[]>(json) ?? Array.Empty<SerializedClaim>();
        return arr.Select(s => new Claim(s.Type, s.Value, s.ValueType, s.Issuer)).ToList();
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record SerializedClaim(string Type, string Value, string ValueType, string Issuer);
}
