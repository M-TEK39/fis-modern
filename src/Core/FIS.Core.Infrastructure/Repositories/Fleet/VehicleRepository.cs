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

    // Master-File edits are direct legacy table writes. These triggers are the
    // database-owned audit and revenue-protection boundary for that path. A
    // disabled trigger is a compatibility failure; an absent trigger is
    // treated as the explicit expanded-schema fallback below.
    private static readonly string[] LegacyUpdateTriggerNames =
    [
        "TRG_Audit_Vehicle_Master_Update",
        "trg_upd_checkvehiclejournalrecords",
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

    public async Task<Vehicle?> GetByIdAsync(
        int vmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        return (
            await QueryAsync(
                $"WHERE [v].[vmf_code] = @vmfCode AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate}",
                command =>
                {
                    AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                    scope.AddParameters(command);
                },
                availableColumns
            )
        ).SingleOrDefault();
    }

    public async Task<Vehicle?> GetByFleetNumberAsync(
        string fleetNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        if (string.IsNullOrWhiteSpace(fleetNumber))
        {
            return null;
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        return (
            await QueryAsync(
                $"WHERE [v].[fleet_number] = @fleetNumber AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate}",
                command =>
                {
                    AddParameter(command, "@fleetNumber", DbType.String, fleetNumber);
                    scope.AddParameters(command);
                },
                availableColumns
            )
        ).SingleOrDefault();
    }

    public async Task<Vehicle?> GetByRegistrationNumberAsync(
        string registrationNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
        {
            return null;
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        return (
            await QueryAsync(
                $"WHERE [v].[registration_number] = @registrationNumber AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate}",
                command =>
                {
                    AddParameter(command, "@registrationNumber", DbType.String, registrationNumber);
                    scope.AddParameters(command);
                },
                availableColumns
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<Vehicle>> GetActiveVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        return await QueryAsync(
            $"WHERE [v].[vehicle_status_code] > 0 AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            command => scope.AddParameters(command),
            knownColumns: availableColumns
        );
    }

    public async Task<VehicleMasterSnapshotPage> GetSnapshotPageAsync(
        int page,
        int pageSize,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        var activePredicate =
            $"[v].[vehicle_status_code] > 0 AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate}";
        var totalRecords = await CountActiveVehiclesAsync(availableColumns, scope);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        var data = await QueryAsync(
            $"WHERE {activePredicate} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code] OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY",
            command =>
            {
                AddParameter(command, "@skip", DbType.Int64, skip);
                AddParameter(command, "@pageSize", DbType.Int32, pageSize);
                scope.AddParameters(command);
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
        int pageSize,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
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

            var scope = BuildVehicleScope(vehicleColumns, allowedSiteCodes, currentUserId);
            var whereClause = BuildVehicleLookupWhereClause(vehicleColumns, searchMode, scope);
            var total = await CountVehicleLookupRowsAsync(whereClause, keyword, scope);
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
            scope.AddParameters(command);

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

    public async Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) => await GetActiveVehiclesAsync(allowedSiteCodes, currentUserId);

    public async Task<IEnumerable<Vehicle>> GetAllAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        return await QueryAsync(
            $"WHERE {scope.Predicate} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            command => scope.AddParameters(command),
            availableColumns
        );
    }

    public async Task<IEnumerable<Vehicle>> SearchVehiclesAsync(
        string searchTerm,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetActiveVehiclesAsync(allowedSiteCodes, currentUserId);
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);
        var term = $"%{searchTerm.Trim().ToLowerInvariant()}%";
        var searchColumns = new[]
        {
            "fleet_number",
            "registration_number",
            "chassis_number",
            "engine_number_1",
            "invoice_number",
        };
        var predicates = searchColumns
            .Where(availableColumns.Contains)
            .Select(column =>
                $"LOWER(COALESCE([v].[{column}], '')) LIKE @searchTerm"
            )
            .ToArray();
        if (predicates.Length == 0)
        {
            return [];
        }

        return await QueryAsync(
            "WHERE ("
                + string.Join(" OR ", predicates)
                + $") AND {scope.Predicate} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            command =>
            {
                AddParameter(command, "@searchTerm", DbType.String, term);
                scope.AddParameters(command);
            },
            availableColumns
        );
    }

    public async Task<IEnumerable<Vehicle>> GetByInvoiceNumberAsync(
        string invoiceNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return [];
        }

        var availableColumns = await GetAvailableColumnsAsync();
        if (!availableColumns.Contains("invoice_number"))
        {
            return [];
        }

        var scope = BuildVehicleScope(availableColumns, allowedSiteCodes, currentUserId);

        return await QueryAsync(
            $"WHERE [v].[invoice_number] = @invoiceNumber AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate} ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]",
            command =>
            {
                AddParameter(command, "@invoiceNumber", DbType.String, invoiceNumber);
                scope.AddParameters(command);
            },
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
        var existing =
            await GetByIdIncludingDeletedAsync(vehicle.vmf_code, availableColumns)
            ?? throw new InvalidOperationException(
                $"Vehicle with vmf_code {vehicle.vmf_code} not found"
            );
        var values = BuildWriteValues(vehicle, availableColumns, includeKey: false);
        if (values.Count == 0)
        {
            return;
        }

        var historyChanges = GetHistoryChanges(existing, vehicle);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        DbTransaction? transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        DbTransaction? ownedTransaction = null;
        var committed = false;
        try
        {
            if (transaction is null)
            {
                ownedTransaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
                transaction = ownedTransaction;
            }

            var changesTariffInputs = existing.model_code != vehicle.model_code
                || existing.year_manufactured != vehicle.year_manufactured;
            await EnsureLegacyUpdateTriggersAsync(
                connection,
                transaction,
                changesTariffInputs
            );

            await EnsureIdentityValuesUniqueAsync(
                connection,
                transaction,
                availableColumns,
                existing,
                vehicle
            );

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
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

            var historyColumns = await GetTableColumnsAsync("vehicle_history", transaction);
            await AppendLegacyVehicleHistoryAsync(
                connection,
                transaction,
                historyColumns,
                vehicle.vmf_code,
                currentUserId,
                historyChanges
            );

            var preVehicleColumns = await GetTableColumnsAsync("pre_vehicle_master", transaction);
            await SynchronizePendingVehicleAsync(
                connection,
                transaction,
                preVehicleColumns,
                existing,
                vehicle
            );

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync();
                committed = true;
            }
        }
        catch
        {
            if (ownedTransaction is not null && !committed)
            {
                await ownedTransaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static IReadOnlyList<HistoryChange> GetHistoryChanges(
        Vehicle existing,
        Vehicle updated
    )
    {
        var changes = new List<HistoryChange>();
        AddHistoryChange(
            changes,
            "hist_registration_number",
            DbType.String,
            existing.registration_number,
            updated.registration_number
        );
        AddHistoryChange(
            changes,
            "hist_fleet_number",
            DbType.String,
            existing.fleet_number,
            updated.fleet_number
        );
        AddHistoryChange(
            changes,
            "hist_colour",
            DbType.String,
            existing.colour,
            updated.colour
        );
        AddHistoryChange(
            changes,
            "hist_engine_number",
            DbType.String,
            existing.engine_number_1,
            updated.engine_number_1
        );
        AddHistoryChange(
            changes,
            "hist_type",
            DbType.Int16,
            existing.type_code,
            updated.type_code
        );
        return changes;
    }

    private static void AddHistoryChange(
        ICollection<HistoryChange> changes,
        string historyColumn,
        DbType type,
        object? original,
        object? updated
    )
    {
        if (original is string || updated is string)
        {
            if (
                string.Equals(
                    original?.ToString() ?? string.Empty,
                    updated?.ToString() ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return;
            }
        }
        else if (Equals(original, updated))
        {
            return;
        }

        changes.Add(new HistoryChange(historyColumn, type, original));
    }

    private static async Task AppendLegacyVehicleHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> historyColumns,
        int vmfCode,
        int currentUserId,
        IReadOnlyList<HistoryChange> changes
    )
    {
        if (
            changes.Count == 0
            || !historyColumns.Contains("hist_vmf_code")
            || !historyColumns.Contains("hist_date_changed")
        )
        {
            return;
        }

        foreach (var change in changes)
        {
            if (!historyColumns.Contains(change.Column))
            {
                continue;
            }

            var columns = new List<string> { "hist_vmf_code", change.Column, "hist_date_changed" };
            var parameters = new List<string> { "@vmfCode", "@historyValue", "@dateChanged" };
            if (historyColumns.Contains("hist_user_access_code"))
            {
                columns.Add("hist_user_access_code");
                parameters.Add("@userAccessCode");
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO [dbo].[vehicle_history] ({string.Join(", ", columns.Select(column => $"[{column}]"))}) VALUES ({string.Join(", ", parameters)})";
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            AddParameter(command, "@historyValue", change.Type, change.Value);
            AddParameter(command, "@dateChanged", DbType.DateTime, DateTime.Now);
            if (historyColumns.Contains("hist_user_access_code"))
            {
                AddParameter(
                    command,
                    "@userAccessCode",
                    DbType.Int16,
                    currentUserId is > 0 and <= short.MaxValue ? (short)currentUserId : null
                );
            }

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task SynchronizePendingVehicleAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> preVehicleColumns,
        Vehicle existing,
        Vehicle updated
    )
    {
        if (!preVehicleColumns.Contains("chassis_number"))
        {
            return;
        }

        var assignments = new List<string>();
        if (
            !string.Equals(
                existing.fleet_number,
                updated.fleet_number,
                StringComparison.OrdinalIgnoreCase
            )
            && preVehicleColumns.Contains("fleet_number")
        )
        {
            assignments.Add("[fleet_number] = @fleetNumber");
        }

        if (
            !string.Equals(
                existing.chassis_number,
                updated.chassis_number,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            assignments.Add("[chassis_number] = @chassisNumber");
        }

        if (
            preVehicleColumns.Contains("engine_number")
            && !string.Equals(
                existing.engine_number_1,
                updated.engine_number_1,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            assignments.Add("[engine_number] = @engineNumber");
        }
        if (
            preVehicleColumns.Contains("registration_number")
            && !string.Equals(
                existing.registration_number,
                updated.registration_number,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            assignments.Add("[registration_number] = @registrationNumber");
        }

        if (assignments.Count == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var identityPredicates = new List<string>();
        if (preVehicleColumns.Contains("fleet_number") && !string.IsNullOrWhiteSpace(existing.fleet_number))
        {
            identityPredicates.Add("[fleet_number] = @oldFleetNumber");
            AddParameter(command, "@oldFleetNumber", DbType.String, existing.fleet_number);
        }
        if (!string.IsNullOrWhiteSpace(existing.chassis_number))
        {
            identityPredicates.Add("[chassis_number] = @oldChassisNumber");
            AddParameter(command, "@oldChassisNumber", DbType.String, existing.chassis_number);
        }
        if (
            preVehicleColumns.Contains("registration_number")
            && !string.IsNullOrWhiteSpace(existing.registration_number)
        )
        {
            identityPredicates.Add("[registration_number] = @oldRegistrationNumber");
            AddParameter(
                command,
                "@oldRegistrationNumber",
                DbType.String,
                existing.registration_number
            );
        }

        if (identityPredicates.Count == 0)
        {
            return;
        }

        command.CommandText = $"UPDATE [dbo].[pre_vehicle_master] SET {string.Join(", ", assignments)} WHERE {string.Join(" OR ", identityPredicates)}";
        if (assignments.Contains("[fleet_number] = @fleetNumber"))
        {
            AddParameter(command, "@fleetNumber", DbType.String, updated.fleet_number);
        }
        if (assignments.Contains("[chassis_number] = @chassisNumber"))
        {
            AddParameter(command, "@chassisNumber", DbType.String, updated.chassis_number);
        }
        if (assignments.Contains("[engine_number] = @engineNumber"))
        {
            AddParameter(command, "@engineNumber", DbType.String, updated.engine_number_1);
        }
        if (assignments.Contains("[registration_number] = @registrationNumber"))
        {
            AddParameter(command, "@registrationNumber", DbType.String, updated.registration_number);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureIdentityValuesUniqueAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> availableColumns,
        Vehicle existing,
        Vehicle updated
    )
    {
        await EnsureIdentityValueUniqueAsync(
            connection,
            transaction,
            availableColumns,
            updated.vmf_code,
            "chassis_number",
            existing.chassis_number,
            updated.chassis_number,
            "Chassis number"
        );
        await EnsureIdentityValueUniqueAsync(
            connection,
            transaction,
            availableColumns,
            updated.vmf_code,
            "engine_number_1",
            existing.engine_number_1,
            updated.engine_number_1,
            "Engine number"
        );
    }

    private static async Task EnsureIdentityValueUniqueAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> availableColumns,
        int vmfCode,
        string column,
        string? originalValue,
        string? updatedValue,
        string label
    )
    {
        if (
            !availableColumns.Contains(column)
            || string.Equals(originalValue, updatedValue, StringComparison.Ordinal)
        )
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT TOP (1) [fleet_number]
            FROM [dbo].[{VehicleTableName}]
            WHERE [{column}] = @identityValue
              AND [vmf_code] <> @vmfCode
            """;
        AddParameter(command, "@identityValue", DbType.String, updatedValue);
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);

        var duplicateFleetNumber = await command.ExecuteScalarAsync();
        if (duplicateFleetNumber is not null && duplicateFleetNumber is not DBNull)
        {
            throw new InvalidOperationException(
                $"{label} already exists for vehicle {Convert.ToString(duplicateFleetNumber, CultureInfo.InvariantCulture)}."
            );
        }
    }

    private static async Task EnsureLegacyUpdateTriggersAsync(
        DbConnection connection,
        DbTransaction? transaction,
        bool requiresTariffProtection
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [tr].[name], [tr].[is_disabled]
            FROM [sys].[triggers] AS [tr]
            INNER JOIN [sys].[tables] AS [tb] ON [tb].[object_id] = [tr].[parent_id]
            INNER JOIN [sys].[schemas] AS [sc] ON [sc].[schema_id] = [tb].[schema_id]
            WHERE [sc].[name] = N'dbo'
              AND [tb].[name] = N'vehicle_master'
              AND [tr].[name] IN (N'TRG_Audit_Vehicle_Master_Update', N'trg_upd_checkvehiclejournalrecords');
            """;

        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                var name = reader.GetString(0);
                if (!reader.IsDBNull(1) && reader.GetBoolean(1))
                {
                    disabled.Add(name);
                }
            }
        }

        // A disabled legacy trigger is never safe to bypass. If the trigger
        // object genuinely does not exist (an expanded compatibility schema),
        // the parameterized update below is the explicit fallback. Audit is
        // expected for every legacy write; the journal-protection trigger is
        // checked only when model/year tariff inputs change.
        var required = requiresTariffProtection
            ? LegacyUpdateTriggerNames
            : [LegacyUpdateTriggerNames[0]];
        var disabledRequired = required
            .Where(disabled.Contains)
            .ToArray();
        if (disabledRequired.Length > 0)
        {
            throw new NotSupportedException(
                $"The legacy vehicle_master update workflow is disabled ({string.Join(", ", disabledRequired)}); no direct-DML fallback was run."
            );
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
            // Licence capture is a dedicated direct update, but it still
            // targets vehicle_master. Preserve the legacy audit trigger
            // boundary rather than allowing this narrow endpoint to bypass
            // the database-owned history/audit behavior.
            await EnsureLegacyUpdateTriggersAsync(
                connection,
                _context.Database.CurrentTransaction?.GetDbTransaction(),
                requiresTariffProtection: false
            );

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

    public Task DeleteAsync(int vmfCode, int currentUserId) =>
        Task.FromException(
            new NotSupportedException(
                "Vehicle Master records cannot be deleted. The legacy FIS database protects vehicle records from deletion."
            )
        );

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
        var hasModelClassCode = hasModel && modelColumns.Contains("class_code");
        var projection = LegacyColumns
            .Concat(OptionalColumns)
            .Select(column => GetColumnProjection("v", column, availableColumns))
            .Concat(GetModelProjection(hasModel, hasModelClassCode))
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

    private async Task<int> CountActiveVehiclesAsync(
        IReadOnlySet<string> availableColumns,
        VehicleScope scope
    )
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
                $"[v].[vehicle_status_code] > 0 AND {GetActiveFilter("v", availableColumns)} AND {scope.Predicate}";
            command.CommandText = $"""
                SELECT COUNT(*)
                FROM [dbo].[{VehicleTableName}] AS [v]
                WHERE {activePredicate}
                """;
            scope.AddParameters(command);

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

    private async Task<int> CountVehicleLookupRowsAsync(
        string whereClause,
        string keyword,
        VehicleScope scope
    )
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
            scope.AddParameters(command);

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
        string? searchMode,
        VehicleScope scope
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

        return $"{GetActiveFilter("v", availableColumns)} AND {scope.Predicate} AND ({searchPredicate})";
    }

    private static string BuildLikeParameter(string keyword) =>
        $"%{keyword.ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[")}%";

    private static VehicleScope BuildVehicleScope(
        IReadOnlySet<string> availableColumns,
        IReadOnlySet<short>? allowedSiteCodes,
        int? currentUserId = null
    )
    {
        // A null set is the explicit unrestricted scope used by system-level
        // services. A non-null empty set must never become an omitted WHERE
        // clause: it represents a user whose profile could not resolve to an
        // allowed site.
        if (allowedSiteCodes is null)
        {
            return new VehicleScope("1 = 1", static _ => { });
        }

        var siteCodes = allowedSiteCodes.Where(code => code > 0).Distinct().ToArray();
        if (siteCodes.Length == 0)
        {
            return new VehicleScope("1 = 0", static _ => { });
        }

        var siteColumns = new[] { "veh_site_code", "initial_site_code", "default_site" }
            .Where(availableColumns.Contains)
            .ToArray();
        var siteParameters = siteCodes
            .Select((_, index) => $"@vehicleSite{index}")
            .ToArray();
        var predicates = siteColumns.Length == 0
            ? []
            : siteColumns
                .Select(column => $"[v].[{column}] IN ({string.Join(", ", siteParameters)})")
                .ToList();
        var ownerColumns = currentUserId is > 0
            ? new[] { "user_access_code", "created_by_user_code" }
                .Where(availableColumns.Contains)
                .ToArray()
            : [];
        if (ownerColumns.Length > 0)
        {
            predicates.AddRange(ownerColumns.Select(column => $"[v].[{column}] = @vehicleOwnerUser"));
        }
        // A vehicle may retain an older master site while it is dispatched
        // under an active contract at an allowed site. Keep the lookup useful
        // for that business flow without broadening it to inactive contracts.
        predicates.Add(
            $"EXISTS (SELECT 1 FROM [dbo].[contract] AS [scope_contract] WHERE [scope_contract].[vmf_code] = [v].[vmf_code] AND [scope_contract].[still_current] = 'Y' AND [scope_contract].[site_code] IN ({string.Join(", ", siteParameters)}))"
        );
        if (predicates.Count == 0)
        {
            return new VehicleScope("1 = 0", static _ => { });
        }

        var predicate = "(" + string.Join(" OR ", predicates) + ")";

        return new VehicleScope(
            predicate,
            command =>
            {
                for (var index = 0; index < siteCodes.Length; index++)
                {
                    AddParameter(command, siteParameters[index], DbType.Int16, siteCodes[index]);
                }
                if (ownerColumns.Length > 0)
                {
                    AddParameter(command, "@vehicleOwnerUser", DbType.Int32, currentUserId);
                }
            }
        );
    }

    private sealed record VehicleScope(string Predicate, Action<DbCommand> AddParameters);

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

    private async Task<HashSet<string>> GetTableColumnsAsync(
        string tableName,
        DbTransaction? transaction = null
    )
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
            command.Transaction = transaction ?? _context.Database.CurrentTransaction?.GetDbTransaction();
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
                class_code = ReadInt16(reader, "model_class_code") ?? 0,
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

    private static IEnumerable<string> GetModelProjection(bool hasModel, bool hasClassCode)
    {
        if (hasModel)
        {
            return
            [
                "[m].[model_code] AS [model_model_code]",
                "[m].[make_code] AS [model_make_code]",
                hasClassCode
                    ? "[m].[class_code] AS [model_class_code]"
                    : "CAST(NULL AS smallint) AS [model_class_code]",
                "[m].[model_description] AS [model_description]",
            ];
        }

        return
        [
            "CAST(NULL AS smallint) AS [model_model_code]",
            "CAST(NULL AS smallint) AS [model_make_code]",
            "CAST(NULL AS smallint) AS [model_class_code]",
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

    private sealed record HistoryChange(string Column, DbType Type, object? Value);
}
