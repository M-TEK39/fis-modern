using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services.SessionManagement;

/// <summary>
/// Provides administration operations only for the durable session-token table.
/// The table is an optional additive object, so every operation probes its live
/// shape before issuing a read or write and never falls back to process memory.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Command text uses fixed table, column, and JSON paths; all values are parameterized."
)]
public sealed class SqlSessionManagementService : ISessionManagementService
{
    private const string SchemaName = "dbo";
    private const string TableName = "fis_session_tokens";
    private const string AccessTokenType = "access";
    private const string RefreshTokenType = "refresh";
    private const string UserAccessCodeClaimType = "user_access_code";
    private const int MaximumListLimit = 500;
    private readonly TimeSpan _standardRefreshLifetime;

    private static readonly string[] RequiredColumns =
    [
        "token_id",
        "token_type",
        "session_id",
        "claims_json",
        "expires_at",
        "created_at",
    ];

    private readonly FisDbContext _context;
    private readonly ILogger<SqlSessionManagementService> _logger;

    public SqlSessionManagementService(
        FisDbContext context,
        IConfiguration configuration,
        ILogger<SqlSessionManagementService> logger
    )
    {
        _context = context;
        _logger = logger;
        _standardRefreshLifetime =
            int.TryParse(
                configuration["SystemSettings:SessionRefreshLifetimeMinutes"],
                out var minutes
            ) && minutes is >= 15 and <= 43_200
                ? TimeSpan.FromMinutes(minutes)
                : TimeSpan.FromHours(8);
    }

