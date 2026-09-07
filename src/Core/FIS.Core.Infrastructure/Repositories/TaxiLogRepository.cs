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
/// Persists taxi logs against both the original client table and the expanded
/// table. Optional audit and accounting columns are negotiated at runtime.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters.")]
public sealed class TaxiLogRepository : ITaxiLogRepository
{
    private const string TableName = "Taxi_logs";

    private static readonly string[] BusinessColumns =
    [
        "request_id", "rek_num", "user_start_odo", "user_end_odo", "user_start_date", "user_end_date",
        "user_start_time", "user_end_time", "driver_start_odo", "driver_end_odo", "driver_start_date",
        "driver_end_date", "driver_start_time", "driver_end_time", "userid", "enter_date", "invoiced_date",
        "division", "distance", "days", "hours", "batch_num", "bas_batch", "prev_batch", "changed",
        "quoted_tariff", "journal_detail_code", "parent_taxi_log_code", "taxi_log_note_code"
    ];

    private static readonly string[] RequiredColumns = ["log_id", "rek_num", "userid", "enter_date"];

    private static readonly string[] AuditColumns =
    ["date_created", "date_updated", "created_by_user_code", "modified_by_user_code", "is_deleted"];

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "user_start_date", "user_end_date", "driver_start_date", "driver_end_date", "enter_date", "invoiced_date",
        "date_created", "date_updated"
    };

    private static readonly HashSet<string> TimeColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "user_start_time", "user_end_time", "driver_start_time", "driver_end_time"
    };

    private readonly FisDbContext _context;

    public TaxiLogRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TaxiLog?> GetByIdAsync(int logId)
        => (await QueryAsync(
            "log.[log_id] = @logId",
            command => AddParameter(command, "@logId", DbType.Int32, logId)))
            .SingleOrDefault();

    public async Task<TaxiLog?> GetLatestByRequisitionAsync(string rekNum)
    {
        var normalized = NormalizeKey(rekNum);
        return (await QueryAsync(
            "UPPER(RTRIM(log.[rek_num])) = @rekNum AND NOT EXISTS (" +
            "SELECT 1 FROM [dbo].[Taxi_logs] child WHERE child.[parent_taxi_log_code] = log.[log_id] AND {CHILD_ACTIVE})",
            command => AddParameter(command, "@rekNum", DbType.String, normalized)))
            .OrderByDescending(log => log.log_id)
            .FirstOrDefault();
    }

    public async Task<IEnumerable<TaxiLog>> GetAllAsync()
        => await QueryAsync();

    public async Task<TaxiLog> CreateAsync(TaxiLog log, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(log);
        ValidateLog(log);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        var now = DateTime.UtcNow;
        log.date_created = now;
        log.created_by_user_code = UserIdOrNull(currentUserId);
        log.is_deleted = false;
        var values = BuildValues(log, columns, currentUserId, includeAudit: true);
        log.log_id = await ExecuteInsertAsync(values);
        return await GetByIdAsync(log.log_id)
            ?? throw new InvalidOperationException($"Taxi log {log.log_id} could not be read after creation.");
    }

    public async Task<TaxiLog> UpdateAsync(TaxiLog log, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(log);
        var existing = await GetByIdAsync(log.log_id)
            ?? throw new InvalidOperationException($"Taxi log {log.log_id} not found");
        MergeLog(log, existing);
        ValidateLog(log);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        var values = BuildValues(log, columns, currentUserId, includeAudit: false);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
        AddValue(values, columns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, UserIdOrNull(currentUserId));
        await ExecuteUpdateAsync(log.log_id, values, columns);
        return await GetByIdAsync(log.log_id)
            ?? throw new InvalidOperationException($"Taxi log {log.log_id} could not be read after update.");
    }

    private async Task<List<TaxiLog>> QueryAsync(string? predicate = null, Action<DbCommand>? configure = null)
    {
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var effectivePredicate = predicate?.Replace("{CHILD_ACTIVE}", GetActiveFilter(columns, "child"), StringComparison.Ordinal)
            ?? "1 = 1";
        var conditions = new List<string> { GetActiveFilter(columns, "log") };
        if (!string.IsNullOrWhiteSpace(effectivePredicate)) conditions.Add($"({effectivePredicate})");

        command.CommandText = $"""
            SELECT {string.Join(", ", BusinessColumns.Concat(AuditColumns).Select(column => GetProjection(columns, column, "log")))},
                   log.[log_id] AS [log_id]
            FROM [dbo].[{TableName}] log
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY log.[log_id] DESC
            """;
        configure?.Invoke(command);

        var results = new List<TaxiLog>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(MapLog(reader));
        return results;
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(IReadOnlyCollection<string> requiredColumns)
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
        while (await reader.ReadAsync()) columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));
        foreach (var required in requiredColumns.Where(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException($"The required {TableName} compatibility column {required} is not available.");
        return columns;
    }

    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
            OUTPUT INSERTED.[log_id]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteUpdateAsync(int logId, IReadOnlyList<WriteValue> values, IReadOnlyDictionary<string, ColumnInfo> columns)
    {
        if (values.Count == 0) return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [dbo].[{TableName}]
            SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
            WHERE [log_id] = @logId AND {GetActiveFilter(columns)}
            """;
        AddParameters(command, values);
        AddParameter(command, "@logId", DbType.Int32, logId);
        await command.ExecuteNonQueryAsync();
    }

    private static List<WriteValue> BuildValues(TaxiLog log, IReadOnlyDictionary<string, ColumnInfo> columns, int currentUserId, bool includeAudit)
    {
        var values = new List<WriteValue>();
        AddRequiredValue(values, columns, "rek_num", "@rekNum", DbType.String, log.rek_num?.Trim());
        AddValue(values, columns, "request_id", "@requestId", DbType.Int32, log.request_id);
        AddValue(values, columns, "user_start_odo", "@userStartOdo", DbType.Decimal, log.user_start_odo);
        AddValue(values, columns, "user_end_odo", "@userEndOdo", DbType.Decimal, log.user_end_odo);
        AddDateTimeValue(values, columns, "user_start_date", "@userStartDate", log.user_start_date);
        AddDateTimeValue(values, columns, "user_end_date", "@userEndDate", log.user_end_date);
        AddDateTimeValue(values, columns, "user_start_time", "@userStartTime", log.user_start_time);
        AddDateTimeValue(values, columns, "user_end_time", "@userEndTime", log.user_end_time);
        AddValue(values, columns, "driver_start_odo", "@driverStartOdo", DbType.Decimal, log.driver_start_odo);
        AddValue(values, columns, "driver_end_odo", "@driverEndOdo", DbType.Decimal, log.driver_end_odo);
        AddDateTimeValue(values, columns, "driver_start_date", "@driverStartDate", log.driver_start_date);
        AddDateTimeValue(values, columns, "driver_end_date", "@driverEndDate", log.driver_end_date);
        AddDateTimeValue(values, columns, "driver_start_time", "@driverStartTime", log.driver_start_time);
        AddDateTimeValue(values, columns, "driver_end_time", "@driverEndTime", log.driver_end_time);
        var userId = log.userid == 0 ? ToLegacyShortUserId(currentUserId) : log.userid;
        AddRequiredValue(values, columns, "userid", "@userid", DbType.Int16, userId);
        AddRequiredValue(values, columns, "enter_date", "@enterDate", DbType.DateTime, log.enter_date == default ? DateTime.Now : log.enter_date);
        AddDateTimeValue(values, columns, "invoiced_date", "@invoicedDate", log.invoiced_date);
        AddValue(values, columns, "division", "@division", DbType.String, log.division);
        AddValue(values, columns, "distance", "@distance", DbType.Decimal, log.distance);
        AddValue(values, columns, "days", "@days", DbType.Int16, log.days);
        AddValue(values, columns, "hours", "@hours", DbType.Double, log.hours);
        AddValue(values, columns, "batch_num", "@batchNum", DbType.Int32, log.batch_num);
        AddValue(values, columns, "bas_batch", "@basBatch", DbType.Int32, log.bas_batch);
        AddValue(values, columns, "prev_batch", "@prevBatch", DbType.Int32, log.prev_batch);
        AddValue(values, columns, "changed", "@changed", DbType.String, log.changed);
        AddValue(values, columns, "quoted_tariff", "@quotedTariff", DbType.Single, log.quoted_tariff);
        AddValue(values, columns, "journal_detail_code", "@journalDetailCode", DbType.Guid, log.journal_detail_code);
        AddValue(values, columns, "parent_taxi_log_code", "@parentTaxiLogCode", DbType.Int32, log.parent_taxi_log_code);
        AddValue(values, columns, "taxi_log_note_code", "@taxiLogNoteCode", DbType.Int16, log.taxi_log_note_code);

        if (includeAudit)
        {
            AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, DateTime.UtcNow);
            AddValue(values, columns, "created_by_user_code", "@createdByUserCode", DbType.Int32, UserIdOrNull(currentUserId));
            AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        }

        return values;
    }

    private static void MergeLog(TaxiLog target, TaxiLog source)
    {
        target.rek_num = string.IsNullOrWhiteSpace(target.rek_num) ? source.rek_num : target.rek_num;
        target.request_id ??= source.request_id;
        target.user_start_odo ??= source.user_start_odo;
        target.user_end_odo ??= source.user_end_odo;
        target.user_start_date ??= source.user_start_date;
        target.user_end_date ??= source.user_end_date;
        target.user_start_time ??= source.user_start_time;
        target.user_end_time ??= source.user_end_time;
        target.driver_start_odo ??= source.driver_start_odo;
        target.driver_end_odo ??= source.driver_end_odo;
        target.driver_start_date ??= source.driver_start_date;
        target.driver_end_date ??= source.driver_end_date;
        target.driver_start_time ??= source.driver_start_time;
        target.driver_end_time ??= source.driver_end_time;
        if (target.userid == 0) target.userid = source.userid;
        if (target.enter_date == default) target.enter_date = source.enter_date;
        target.invoiced_date ??= source.invoiced_date;
        target.division ??= source.division;
        target.distance ??= source.distance;
        target.days ??= source.days;
        target.hours ??= source.hours;
        target.batch_num ??= source.batch_num;
        target.bas_batch ??= source.bas_batch;
        target.prev_batch ??= source.prev_batch;
        target.changed ??= source.changed;
        target.quoted_tariff ??= source.quoted_tariff;
        target.journal_detail_code ??= source.journal_detail_code;
        target.parent_taxi_log_code ??= source.parent_taxi_log_code;
        target.taxi_log_note_code ??= source.taxi_log_note_code;
    }

    private static void ValidateLog(TaxiLog log)
    {
        if (string.IsNullOrWhiteSpace(log.rek_num)) throw new ArgumentException("Requisition number is required.", nameof(log));
    }

    private static TaxiLog MapLog(DbDataReader reader)
        => new()
        {
            log_id = ReadInt32(reader, "log_id") ?? 0,
            request_id = ReadInt32(reader, "request_id"),
            rek_num = ReadString(reader, "rek_num"),
            user_start_odo = ReadDecimal(reader, "user_start_odo"),
            user_end_odo = ReadDecimal(reader, "user_end_odo"),
            user_start_date = ReadDateTime(reader, "user_start_date"),
            user_end_date = ReadDateTime(reader, "user_end_date"),
            user_start_time = ReadDateTime(reader, "user_start_time"),
            user_end_time = ReadDateTime(reader, "user_end_time"),
            driver_start_odo = ReadDecimal(reader, "driver_start_odo"),
            driver_end_odo = ReadDecimal(reader, "driver_end_odo"),
            driver_start_date = ReadDateTime(reader, "driver_start_date"),
            driver_end_date = ReadDateTime(reader, "driver_end_date"),
            driver_start_time = ReadDateTime(reader, "driver_start_time"),
            driver_end_time = ReadDateTime(reader, "driver_end_time"),
            userid = ReadInt16(reader, "userid") ?? 0,
            enter_date = ReadDateTime(reader, "enter_date") ?? default,
            invoiced_date = ReadDateTime(reader, "invoiced_date"),
            division = ReadString(reader, "division"),
            distance = ReadDecimal(reader, "distance"),
            days = ReadInt16(reader, "days"),
            hours = ReadDouble(reader, "hours"),
            batch_num = ReadInt32(reader, "batch_num"),
            bas_batch = ReadInt32(reader, "bas_batch"),
            prev_batch = ReadInt32(reader, "prev_batch"),
            changed = ReadString(reader, "changed"),
            quoted_tariff = ReadSingle(reader, "quoted_tariff"),
            journal_detail_code = ReadGuid(reader, "journal_detail_code"),
            parent_taxi_log_code = ReadInt32(reader, "parent_taxi_log_code"),
            taxi_log_note_code = ReadInt16(reader, "taxi_log_note_code"),
            date_created = ReadDateTime(reader, "date_created") ?? default,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false
        };

    private static void AddDateTimeValue(ICollection<WriteValue> values, IReadOnlyDictionary<string, ColumnInfo> columns, string column, string parameter, DateTime? value)
    {
        if (!columns.ContainsKey(column) || value is null) return;
        if (TimeColumns.Contains(column) && columns[column].DataType.Equals("time", StringComparison.OrdinalIgnoreCase))
            values.Add(new WriteValue(column, parameter, DbType.Time, value.Value.TimeOfDay));
        else
            values.Add(new WriteValue(column, parameter, DbType.DateTime2, value.Value));
    }

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns, string alias = "")
        => columns.ContainsKey("is_deleted")
            ? $"ISNULL({(string.IsNullOrWhiteSpace(alias) ? "" : $"{alias}.")}[is_deleted], 0) = 0"
            : "1 = 1";

    private static string GetProjection(IReadOnlyDictionary<string, ColumnInfo> columns, string column, string alias)
        => columns.ContainsKey(column)
            ? $"{alias}.[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column)
        => TimeColumns.Contains(column) ? "time" : DateColumns.Contains(column) ? "datetime2" : column switch
        {
            "log_id" or "request_id" or "batch_num" or "bas_batch" or "prev_batch" or "parent_taxi_log_code" => "int",
            "userid" or "days" or "taxi_log_note_code" => "smallint",
            "user_start_odo" or "user_end_odo" or "driver_start_odo" or "driver_end_odo" or "distance" => "decimal(18, 2)",
            "hours" => "float",
            "quoted_tariff" => "real",
            "journal_detail_code" => "uniqueidentifier",
            "is_deleted" => "bit",
            _ => "varchar(1)"
        };

    private static void AddRequiredValue(ICollection<WriteValue> values, IReadOnlyDictionary<string, ColumnInfo> columns, string column, string parameter, DbType type, object? value)
    {
        if (!columns.ContainsKey(column)) throw new InvalidOperationException($"The required compatibility column {column} is not available.");
        values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddValue(ICollection<WriteValue> values, IReadOnlyDictionary<string, ColumnInfo> columns, string column, string parameter, DbType type, object? value)
    {
        if (columns.ContainsKey(column) && value is not null) values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values) AddParameter(command, value.Parameter, value.Type, value.Value);
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
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
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

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static double? ReadDouble(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDouble(reader.GetValue(ordinal));
    }

    private static float? ReadSingle(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToSingle(reader.GetValue(ordinal));
    }

    private static Guid? ReadGuid(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetValue(ordinal);
        return value is Guid guid ? guid : Guid.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            TimeSpan timeSpan => DateTime.Today.Add(timeSpan),
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null
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
        if (shouldClose) await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static string NormalizeKey(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static short ToLegacyShortUserId(int userId)
    {
        if (userId > short.MaxValue || userId < short.MinValue)
            throw new InvalidOperationException($"User id {userId} cannot be stored in legacy taxi log userid column.");
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
            if (_shouldClose) await Connection.CloseAsync();
        }
    }
}
