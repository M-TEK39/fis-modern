using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FIS.Api.Services.SessionManagement;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

public class SqlSessionTokenStore : ISessionTokenStore
{
    private const string AccessType = "access";
    private const string RefreshType = "refresh";
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _standardRefreshLifetime;
    private readonly TimeSpan _rememberedRefreshLifetime;

    private readonly IServiceProvider _services;
    private readonly ILogger<SqlSessionTokenStore> _logger;
    private readonly InMemorySessionTokenStore _legacyFallback;
    private int _sqlTableState;

    public SqlSessionTokenStore(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<SqlSessionTokenStore> logger
    )
    {
        _services = services;
        _logger = logger;
        _standardRefreshLifetime = ReadRefreshLifetime(
            configuration["SystemSettings:SessionRefreshLifetimeMinutes"],
            TimeSpan.FromHours(8)
        );
        _rememberedRefreshLifetime = ReadRefreshLifetime(
            configuration["SystemSettings:RememberRefreshLifetimeMinutes"],
            TimeSpan.FromDays(7)
        );
        _legacyFallback = new InMemorySessionTokenStore(
            _standardRefreshLifetime,
            _rememberedRefreshLifetime
        );
    }

    public (
        string AccessToken,
        DateTimeOffset AccessExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt
    ) IssueTokens(IEnumerable<Claim> claims, bool rememberMe = false)
    {
        var claimList = claims.ToList();
        if (!IsSqlStoreAvailable())
        {
            return _legacyFallback.IssueTokens(claimList, rememberMe);
        }

        var claimsJson = SerializeClaims(claimList);
        var now = DateTime.UtcNow;
        var sessionId = GenerateToken();
        var accessToken = GenerateToken();
        var refreshToken = GenerateToken();
        var accessExpiresAt = now.Add(AccessLifetime);
        var refreshExpiresAt = now.Add(
            rememberMe ? _rememberedRefreshLifetime : _standardRefreshLifetime
        );

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();

        db.SessionTokens.Add(
            new SessionToken
            {
                token_id = accessToken,
                token_type = AccessType,
                session_id = sessionId,
                claims_json = claimsJson,
                expires_at = accessExpiresAt,
                created_at = now,
            }
        );
        db.SessionTokens.Add(
            new SessionToken
            {
                token_id = refreshToken,
                token_type = RefreshType,
                session_id = sessionId,
                claims_json = claimsJson,
                expires_at = refreshExpiresAt,
                created_at = now,
            }
        );
        try
        {
            db.SaveChanges();
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            MarkSqlStoreUnavailable(exception);
            return _legacyFallback.IssueTokens(claimList, rememberMe);
        }
        catch (DbUpdateException exception) when (IsMissingObject(exception))
        {
            MarkSqlStoreUnavailable(exception);
            return _legacyFallback.IssueTokens(claimList, rememberMe);
        }

        return (
            accessToken,
            new DateTimeOffset(accessExpiresAt, TimeSpan.Zero),
            refreshToken,
            new DateTimeOffset(refreshExpiresAt, TimeSpan.Zero)
        );
    }

