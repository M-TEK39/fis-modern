using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table, schema, projection, and ordering identifiers are fixed compatibility allowlists; predicates and values are parameterized."
)]
public class VehicleTariffRepository : IVehicleTariffRepository
{
    private const string TableName = "vehicle_tariff";
    private const string SchemaName = "fin";
    private static readonly string[] RequiredColumns = ["vehicle_tariff_code", "vmf_code", "start_date", "end_date"];
    private static readonly string[] OptionalColumns =
    [
        "residual_percentage", "parameter_year", "annual_interest_percentage", "purchase_amount",
        "purchase_date", "purchase_amount_group", "overhead_unit_factor", "target_replacement_date",
        "year_manufactured", "model_code", "class_code", "kilometer_life", "months_life",
        "residual_amount", "capital_payment", "overhead_payment", "adjustment_amount",
        "vehicle_fixed_tariff", "vehicle_fixed_daily_tariff", "vehicle_fixed_tariff_pool",
        "class_fixed_tariff", "class_fixed_pool_tariff", "lease_fixed_tariff", "overhead_kilometer_amount",
        "maintenance_kilometer_amount", "vehicle_kilometer_tariff", "fuel_kilo_tariff",
        "calculation_date", "comment", "TariffWeightCalculation_Code", "date_created", "date_updated",
        "created_by_user_code", "modified_by_user_code", "is_deleted"
    ];

    private readonly FisDbContext _context;
    private readonly ILogger<VehicleTariffRepository> _logger;

    public VehicleTariffRepository(FisDbContext context, ILogger<VehicleTariffRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VehicleTariff?> GetByIdAsync(int vehicleTariffCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            "[vehicle_tariff_code] = @vehicleTariffCode",
            command => AddParameter(command, "@vehicleTariffCode", DbType.Int32, vehicleTariffCode),
            columns
        );
    }

    public async Task<VehicleTariff?> GetCurrentTariffForVehicleAsync(int vmfCode)
    {
        return await GetTariffForVehicleAsync(vmfCode, null, DateTime.Today);
    }

    public async Task<VehicleTariff?> GetTariffForVehicleAsync(
        int vmfCode,
        int? parameterYear,
        DateTime effectiveDate
    )
    {
        var columns = await GetAvailableColumnsAsync();
        var predicates = new List<string>
        {
            "[vmf_code] = @vmfCode",
            "[start_date] <= @effectiveDate",
            "([end_date] IS NULL OR [end_date] >= @effectiveDate)",
            GetNotDeletedPredicate(columns),
        };
        if (parameterYear.HasValue && columns.Contains("parameter_year"))
        {
            predicates.Add("[parameter_year] = @parameterYear");
        }

        return await QueryOneAsync(
            string.Join(" AND ", predicates),
            command =>
            {
                AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                AddParameter(command, "@effectiveDate", DbType.DateTime2, effectiveDate);
                if (parameterYear.HasValue && columns.Contains("parameter_year"))
                    AddParameter(command, "@parameterYear", DbType.Int32, parameterYear.Value);
            },
            columns
        );
    }

