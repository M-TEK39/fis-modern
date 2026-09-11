using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Provides a per-user inbox over the legacy <c>dbo.user_message</c> table.
/// The original client schema contains only the message fields; audit and
/// soft-delete columns are projected only when an expanded database provides
/// them.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Command text uses only fixed statements and allowlisted legacy column names."
)]
public sealed class UserMessageCompatibilityService
{
    private const string SchemaName = "dbo";
    private const string TableName = "user_message";

    private readonly FisDbContext _context;

    public UserMessageCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    public async Task<UserMessageInbox> GetInboxAsync(
        short userAccessCode,
        int limit,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var items = await ReadAsync(
                connection,
                columns,
                userAccessCode,
                limit,
                cancellationToken
            );
            var unreadCount = await ReadUnreadCountAsync(
                connection,
                columns,
                userAccessCode,
                cancellationToken
            );
            return new UserMessageInbox(items, unreadCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Marks a message read only when it belongs to the authenticated user.
    /// A previously read message remains a successful, idempotent update.
    /// </summary>
    public async Task<UserMessageRecord?> MarkReadAsync(
        int userMessageCode,
        short userAccessCode,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            var assignments = new List<string> { "[message_read] = @readValue" };
            if (columns.Contains("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
            }

            command.CommandText = $"""
                UPDATE [{SchemaName}].[{TableName}]
                SET {string.Join(", ", assignments)}
                WHERE [user_message_code] = @messageCode
                  AND [user_access_code] = @userAccessCode
                  AND {GetActiveFilter(columns)};
                SELECT @@ROWCOUNT;
                """;
            AddParameter(command, "@messageCode", DbType.Int32, userMessageCode);
            AddParameter(command, "@userAccessCode", DbType.Int16, userAccessCode);
            AddParameter(command, "@readValue", DbType.StringFixedLength, "Y", size: 1);
            if (columns.Contains("date_updated"))
            {
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }

            var affected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (affected == 0)
            {
                return null;
            }

            return await ReadByIdAsync(
                connection,
                columns,
                userMessageCode,
                userAccessCode,
                cancellationToken
            );
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<UserMessageRecord>> ReadAsync(
        DbConnection connection,
        IReadOnlySet<string> columns,
        short userAccessCode,
        int limit,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT TOP (@limit)
                [user_message_code],
                [message],
                CASE
                    WHEN UPPER(LTRIM(RTRIM(ISNULL([message_read], '')))) IN ('Y', '1', 'TRUE')
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS [is_read],
                {GetOptionalProjection(columns, "date_created", "datetime2")}
            FROM [{SchemaName}].[{TableName}]
            WHERE [user_access_code] = @userAccessCode
              AND {GetActiveFilter(columns)}
            ORDER BY [user_message_code] DESC;
            """;
        AddParameter(command, "@limit", DbType.Int32, limit);
        AddParameter(command, "@userAccessCode", DbType.Int16, userAccessCode);

        var records = new List<UserMessageRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(
                new UserMessageRecord(
                    reader.GetInt32(reader.GetOrdinal("user_message_code")),
                    ReadString(reader, "message"),
                    reader.GetBoolean(reader.GetOrdinal("is_read")),
                    ReadDateTime(reader, "date_created")
                )
            );
        }

        return records;
    }

    private async Task<UserMessageRecord?> ReadByIdAsync(
        DbConnection connection,
        IReadOnlySet<string> columns,
        int userMessageCode,
        short userAccessCode,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT
                [user_message_code],
                [message],
                CASE
                    WHEN UPPER(LTRIM(RTRIM(ISNULL([message_read], '')))) IN ('Y', '1', 'TRUE')
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS [is_read],
                {GetOptionalProjection(columns, "date_created", "datetime2")}
            FROM [{SchemaName}].[{TableName}]
            WHERE [user_message_code] = @messageCode
              AND [user_access_code] = @userAccessCode
              AND {GetActiveFilter(columns)};
            """;
        AddParameter(command, "@messageCode", DbType.Int32, userMessageCode);
        AddParameter(command, "@userAccessCode", DbType.Int16, userAccessCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UserMessageRecord(
            reader.GetInt32(reader.GetOrdinal("user_message_code")),
            ReadString(reader, "message"),
            reader.GetBoolean(reader.GetOrdinal("is_read")),
            ReadDateTime(reader, "date_created")
        );
    }

    private async Task<int> ReadUnreadCountAsync(
        DbConnection connection,
        IReadOnlySet<string> columns,
        short userAccessCode,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT COUNT_BIG(*)
            FROM [{SchemaName}].[{TableName}]
            WHERE [user_access_code] = @userAccessCode
              AND {GetActiveFilter(columns)}
              AND {GetUnreadFilter()};
            """;
        AddParameter(command, "@userAccessCode", DbType.Int16, userAccessCode);

        var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                  AND [COLUMN_NAME] IN
                      ('user_message_code', 'user_access_code', 'message', 'message_read',
                       'date_created', 'date_updated', 'is_deleted')
                """;
            AddParameter(command, "@schema", DbType.String, SchemaName);
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            var requiredColumns = new[]
            {
                "user_message_code",
                "user_access_code",
                "message",
                "message_read",
            };
            if (requiredColumns.Any(column => !columns.Contains(column)))
            {
                throw new UserMessageCompatibilityUnavailableException(
                    "The legacy per-user notification inbox is not available in this database."
                );
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetUnreadFilter() =>
        "UPPER(LTRIM(RTRIM(ISNULL([message_read], '')))) NOT IN ('Y', '1', 'TRUE')";

    private static string GetOptionalProjection(
        IReadOnlySet<string> columns,
        string column,
        string sqlType
    ) =>
        columns.Contains(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {sqlType}) AS [{column}]";

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
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
}

public sealed class UserMessageCompatibilityUnavailableException : Exception
{
    public UserMessageCompatibilityUnavailableException(string message)
        : base(message) { }
}

public sealed record UserMessageInbox(IReadOnlyList<UserMessageRecord> Items, int UnreadCount);

public sealed record UserMessageRecord(int Id, string? Message, bool IsRead, DateTime? CreatedAt);
