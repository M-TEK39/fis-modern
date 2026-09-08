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
/// Persists white logs against the original smallint-keyed table and the expanded
/// table without requiring modern audit columns on the client database.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class TaxiWhiteLogRepository : ITaxiWhiteLogRepository
{
    private const string TableName = "Taxi_white_log";

    private static readonly string[] BusinessColumns =
    [
        "vmf_code",
        "start_odo",
        "end_odo",
        "start_date",
        "end_date",
        "driver",
        "user_access_code",
    ];

    private static readonly string[] RequiredColumns =
    [
        "Log_id",
        "vmf_code",
        "start_odo",
        "end_odo",
        "start_date",
        "end_date",
        "driver",
        "user_access_code",
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

    public TaxiWhiteLogRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TaxiWhiteLog?> GetByIdAsync(int logId) =>
        (
            await QueryAsync(
                "whiteLog.[Log_id] = @logId",
                command => AddParameter(command, "@logId", DbType.Int32, logId)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<TaxiWhiteLog>> GetAllAsync() => await QueryAsync();

    public async Task<IEnumerable<TaxiWhiteLog>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "whiteLog.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<TaxiWhiteLog> CreateAsync(TaxiWhiteLog log, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(log);
        ValidateLog(log);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        log.driver ??= string.Empty;
        log.user_access_code =
            log.user_access_code == 0 ? ToLegacyShortUserId(currentUserId) : log.user_access_code;
        log.date_created = DateTime.UtcNow;
        log.created_by_user_code = UserIdOrNull(currentUserId);
        log.is_deleted = false;
        var values = BuildValues(log, columns, includeAudit: true);
        log.Log_id = await ExecuteInsertAsync(values);
        return await GetByIdAsync(log.Log_id)
            ?? throw new InvalidOperationException(
                $"Taxi white log {log.Log_id} could not be read after creation."
            );
    }

    public async Task DeleteAsync(int logId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        if (columns.ContainsKey("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            AddParameter(command, "@isDeleted", DbType.Boolean, true);
            AddOptionalAssignment(
                assignments,
                command,
                columns,
                "date_updated",
                "@dateUpdated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddOptionalAssignment(
                assignments,
                command,
                columns,
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                UserIdOrNull(currentUserId)
            );
            command.CommandText =
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [Log_id] = @logId AND {GetActiveFilter(columns)}";
        }
        else
        {
            command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [Log_id] = @logId";
        }

        AddParameter(command, "@logId", DbType.Int32, logId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<TaxiWhiteLog>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var conditions = new List<string> { GetActiveFilter(columns, "whiteLog") };
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                BusinessColumns.Concat(AuditColumns).Select(column =>
                    GetProjection(columns, column, "whiteLog")
                )
            )},
                   whiteLog.[Log_id] AS [Log_id]
            FROM [dbo].[{TableName}] whiteLog
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY whiteLog.[Log_id] DESC
            """;
        configure?.Invoke(command);

        var results = new List<TaxiWhiteLog>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapLog(reader));
        return results;
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(
        IReadOnlyCollection<string> requiredColumns
    )
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
        foreach (var required in requiredColumns.Where(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException(
                $"The required {TableName} compatibility column {required} is not available."
            );
        return columns;
    }

    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{TableName}] ({string.Join(
                ", ",
                values.Select(value => $"[{value.Column}]")
            )})
            OUTPUT INSERTED.[Log_id]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static List<WriteValue> BuildValues(
        TaxiWhiteLog log,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        bool includeAudit
    )
    {
        var values = new List<WriteValue>();
        AddRequiredValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, log.vmf_code);
        AddRequiredValue(values, columns, "start_odo", "@startOdo", DbType.Int64, log.start_odo);
        AddRequiredValue(values, columns, "end_odo", "@endOdo", DbType.Int64, log.end_odo);
        AddRequiredValue(
            values,
            columns,
            "start_date",
            "@startDate",
            DbType.DateTime,
            log.start_date
        );
        AddRequiredValue(values, columns, "end_date", "@endDate", DbType.DateTime, log.end_date);
        AddRequiredValue(
            values,
            columns,
            "driver",
            "@driver",
            DbType.String,
            log.driver?.Trim() ?? string.Empty
        );
        AddRequiredValue(
            values,
            columns,
            "user_access_code",
            "@userAccessCode",
            DbType.Int16,
            log.user_access_code
        );
        if (includeAudit)
        {
            AddValue(
                values,
                columns,
                "date_created",
                "@dateCreated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddValue(
                values,
                columns,
                "created_by_user_code",
                "@createdByUserCode",
                DbType.Int32,
                log.created_by_user_code
            );
            AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        }
        return values;
    }

    private static void ValidateLog(TaxiWhiteLog log)
    {
        if (log.vmf_code <= 0)
            throw new ArgumentException("Vehicle code is required.", nameof(log));
        if (log.end_odo <= log.start_odo)
            throw new ArgumentException(
                "End odometer must be greater than start odometer.",
                nameof(log)
            );
        if (log.end_date < log.start_date)
            throw new ArgumentException("End date must be on or after start date.", nameof(log));
    }

    private static TaxiWhiteLog MapLog(DbDataReader reader) =>
        new()
        {
            Log_id = ReadInt32(reader, "Log_id") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            start_odo = ReadInt64(reader, "start_odo") ?? 0,
            end_odo = ReadInt64(reader, "end_odo") ?? 0,
            start_date = ReadDateTime(reader, "start_date") ?? default,
            end_date = ReadDateTime(reader, "end_date") ?? default,
            driver = ReadString(reader, "driver"),
            user_access_code = ReadInt16(reader, "user_access_code") ?? 0,
            date_created = ReadDateTime(reader, "date_created") ?? default,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private static string GetActiveFilter(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string alias = ""
    ) =>
        columns.ContainsKey("is_deleted")
            ? $"ISNULL({(string.IsNullOrWhiteSpace(alias) ? "" : $"{alias}.")}[is_deleted], 0) = 0"
            : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string alias
    ) =>
        columns.ContainsKey(column)
            ? $"{alias}.[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        column switch
        {
            "Log_id" or "vmf_code" or "user_access_code" => "int",
            "start_odo" or "end_odo" => "bigint",
            "start_date" or "end_date" or "date_created" or "date_updated" => "datetime2",
            "is_deleted" => "bit",
            "created_by_user_code" or "modified_by_user_code" => "int",
            _ => "varchar(1)",
        };

    private static void AddRequiredValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            throw new InvalidOperationException(
                $"The required compatibility column {column} is not available."
            );
        values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.ContainsKey(column) && value is not null)
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddOptionalAssignment(
        List<string> assignments,
        DbCommand command,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        AddParameter(command, parameter, type, value);
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.Type, value.Value);
    }

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

    private static long? ReadInt64(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
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

    private static short ToLegacyShortUserId(int userId)
    {
        if (userId > short.MaxValue || userId < short.MinValue)
            throw new InvalidOperationException(
                $"User id {userId} cannot be stored in legacy taxi white log user_access_code column."
            );
        return (short)userId;
    }

    private static int? UserIdOrNull(int currentUserId) => currentUserId > 0 ? currentUserId : null;

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

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
