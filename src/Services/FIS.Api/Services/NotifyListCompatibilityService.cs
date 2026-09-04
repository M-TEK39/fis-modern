using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Reads and writes the legacy Notify_List business fields while treating the
/// expanded audit columns as optional. The client database still has the
/// original three-column table, while the modern database may also have the
/// shared audit columns.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Command text is assembled only from fixed statements and allowlisted schema identifiers.")]
public sealed class NotifyListCompatibilityService
{
    private const string TableName = "Notify_List";

    private readonly FisDbContext _context;

    public NotifyListCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<NotifyListRecord>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        return await ReadAsync(columns, search, code: null, cancellationToken);
    }

    public async Task<NotifyListRecord?> GetByIdAsync(
        int code,
        CancellationToken cancellationToken = default)
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var records = await ReadAsync(columns, search: null, code: code, cancellationToken);
        return records.Count == 0 ? null : records[0];
    }

    public async Task<NotifyListRecord?> CreateAsync(
        string? description,
        string? email,
        int? userCode,
        CancellationToken cancellationToken = default)
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var insertColumns = new List<string>
        {
            "[Notify_list_desc]",
            "[Notify_email1]"
        };
        var values = new List<string> { "@description", "@email" };

        if (columns.Contains("date_created"))
        {
            insertColumns.Add("[date_created]");
            values.Add("@dateCreated");
        }

        if (columns.Contains("created_by_user_code"))
        {
            insertColumns.Add("[created_by_user_code]");
            values.Add("@createdByUserCode");
        }

        if (columns.Contains("is_deleted"))
        {
            insertColumns.Add("[is_deleted]");
            values.Add("0");
        }

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
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(", ", insertColumns)})
                OUTPUT INSERTED.[Notify_list_code]
                VALUES ({string.Join(", ", values)})
                """;
            AddParameter(command, "@description", DbType.String, description, 255);
            AddParameter(command, "@email", DbType.String, email, 255);

            if (columns.Contains("date_created"))
            {
                AddParameter(command, "@dateCreated", DbType.DateTime2, DateTime.UtcNow);
            }

            if (columns.Contains("created_by_user_code"))
            {
                AddParameter(command, "@createdByUserCode", DbType.Int32, userCode);
            }

            var newCode = await command.ExecuteScalarAsync(cancellationToken);
            if (newCode is null or DBNull)
            {
                return null;
            }

            return await ReadByIdAsync(connection, columns, Convert.ToInt32(newCode), cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<NotifyListRecord?> UpdateAsync(
        int code,
        string? description,
        string? email,
        int? userCode,
        CancellationToken cancellationToken = default)
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var assignments = new List<string>
        {
            "[Notify_list_desc] = @description",
            "[Notify_email1] = @email"
        };

        if (columns.Contains("date_updated"))
        {
            assignments.Add("[date_updated] = @dateUpdated");
        }

        if (columns.Contains("modified_by_user_code"))
        {
            assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
        }

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
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", assignments)}
                WHERE [Notify_list_code] = @code
                AND {GetActiveFilter(columns)};
                SELECT @@ROWCOUNT;
                """;
            AddParameter(command, "@code", DbType.Int32, code);
            AddParameter(command, "@description", DbType.String, description, 255);
            AddParameter(command, "@email", DbType.String, email, 255);

            if (columns.Contains("date_updated"))
            {
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }

            if (columns.Contains("modified_by_user_code"))
            {
                AddParameter(command, "@modifiedByUserCode", DbType.Int32, userCode);
            }

            var affected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return affected == 0
                ? null
                : await ReadByIdAsync(connection, columns, code, cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> DeleteAsync(int code, int? userCode, CancellationToken cancellationToken = default)
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var assignments = new List<string>();
        var isSoftDelete = columns.Contains("is_deleted");

        if (isSoftDelete)
        {
            assignments.Add("[is_deleted] = 1");
            if (columns.Contains("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
            }

            if (columns.Contains("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
            }
        }

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
            command.CommandText = isSoftDelete
                ? $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [Notify_list_code] = @code
                    AND {GetActiveFilter(columns)};
                    SELECT @@ROWCOUNT;
                    """
                : $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [Notify_list_code] = @code;
                    SELECT @@ROWCOUNT;
                    """;
            AddParameter(command, "@code", DbType.Int32, code);

            if (columns.Contains("date_updated"))
            {
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }

            if (columns.Contains("modified_by_user_code"))
            {
                AddParameter(command, "@modifiedByUserCode", DbType.Int32, userCode);
            }

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<NotifyListRecord>> ReadAsync(
        IReadOnlySet<string> columns,
        string? search,
        int? code,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await ReadManyAsync(connection, columns, search, code, cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<NotifyListRecord?> ReadByIdAsync(
        DbConnection connection,
        IReadOnlySet<string> columns,
        int code,
        CancellationToken cancellationToken)
    {
        var records = await ReadManyAsync(connection, columns, search: null, code: code, cancellationToken);
        return records.Count == 0 ? null : records[0];
    }

    private async Task<IReadOnlyList<NotifyListRecord>> ReadManyAsync(
        DbConnection connection,
        IReadOnlySet<string> columns,
        string? search,
        int? code,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        var predicates = new List<string> { GetActiveFilter(columns).Trim() };
        if (!string.IsNullOrWhiteSpace(search))
        {
            predicates.Add("([Notify_list_desc] LIKE '%' + @search + '%' OR [Notify_email1] LIKE '%' + @search + '%')");
        }

        if (code.HasValue)
        {
            predicates.Add("[Notify_list_code] = @code");
        }

        command.CommandText = $"""
            SELECT
                [Notify_list_code],
                [Notify_list_desc],
                [Notify_email1],
                {GetOptionalProjection(columns, "date_created", "datetime2")},
                {GetOptionalProjection(columns, "date_updated", "datetime2")}
            FROM [dbo].[{TableName}]
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY [Notify_list_desc], [Notify_list_code]
            """;

        if (!string.IsNullOrWhiteSpace(search))
        {
            AddParameter(command, "@search", DbType.String, search.Trim(), 255);
        }

        if (code.HasValue)
        {
            AddParameter(command, "@code", DbType.Int32, code.Value);
        }

        var records = new List<NotifyListRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new NotifyListRecord(
                reader.GetInt32(reader.GetOrdinal("Notify_list_code")),
                ReadString(reader, "Notify_list_desc"),
                ReadString(reader, "Notify_email1"),
                ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
                ReadDateTime(reader, "date_updated")));
        }

        return records;
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(CancellationToken cancellationToken)
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
                      ('Notify_list_code', 'Notify_list_desc', 'Notify_email1',
                       'date_created', 'date_updated', 'created_by_user_code',
                       'modified_by_user_code', 'is_deleted')
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            if (!columns.Contains("Notify_list_code")
                || !columns.Contains("Notify_list_desc")
                || !columns.Contains("Notify_email1"))
            {
                throw new InvalidOperationException(
                    "The required Notify_List legacy columns are not available.");
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

    private static string GetActiveFilter(IReadOnlySet<string> columns)
        => columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetOptionalProjection(
        IReadOnlySet<string> columns,
        string column,
        string sqlType)
        => columns.Contains(column)
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
        int? size = null)
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

public sealed record NotifyListRecord(
    int Notify_list_code,
    string? Notify_list_desc,
    string? Notify_email1,
    DateTime date_created,
    DateTime? date_updated);