    public async Task<IEnumerable<VehicleTariff>> GetTariffHistoryForVehicleAsync(int vmfCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"[vmf_code] = @vmfCode AND {GetNotDeletedPredicate(columns)}",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            columns
        );
    }

    public async Task<VehicleTariff> CreateAsync(VehicleTariff tariff)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        await EnsureLegacyTariffTriggerAsync();

        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, tariff.vmf_code);
        AddValue(values, columns, "start_date", "@startDate", DbType.DateTime2, tariff.start_date);
        AddValue(values, columns, "end_date", "@endDate", DbType.DateTime2, tariff.end_date);
        AddValue(
            values,
            columns,
            "residual_percentage",
            "@residualPercentage",
            DbType.Decimal,
            tariff.residual_percentage
        );
        AddValue(
            values,
            columns,
            "parameter_year",
            "@parameterYear",
            DbType.Int32,
            tariff.parameter_year
        );
        AddTariffComponentValues(values, columns, tariff);
        AddValue(
            values,
            columns,
            "calculation_date",
            "@calculationDate",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(values, columns, "comment", "@comment", DbType.String, tariff.comment);
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for inserting into fin.vehicle_tariff."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [{SchemaName}].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
            OUTPUT INSERTED.[vehicle_tariff_code]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        tariff.vehicle_tariff_code = Convert.ToInt32(await command.ExecuteScalarAsync());

        _logger.LogInformation(
            "Created vehicle tariff {TariffCode} for vehicle {VmfCode}",
            tariff.vehicle_tariff_code,
            tariff.vmf_code
        );

        return await GetByIdAsync(tariff.vehicle_tariff_code)
            ?? throw new InvalidOperationException("Created vehicle tariff could not be read.");
    }

    public async Task UpdateAsync(VehicleTariff tariff)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        await EnsureLegacyTariffTriggerAsync();

        var existing =
            await GetByIdAsync(tariff.vehicle_tariff_code)
            ?? throw new KeyNotFoundException(
                $"Vehicle tariff {tariff.vehicle_tariff_code} not found"
            );

        var columns = await GetAvailableColumnsAsync();
        var assignments = new List<string>();
        var values = new List<WriteValue>();
        AddAssignment(
            assignments,
            values,
            columns,
            "start_date",
            "@startDate",
            DbType.DateTime2,
            tariff.start_date
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "end_date",
            "@endDate",
            DbType.DateTime2,
            tariff.end_date
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "residual_percentage",
            "@residualPercentage",
            DbType.Decimal,
            tariff.residual_percentage
        );
        AddAssignment(
            assignments,
            values,
            columns,
            "parameter_year",
            "@parameterYear",
            DbType.Int32,
            tariff.parameter_year
        );
        AddTariffComponentAssignments(assignments, values, columns, tariff);
        AddAssignment(
            assignments,
            values,
            columns,
            "calculation_date",
            "@calculationDate",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddAssignment(assignments, values, columns, "comment", "@comment", DbType.String, tariff.comment);
        AddAssignment(
            assignments,
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for updating fin.vehicle_tariff."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [{SchemaName}].[{TableName}]
            SET {string.Join(", ", assignments)}
            WHERE [vehicle_tariff_code] = @vehicleTariffCode
            """;
        AddParameters(command, values);
        AddParameter(command, "@vehicleTariffCode", DbType.Int32, tariff.vehicle_tariff_code);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException(
                $"Vehicle tariff {tariff.vehicle_tariff_code} not found"
            );
        }

        _logger.LogInformation(
            "Updated vehicle tariff {TariffCode} for vehicle {VmfCode}",
            existing.vehicle_tariff_code,
            existing.vmf_code
        );
    }

    public async Task RecalculateTariffAsync(int vmfCode)
    {
        if (vmfCode <= 0)
            throw new ArgumentException("A valid vehicle code is required.", nameof(vmfCode));

        // The legacy database owns tariff calculation in
        // fin.TRG_UPSERT_Vehicle_Tariff. The previous implementation used
        // guessed interest, overhead, maintenance, and fuel constants, which
        // can silently under/over-bill a client. Request the same trigger by
        // issuing the no-op update used by the legacy maintenance page.
        await EnsureLegacyTariffTriggerAsync();
        if (!await VehicleExistsAsync(vmfCode))
            throw new KeyNotFoundException($"Vehicle {vmfCode} was not found.");

        var currentTariff = await GetCurrentTariffForVehicleAsync(vmfCode);
        if (currentTariff is null)
        {
            throw new NotSupportedException(
                "The vehicle has no current legacy tariff row. Capture the tariff through the legacy tariff workflow before recalculating it."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        var currentTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = currentTransaction is null;
        await using var transaction = ownsTransaction
            ? await connection.BeginTransactionAsync()
            : null;

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction ?? currentTransaction!.GetDbTransaction();
            command.CommandType = CommandType.Text;
            command.CommandText = """
                UPDATE [fin].[vehicle_tariff]
                SET [vmf_code] = [vmf_code]
                WHERE [vehicle_tariff_code] = @vehicleTariffCode;
                """;
            AddParameter(command, "@vehicleTariffCode", DbType.Int32, currentTariff.vehicle_tariff_code);
            if (await command.ExecuteNonQueryAsync() != 1)
            {
                throw new InvalidOperationException(
                    $"The legacy tariff trigger update did not affect tariff {currentTariff.vehicle_tariff_code}."
                );
            }

            if (transaction is not null)
                await transaction.CommitAsync();

            _logger.LogInformation(
                "Requested legacy tariff trigger recalculation for vehicle {VmfCode} (tariff {TariffCode})",
                vmfCode,
                currentTariff.vehicle_tariff_code
            );
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task EnsureLegacyTariffTriggerAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COUNT(1)
                FROM [sys].[triggers] AS [tr]
                INNER JOIN [sys].[tables] AS [tb] ON [tb].[object_id] = [tr].[parent_id]
                INNER JOIN [sys].[schemas] AS [sc] ON [sc].[schema_id] = [tb].[schema_id]
                WHERE [sc].[name] = @schemaName
                  AND [tb].[name] = @tableName
                  AND [tr].[name] = @triggerName
                  AND [tr].[is_disabled] = 0;
                """;
            AddParameter(command, "@schemaName", DbType.String, "fin");
            AddParameter(command, "@tableName", DbType.String, "vehicle_tariff");
            AddParameter(command, "@triggerName", DbType.String, "TRG_UPSERT_Vehicle_Tariff");
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            {
                throw new NotSupportedException(
                    "The legacy fin.TRG_UPSERT_Vehicle_Tariff trigger is unavailable or disabled; vehicle tariff calculation cannot be approximated."
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<bool> VehicleExistsAsync(int vmfCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP (1) 1 FROM [dbo].[vehicle_master] WHERE [vmf_code] = @vmfCode";
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            return await command.ExecuteScalarAsync() is not null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<List<VehicleTariff>> QueryAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns,
        bool firstOnly = false
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = $"SELECT {(firstOnly ? "TOP (1) " : string.Empty)}{BuildProjection(columns)} FROM [{SchemaName}].[{TableName}] WHERE {predicate} ORDER BY [start_date] DESC, [vehicle_tariff_code] DESC";
        configure?.Invoke(command);

        var result = new List<VehicleTariff>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapVehicleTariff(reader));
        }

        return result;
    }

    private async Task<VehicleTariff?> QueryOneAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns
    ) => (await QueryAsync(predicate, configure, columns, firstOnly: true)).SingleOrDefault();

    private static string BuildProjection(IReadOnlySet<string> columns) =>
        string.Join(
            ", ",
            RequiredColumns
                .Concat(OptionalColumns)
                .Select(column =>
                    columns.Contains(column)
                        ? $"[{column}] AS [{column}]"
                        : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]"
                )
        );

    private static string GetNotDeletedPredicate(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted")
            ? "([is_deleted] = 0 OR [is_deleted] IS NULL)"
            : "1 = 1";

    private static string GetSqlType(string column) =>
        column switch
        {
            "vehicle_tariff_code" or "vmf_code" or "parameter_year" or "year_manufactured"
                or "model_code" or "class_code" or "kilometer_life" or "TariffWeightCalculation_Code"
                => "int",
            "months_life" or "purchase_amount_group" => "tinyint",
            "start_date" or "end_date" or "purchase_date" or "target_replacement_date"
                or "calculation_date" or "date_created" or "date_updated" => "datetime2",
            "is_deleted" => "bit",
            "comment" => "varchar(1000)",
            _ => "decimal(18, 4)",
        };

    private static VehicleTariff MapVehicleTariff(DbDataReader reader) =>
        new()
        {
            vehicle_tariff_code = ReadInt(reader, "vehicle_tariff_code"),
            vmf_code = ReadInt(reader, "vmf_code"),
            start_date = ReadDate(reader, "start_date"),
            end_date = ReadNullableDate(reader, "end_date"),
            residual_percentage = ReadNullableDecimal(reader, "residual_percentage") ?? 0m,
            parameter_year = (short)(ReadNullableInt(reader, "parameter_year") ?? 0),
            annual_interest_percentage = ReadNullableDecimal(reader, "annual_interest_percentage"),
            purchase_amount = ReadNullableDecimal(reader, "purchase_amount"),
            purchase_date = ReadNullableDate(reader, "purchase_date"),
            purchase_amount_group = ReadNullableByte(reader, "purchase_amount_group"),
            overhead_unit_factor = ReadNullableDouble(reader, "overhead_unit_factor"),
            target_replacement_date = ReadNullableDate(reader, "target_replacement_date"),
            year_manufactured = ReadNullableInt(reader, "year_manufactured"),
            model_code = ReadNullableInt(reader, "model_code"),
            class_code = ReadNullableInt(reader, "class_code"),
            kilometer_life = ReadNullableInt(reader, "kilometer_life"),
            months_life = ReadNullableByte(reader, "months_life"),
            residual_amount = ReadNullableDecimal(reader, "residual_amount"),
            capital_payment = ReadNullableDecimal(reader, "capital_payment"),
            overhead_payment = ReadNullableDecimal(reader, "overhead_payment"),
            adjustment_amount = ReadNullableDecimal(reader, "adjustment_amount"),
            vehicle_fixed_tariff = ReadNullableDecimal(reader, "vehicle_fixed_tariff"),
            vehicle_fixed_daily_tariff = ReadNullableDecimal(reader, "vehicle_fixed_daily_tariff"),
            vehicle_fixed_tariff_pool = ReadNullableDecimal(reader, "vehicle_fixed_tariff_pool"),
            class_fixed_tariff = ReadNullableDecimal(reader, "class_fixed_tariff"),
            class_fixed_pool_tariff = ReadNullableDecimal(reader, "class_fixed_pool_tariff"),
            lease_fixed_tariff = ReadNullableDecimal(reader, "lease_fixed_tariff"),
            overhead_kilometer_amount = ReadNullableDecimal(reader, "overhead_kilometer_amount"),
            maintenance_kilometer_amount = ReadNullableDecimal(reader, "maintenance_kilometer_amount"),
            vehicle_kilometer_tariff = ReadNullableDecimal(reader, "vehicle_kilometer_tariff"),
            fuel_kilo_tariff = ReadNullableDecimal(reader, "fuel_kilo_tariff"),
            calculation_date = ReadNullableDate(reader, "calculation_date") ?? default,
            comment = ReadString(reader, "comment"),
            TariffWeightCalculation_Code = ReadNullableInt(reader, "TariffWeightCalculation_Code"),
            date_created = ReadNullableDate(reader, "date_created") ?? default,
            date_updated = ReadNullableDate(reader, "date_updated"),
            created_by_user_code = ReadNullableInt(reader, "created_by_user_code"),
            modified_by_user_code = ReadNullableInt(reader, "modified_by_user_code"),
            is_deleted = ReadBool(reader, "is_deleted"),
        };

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.CommandText = "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, SchemaName);
        AddParameter(command, "@table", DbType.String, TableName);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException($"The legacy fin.vehicle_tariff table is missing required columns: {string.Join(", ", missing)}");
        return columns;
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static int ReadInt(DbDataReader reader, string column) => Convert.ToInt32(reader[column]);
    private static int? ReadNullableInt(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);
    private static byte? ReadNullableByte(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToByte(reader[column]);
    private static decimal ReadDecimal(DbDataReader reader, string column) => Convert.ToDecimal(reader[column]);
    private static decimal? ReadNullableDecimal(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToDecimal(reader[column]);
    private static double? ReadNullableDouble(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToDouble(reader[column]);
    private static DateTime ReadDate(DbDataReader reader, string column) => Convert.ToDateTime(reader[column]);
    private static DateTime? ReadNullableDate(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);
    private static string? ReadString(DbDataReader reader, string column) => reader[column] is DBNull ? null : reader[column]?.ToString()?.Trim();
    private static bool ReadBool(DbDataReader reader, string column) => reader[column] is not DBNull && Convert.ToBoolean(reader[column]);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose) : IAsyncDisposable
    {
        public DbConnection Connection => connection;
        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddTariffComponentValues(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        VehicleTariff tariff
    )
    {
        AddValue(
            values,
            columns,
            "annual_interest_percentage",
            "@annualInterestPercentage",
            DbType.Decimal,
            tariff.annual_interest_percentage
        );
        AddValue(
            values,
            columns,
            "purchase_amount",
            "@purchaseAmount",
            DbType.Decimal,
            tariff.purchase_amount
        );
        AddValue(
            values,
            columns,
            "purchase_date",
            "@purchaseDate",
            DbType.DateTime2,
            tariff.purchase_date
        );
        AddValue(
            values,
            columns,
            "purchase_amount_group",
            "@purchaseAmountGroup",
            DbType.Byte,
            tariff.purchase_amount_group
        );
        AddValue(
            values,
            columns,
            "overhead_unit_factor",
            "@overheadUnitFactor",
            DbType.Double,
            tariff.overhead_unit_factor
        );
        AddValue(
            values,
            columns,
            "target_replacement_date",
            "@targetReplacementDate",
            DbType.DateTime2,
            tariff.target_replacement_date
        );
        AddValue(
            values,
            columns,
            "year_manufactured",
            "@yearManufactured",
            DbType.Int32,
            tariff.year_manufactured
        );
        AddValue(values, columns, "model_code", "@modelCode", DbType.Int32, tariff.model_code);
        AddValue(values, columns, "class_code", "@classCode", DbType.Int32, tariff.class_code);
        AddValue(
            values,
            columns,
            "kilometer_life",
            "@kilometerLife",
            DbType.Int32,
            tariff.kilometer_life
        );
        AddValue(values, columns, "months_life", "@monthsLife", DbType.Byte, tariff.months_life);
        AddValue(
            values,
            columns,
            "residual_amount",
            "@residualAmount",
            DbType.Decimal,
            tariff.residual_amount
        );
        AddValue(
            values,
            columns,
            "capital_payment",
            "@capitalPayment",
            DbType.Decimal,
            tariff.capital_payment
        );
        AddValue(
            values,
            columns,
            "overhead_payment",
            "@overheadPayment",
            DbType.Decimal,
            tariff.overhead_payment
        );
        AddValue(
            values,
            columns,
            "adjustment_amount",
            "@adjustmentAmount",
            DbType.Decimal,
            tariff.adjustment_amount
        );
        AddValue(
            values,
            columns,
            "vehicle_fixed_tariff",
            "@vehicleFixedTariff",
            DbType.Decimal,
            tariff.vehicle_fixed_tariff
        );
        AddValue(
            values,
            columns,
            "vehicle_fixed_daily_tariff",
            "@vehicleFixedDailyTariff",
            DbType.Decimal,
            tariff.vehicle_fixed_daily_tariff
        );
        AddValue(
            values,
            columns,
            "vehicle_fixed_tariff_pool",
            "@vehicleFixedTariffPool",
            DbType.Decimal,
            tariff.vehicle_fixed_tariff_pool
        );
        AddValue(
            values,
            columns,
            "class_fixed_tariff",
            "@classFixedTariff",
            DbType.Decimal,
            tariff.class_fixed_tariff
        );
        AddValue(
            values,
            columns,
            "class_fixed_pool_tariff",
            "@classFixedPoolTariff",
            DbType.Decimal,
            tariff.class_fixed_pool_tariff
        );
        AddValue(
            values,
            columns,
            "lease_fixed_tariff",
            "@leaseFixedTariff",
            DbType.Decimal,
            tariff.lease_fixed_tariff
        );
        AddValue(
            values,
            columns,
            "overhead_kilometer_amount",
            "@overheadKilometerAmount",
            DbType.Decimal,
            tariff.overhead_kilometer_amount
        );
        AddValue(
            values,
            columns,
            "maintenance_kilometer_amount",
            "@maintenanceKilometerAmount",
            DbType.Decimal,
            tariff.maintenance_kilometer_amount
        );
        AddValue(
            values,
            columns,
            "vehicle_kilometer_tariff",
            "@vehicleKilometerTariff",
            DbType.Decimal,
            tariff.vehicle_kilometer_tariff
        );
        AddValue(
            values,
            columns,
            "fuel_kilo_tariff",
            "@fuelKiloTariff",
            DbType.Decimal,
            tariff.fuel_kilo_tariff
        );
        AddValue(
            values,
            columns,
            "TariffWeightCalculation_Code",
            "@tariffWeightCalculationCode",
            DbType.Int32,
            tariff.TariffWeightCalculation_Code
        );
    }

    private static void AddTariffComponentAssignments(
        ICollection<string> assignments,
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        VehicleTariff tariff
    )
    {
        var buffer = new List<WriteValue>();
        AddTariffComponentValues(buffer, columns, tariff);
        foreach (var value in buffer)
        {
            assignments.Add($"[{value.Column}] = {value.Parameter}");
            values.Add(value);
        }
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

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
