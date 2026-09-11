using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Vehicle repository compatible with both the original client
/// <c>vehicle_master</c> table and databases containing the optional modern
/// columns. EF cannot conditionally omit mapped columns from a projection, so
/// this repository negotiates the available schema before composing SQL.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class VehicleRepository : IVehicleRepository
{
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const string TypeTableName = "type";
    private const string VehicleStatusTableName = "vehicle_status";
    private const string VehicleSourceTableName = "vehicle_source";

    private static readonly string[] LegacyColumns =
    [
        "vmf_code",
        "model_code",
        "type_code",
        "vehicle_status_code",
        "location_code",
        "fleet_number",
        "registration_number",
        "take_on_date",
        "take_on_odo",
        "current_odo",
        "odo_adjustment",
        "derived_odo",
        "odo_update_date",
        "engine_number_1",
        "chassis_number",
        "tare",
        "gvm",
        "year_manufactured",
        "optional_extras",
        "licence_due_date",
        "additional_fuel_tank",
        "average_consumption",
        "fuel_card_number",
        "fuel_card_date",
        "purchase_date",
        "purchase_amount",
        "book_value",
        "book_value_date",
        "maint_card_number",
        "maint_card_exdate",
        "purchased_from",
        "sold_to",
        "sold_date",
        "sold_amount",
        "service_last_done",
        "service_last_odo",
        "cof_last_done",
        "cof_required",
        "cof_number",
        "operator_card_number",
        "monthly_overhead",
        "colour",
        "tow_hitch",
        "canopy",
        "Cof_amount",
        "Licence_receiver",
        "Licence_receiver_id",
        "Licence_receiver_tel",
        "Licence_receiver_site",
        "Licence_date_taken",
        "highest_km",
        "fuel_ltd",
        "fuel_ytd",
        "fuel_3month_average",
        "oil_ltd",
        "oil_ytd",
        "oil_3month_average",
        "maint_ltd",
        "maint_ytd",
        "maint_3month_average",
        "repairs_ltd",
        "repairs_ytd",
        "repairs_3month_average",
        "tyres_ltd",
        "tyres_ytd",
        "tyres_3month_average",
        "accident_ltd",
        "accident_ytd",
        "accident_3month_average",
        "toll_ltd",
        "toll_ytd",
        "toll_3month_average",
        "other_ltd",
        "other_ytd",
        "other_3month_average",
        "km_ltd",
        "km_ytd",
        "km_3month_average",
        "lic_register_number",
        "lic_registration_doc",
        "licence_comments",
        "default_site",
        "previos_gg_number",
        "followup_gg_number",
        "vehicle_status_date",
        "renumbered_to",
        "barcode",
        "user_access_code",
        "captured_date",
        "reserved",
        "LPG",
        "extended_service",
        "destroyed_date",
        "destroyed_amount",
        "destroyed_receipt",
        "previos_gg_number_2",
        "date_First_Regist",
        "vs_code",
        "invoice_number",
        "RelieveVehicle",
        "initial_site_code",
        "veh_site_code",
        "temp_vmf_code",
    ];

    private static readonly string[] OptionalColumns =
    [
        "supplier_id",
        "ifms_vehicle_register_number",
        "natis_model_number",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredColumns =
    [
        "vmf_code",
        "model_code",
        "type_code",
        "vehicle_status_code",
        "location_code",
        "fleet_number",
        "registration_number",
        "take_on_date",
        "take_on_odo",
        "current_odo",
    ];

    private static readonly string[] RequiredModelColumns =
    [
        "model_code",
        "make_code",
        "model_description",
    ];

    private static readonly IReadOnlyDictionary<string, PropertyInfo> VehicleProperties =
        typeof(Vehicle)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => new
            {
                Property = property,
                Column = property.GetCustomAttribute<ColumnAttribute>()?.Name,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Column))
            .ToDictionary(
                item => item.Column!,
                item => item.Property,
                StringComparer.OrdinalIgnoreCase
            );

    private readonly FisDbContext _context;

    public VehicleRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Vehicle?> GetByIdAsync(int vmfCode)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        return (
            await QueryAsync(
                $"WHERE [v].[vmf_code] = @vmfCode AND {GetActiveFilter("v", availableColumns)}",
                command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
                availableColumns
            )
        ).SingleOrDefault();
    }

    public async Task<Vehicle?> GetByFleetNumberAsync(string fleetNumber)
    {
        if (string.IsNullOrWhiteSpace(fleetNumber))
        {
            return null;
        }

        var availableColumns = await GetAvailableColumnsAsync();
        return (
            await QueryAsync(
                $"WHERE [v].[fleet_number] = @fleetNumber AND {GetActiveFilter("v", availableColumns)}",
                command => AddParameter(command, "@fleetNumber", DbType.String, fleetNumber),
                availableColumns
            )
        ).SingleOrDefault();
    }

    public async Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return null;
        }

        var availableColumns = await GetAvailableColumnsAsync();
        return (
            await QueryAsync(
                $"WHERE [v].[registration_number] = @registrationNumber AND {GetActiveFilter("v", availableColumns)}",
                command =>
                    AddParameter(command, "@registrationNumber", DbType.String, registrationNumber),
                availableColumns
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<Vehicle>> GetActiveVehiclesAsync()
    {
        var availableColumns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"WHERE [v].[vehicle_status_code] > 0 AND {GetActiveFilter("v", availableColumns)} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            knownColumns: availableColumns
        );
    }

    public async Task<VehicleMasterSnapshotPage> GetSnapshotPageAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var availableColumns = await GetAvailableColumnsAsync();
        var activePredicate =
            $"[v].[vehicle_status_code] > 0 AND {GetActiveFilter("v", availableColumns)}";
        var totalRecords = await CountActiveVehiclesAsync(availableColumns);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        var data = await QueryAsync(
            $"WHERE {activePredicate} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code] OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY",
            command =>
            {
                AddParameter(command, "@skip", DbType.Int64, skip);
                AddParameter(command, "@pageSize", DbType.Int32, pageSize);
            },
            availableColumns
        );

        return new VehicleMasterSnapshotPage(data, page, pageSize, totalRecords);
    }

    public async Task<RenumberedVehicleReportPage> GetRenumberedVehicleReportPageAsync(
        int page,
        int pageSize
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var vehicleColumns = await GetAvailableColumnsAsync();
            if (!vehicleColumns.Contains("renumbered_to"))
            {
                return new RenumberedVehicleReportPage([], 1, pageSize, 0);
            }

            var statusColumns = await GetTableColumnsAsync(VehicleStatusTableName);
            var hasStatusLookup =
                statusColumns.Contains("vehicle_status_code")
                && statusColumns.Contains("status_description");
            var renumberedPredicate =
                "[old].[renumbered_to] IS NOT NULL AND [old].[renumbered_to] <> ''";
            var oldStatusJoin = hasStatusLookup
                ? $"INNER JOIN [dbo].[{VehicleStatusTableName}] AS [old_status] ON [old_status].[vehicle_status_code] = [old].[vehicle_status_code]"
                : string.Empty;

            int total;
            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                countCommand.CommandText = $"""
                    SELECT COUNT(1)
                    FROM [dbo].[{VehicleTableName}] AS [old]
                    {oldStatusJoin}
                    WHERE {renumberedPredicate}
                    """;
                total = Convert.ToInt32(
                    await countCommand.ExecuteScalarAsync(),
                    CultureInfo.InvariantCulture
                );
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);
            var skip = (long)(page - 1) * pageSize;
            var oldStatusProjection = hasStatusLookup
                ? "[old_status].[status_description]"
                : "CAST(NULL AS varchar(255))";
            var newStatusProjection = hasStatusLookup
                ? "[new_status].[status_description]"
                : "CAST(NULL AS varchar(255))";
            var replacementJoin = $"""
                OUTER APPLY (
                    SELECT TOP (1) [new].[vehicle_status_code]
                    FROM [dbo].[{VehicleTableName}] AS [new]
                    WHERE [new].[fleet_number] = [old].[renumbered_to]
                    ORDER BY [new].[vmf_code]
                ) AS [new]
                """;
            var statusJoins = hasStatusLookup
                ? $"""
                    LEFT JOIN [dbo].[{VehicleStatusTableName}] AS [new_status]
                        ON [new_status].[vehicle_status_code] = [new].[vehicle_status_code]
                    """
                : string.Empty;

            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT
                    [old].[vmf_code] AS [old_vmf_code],
                    [old].[fleet_number] AS [old_fleet_number],
                    {oldStatusProjection} AS [old_status_description],
                    [old].[renumbered_to] AS [new_fleet_number],
                    {newStatusProjection} AS [new_status_description]
                FROM [dbo].[{VehicleTableName}] AS [old]
                {oldStatusJoin}
                {replacementJoin}
                {statusJoins}
                WHERE {renumberedPredicate}
                ORDER BY [old].[fleet_number], [old].[vmf_code]
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddParameter(command, "@skip", DbType.Int64, skip);
            AddParameter(command, "@pageSize", DbType.Int32, pageSize);

            var items = new List<RenumberedVehicleReportRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(
                    new RenumberedVehicleReportRow(
                        ReadInt32(reader, "old_vmf_code") ?? 0,
                        ReadString(reader, "old_fleet_number"),
                        ReadString(reader, "old_status_description"),
                        ReadString(reader, "new_fleet_number"),
                        ReadString(reader, "new_status_description")
                    )
                );
            }

            return new RenumberedVehicleReportPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<VehicleLookupPage> GetVehicleLookupPageAsync(
        string? keyword,
        string? searchMode,
        int page,
        int pageSize
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        keyword = keyword?.Trim();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new VehicleLookupPage([], 1, pageSize, 0);
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var vehicleColumns = await GetAvailableColumnsAsync();
            var modelColumns = await GetTableColumnsAsync(ModelTableName);
            var typeColumns = await GetTableColumnsAsync(TypeTableName);
            var statusColumns = await GetTableColumnsAsync(VehicleStatusTableName);
            var sourceColumns = await GetTableColumnsAsync(VehicleSourceTableName);

            var whereClause = BuildVehicleLookupWhereClause(vehicleColumns, searchMode);
            var total = await CountVehicleLookupRowsAsync(whereClause, keyword);
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);
            var skip = (long)(page - 1) * pageSize;

            var hasModel =
                modelColumns.Contains("model_code") && modelColumns.Contains("model_description");
            var hasType =
                typeColumns.Contains("type_code") && typeColumns.Contains("type_description");
            var hasStatus =
                statusColumns.Contains("vehicle_status_code")
                && statusColumns.Contains("status_description");
            var hasSource =
                vehicleColumns.Contains("vs_code")
                && sourceColumns.Contains("vs_code")
                && sourceColumns.Contains("name");

            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT
                    [v].[vmf_code] AS [vmf_code],
                    {GetColumnProjection("v", "fleet_number", vehicleColumns)},
                    {GetColumnProjection("v", "registration_number", vehicleColumns)},
                    {GetColumnProjection("v", "model_code", vehicleColumns)},
                    {GetColumnProjection("v", "year_manufactured", vehicleColumns)},
                    {GetColumnProjection("v", "colour", vehicleColumns)},
                    {GetColumnProjection("v", "vehicle_status_date", vehicleColumns)},
                    {(
                    hasModel ? "[m].[model_description]" : "CAST(NULL AS varchar(100))"
                )} AS [make_and_model],
                    {(
                    hasType ? "[t].[type_description]" : "CAST(NULL AS varchar(255))"
                )} AS [hire_type],
                    {(
                    hasStatus ? "[s].[status_description]" : "CAST(NULL AS varchar(255))"
                )} AS [status],
                    {(hasSource ? "[vs].[name]" : "CAST(NULL AS varchar(255))")} AS [hired_from]
                FROM [dbo].[{VehicleTableName}] AS [v]
                {(
                    hasModel
                        ? $"LEFT JOIN [dbo].[{ModelTableName}] AS [m] ON [m].[model_code] = [v].[model_code] {GetLookupActiveFilter("m", modelColumns)}"
                        : string.Empty
                )}
                {(
                    hasType
                        ? $"LEFT JOIN [dbo].[{TypeTableName}] AS [t] ON [t].[type_code] = [v].[type_code] {GetLookupActiveFilter("t", typeColumns)}"
                        : string.Empty
                )}
                {(
                    hasStatus
                        ? $"LEFT JOIN [dbo].[{VehicleStatusTableName}] AS [s] ON [s].[vehicle_status_code] = [v].[vehicle_status_code] {GetLookupActiveFilter("s", statusColumns)}"
                        : string.Empty
                )}
                {(
                    hasSource
                        ? $"LEFT JOIN [dbo].[{VehicleSourceTableName}] AS [vs] ON [vs].[vs_code] = [v].[vs_code] {GetLookupActiveFilter("vs", sourceColumns)}"
                        : string.Empty
                )}
                WHERE {whereClause}
                ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddParameter(command, "@keyword", DbType.String, BuildLikeParameter(keyword));
            AddParameter(command, "@skip", DbType.Int64, skip);
            AddParameter(command, "@pageSize", DbType.Int32, pageSize);

            var items = new List<VehicleLookupPageItem>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(
                    new VehicleLookupPageItem(
                        ReadInt32(reader, "vmf_code") ?? 0,
                        ReadString(reader, "fleet_number"),
                        ReadString(reader, "registration_number"),
                        ReadString(reader, "make_and_model"),
                        ReadInt16(reader, "year_manufactured"),
                        ReadString(reader, "colour"),
                        ReadString(reader, "hire_type"),
                        ReadString(reader, "status"),
                        ReadString(reader, "hired_from"),
                        ReadDateTime(reader, "vehicle_status_date"),
                        ReadInt16(reader, "model_code") ?? 0
                    )
                );
            }

            return new VehicleLookupPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync() =>
        await GetActiveVehiclesAsync();

    public async Task<IEnumerable<Vehicle>> GetAllAsync() =>
        await QueryAsync("ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]");

    public async Task<IEnumerable<Vehicle>> SearchVehiclesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetActiveVehiclesAsync();
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var term = $"%{searchTerm.Trim().ToLowerInvariant()}%";
        return await QueryAsync(
            "WHERE ("
                + string.Join(
                    " OR ",
                    [
                        "LOWER(COALESCE([v].[fleet_number], '')) LIKE @searchTerm",
                        "LOWER(COALESCE([v].[registration_number], '')) LIKE @searchTerm",
                        "LOWER(COALESCE([v].[chassis_number], '')) LIKE @searchTerm",
                        "LOWER(COALESCE([v].[engine_number_1], '')) LIKE @searchTerm",
                        "LOWER(COALESCE([v].[invoice_number], '')) LIKE @searchTerm",
                    ]
                )
                + ") ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            command => AddParameter(command, "@searchTerm", DbType.String, term),
            availableColumns
        );
    }

    public async Task<IEnumerable<Vehicle>> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return [];
        }

        var availableColumns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"WHERE [v].[invoice_number] = @invoiceNumber AND {GetActiveFilter("v", availableColumns)} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            command => AddParameter(command, "@invoiceNumber", DbType.String, invoiceNumber),
            availableColumns
        );
    }

    public async Task<Vehicle> CreateAsync(Vehicle vehicle, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        vehicle.date_created = now;
        vehicle.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        vehicle.is_deleted = false;

        var values = BuildWriteValues(vehicle, availableColumns, includeKey: false);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{VehicleTableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[vmf_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            vehicle.vmf_code = Convert.ToInt32(
                await command.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
            );
            return vehicle;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task UpdateAsync(Vehicle vehicle, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        var availableColumns = await GetAvailableColumnsAsync();
        _ =
            await GetByIdIncludingDeletedAsync(vehicle.vmf_code, availableColumns)
            ?? throw new InvalidOperationException(
                $"Vehicle with vmf_code {vehicle.vmf_code} not found"
            );
        var values = BuildWriteValues(vehicle, availableColumns, includeKey: false);
        if (values.Count == 0)
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{VehicleTableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [vmf_code] = @vmfCode
                """;
            AddParameters(command, values);
            AddParameter(command, "@vmfCode", DbType.Int32, vehicle.vmf_code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task UpdateLicenceFieldsAsync(
        int vmfCode,
        VehicleLicenceUpdate update,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(update);

        var availableColumns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddColumnValue(
            values,
            availableColumns,
            "licence_due_date",
            DbType.DateTime2,
            update.LicenceDueDate
        );
        AddColumnValue(
            values,
            availableColumns,
            "lic_register_number",
            DbType.String,
            update.LicenceRegisterNumber
        );
        AddColumnValue(
            values,
            availableColumns,
            "lic_registration_doc",
            DbType.String,
            update.LicenceRegistrationDocument
        );
        AddColumnValue(values, availableColumns, "tare", DbType.Int32, update.Tare);
        AddColumnValue(
            values,
            availableColumns,
            "Licence_receiver",
            DbType.String,
            update.LicenceReceiver
        );
        AddColumnValue(
            values,
            availableColumns,
            "Licence_receiver_id",
            DbType.String,
            update.LicenceReceiverId
        );
        AddColumnValue(
            values,
            availableColumns,
            "Licence_receiver_tel",
            DbType.String,
            update.LicenceReceiverTelephone
        );
        AddColumnValue(
            values,
            availableColumns,
            "Licence_receiver_site",
            DbType.Int16,
            update.LicenceReceiverSite
        );
        AddColumnValue(
            values,
            availableColumns,
            "Licence_date_taken",
            DbType.DateTime2,
            update.LicenceDateTaken
        );
        AddColumnValue(values, availableColumns, "cof_required", DbType.String, update.CofRequired);
        AddColumnValue(
            values,
            availableColumns,
            "cof_last_done",
            DbType.DateTime2,
            update.CofLastDone
        );
        AddColumnValue(
            values,
            availableColumns,
            "licence_comments",
            DbType.String,
            update.LicenceComments
        );
        AddColumnValue(
            values,
            availableColumns,
            "date_updated",
            DbType.DateTime2,
            DateTime.UtcNow,
            includeNull: false
        );
        AddColumnValue(
            values,
            availableColumns,
            "modified_by_user_code",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null,
            includeNull: false
        );

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "The vehicle_master table has no licence fields available for update."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{VehicleTableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [vmf_code] = @vmfCode
                AND {GetActiveFilter("", availableColumns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new KeyNotFoundException($"Vehicle with vmf_code {vmfCode} was not found.");
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task AddLicenceReceiveNoteAsync(int vmfCode, string username, int currentUserId)
    {
        var columns = await GetTableColumnsAsync("fleet_notes");
        if (
            !columns.Contains("vmf_code")
            || (!columns.Contains("notes") && !columns.Contains("fleet_note"))
        )
        {
            return;
        }

        var noteColumn = columns.Contains("notes") ? "notes" : "fleet_note";
        var values = new List<WriteValue>
        {
            new("vmf_code", "@vmfCode", DbType.Int32, vmfCode),
            new(noteColumn, "@note", DbType.String, $"Licence Receive: {username}"),
        };
        AddOptionalColumnValue(
            values,
            columns,
            "update_date",
            "@updateDate",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddOptionalColumnValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddOptionalColumnValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddOptionalColumnValue(
            values,
            columns,
            "created_by_user_code",
            "@createdBy",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalColumnValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedBy",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalColumnValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"INSERT INTO [dbo].[fleet_notes] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
            AddParameters(command, values);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task DeleteAsync(int vmfCode, int currentUserId)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            if (availableColumns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText = $"""
                    UPDATE [dbo].[{VehicleTableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [vmf_code] = @vmfCode
                    AND {GetActiveFilter("", availableColumns)}
                    """;
            }
            else
            {
                // The client-era table has no deletion marker. Preserve the
                // endpoint's legacy hard-delete behavior only on that schema.
                command.CommandText = $"""
                    DELETE FROM [dbo].[{VehicleTableName}]
                    WHERE [vmf_code] = @vmfCode
                    """;
            }

            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<Vehicle?> GetByIdIncludingDeletedAsync(
        int vmfCode,
        IReadOnlySet<string> availableColumns
    ) =>
        (
            await QueryAsync(
                "WHERE [v].[vmf_code] = @vmfCode",
                command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
                availableColumns
            )
        ).SingleOrDefault();

    private async Task<List<Vehicle>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlySet<string>? knownColumns = null
    )
    {
        var availableColumns = knownColumns ?? await GetAvailableColumnsAsync();
        var modelColumns = await GetTableColumnsAsync(ModelTableName);
        var hasModel = RequiredModelColumns.All(modelColumns.Contains);
        var projection = LegacyColumns
            .Concat(OptionalColumns)
            .Select(column => GetColumnProjection("v", column, availableColumns))
            .Concat(GetModelProjection(hasModel))
            .ToArray();
        var modelJoin = hasModel
            ? $"LEFT JOIN [dbo].[{ModelTableName}] AS [m] ON [m].[model_code] = [v].[model_code]"
            : string.Empty;
        var normalizedPredicate = predicate?.Trim();
        var whereClause =
            string.IsNullOrWhiteSpace(normalizedPredicate) ? string.Empty
            : normalizedPredicate.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase)
            || normalizedPredicate.StartsWith("ORDER BY ", StringComparison.OrdinalIgnoreCase)
                ? normalizedPredicate
            : $"WHERE {normalizedPredicate}";

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{VehicleTableName}] AS [v]
                {modelJoin}
                {whereClause}
                """;
            configure?.Invoke(command);

            var results = new List<Vehicle>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapVehicle(reader));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<int> CountActiveVehiclesAsync(IReadOnlySet<string> availableColumns)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var activePredicate =
                $"[v].[vehicle_status_code] > 0 AND {GetActiveFilter("v", availableColumns)}";
            command.CommandText = $"""
                SELECT COUNT(*)
                FROM [dbo].[{VehicleTableName}] AS [v]
                WHERE {activePredicate}
                """;

            return Convert.ToInt32(
                await command.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
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

    private async Task<int> CountVehicleLookupRowsAsync(string whereClause, string keyword)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT COUNT(*)
                FROM [dbo].[{VehicleTableName}] AS [v]
                WHERE {whereClause}
                """;
            AddParameter(command, "@keyword", DbType.String, BuildLikeParameter(keyword));

            return Convert.ToInt32(
                await command.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
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

    private static string BuildVehicleLookupWhereClause(
        IReadOnlySet<string> availableColumns,
        string? searchMode
    )
    {
        var normalizedSearchMode = string.IsNullOrWhiteSpace(searchMode)
            ? null
            : searchMode.Trim().ToUpperInvariant();
        var searchPredicate = normalizedSearchMode switch
        {
            null =>
                "(LOWER(COALESCE([v].[fleet_number], '')) LIKE @keyword ESCAPE '\\' OR LOWER(COALESCE([v].[registration_number], '')) LIKE @keyword ESCAPE '\\')",
            "GG" => "LOWER(COALESCE([v].[fleet_number], '')) LIKE @keyword ESCAPE '\\'",
            "GP" => "LOWER(COALESCE([v].[registration_number], '')) LIKE @keyword ESCAPE '\\'",
            _ => throw new ArgumentException("Search mode must be GG or GP.", nameof(searchMode)),
        };

        return $"{GetActiveFilter("v", availableColumns)} AND ({searchPredicate})";
    }

    private static string BuildLikeParameter(string keyword) =>
        $"%{keyword.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[")}%";

    private static string GetLookupActiveFilter(
        string alias,
        IReadOnlySet<string> availableColumns
    ) =>
        availableColumns.Contains("is_deleted")
            ? $"AND ISNULL([{alias}].[is_deleted], 0) = 0"
            : string.Empty;

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        var columns = await GetTableColumnsAsync(VehicleTableName);
        var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required vehicle_master compatibility columns are not available: {string.Join(", ", missingColumns)}"
            );
        }

        return columns;
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
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
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
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

    private static Vehicle MapVehicle(DbDataReader reader)
    {
        var vehicle = new Vehicle();
        foreach (var column in LegacyColumns.Concat(OptionalColumns))
        {
            if (!VehicleProperties.TryGetValue(column, out var property))
            {
                continue;
            }

            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
            {
                continue;
            }

            property.SetValue(
                vehicle,
                ConvertValue(reader.GetValue(ordinal), property.PropertyType)
            );
        }

        var modelCode = ReadInt16(reader, "model_model_code");
        var makeCode = ReadInt16(reader, "model_make_code");
        if (modelCode.HasValue && makeCode.HasValue)
        {
            vehicle.Model = new Model
            {
                model_code = modelCode.Value,
                make_code = makeCode.Value,
                model_description = ReadString(reader, "model_description") ?? string.Empty,
            };
        }

        return vehicle;
    }

    private static List<WriteValue> BuildWriteValues(
        Vehicle vehicle,
        IReadOnlySet<string> availableColumns,
        bool includeKey
    )
    {
        var values = new List<WriteValue>();
        foreach (var column in LegacyColumns.Concat(OptionalColumns))
        {
            if (column.Equals("vmf_code", StringComparison.OrdinalIgnoreCase) && !includeKey)
            {
                continue;
            }

            if (
                !availableColumns.Contains(column)
                || !VehicleProperties.TryGetValue(column, out var property)
            )
            {
                continue;
            }

            values.Add(
                new WriteValue(
                    column,
                    $"@vehicle_{values.Count}",
                    GetDbType(property.PropertyType),
                    property.GetValue(vehicle)
                )
            );
        }

        return values;
    }

    private static void AddColumnValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        DbType type,
        object? value,
        bool includeNull = true
    )
    {
        if (!availableColumns.Contains(column) || (!includeNull && value is null))
        {
            return;
        }

        values.Add(new WriteValue(column, $"@licence_{values.Count}", type, value));
    }

    private static void AddOptionalColumnValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (availableColumns.Contains(column))
        {
            values.Add(new WriteValue(column, parameter, type, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetColumnProjection(
        string alias,
        string column,
        IReadOnlySet<string> availableColumns
    ) =>
        availableColumns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static IEnumerable<string> GetModelProjection(bool hasModel)
    {
        if (hasModel)
        {
            return
            [
                "[m].[model_code] AS [model_model_code]",
                "[m].[make_code] AS [model_make_code]",
                "[m].[model_description] AS [model_description]",
            ];
        }

        return
        [
            "CAST(NULL AS smallint) AS [model_model_code]",
            "CAST(NULL AS smallint) AS [model_make_code]",
            "CAST(NULL AS varchar(100)) AS [model_description]",
        ];
    }

    private static string GetActiveFilter(
        string alias,
        IReadOnlySet<string>? availableColumns = null
    )
    {
        if (availableColumns is null || availableColumns.Contains("is_deleted"))
        {
            var qualifiedColumn = string.IsNullOrWhiteSpace(alias)
                ? "[is_deleted]"
                : $"[{alias}].[is_deleted]";
            return $"ISNULL({qualifiedColumn}, 0) = 0";
        }

        return "1 = 1";
    }

    private static string GetSqlType(string column) =>
        column switch
        {
            "vmf_code"
            or "take_on_odo"
            or "current_odo"
            or "odo_adjustment"
            or "tare"
            or "additional_fuel_tank"
            or "service_last_odo"
            or "km_ltd"
            or "km_ytd"
            or "km_3month_average"
            or "user_access_code"
            or "temp_vmf_code" => "int",
            "model_code"
            or "type_code"
            or "vehicle_status_code"
            or "location_code"
            or "year_manufactured"
            or "Licence_receiver_site"
            or "default_site"
            or "initial_site_code"
            or "veh_site_code"
            or "supplier_id" => "smallint",
            "vs_code" => "tinyint",
            "LPG" or "RelieveVehicle" or "is_deleted" => "bit",
            "date_created"
            or "date_updated"
            or "take_on_date"
            or "odo_update_date"
            or "licence_due_date"
            or "fuel_card_date"
            or "purchase_date"
            or "book_value_date"
            or "maint_card_exdate"
            or "sold_date"
            or "service_last_done"
            or "cof_last_done"
            or "Licence_date_taken"
            or "vehicle_status_date"
            or "captured_date"
            or "destroyed_date"
            or "date_First_Regist" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "average_consumption"
            or "purchase_amount"
            or "book_value"
            or "sold_amount"
            or "monthly_overhead"
            or "Cof_amount"
            or "highest_km"
            or "fuel_ltd"
            or "fuel_ytd"
            or "fuel_3month_average"
            or "oil_ltd"
            or "oil_ytd"
            or "oil_3month_average"
            or "maint_ltd"
            or "maint_ytd"
            or "maint_3month_average"
            or "repairs_ltd"
            or "repairs_ytd"
            or "repairs_3month_average"
            or "tyres_ltd"
            or "tyres_ytd"
            or "tyres_3month_average"
            or "accident_ltd"
            or "accident_ytd"
            or "accident_3month_average"
            or "toll_ltd"
            or "toll_ytd"
            or "toll_3month_average"
            or "other_ltd"
            or "other_ytd"
            or "other_3month_average"
            or "destroyed_amount" => "decimal(18, 4)",
            _ => "varchar(500)",
        };

    private static DbType GetDbType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => DbType.Boolean,
            TypeCode.Byte => DbType.Byte,
            TypeCode.Int16 => DbType.Int16,
            TypeCode.Int32 => DbType.Int32,
            TypeCode.Decimal => DbType.Decimal,
            TypeCode.DateTime => DbType.DateTime2,
            TypeCode.String => DbType.String,
            _ => throw new InvalidOperationException(
                $"Unsupported vehicle property type: {propertyType}"
            ),
        };
    }

    private static object ConvertValue(object value, Type propertyType)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt16(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.TrimEnd();
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