    public async Task<SessionStoreStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            await using var connectionScope = await OpenConnectionAsync(cancellationToken);
            var probe = await ProbeAsync(connectionScope.Connection, cancellationToken);
            return new SessionStoreStatus(
                probe,
                probe
                    ? "Durable session management is available."
                    : "Durable session management is unavailable because dbo.fis_session_tokens is not available.",
                checkedAtUtc
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Durable session management capability probe failed.");
            return new SessionStoreStatus(
                false,
                "Durable session management is unavailable.",
                checkedAtUtc
            );
        }
    }

    public async Task<SessionListResult> ListActiveSessionsAsync(
        int? userAccessCode,
        int currentUserAccessCode,
        string? currentAccessToken,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var boundedLimit = Math.Clamp(limit, 1, MaximumListLimit);

        return await ExecuteAgainstDurableStoreAsync(
            async (connection, cancellationToken) =>
            {
                var currentSessionId = await FindCurrentSessionIdAsync(
                    connection,
                    currentUserAccessCode,
                    currentAccessToken,
                    cancellationToken
                );
                var sessions = await ReadActiveSessionsAsync(
                    connection,
                    userAccessCode,
                    currentSessionId,
                    boundedLimit,
                    cancellationToken
                );
                return new SessionListResult(
                    SessionManagementStatus.Succeeded,
                    sessions,
                    "Active durable sessions retrieved."
                );
            },
            (status, description) =>
                new SessionListResult(status, Array.Empty<ActiveSession>(), description),
            cancellationToken
        );
    }

    public async Task<SessionOperationResult> RevokeCurrentSessionAsync(
        int userAccessCode,
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        if (userAccessCode <= 0 || string.IsNullOrWhiteSpace(accessToken))
        {
            return new SessionOperationResult(
                SessionManagementStatus.Failed,
                0,
                "The current session could not be identified."
            );
        }

        return await ExecuteAgainstDurableStoreAsync(
            async (connection, cancellationToken) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                DELETE [session_token]
                FROM [dbo].[{TableName}] AS [session_token]
                WHERE [session_token].[session_id] =
                (
                    SELECT TOP (1) [candidate].[session_id]
                    FROM [dbo].[{TableName}] AS [candidate]
                    WHERE [candidate].[token_id] = @accessToken
                      AND [candidate].[token_type] = @accessTokenType
                      AND EXISTS
                      (
                          SELECT 1
                          FROM OPENJSON(
                              CASE WHEN ISJSON([candidate].[claims_json]) = 1
                                   THEN [candidate].[claims_json]
                                   ELSE N'[]' END
                          ) AS [claim]
                          WHERE JSON_VALUE([claim].[value], '$.Type') = @claimType
                            AND TRY_CONVERT(
                                int,
                                JSON_VALUE([claim].[value], '$.Value')
                            ) = @userAccessCode
                      )
                )
                """;
                AddParameter(command, "@accessToken", DbType.String, accessToken, 64);
                AddParameter(command, "@accessTokenType", DbType.String, AccessTokenType, 16);
                AddParameter(command, "@claimType", DbType.String, UserAccessCodeClaimType, 128);
                AddParameter(command, "@userAccessCode", DbType.Int32, userAccessCode);

                var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
                return new SessionOperationResult(
                    SessionManagementStatus.Succeeded,
                    affectedRows > 0 ? 1 : 0,
                    affectedRows > 0
                        ? "Current session revoked."
                        : "The current session was already revoked or expired."
                );
            },
            static (status, description) => new SessionOperationResult(status, 0, description),
            cancellationToken
        );
    }

    public async Task<SessionOperationResult> RevokeUserSessionsAsync(
        int userAccessCode,
        CancellationToken cancellationToken = default
    )
    {
        if (userAccessCode <= 0)
        {
            return new SessionOperationResult(
                SessionManagementStatus.Failed,
                0,
                "A valid user access code is required."
            );
        }

        return await ExecuteAgainstDurableStoreAsync(
            async (connection, cancellationToken) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                DECLARE @deleted_sessions TABLE ([session_id] nvarchar(64) NOT NULL);

                DELETE [session_token]
                OUTPUT DELETED.[session_id]
                    INTO @deleted_sessions ([session_id])
                FROM [dbo].[{TableName}] AS [session_token]
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
                );

                SELECT COUNT(DISTINCT [session_id])
                FROM @deleted_sessions;
                """;
                AddParameter(command, "@claimType", DbType.String, UserAccessCodeClaimType, 128);
                AddParameter(command, "@userAccessCode", DbType.Int32, userAccessCode);

                var affectedSessions = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken)
                );
                return new SessionOperationResult(
                    SessionManagementStatus.Succeeded,
                    affectedSessions,
                    affectedSessions > 0
                        ? "User sessions revoked."
                        : "No durable sessions were found for the user."
                );
            },
            static (status, description) => new SessionOperationResult(status, 0, description),
            cancellationToken
        );
    }

    public async Task<SessionOperationResult> RevokeAllSessionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await ExecuteAgainstDurableStoreAsync(
            async (connection, cancellationToken) =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                DECLARE @deleted_sessions TABLE ([session_id] nvarchar(64) NOT NULL);

                DELETE [session_token]
                OUTPUT DELETED.[session_id]
                    INTO @deleted_sessions ([session_id])
                FROM [dbo].[{TableName}] AS [session_token];

                SELECT COUNT(DISTINCT [session_id])
                FROM @deleted_sessions;
                """;
                var affectedSessions = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken)
                );
                return new SessionOperationResult(
                    SessionManagementStatus.Succeeded,
                    affectedSessions,
                    affectedSessions > 0
                        ? "All durable sessions revoked."
                        : "No durable sessions were found."
                );
            },
            static (status, description) => new SessionOperationResult(status, 0, description),
            cancellationToken
        );
    }

    private async Task<IReadOnlyList<ActiveSession>> ReadActiveSessionsAsync(
        DbConnection connection,
        int? userAccessCode,
        string? currentSessionId,
        int limit,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (@limit)
                [user_claim].[user_access_code],
                [session_token].[created_at],
                [session_token].[expires_at],
                CONVERT(bit, CASE
                    WHEN @currentSessionId IS NOT NULL
                     AND [session_token].[session_id] = @currentSessionId
                    THEN 1 ELSE 0 END) AS [is_current]
            FROM [dbo].[{TableName}] AS [session_token]
            CROSS APPLY
            (
                SELECT TOP (1)
                    TRY_CONVERT(int, JSON_VALUE([claim].[value], '$.Value')) AS [user_access_code]
                FROM OPENJSON(
                    CASE WHEN ISJSON([session_token].[claims_json]) = 1
                         THEN [session_token].[claims_json]
                         ELSE N'[]' END
                ) AS [claim]
                WHERE JSON_VALUE([claim].[value], '$.Type') = @claimType
            ) AS [user_claim]
            WHERE [session_token].[token_type] = @refreshTokenType
              AND [session_token].[expires_at] > @nowUtc
              AND [user_claim].[user_access_code] IS NOT NULL
              AND (@userAccessCode IS NULL
                   OR [user_claim].[user_access_code] = @userAccessCode)
            ORDER BY [session_token].[created_at] DESC, [session_token].[session_id]
            """;
        AddParameter(command, "@limit", DbType.Int32, limit);
        AddParameter(command, "@claimType", DbType.String, UserAccessCodeClaimType, 128);
        AddParameter(command, "@refreshTokenType", DbType.String, RefreshTokenType, 16);
        AddParameter(command, "@nowUtc", DbType.DateTime2, DateTime.UtcNow);
        AddParameter(command, "@userAccessCode", DbType.Int32, userAccessCode);
        AddParameter(command, "@currentSessionId", DbType.String, currentSessionId, 64);

        var sessions = new List<ActiveSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var userAccessCodeOrdinal = reader.GetOrdinal("user_access_code");
        var createdAtOrdinal = reader.GetOrdinal("created_at");
        var expiresAtOrdinal = reader.GetOrdinal("expires_at");
        var isCurrentOrdinal = reader.GetOrdinal("is_current");
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(userAccessCodeOrdinal))
            {
                continue;
            }

            var createdAt = DateTime.SpecifyKind(
                reader.GetDateTime(createdAtOrdinal),
                DateTimeKind.Utc
            );
            var expiresAt = DateTime.SpecifyKind(
                reader.GetDateTime(expiresAtOrdinal),
                DateTimeKind.Utc
            );
            sessions.Add(
                new ActiveSession(
                    reader.GetInt32(userAccessCodeOrdinal),
                    new DateTimeOffset(createdAt),
                    new DateTimeOffset(expiresAt),
                    SessionTokenConventions.IsRememberedSession(
                        createdAt,
                        expiresAt,
                        _standardRefreshLifetime
                    ),
                    reader.GetBoolean(isCurrentOrdinal)
                )
            );
        }

        return sessions;
    }

    private async Task<string?> FindCurrentSessionIdAsync(
        DbConnection connection,
        int currentUserAccessCode,
        string? currentAccessToken,
        CancellationToken cancellationToken
    )
    {
        if (currentUserAccessCode <= 0 || string.IsNullOrWhiteSpace(currentAccessToken))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT TOP (1) [candidate].[session_id]
            FROM [dbo].[{TableName}] AS [candidate]
            WHERE [candidate].[token_id] = @accessToken
              AND [candidate].[token_type] = @accessTokenType
              AND [candidate].[expires_at] > @nowUtc
              AND EXISTS
              (
                  SELECT 1
                  FROM OPENJSON(
                      CASE WHEN ISJSON([candidate].[claims_json]) = 1
                           THEN [candidate].[claims_json]
                           ELSE N'[]' END
                  ) AS [claim]
                  WHERE JSON_VALUE([claim].[value], '$.Type') = @claimType
                    AND TRY_CONVERT(
                        int,
                        JSON_VALUE([claim].[value], '$.Value')
                    ) = @userAccessCode
              )
            """;
        AddParameter(command, "@accessToken", DbType.String, currentAccessToken, 64);
        AddParameter(command, "@accessTokenType", DbType.String, AccessTokenType, 16);
        AddParameter(command, "@nowUtc", DbType.DateTime2, DateTime.UtcNow);
        AddParameter(command, "@claimType", DbType.String, UserAccessCodeClaimType, 128);
        AddParameter(command, "@userAccessCode", DbType.Int32, currentUserAccessCode);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    private async Task<TResult> ExecuteAgainstDurableStoreAsync<TResult>(
        Func<DbConnection, CancellationToken, Task<TResult>> operation,
        Func<SessionManagementStatus, string, TResult> unavailable,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var connectionScope = await OpenConnectionAsync(cancellationToken);
            if (!await ProbeAsync(connectionScope.Connection, cancellationToken))
            {
                return unavailable(
                    SessionManagementStatus.Unavailable,
                    "Durable session management is unavailable because dbo.fis_session_tokens is not available."
                );
            }

            return await operation(connectionScope.Connection, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SqlException exception) when (IsMissingObject(exception))
        {
            _logger.LogWarning(
                exception,
                "Durable session management table became unavailable during an operation."
            );
            return unavailable(
                SessionManagementStatus.Unavailable,
                "Durable session management is unavailable."
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Durable session management operation failed.");
            return unavailable(
                SessionManagementStatus.Failed,
                "Durable session management failed."
            );
        }
    }

    private async Task<ConnectionScope> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return new ConnectionScope(connection, shouldClose);
    }

    private static async Task<bool> ProbeAsync(
        DbConnection connection,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [columns].[COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS] AS [columns]
            INNER JOIN [INFORMATION_SCHEMA].[TABLES] AS [tables]
                ON [tables].[TABLE_SCHEMA] = [columns].[TABLE_SCHEMA]
               AND [tables].[TABLE_NAME] = [columns].[TABLE_NAME]
            WHERE [columns].[TABLE_SCHEMA] = @schema
              AND [columns].[TABLE_NAME] = @table
              AND [tables].[TABLE_TYPE] = 'BASE TABLE'
            """;
        AddParameter(command, "@schema", DbType.String, SchemaName, 128);
        AddParameter(command, "@table", DbType.String, TableName, 128);

        var availableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            availableColumns.Add(reader.GetString(0));
        }

        return RequiredColumns.All(availableColumns.Contains);
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
        {
            parameter.Size = size.Value;
        }
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static bool IsMissingObject(SqlException exception) => exception.Number is 208 or 3701;

    private sealed class ConnectionScope : IAsyncDisposable
    {
        public ConnectionScope(DbConnection connection, bool shouldClose)
        {
            Connection = connection;
            ShouldClose = shouldClose;
        }

        public DbConnection Connection { get; }
        private bool ShouldClose { get; }

        public async ValueTask DisposeAsync()
        {
            if (ShouldClose)
            {
                await Connection.CloseAsync();
            }
        }
    }
}
