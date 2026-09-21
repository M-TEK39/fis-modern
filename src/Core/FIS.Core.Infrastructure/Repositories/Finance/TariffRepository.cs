using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
    /// Create new tariff using only columns confirmed on dbo.tariff.
    /// </summary>
    public async Task<Tariff> CreateAsync(Tariff tariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.Now;
        var values = new List<WriteValue>();
        AddValue(values, columns, "class_code", "@classCode", DbType.Int16, tariff.class_code);
        AddValue(values, columns, "year_manufactured", "@yearManufactured", DbType.Int16, tariff.year_manufactured);
        AddValue(values, columns, "monthly_fixed_amount", "@monthlyFixed", DbType.Decimal, tariff.monthly_fixed_amount);
        AddValue(values, columns, "monthly_odo_amount", "@monthlyOdo", DbType.Decimal, tariff.monthly_odo_amount);
        AddValue(values, columns, "daily_fixed_amount", "@dailyFixed", DbType.Decimal, tariff.daily_fixed_amount);
        AddValue(values, columns, "effective_start_date", "@startDate", DbType.DateTime2, tariff.effective_start_date);
        AddValue(values, columns, "effective_end_date", "@endDate", DbType.DateTime2, tariff.effective_end_date);
        AddValue(values, columns, "replacement_percent", "@replacementPercent", DbType.Int16, tariff.replacement_percent);
        AddValue(values, columns, "loss_percent", "@lossPercent", DbType.Int16, tariff.loss_percent);
        AddValue(values, columns, "profit_percent", "@profitPercent", DbType.Int16, tariff.profit_percent);
        AddValue(values, columns, "overhead_percent", "@overheadPercent", DbType.Int16, tariff.overhead_percent);
        AddValue(values, columns, "accident_percent", "@accidentPercent", DbType.Int16, tariff.accident_percent);
        AddValue(values, columns, "fuel_kilo_tariff", "@fuelKilo", DbType.Decimal, tariff.fuel_kilo_tariff);
        AddValue(values, columns, "tariff_approval_status", "@approvalStatus", DbType.Int16, tariff.tariff_approval_status);
        AddValue(values, columns, "approver_code", "@approverCode", DbType.Int32, tariff.approver_code);
        AddValue(values, columns, "approval_date", "@approvalDate", DbType.DateTime2, tariff.approval_date);
        AddValue(values, columns, "rejection_reason", "@rejectionReason", DbType.String, tariff.rejection_reason);
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddValue(values, columns, "created_by", "@createdBy", DbType.Int32, currentUserId);
        AddValue(values, columns, "created_by_user_code", "@createdByUser", DbType.Int32, currentUserId);
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for inserting into dbo.tariff."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
            OUTPUT INSERTED.[tariff_code]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        tariff.tariff_code = Convert.ToInt32(await command.ExecuteScalarAsync());
        return await GetByIdAsync(tariff.tariff_code)
            ?? throw new InvalidOperationException("Created tariff could not be read.");
    }

    /// <summary>
    /// Update existing tariff using only columns confirmed on dbo.tariff.
    /// </summary>
    public async Task<Tariff> UpdateAsync(Tariff tariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        _ =
            await GetByIdAsync(tariff.tariff_code)
            ?? throw new InvalidOperationException(
                $"Tariff with tariff_code {tariff.tariff_code} not found"
            );

        var columns = await GetAvailableColumnsAsync();
        var assignments = new List<string>();
        var values = new List<WriteValue>();
        AddAssignment(assignments, values, columns, "class_code", "@classCode", DbType.Int16, tariff.class_code);
        AddAssignment(assignments, values, columns, "year_manufactured", "@yearManufactured", DbType.Int16, tariff.year_manufactured);
        AddAssignment(assignments, values, columns, "monthly_fixed_amount", "@monthlyFixed", DbType.Decimal, tariff.monthly_fixed_amount);
        AddAssignment(assignments, values, columns, "monthly_odo_amount", "@monthlyOdo", DbType.Decimal, tariff.monthly_odo_amount);
        AddAssignment(assignments, values, columns, "daily_fixed_amount", "@dailyFixed", DbType.Decimal, tariff.daily_fixed_amount);
        AddAssignment(assignments, values, columns, "effective_start_date", "@startDate", DbType.DateTime2, tariff.effective_start_date);
        AddAssignment(assignments, values, columns, "effective_end_date", "@endDate", DbType.DateTime2, tariff.effective_end_date);
        AddAssignment(assignments, values, columns, "replacement_percent", "@replacementPercent", DbType.Int16, tariff.replacement_percent);
        AddAssignment(assignments, values, columns, "loss_percent", "@lossPercent", DbType.Int16, tariff.loss_percent);
        AddAssignment(assignments, values, columns, "profit_percent", "@profitPercent", DbType.Int16, tariff.profit_percent);
        AddAssignment(assignments, values, columns, "overhead_percent", "@overheadPercent", DbType.Int16, tariff.overhead_percent);
        AddAssignment(assignments, values, columns, "accident_percent", "@accidentPercent", DbType.Int16, tariff.accident_percent);
        AddAssignment(assignments, values, columns, "fuel_kilo_tariff", "@fuelKilo", DbType.Decimal, tariff.fuel_kilo_tariff);
        AddAssignment(assignments, values, columns, "tariff_approval_status", "@approvalStatus", DbType.Int16, tariff.tariff_approval_status);
        AddAssignment(assignments, values, columns, "approver_code", "@approverCode", DbType.Int32, tariff.approver_code);
        AddAssignment(assignments, values, columns, "approval_date", "@approvalDate", DbType.DateTime2, tariff.approval_date);
        AddAssignment(assignments, values, columns, "rejection_reason", "@rejectionReason", DbType.String, tariff.rejection_reason);
        AddAssignment(assignments, values, columns, "date_modified", "@dateModified", DbType.DateTime2, DateTime.Now);
        AddAssignment(assignments, values, columns, "modified_by", "@modifiedBy", DbType.Int32, currentUserId);
        AddAssignment(assignments, values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.Now);
        AddAssignment(assignments, values, columns, "modified_by_user_code", "@modifiedByUser", DbType.Int32, currentUserId);

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for updating dbo.tariff."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [dbo].[{TableName}]
            SET {string.Join(", ", assignments)}
            WHERE [tariff_code] = @tariffCode
            """;
        AddParameters(command, values);
        AddParameter(command, "@tariffCode", DbType.Int32, tariff.tariff_code);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new InvalidOperationException(
                $"Tariff with tariff_code {tariff.tariff_code} not found"
            );
        }

        return await GetByIdAsync(tariff.tariff_code)
            ?? throw new InvalidOperationException("Updated tariff could not be read.");
    }

    /// <summary>
    /// Delete tariff. Soft-delete when is_deleted exists; otherwise labelled hard DELETE.
    /// </summary>
    public async Task DeleteAsync(int tariffCode, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        if (columns.Contains("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            var values = new List<WriteValue> { new("is_deleted", "@isDeleted", DbType.Boolean, true) };
            AddAssignment(assignments, values, columns, "date_modified", "@dateModified", DbType.DateTime2, DateTime.Now);
            AddAssignment(assignments, values, columns, "modified_by", "@modifiedBy", DbType.Int32, currentUserId);
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", assignments)}
                WHERE [tariff_code] = @tariffCode
                """;
            AddParameters(command, values);
        }
        else
        {
            command.CommandText = $"""
                DELETE FROM [dbo].[{TableName}]
                WHERE [tariff_code] = @tariffCode
                """;
        }

        AddParameter(command, "@tariffCode", DbType.Int32, tariffCode);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (columns.Contains(column))
            values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddAssignment(
        ICollection<string> assignments,
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (!columns.Contains(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.DbType, value.Value);
    }

    private sealed record WriteValue(string Column, string Parameter, DbType DbType, object? Value);
}
