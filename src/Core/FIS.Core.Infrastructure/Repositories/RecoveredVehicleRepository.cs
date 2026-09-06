using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Implements the legacy recovered-vehicle workflow without selecting every
/// property on the expanded Vehicle EF entity. The original vehicle update,
/// replacement vehicle insert, and history rows are one transaction.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers are fixed compatibility allowlists; all submitted values are parameters.")]
public sealed class RecoveredVehicleRepository : IRecoveredVehicleRepository
{
    private const string VehicleTableName = "vehicle_master";
    private const string HistoryTableName = "vehicle_history";
    private const string StatusTableName = "vehicle_status";

    private static readonly string[] RequiredVehicleColumns =
    [
        "vmf_code",
        "fleet_number",
        "registration_number",
        "vehicle_status_code",
        "renumbered_to"
    ];

    private static readonly string[] RequiredCopyColumns =
    [
        "model_code",
        "type_code",
        "location_code",
        "take_on_date",
        "take_on_odo",
        "current_odo",
        "vehicle_status_date",
        "chassis_number",
        "engine_number_1",
        "previos_gg_number",
        "followup_gg_number",
        "year_manufactured",
        "colour",
        "gvm",
        "tare",
        "purchase_date",
        "purchase_amount",
        "purchased_from",
        "licence_due_date",
        "additional_fuel_tank",
        "Licence_receiver",
        "lic_register_number",
        "licence_comments",
        "user_access_code"
    ];

    private static readonly string[] CopyableVehicleColumns =
    [
        "model_code", "type_code", "location_code", "take_on_date", "take_on_odo", "current_odo",
        "odo_adjustment", "derived_odo", "odo_update_date", "engine_number_1", "chassis_number", "tare",
        "gvm", "year_manufactured", "optional_extras", "licence_due_date", "additional_fuel_tank",
        "average_consumption", "fuel_card_number", "fuel_card_date", "purchase_date", "purchase_amount",
        "book_value", "book_value_date", "maint_card_number", "maint_card_exdate", "purchased_from",
        "sold_to", "sold_date", "sold_amount", "service_last_done", "service_last_odo", "cof_last_done",
        "cof_required", "cof_number", "operator_card_number", "monthly_overhead", "colour", "tow_hitch",
        "canopy", "Cof_amount", "Licence_receiver", "Licence_receiver_id", "Licence_receiver_tel",
        "Licence_receiver_site", "Licence_date_taken", "highest_km", "fuel_ltd", "fuel_ytd",
        "fuel_3month_average", "oil_ltd", "oil_ytd", "oil_3month_average", "maint_ltd", "maint_ytd",
        "maint_3month_average", "repairs_ltd", "repairs_ytd", "repairs_3month_average", "tyres_ltd",
        "tyres_ytd", "tyres_3month_average", "accident_ltd", "accident_ytd", "accident_3month_average",
        "toll_ltd", "toll_ytd", "toll_3month_average", "other_ltd", "other_ytd", "other_3month_average",
        "km_ltd", "km_ytd", "km_3month_average", "lic_register_number", "lic_registration_doc",
        "licence_comments", "default_site", "previos_gg_number", "followup_gg_number", "vehicle_status_date",
        "barcode", "user_access_code", "captured_date", "reserved", "LPG", "extended_service",
        "destroyed_date", "destroyed_amount", "destroyed_receipt", "previos_gg_number_2", "date_First_Regist",
        "vs_code", "invoice_number", "RelieveVehicle", "initial_site_code", "veh_site_code", "temp_vmf_code",
        "supplier_id"
    ];

    private static readonly string[] RequiredHistoryColumns =
    [
        "hist_vmf_code",
        "hist_fleet_number",
        "hist_vehicle_status_code",
        "hist_date_changed",
        "hist_user_access_code"
    ];

    private static readonly IReadOnlyList<RecoveredVehicleStatusOption> DefaultStatusOptions =
    [
        new(1, "In Service"),
        new(2, "Withdrawn"),
        new(3, "Board of Survey"),
        new(4, "Stolen"),
        new(5, "Sold"),
        new(6, "Transferred"),
        new(7, "Subsidized"),
        new(8, "From Focus"),
        new(9, "Privatised"),
        new(10, "Recovered"),
        new(11, "Missing"),
        new(12, "Destroyed")
    ];

