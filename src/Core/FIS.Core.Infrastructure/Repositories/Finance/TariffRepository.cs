using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for legacy Tariff table (pre-2009 tariff system).
/// Handles tariff lookups by vehicle class, year manufactured, and effective date.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers are fixed compatibility allowlists; lookup values are parameters."
)]
public class TariffRepository : ITariffRepository
{
    private const string TableName = "tariff";

    private static readonly string[] RequiredColumns =
    [
        "tariff_code",
        "class_code",
        "year_manufactured",
        "monthly_fixed_amount",
        "monthly_odo_amount",
        "daily_fixed_amount",
        "effective_start_date",
        "effective_end_date",
        "replacement_percent",
        "loss_percent",
        "profit_percent",
        "overhead_percent",
        "accident_percent",
        "fuel_kilo_tariff",
        "date_created",
        "comment",
    ];

    private static readonly string[] OptionalColumns =
    [
        "hourly_fixed_amount",
        "tariff_approval_status",
        "approver_code",
        "approval_date",
        "rejection_reason",
        "created_by",
        "date_modified",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public TariffRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get tariff by class code, year manufactured, and effective date.
    /// Finds the tariff where the effective date falls within the start/end date range.
    /// </summary>
    public async Task<Tariff?> GetTariffAsync(
        short classCode,
        short yearManufactured,
        DateTime effectiveDate
    )
    {
        return await QueryOneAsync(
            "[class_code] = @classCode AND [year_manufactured] = @yearManufactured AND [effective_start_date] <= @effectiveDate AND ([effective_end_date] IS NULL OR [effective_end_date] >= @effectiveDate)"
                + " AND "
                + GetNotDeletedPredicate(await GetAvailableColumnsAsync()),
            command =>
            {
                AddParameter(command, "@classCode", DbType.Int16, classCode);
                AddParameter(command, "@yearManufactured", DbType.Int16, yearManufactured);
                AddParameter(command, "@effectiveDate", DbType.DateTime2, effectiveDate);
            }
        );
    }

    public async Task<Tariff?> GetApprovedTariffForClassAsync(
        short classCode,
        DateTime effectiveDate
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var approvalPredicate = availableColumns.Contains("tariff_approval_status")
            ? " AND [tariff_approval_status] = 2"
            : string.Empty;
        return await QueryOneAsync(
            "[class_code] = @classCode AND [effective_start_date] <= @effectiveDate AND ([effective_end_date] IS NULL OR [effective_end_date] >= @effectiveDate)"
                + " AND "
                + GetNotDeletedPredicate(availableColumns)
                + approvalPredicate,
            command =>
            {
                AddParameter(command, "@classCode", DbType.Int16, classCode);
                AddParameter(command, "@effectiveDate", DbType.DateTime2, effectiveDate);
            },
            availableColumns
        );
    }

    /// <summary>
    /// Get all tariffs for a vehicle class.
    /// </summary>
    public async Task<List<Tariff>> GetTariffsByClassAsync(short classCode)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"[class_code] = @classCode AND {GetNotDeletedPredicate(availableColumns)}",
            command => AddParameter(command, "@classCode", DbType.Int16, classCode),
            availableColumns
        );
    }

    /// <summary>
    /// Get all tariffs effective on a specific date.
    /// </summary>
    public async Task<List<Tariff>> GetTariffsByDateAsync(DateTime effectiveDate)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"[effective_start_date] <= @effectiveDate AND ([effective_end_date] IS NULL OR [effective_end_date] >= @effectiveDate) AND {GetNotDeletedPredicate(availableColumns)}",
            command => AddParameter(command, "@effectiveDate", DbType.DateTime2, effectiveDate),
            availableColumns,
            orderBy: "[class_code], [year_manufactured], [effective_start_date] DESC"
        );
    }

    /// <summary>
    /// Get tariff by ID.
    /// </summary>
    public async Task<Tariff?> GetByIdAsync(int tariffCode)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            $"[tariff_code] = @tariffCode AND {GetNotDeletedPredicate(availableColumns)}",
            command => AddParameter(command, "@tariffCode", DbType.Int32, tariffCode),
            availableColumns
        );
    }

    /// <summary>
    /// Get all tariffs.
    /// </summary>
    public async Task<List<Tariff>> GetAllAsync()
    {
        var availableColumns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            GetNotDeletedPredicate(availableColumns).TrimStart(),
            configure: null,
            availableColumns,
            orderBy: "[effective_start_date] DESC, [class_code], [year_manufactured]"
        );
    }

    private async Task<List<Tariff>> QueryAsync(
        string? predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> availableColumns,
        string orderBy = "[effective_start_date] DESC, [tariff_code] DESC",
        bool firstOnly = false
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"SELECT {(firstOnly ? "TOP (1) " : string.Empty)}{BuildProjection(availableColumns)} FROM [dbo].[{TableName}] WHERE {(string.IsNullOrWhiteSpace(predicate) ? "1 = 1" : predicate)} ORDER BY {orderBy}";
        configure?.Invoke(command);

        var tariffs = new List<Tariff>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tariffs.Add(MapTariff(reader, availableColumns));
        }

        return tariffs;
    }

    private async Task<Tariff?> QueryOneAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string>? availableColumns = null
    )
    {
        var columns = availableColumns ?? await GetAvailableColumnsAsync();
        var rows = await QueryAsync(
            predicate,
            configure,
            columns,
            firstOnly: true
        );
        return rows.SingleOrDefault();
    }

    private static string BuildProjection(IReadOnlySet<string> availableColumns) =>
        string.Join(
            ", ",
            RequiredColumns
                .Concat(OptionalColumns)
                .Select(column =>
                    availableColumns.Contains(column)
                        ? $"[{column}] AS [{column}]"
                        : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]"
                )
        );

    private static string GetNotDeletedPredicate(IReadOnlySet<string> availableColumns) =>
        availableColumns.Contains("is_deleted")
            ? "([is_deleted] = 0 OR [is_deleted] IS NULL)"
            : "1 = 1";

    private static string GetSqlType(string column) =>
        column switch
        {
            "tariff_code" or "class_code" => "int",
            "year_manufactured" or "replacement_percent" or "loss_percent" or "profit_percent"
                or "overhead_percent" or "accident_percent" or "tariff_approval_status" => "smallint",
            "approver_code" or "created_by" or "modified_by" or "created_by_user_code"
                or "modified_by_user_code" => "int",
            "monthly_fixed_amount" or "monthly_odo_amount" or "daily_fixed_amount"
                or "hourly_fixed_amount" or "fuel_kilo_tariff" => "decimal(18, 4)",
            "effective_start_date" or "effective_end_date" or "approval_date" or "date_created"
                or "date_modified" or "date_updated" => "datetime2",
            "is_deleted" => "bit",
            _ => "varchar(1000)",
        };

    private static Tariff MapTariff(DbDataReader reader, IReadOnlySet<string> availableColumns) =>
        new()
        {
            tariff_code = ReadInt32(reader, "tariff_code"),
            class_code = ReadInt16(reader, "class_code"),
            year_manufactured = ReadNullableInt16(reader, "year_manufactured"),
            monthly_fixed_amount = ReadDecimal(reader, "monthly_fixed_amount"),
            monthly_odo_amount = ReadDecimal(reader, "monthly_odo_amount"),
            daily_fixed_amount = ReadNullableDecimal(reader, "daily_fixed_amount"),
            hourly_fixed_amount = ReadNullableDecimal(reader, "hourly_fixed_amount"),
            effective_start_date = ReadDateTime(reader, "effective_start_date"),
            effective_end_date = ReadNullableDateTime(reader, "effective_end_date"),
            replacement_percent = ReadNullableInt16(reader, "replacement_percent"),
            loss_percent = ReadNullableInt16(reader, "loss_percent"),
            profit_percent = ReadNullableInt16(reader, "profit_percent"),
            overhead_percent = ReadNullableInt16(reader, "overhead_percent"),
            accident_percent = ReadNullableInt16(reader, "accident_percent"),
            fuel_kilo_tariff = ReadNullableDecimal(reader, "fuel_kilo_tariff"),
            tariff_approval_status = ReadNullableInt16(reader, "tariff_approval_status") ?? 2,
            approver_code = ReadNullableInt32(reader, "approver_code"),
            approval_date = ReadNullableDateTime(reader, "approval_date"),
            rejection_reason = ReadString(reader, "rejection_reason"),
            date_created = ReadNullableDateTime(reader, "date_created"),
            created_by = ReadNullableInt32(reader, "created_by"),
            date_modified = ReadNullableDateTime(reader, "date_modified"),
            modified_by = ReadNullableInt32(reader, "modified_by"),
            date_updated = ReadNullableDateTime(reader, "date_updated"),
            created_by_user_code = ReadNullableInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadNullableInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted"),
        };

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The legacy tariff table is missing required columns: {string.Join(", ", missing)}"
            );
        }

        return columns;
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        return new ConnectionScope(connection, shouldClose);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt32(DbDataReader reader, string column) =>
        Convert.ToInt32(reader[column]);

    private static short ReadInt16(DbDataReader reader, string column) =>
        Convert.ToInt16(reader[column]);

    private static short? ReadNullableInt16(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static int? ReadNullableInt32(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static decimal ReadDecimal(DbDataReader reader, string column) =>
        Convert.ToDecimal(reader[column]);

    private static decimal? ReadNullableDecimal(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDecimal(reader[column]);

    private static DateTime ReadDateTime(DbDataReader reader, string column) =>
        Convert.ToDateTime(reader[column]);

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : reader[column]?.ToString()?.Trim();

    private static bool ReadBoolean(DbDataReader reader, string column) =>
        reader[column] is not DBNull && Convert.ToBoolean(reader[column]);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose) : IAsyncDisposable
    {
        public DbConnection Connection => connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// Create new tariff.
    /// </summary>
    public async Task<Tariff> CreateAsync(Tariff tariff, int currentUserId)
    {
        tariff.date_created = DateTime.Now;
        _context.Set<Tariff>().Add(tariff);
        await _context.SaveChangesAsync();
        return tariff;
    }

    /// <summary>
    /// Update existing tariff.
    /// </summary>
    public async Task<Tariff> UpdateAsync(Tariff tariff, int currentUserId)
    {
        if (tariff == null)
            throw new ArgumentNullException(nameof(tariff));

        var existing = await _context.Set<Tariff>().FindAsync(tariff.tariff_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"Tariff with tariff_code {tariff.tariff_code} not found"
            );

        tariff.date_modified = DateTime.Now;
        _context.Entry(existing).CurrentValues.SetValues(tariff);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete tariff.
    /// </summary>
    public async Task DeleteAsync(int tariffCode, int currentUserId)
    {
        var tariff = await GetByIdAsync(tariffCode);
        if (tariff != null)
        {
            _context.Set<Tariff>().Remove(tariff);
            await _context.SaveChangesAsync();
        }
    }
}
