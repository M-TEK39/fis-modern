using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads taxi classes from both the original client table and the expanded
/// table. The legacy tariff and schedule columns are negotiated at runtime so
/// a missing modern column cannot make the entire EF model fail to load.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; this read path has no user-controlled SQL fragments.")]
public sealed class ContractorTaxiClassRepository : IContractorTaxiClassRepository
{
    private const string TableName = "Contractor_taxi_class";

    private static readonly string[] BusinessColumns =
    [
        "contractor_id", "description", "km_tariff", "driver_per_hour", "daily_tariff",
        "half_day_tariff", "tariff_type", "tariff_date", "model_code", "km_tariff_bus", "active",
        "tariff_end_date", "normalhours_start_time", "normalhours_end_time",
        "midweekovertime_start_time", "midweekovertime_end_time", "holidayhours_start_time",
        "holidayhours_end_time"
    ];

    private static readonly string[] RequiredColumns = ["class_id", "contractor_id", "description"];

    private static readonly string[] AuditColumns =
    ["date_created", "date_updated", "created_by_user_code", "modified_by_user_code", "is_deleted"];

    private readonly FisDbContext _context;

    public ContractorTaxiClassRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<ContractorTaxiClass>> GetAllAsync()
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT {string.Join(", ", BusinessColumns.Concat(AuditColumns).Select(column => GetProjection(columns, column)))},
                   [{columns["class_id"].Name}] AS [class_id]
            FROM [dbo].[{TableName}]
            WHERE {GetActiveFilter(columns)}
            ORDER BY [{columns["contractor_id"].Name}], [{columns["description"].Name}], [{columns["class_id"].Name}]
            """;

        var results = new List<ContractorTaxiClass>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(Map(reader));
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
        {
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));
        }

        foreach (var required in RequiredColumns.Where(column => !columns.ContainsKey(column)))
        {
            throw new InvalidOperationException($"The required {TableName} compatibility column {required} is not available.");
        }

        return columns;
    }

    private static ContractorTaxiClass Map(DbDataReader reader)
        => new()
        {
            class_id = ReadInt16(reader, "class_id") ?? 0,
            contractor_id = ReadInt16(reader, "contractor_id") ?? 0,
            description = ReadString(reader, "description"),
            km_tariff = ReadDecimal(reader, "km_tariff"),
            driver_per_hour = ReadDecimal(reader, "driver_per_hour"),
            daily_tariff = ReadDecimal(reader, "daily_tariff"),
            half_day_tariff = ReadDecimal(reader, "half_day_tariff"),
            tariff_type = ReadString(reader, "tariff_type"),
            tariff_date = ReadDateTime(reader, "tariff_date"),
            model_code = ReadInt16(reader, "model_code"),
            km_tariff_bus = ReadDecimal(reader, "km_tariff_bus"),
            active = ReadBoolean(reader, "active"),
            tariff_end_date = ReadDateTime(reader, "tariff_end_date"),
            normalhours_start_time = ReadTimeSpan(reader, "normalhours_start_time"),
            normalhours_end_time = ReadTimeSpan(reader, "normalhours_end_time"),
            midweekovertime_start_time = ReadTimeSpan(reader, "midweekovertime_start_time"),
            midweekovertime_end_time = ReadTimeSpan(reader, "midweekovertime_end_time"),
            holidayhours_start_time = ReadTimeSpan(reader, "holidayhours_start_time"),
            holidayhours_end_time = ReadTimeSpan(reader, "holidayhours_end_time"),
            date_created = ReadDateTime(reader, "date_created") ?? default,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false
        };

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(IReadOnlyDictionary<string, ColumnInfo> columns, string column)
        => columns.ContainsKey(column)
            ? $"[{columns[column].Name}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column)
        => column switch
        {
            "contractor_id" or "model_code" or "class_id" => "smallint",
            "km_tariff" or "driver_per_hour" or "daily_tariff" or "half_day_tariff" or "km_tariff_bus" => "decimal(18, 4)",
            "tariff_date" or "tariff_end_date" or "date_created" or "date_updated" => "datetime2",
            "normalhours_start_time" or "normalhours_end_time" or "midweekovertime_start_time" or "midweekovertime_end_time"
                or "holidayhours_start_time" or "holidayhours_end_time" => "time",
            "active" or "is_deleted" => "bit",
            "created_by_user_code" or "modified_by_user_code" => "int",
            _ => "varchar(1)"
        };

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

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null
        };
    }

    private static TimeSpan? ReadTimeSpan(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal)) return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            TimeSpan timeSpan => timeSpan,
            DateTime dateTime => dateTime.TimeOfDay,
            DateTimeOffset dateTimeOffset => dateTimeOffset.TimeOfDay,
            _ when TimeSpan.TryParse(Convert.ToString(value), out var parsed) => parsed,
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

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
            if (_shouldClose) await Connection.CloseAsync();
        }
    }
}