    private readonly FisDbContext _context;

    public RecoveredVehicleRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<RecoveredVehicleSearchRecord>> SearchAsync(string searchTerm, bool byRegistration)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return [];
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var vehicleColumns = await GetColumnsAsync(connection, VehicleTableName, RequiredVehicleColumns, transaction);
            var statusColumns = await GetColumnsAsync(connection, StatusTableName, transaction);
            var rows = await QuerySearchAsync(connection, transaction, vehicleColumns, searchTerm.Trim(), byRegistration);
            var statusOptions = await QueryStatusOptionsAsync(connection, transaction, statusColumns);
            var statusDescriptions = statusOptions.ToDictionary(option => option.Code, option => option.Description);

            return rows
                .Select(row => row with
                {
                    StatusDescription = statusDescriptions.TryGetValue(row.VehicleStatusCode, out var description)
                        ? description
                        : GetDefaultStatusDescription(row.VehicleStatusCode)
                })
                .ToList();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<RecoveredVehicleDetails?> GetDetailsAsync(int vmfCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var vehicleColumns = await GetColumnsAsync(connection, VehicleTableName, RequiredVehicleColumns, transaction);
            var historyColumns = await GetColumnsAsync(connection, HistoryTableName, transaction);
            var statusColumns = await GetColumnsAsync(connection, StatusTableName, transaction);
            return await QueryDetailsAsync(connection, transaction, vehicleColumns, historyColumns, statusColumns, vmfCode);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<RecoveredVehicleUpdateResult> UpdateAsync(RecoveredVehicleUpdate update, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(update);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        await using var ownedTransaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var vehicleColumns = await GetColumnsAsync(connection, VehicleTableName, RequiredVehicleColumns.Concat(RequiredCopyColumns).Distinct().ToArray(), ownedTransaction);
            var historyColumns = await GetColumnsAsync(connection, HistoryTableName, RequiredHistoryColumns, ownedTransaction);
            var oldVehicle = await QueryVehicleForUpdateAsync(connection, ownedTransaction, vehicleColumns, update.VmfCode);
            if (oldVehicle is null)
            {
                throw new KeyNotFoundException($"Vehicle with vmf_code {update.VmfCode} was not found.");
            }

            if (!string.IsNullOrWhiteSpace(oldVehicle.RenumberedTo))
            {
                throw new InvalidOperationException("This vehicle has already been renumbered.");
            }

            if (string.Equals(oldVehicle.FleetNumber, update.RecoveredFleetNumber, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The recovered GG number must be different from the stolen vehicle's GG number.");
            }

            if (await FleetNumberExistsAsync(connection, ownedTransaction, vehicleColumns, update.RecoveredFleetNumber, update.VmfCode))
            {
                throw new InvalidOperationException($"The recovered GG number {update.RecoveredFleetNumber} already exists.");
            }

            await UpdateOriginalVehicleAsync(connection, ownedTransaction, vehicleColumns, update, currentUserId);
            var newVmfCode = await InsertRecoveredVehicleAsync(
                connection,
                ownedTransaction,
                vehicleColumns,
                oldVehicle,
                update,
                currentUserId);

            await InsertHistoryAsync(
                connection,
                ownedTransaction,
                historyColumns,
                newVmfCode,
                oldVehicle.FleetNumber,
                null,
                update.DateChanged,
                currentUserId);
            await InsertHistoryAsync(
                connection,
                ownedTransaction,
                historyColumns,
                newVmfCode,
                null,
                oldVehicle.VehicleStatusCode,
                update.DateChanged,
                currentUserId);

            await ownedTransaction.CommitAsync();

            var statusColumns = await GetColumnsAsync(connection, StatusTableName, transaction: null);
            var updatedVehicle = await QueryDetailsAsync(
                connection,
                transaction: null,
                vehicleColumns,
                historyColumns,
                statusColumns,
                update.VmfCode)
                ?? throw new InvalidOperationException("The recovered vehicle update completed but the original vehicle could not be reloaded.");

            return new RecoveredVehicleUpdateResult(updatedVehicle, newVmfCode);
        }
        catch
        {
            try
            {
                await ownedTransaction.RollbackAsync();
            }
            catch
            {
                // Preserve the original transaction error.
            }

            throw;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<List<RecoveredVehicleSearchRecord>> QuerySearchAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> columns,
        string searchTerm,
        bool byRegistration)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var searchColumn = byRegistration ? "registration_number" : "fleet_number";
        command.CommandText = $"""
            SELECT TOP (50)
                   [vmf_code] AS [vmf_code],
                   [fleet_number] AS [fleet_number],
                   [registration_number] AS [registration_number],
                   [vehicle_status_code] AS [vehicle_status_code],
                   [renumbered_to] AS [renumbered_to]
            FROM [dbo].[{VehicleTableName}]
            WHERE {GetNotDeletedFilter(string.Empty, columns)}
              AND LOWER(COALESCE([{searchColumn}], '')) LIKE @search
            ORDER BY [fleet_number], [vmf_code]
            """;
        AddParameter(command, "@search", DbType.String, $"%{searchTerm.ToLowerInvariant()}%");

        var results = new List<RecoveredVehicleSearchRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new RecoveredVehicleSearchRecord(
                ReadInt32(reader, "vmf_code") ?? 0,
                ReadString(reader, "fleet_number"),
                ReadString(reader, "registration_number"),
                ReadInt16(reader, "vehicle_status_code") ?? 0,
                null,
                ReadString(reader, "renumbered_to")));
        }

