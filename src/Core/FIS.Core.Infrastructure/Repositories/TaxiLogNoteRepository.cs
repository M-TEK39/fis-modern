using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads taxi-log notes from the original two-column table or the expanded table
/// without statically requiring modern audit columns.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class TaxiLogNoteRepository : ITaxiLogNoteRepository
{
    private const string TableName = "taxi_log_notes";
    private static readonly string[] RequiredColumns =
    [
        "taxi_log_note_code",
        "taxi_log_note_description",
    ];
    private static readonly string[] AuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public TaxiLogNoteRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<TaxiLogNote>> GetAllAsync()
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT [taxi_log_note_code], [taxi_log_note_description],
                   {string.Join(
                ", ",
                AuditColumns.Select(column => GetProjection(columns, column))
            )}
            FROM [dbo].[{TableName}]
            WHERE {GetActiveFilter(columns)}
            ORDER BY [taxi_log_note_description], [taxi_log_note_code]
            """;

        var results = new List<TaxiLogNote>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(
                new TaxiLogNote
                {
                    taxi_log_note_code = ReadInt16(reader, "taxi_log_note_code") ?? 0,
                    taxi_log_note_description = ReadString(reader, "taxi_log_note_description"),
                    date_created = ReadDateTime(reader, "date_created") ?? default,
                    date_updated = ReadDateTime(reader, "date_updated"),
                    created_by_user_code = ReadInt32(reader, "created_by_user_code"),
                    modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
                    is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
                }
            );
        }

        return results;
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME], [DATA_TYPE]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));
        foreach (var required in RequiredColumns.Where(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException(
                $"The required {TableName} compatibility column {required} is not available."
            );
        return columns;
    }

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        column switch
        {
            "taxi_log_note_code" => "smallint",
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "varchar(1)",
        };

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed class ConnectionScope : IAsyncDisposable
    {
        public ConnectionScope(DbConnection connection, bool shouldClose)
        {
            Connection = connection;
            _shouldClose = shouldClose;
        }

        public DbConnection Connection { get; }
        private readonly bool _shouldClose;

        public async ValueTask DisposeAsync()
        {
            if (_shouldClose)
                await Connection.CloseAsync();
        }
    }
}