    public bool TryValidateAccessToken(string accessToken, out IReadOnlyCollection<Claim> claims)
    {
        claims = Array.Empty<Claim>();
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        if (!IsSqlStoreAvailable())
        {
            return _legacyFallback.TryValidateAccessToken(accessToken, out claims);
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();

        SessionToken? record;
        try
        {
            record = db
                .SessionTokens.AsNoTracking()
                .FirstOrDefault(t => t.token_id == accessToken && t.token_type == AccessType);
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            MarkSqlStoreUnavailable(exception);
            return _legacyFallback.TryValidateAccessToken(accessToken, out claims);
        }

        if (record is null)
            return false;

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
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        if (!IsSqlStoreAvailable())
        {
            return _legacyFallback.TryRefresh(refreshToken, out refreshedTokens, out claims);
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();

        SessionToken? record;
        using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Serializable isolation keeps two simultaneous refresh requests from
            // both observing the same one-time refresh token. The token row is
            // read without tracking because it is removed and replaced below.
            record = db
                .SessionTokens.AsNoTracking()
                .FirstOrDefault(t => t.token_id == refreshToken && t.token_type == RefreshType);
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            transaction.Rollback();
            MarkSqlStoreUnavailable(exception);
            return _legacyFallback.TryRefresh(refreshToken, out refreshedTokens, out claims);
        }

        if (record is null)
        {
            transaction.Rollback();
            return false;
        }

        var sessionId = record.session_id;
        var claimsJson = record.claims_json;

        if (record.expires_at <= DateTime.UtcNow)
        {
            var expired = db.SessionTokens.Where(t => t.session_id == sessionId).ToList();
            db.SessionTokens.RemoveRange(expired);
            db.SaveChanges();
            transaction.Commit();
            return false;
        }

        // Atomic rotate: delete the old session and issue the replacement pair
        // in the same transaction. A failed request therefore cannot consume the
        // old token without creating its replacement.
        var all = db.SessionTokens.Where(t => t.session_id == sessionId).ToList();
        db.SessionTokens.RemoveRange(all);

        var deserialized = DeserializeClaims(claimsJson);
        var rememberMe = SessionTokenConventions.IsRememberedSession(
            record.created_at,
            record.expires_at,
            _standardRefreshLifetime
        );
        var replacement = CreateTokenPair(db, deserialized, rememberMe);
        db.SaveChanges();
        transaction.Commit();

        claims = deserialized;
        refreshedTokens = replacement;
        return true;
    }

    private (
        string AccessToken,
        DateTimeOffset AccessExpiresAt,
        string RefreshToken,
        DateTimeOffset RefreshExpiresAt
    ) CreateTokenPair(FisDbContext db, IEnumerable<Claim> claims, bool rememberMe)
    {
        var claimList = claims.ToList();
        var claimsJson = SerializeClaims(claimList);
        var now = DateTime.UtcNow;
        var sessionId = GenerateToken();
        var accessToken = GenerateToken();
        var refreshToken = GenerateToken();
        var accessExpiresAt = now.Add(AccessLifetime);
        var refreshExpiresAt = now.Add(
            rememberMe ? _rememberedRefreshLifetime : _standardRefreshLifetime
        );

        db.SessionTokens.AddRange(
            new SessionToken
            {
                token_id = accessToken,
                token_type = AccessType,
                session_id = sessionId,
                claims_json = claimsJson,
                expires_at = accessExpiresAt,
                created_at = now,
            },
            new SessionToken
            {
                token_id = refreshToken,
                token_type = RefreshType,
                session_id = sessionId,
                claims_json = claimsJson,
                expires_at = refreshExpiresAt,
                created_at = now,
            }
        );

        return (
            accessToken,
            new DateTimeOffset(accessExpiresAt, TimeSpan.Zero),
            refreshToken,
            new DateTimeOffset(refreshExpiresAt, TimeSpan.Zero)
        );
    }

    public void RevokeByAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return;

        if (!IsSqlStoreAvailable())
        {
            _legacyFallback.RevokeByAccessToken(accessToken);
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        SessionToken? rec;
        try
        {
            rec = db.SessionTokens.FirstOrDefault(t =>
                t.token_id == accessToken && t.token_type == AccessType
            );
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            MarkSqlStoreUnavailable(exception);
            _legacyFallback.RevokeByAccessToken(accessToken);
            return;
        }
        if (rec is null)
            return;
        var all = db.SessionTokens.Where(t => t.session_id == rec.session_id).ToList();
        db.SessionTokens.RemoveRange(all);
        db.SaveChanges();
    }

    public void RevokeByRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        if (!IsSqlStoreAvailable())
        {
            _legacyFallback.RevokeByRefreshToken(refreshToken);
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        SessionToken? rec;
        try
        {
            rec = db.SessionTokens.FirstOrDefault(t =>
                t.token_id == refreshToken && t.token_type == RefreshType
            );
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            MarkSqlStoreUnavailable(exception);
            _legacyFallback.RevokeByRefreshToken(refreshToken);
            return;
        }
        if (rec is null)
            return;
        var all = db.SessionTokens.Where(t => t.session_id == rec.session_id).ToList();
        db.SessionTokens.RemoveRange(all);
        db.SaveChanges();
    }

    public void RevokeByUserAccessCode(int userAccessCode)
    {
        if (userAccessCode <= 0)
        {
            return;
        }

        // Always clear the compatibility store as well. A process may have
        // issued legacy fallback tokens before the optional durable table was
        // discovered, and those tokens must not survive a role/profile change.
        _legacyFallback.RevokeByUserAccessCode(userAccessCode);

        if (!IsSqlStoreAvailable())
        {
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE [session_token]
                FROM [dbo].[fis_session_tokens] AS [session_token]
                WHERE EXISTS
                (
                    SELECT 1
                    FROM OPENJSON(
                        CASE WHEN ISJSON([session_token].[claims_json]) = 1
                             THEN [session_token].[claims_json]
                             ELSE N'[]' END
                    ) AS [claim]
                    WHERE JSON_VALUE([claim].[value], '$.Type') = @claimType
                      AND TRY_CONVERT(
                          int,
                          JSON_VALUE([claim].[value], '$.Value')
                      ) = @userAccessCode
                )
                """;

            var claimType = command.CreateParameter();
            claimType.ParameterName = "@claimType";
            claimType.Value = "user_access_code";
            command.Parameters.Add(claimType);

            var userCode = command.CreateParameter();
            userCode.ParameterName = "@userAccessCode";
            userCode.Value = userAccessCode;
            command.Parameters.Add(userCode);

            command.ExecuteNonQuery();
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            MarkSqlStoreUnavailable(exception);
        }
        catch (Exception exception)
        {
            // Entitlement changes must not fail merely because the optional
            // session table is temporarily unavailable. The durable service
            // reports the capability separately; this path still clears any
            // process-local compatibility sessions.
            _logger.LogWarning(
                exception,
                "Could not revoke durable sessions for user {UserAccessCode} after a profile change.",
                userAccessCode
            );
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }
        }
    }

    private static string SerializeClaims(IEnumerable<Claim> claims)
    {
        var array = claims
            .Select(c => new SerializedClaim(c.Type, c.Value, c.ValueType, c.Issuer))
            .ToArray();
        return JsonSerializer.Serialize(array);
    }

    private static IReadOnlyCollection<Claim> DeserializeClaims(string json)
    {
        var arr =
            JsonSerializer.Deserialize<SerializedClaim[]>(json) ?? Array.Empty<SerializedClaim>();
        return arr.Select(s => new Claim(s.Type, s.Value, s.ValueType, s.Issuer)).ToList();
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static TimeSpan ReadRefreshLifetime(string? rawValue, TimeSpan fallback)
    {
        return int.TryParse(rawValue, out var minutes) && minutes is >= 15 and <= 43_200
            ? TimeSpan.FromMinutes(minutes)
            : fallback;
    }

    private bool IsSqlStoreAvailable()
    {
        var state = Volatile.Read(ref _sqlTableState);
        if (state != 0)
        {
            return state > 0;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table) THEN 1 ELSE 0 END";
            var schemaParameter = command.CreateParameter();
            schemaParameter.ParameterName = "@schema";
            schemaParameter.Value = "dbo";
            command.Parameters.Add(schemaParameter);
            var tableParameter = command.CreateParameter();
            tableParameter.ParameterName = "@table";
            tableParameter.Value = "fis_session_tokens";
            command.Parameters.Add(tableParameter);

            var exists = Convert.ToInt32(command.ExecuteScalar()) == 1;
            Volatile.Write(ref _sqlTableState, exists ? 1 : -1);

            if (!exists)
            {
                _logger.LogWarning(
                    "Optional dbo.fis_session_tokens is absent; using in-memory session tokens for legacy database compatibility."
                );
            }

            return exists;
        }
        finally
        {
            if (shouldClose && connection.State != ConnectionState.Closed)
            {
                connection.Close();
            }
        }
    }

    private void MarkSqlStoreUnavailable(Exception exception)
    {
        Volatile.Write(ref _sqlTableState, -1);
        _logger.LogWarning(
            exception,
            "Optional dbo.fis_session_tokens is unavailable; using in-memory session tokens for legacy database compatibility."
        );
    }

    // The session table is optional during rollout. Some restored legacy
    // databases contain an older table with the same name but without the
    // modern audit columns; EF then surfaces a 207 inside DbUpdateException.
    // Treat both forms as unavailable and use the process-local compatibility
    // session store instead of rejecting otherwise valid legacy credentials.
    private static bool IsMissingObject(SqlException exception) => exception.Number is 207 or 208;

    private static bool IsMissingObject(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException && IsMissingObject(sqlException);

    private sealed record SerializedClaim(
        string Type,
        string Value,
        string ValueType,
        string Issuer
    );
}