        return results;
    }

    private static async Task<RecoveredVehicleDetails?> QueryDetailsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> vehicleColumns,
        IReadOnlySet<string> historyColumns,
        IReadOnlySet<string> statusColumns,
        int vmfCode)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT [vmf_code] AS [vmf_code],
                   [fleet_number] AS [fleet_number],
                   [registration_number] AS [registration_number],
                   [vehicle_status_code] AS [vehicle_status_code],
                   [renumbered_to] AS [renumbered_to]
            FROM [dbo].[{VehicleTableName}]
            WHERE [vmf_code] = @vmfCode
              AND {GetNotDeletedFilter(string.Empty, vehicleColumns)}
            """;
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);

        int? loadedVmfCode = null;
        string? fleetNumber = null;
        string? registrationNumber = null;
        short vehicleStatusCode = 0;
        string? renumberedTo = null;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                return null;
            }

            loadedVmfCode = ReadInt32(reader, "vmf_code");
            fleetNumber = ReadString(reader, "fleet_number");
            registrationNumber = ReadString(reader, "registration_number");
            vehicleStatusCode = ReadInt16(reader, "vehicle_status_code") ?? 0;
            renumberedTo = ReadString(reader, "renumbered_to");
        }

        var statusOptions = await QueryStatusOptionsAsync(connection, transaction, statusColumns);
        var statusDescription = statusOptions.FirstOrDefault(option => option.Code == vehicleStatusCode)?.Description;
        if (string.IsNullOrWhiteSpace(statusDescription))
        {
            statusDescription = GetDefaultStatusDescription(vehicleStatusCode);
        }

        var previous = await QueryPreviousHistoryAsync(connection, transaction, historyColumns, vmfCode);
        return new RecoveredVehicleDetails(
            loadedVmfCode ?? vmfCode,
            fleetNumber,
            registrationNumber,
            vehicleStatusCode,
            statusDescription,
            renumberedTo,
            previous.FleetNumber,
            previous.DateChanged,
            statusOptions);
    }

    private static async Task<RecoveredVehicleSearchRecord?> QueryVehicleForUpdateAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        int vmfCode)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT [vmf_code] AS [vmf_code],
                   [fleet_number] AS [fleet_number],
                   [registration_number] AS [registration_number],
                   [vehicle_status_code] AS [vehicle_status_code],
                   [renumbered_to] AS [renumbered_to]
            FROM [dbo].[{VehicleTableName}]
            WHERE [vmf_code] = @vmfCode
              AND {GetNotDeletedFilter(string.Empty, columns)}
            """;
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new RecoveredVehicleSearchRecord(
                ReadInt32(reader, "vmf_code") ?? vmfCode,
                ReadString(reader, "fleet_number"),
                ReadString(reader, "registration_number"),
                ReadInt16(reader, "vehicle_status_code") ?? 0,
                null,
                ReadString(reader, "renumbered_to"))
            : null;
    }

    private static async Task<bool> FleetNumberExistsAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        string fleetNumber,
        int excludedVmfCode)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT COUNT(1)
            FROM [dbo].[{VehicleTableName}]
            WHERE [vmf_code] <> @excludedVmfCode
              AND [fleet_number] = @fleetNumber
              AND {GetNotDeletedFilter(string.Empty, columns)}
            """;
        AddParameter(command, "@excludedVmfCode", DbType.Int32, excludedVmfCode);
        AddParameter(command, "@fleetNumber", DbType.String, fleetNumber);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task UpdateOriginalVehicleAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        RecoveredVehicleUpdate update,
        int currentUserId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var assignments = new List<string>
        {
            "[renumbered_to] = @renumberedTo",
            "[vehicle_status_code] = @stolenStatusCode"
        };
        AddParameter(command, "@renumberedTo", DbType.String, update.RecoveredFleetNumber);
        AddParameter(command, "@stolenStatusCode", DbType.Int16, 10);

        if (columns.Contains("date_updated"))
        {
            assignments.Add("[date_updated] = @dateUpdated");
            AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
        }

        if (columns.Contains("modified_by_user_code"))
        {
            assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
            AddParameter(command, "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        }

        command.CommandText = $"""
            UPDATE [dbo].[{VehicleTableName}]
            SET {string.Join(", ", assignments)}
            WHERE [vmf_code] = @vmfCode
              AND {GetNotDeletedFilter(string.Empty, columns)}
            """;
        AddParameter(command, "@vmfCode", DbType.Int32, update.VmfCode);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException($"Vehicle with vmf_code {update.VmfCode} was not found.");
        }
    }

    private static async Task<int> InsertRecoveredVehicleAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        RecoveredVehicleSearchRecord oldVehicle,
        RecoveredVehicleUpdate update,
        int currentUserId)
    {
        var insertColumns = new List<string>();
        var selectExpressions = new List<string>();
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        var now = DateTime.UtcNow;

        foreach (var column in CopyableVehicleColumns.Where(columns.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            insertColumns.Add($"[{column}]");
            switch (column.ToLowerInvariant())
            {
                case "vehicle_status_date":
                    selectExpressions.Add("@vehicleStatusDate");
                    break;
                default:
                    selectExpressions.Add($"[source].[{column}]");
                    break;
            }
        }

        insertColumns.Insert(0, "[fleet_number]");
        selectExpressions.Insert(0, "@fleetNumber");
        insertColumns.Insert(1, "[registration_number]");
        selectExpressions.Insert(1, "@registrationNumber");
        insertColumns.Insert(2, "[vehicle_status_code]");
        selectExpressions.Insert(2, "@vehicleStatusCode");

        if (columns.Contains("date_created"))
        {
            insertColumns.Add("[date_created]");
            selectExpressions.Add("@dateCreated");
        }

        if (columns.Contains("created_by_user_code"))
        {
            insertColumns.Add("[created_by_user_code]");
            selectExpressions.Add("@createdByUserCode");
        }

        if (columns.Contains("is_deleted"))
        {
            insertColumns.Add("[is_deleted]");
            selectExpressions.Add("@isDeleted");
        }

        command.CommandText = $"""
            INSERT INTO [dbo].[{VehicleTableName}] ({string.Join(", ", insertColumns)})
            OUTPUT INSERTED.[vmf_code]
            SELECT {string.Join(", ", selectExpressions)}
            FROM [dbo].[{VehicleTableName}] AS [source]
            WHERE [source].[vmf_code] = @sourceVmfCode
            """;
        AddParameter(command, "@fleetNumber", DbType.String, update.RecoveredFleetNumber);
        AddParameter(command, "@registrationNumber", DbType.String, update.RecoveredFleetNumber);
        AddParameter(command, "@vehicleStatusCode", DbType.Int16, update.NewStatusCode);
        AddParameter(command, "@vehicleStatusDate", DbType.DateTime2, update.DateChanged);
        AddParameter(command, "@dateCreated", DbType.DateTime2, now);
        AddParameter(command, "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        AddParameter(command, "@isDeleted", DbType.Boolean, false);
        AddParameter(command, "@sourceVmfCode", DbType.Int32, oldVehicle.VmfCode);

        try
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            await command.DisposeAsync();
        }
    }

    private static async Task InsertHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        int vmfCode,
        string? fleetNumber,
        short? statusCode,
        DateTime dateChanged,
        int currentUserId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var insertColumns = new List<string>
        {
            "[hist_vmf_code]",
            "[hist_fleet_number]",
            "[hist_vehicle_status_code]",
            "[hist_date_changed]",
            "[hist_user_access_code]"
        };
        var values = new List<string>
        {
            "@histVmfCode",
            "@histFleetNumber",
            "@histStatusCode",
            "@histDateChanged",
            "@histUserAccessCode"
        };

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
            values.Add("@isDeleted");
        }

        command.CommandText = $"INSERT INTO [dbo].[{HistoryTableName}] ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", values)})";
        AddParameter(command, "@histVmfCode", DbType.Int32, vmfCode);
        AddParameter(command, "@histFleetNumber", DbType.String, fleetNumber);
        AddParameter(command, "@histStatusCode", DbType.Int16, statusCode);
        AddParameter(command, "@histDateChanged", DbType.DateTime2, dateChanged);
        AddParameter(command, "@histUserAccessCode", DbType.Int16, currentUserId > 0 ? currentUserId : null);
        AddParameter(command, "@dateCreated", DbType.DateTime2, DateTime.UtcNow);
        AddParameter(command, "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        AddParameter(command, "@isDeleted", DbType.Boolean, false);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(string? FleetNumber, DateTime? DateChanged)> QueryPreviousHistoryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> columns,
        int vmfCode)
    {
        if (!RequiredHistoryColumns.All(columns.Contains))
        {
            return (null, null);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var deletedFilter = columns.Contains("is_deleted") ? "AND ([is_deleted] = 0 OR [is_deleted] IS NULL)" : string.Empty;
        var tieBreaker = columns.Contains("hist_code") ? ", [hist_code] DESC" : string.Empty;
        command.CommandText = $"""
            SELECT TOP (1) [hist_fleet_number] AS [hist_fleet_number],
                           [hist_date_changed] AS [hist_date_changed]
            FROM [dbo].[{HistoryTableName}]
            WHERE [hist_vmf_code] = @vmfCode
              AND NULLIF(LTRIM(RTRIM([hist_fleet_number])), '') IS NOT NULL
              {deletedFilter}
            ORDER BY [hist_date_changed] DESC{tieBreaker}
            """;
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? (ReadString(reader, "hist_fleet_number"), ReadDateTime(reader, "hist_date_changed"))
            : (null, null);
    }

    private static async Task<IReadOnlyList<RecoveredVehicleStatusOption>> QueryStatusOptionsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> columns)
    {
        if (!columns.Contains("vehicle_status_code") || !columns.Contains("status_description"))
        {
            return DefaultStatusOptions;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT [vehicle_status_code] AS [status_code],
                   [status_description] AS [status_description]
            FROM [dbo].[{StatusTableName}]
            ORDER BY [vehicle_status_code]
            """;

        var results = new List<RecoveredVehicleStatusOption>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = ReadInt16(reader, "status_code");
            var description = ReadString(reader, "status_description");
            if (code.HasValue && !string.IsNullOrWhiteSpace(description))
            {
                results.Add(new RecoveredVehicleStatusOption(code.Value, description));
            }
        }

        return results.Count > 0 ? results : DefaultStatusOptions;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        DbTransaction? transaction)
        => await GetColumnsAsync(connection, tableName, null, transaction);

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyCollection<string>? requiredColumns,
        DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema
              AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        if (requiredColumns is not null)
        {
            var missing = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException($"The required recovered vehicle compatibility columns are not available on {tableName}: {string.Join(", ", missing)}");
            }
        }

        return columns;
    }

    private static string GetNotDeletedFilter(string alias, IReadOnlySet<string> columns)
    {
        var prefix = string.IsNullOrEmpty(alias) ? string.Empty : $"[{alias}].";
        return columns.Contains("is_deleted")
            ? $"({prefix}[is_deleted] = 0 OR {prefix}[is_deleted] IS NULL)"
            : "1 = 1";
    }

    private static string? GetDefaultStatusDescription(short statusCode)
        => DefaultStatusOptions.FirstOrDefault(option => option.Code == statusCode)?.Description;

    private static string? ReadString(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : reader[column]?.ToString();

    private static int? ReadInt32(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static short? ReadInt16(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
