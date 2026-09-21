using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Vehicle inception access negotiated against the connected database at
/// runtime. The client's pre_vehicle_master table predates the modern EF
/// model, so a static EF query would select columns that do not exist on the
/// client's database. Legacy columns are selected/written when present and
/// modern audit columns are optional.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table, column, and stored procedure identifiers come from fixed compatibility allowlists; values are parameters."
)]
public sealed class VehicleAuthorizationRepository : IVehicleAuthorizationRepository
{
    private const string PreVehicleTableName = "pre_vehicle_master";
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const string GgNumberTableName = "block_gg_numbers";
    private const string ExtraCodeTableName = "extra_codes";

    private static readonly string[] SelectedColumns =
    [
        "temp_vmf_code",
        "fleet_number",
        "registration_number",
        "replaced_gg_number",
        "model_code",
        "colour",
        "year_manufactured",
        "chassis_number",
        "engine_number",
        "take_on_odo",
        "take_on_date",
        "location_code",
        "vehicle_status_code",
        "vehicle_status_date",
        "type_code",
        "vs_code",
        "comment",
        "purchase_amount",
        "purchase_from",
        "purchase_date",
        "captured_by_user_code",
        "action_user_access_code",
        "site_code",
        "Authority_Status",
        "captured_date",
        "printed",
        "invoice_number",
        "gp_number",
        "Fleet_Notes",
        "damage_status",
        "damages_comment",
        "authorized_by_user_code",
        "authorization_date",
        "rejection_reason",
        "authorization_comment",
        "vmf_code",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] StringColumns =
    [
        "fleet_number",
        "registration_number",
        "replaced_gg_number",
        "colour",
        "chassis_number",
        "engine_number",
        "comment",
        "purchase_from",
        "Authority_Status",
        "printed",
        "invoice_number",
        "gp_number",
        "Fleet_Notes",
        "damage_status",
        "damages_comment",
        "rejection_reason",
        "authorization_comment",
    ];

    private static readonly string[] DateColumns =
    [
        "take_on_date",
        "vehicle_status_date",
        "purchase_date",
        "captured_date",
        "authorization_date",
        "date_created",
        "date_updated",
    ];

    private static readonly string[] IntegerColumns =
    [
        "temp_vmf_code",
        "take_on_odo",
        "authorized_by_user_code",
        "vmf_code",
        "created_by_user_code",
        "modified_by_user_code",
    ];

    private static readonly string[] ShortColumns =
    [
        "model_code",
        "year_manufactured",
        "location_code",
        "vehicle_status_code",
        "type_code",
        "action_user_access_code",
        "site_code",
    ];

    private static readonly string[] ByteColumns = ["vs_code"];
    private static readonly string[] DecimalColumns = ["purchase_amount"];

    private readonly FisDbContext _context;

    public VehicleAuthorizationRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<PreVehicleMaster?> GetByIdAsync(
        int tempVmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) =>
        WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            return (
                await QueryAsync(
                    connection,
                    null,
                    schema,
                    tempVmfCode: tempVmfCode,
                    allowedSiteCodes: allowedSiteCodes,
                    currentUserId: currentUserId
                )
            ).SingleOrDefault();
        });

    public Task<PreVehicleMaster?> GetByChassisNumberAsync(
        string chassisNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) =>
        string.IsNullOrWhiteSpace(chassisNumber)
            ? Task.FromResult<PreVehicleMaster?>(null)
            : WithConnectionAsync(async connection =>
            {
                var schema = await GetSchemaAsync(connection, null);
                var trimmedChassis = chassisNumber.Trim();
                if (
                    await TryConfirmChassisFromDetailsProcedureAsync(
                        connection,
                        trimmedChassis
                    ) == false
                )
                {
                    return null;
                }

                var vehicle = (
                    await QueryAsync(
                        connection,
                        null,
                        schema,
                        chassisNumber: trimmedChassis,
                        allowedSiteCodes: allowedSiteCodes,
                        currentUserId: currentUserId
                    )
                ).SingleOrDefault();
                await OverlayCaptureExtrasAsync(connection, vehicle);
                return vehicle;
            });

    public Task<IReadOnlyList<PreVehicleMaster>> SearchPreVehiclesAsync(
        string chassisNo,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) =>
        string.IsNullOrWhiteSpace(chassisNo)
            ? Task.FromResult<IReadOnlyList<PreVehicleMaster>>([])
            : WithConnectionAsync(async connection =>
            {
                var schema = await GetSchemaAsync(connection, null);
                var trimmed = chassisNo.Trim();
                var overlay = await TryLoadFilterPreVehiclesAsync(
                    connection,
                    schema,
                    trimmed,
                    allowedSiteCodes
                );
                if (overlay is not null)
                {
                    return overlay;
                }

                return (IReadOnlyList<PreVehicleMaster>)
                    await QueryAsync(
                        connection,
                        null,
                        schema,
                        chassisNumber: trimmed,
                        allowedSiteCodes: allowedSiteCodes,
                        currentUserId: currentUserId
                    );
            });

    public Task<VehicleAuthorizationPage> GetPendingAuthorizationsAsync(
        int page = 1,
        int pageSize = 24,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) => GetStatusPageAsync(
        "Awaiting Authorization",
        page,
        pageSize,
        awaiting: true,
        allowedSiteCodes: allowedSiteCodes,
        currentUserId: currentUserId
    );

    public Task<VehicleAuthorizationPage> GetAuthorizedVehiclesAsync(
        int page = 1,
        int pageSize = 24,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) => GetStatusPageAsync(
        "Authorized",
        page,
        pageSize,
        allowedSiteCodes: allowedSiteCodes,
        currentUserId: currentUserId
    );

    public Task<VehicleAuthorizationPage> GetRejectedVehiclesAsync(
        int page = 1,
        int pageSize = 24,
        int? capturedByUserCode = null,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) => GetStatusPageAsync(
        "Rejected",
        page,
        pageSize,
        capturedByUserCode: capturedByUserCode,
        allowedSiteCodes: allowedSiteCodes,
        currentUserId: currentUserId
    );

    public async Task<IEnumerable<PreVehicleMaster>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return [];
        }

        return await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            return (IEnumerable<PreVehicleMaster>)
                await QueryAsync(connection, null, schema, status: status);
        });
    }

    private async Task<VehicleAuthorizationPage> GetStatusPageAsync(
        string status,
        int requestedPage,
        int requestedPageSize,
        bool awaiting = false,
        int? capturedByUserCode = null,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) =>
        await WithConnectionAsync(async connection =>
        {
            var pageSize = Math.Clamp(requestedPageSize, 1, 100);
            var page = Math.Max(1, requestedPage);
            var schema = await GetSchemaAsync(connection, null);
            var overlay = await TryLoadQueueFromProcedureAsync(
                connection,
                schema,
                status,
                awaiting,
                allowedSiteCodes,
                currentUserId
            );
            if (overlay is not null)
            {
                var overlayTotal = overlay.Count;
                var overlayPages = Math.Max(
                    1,
                    (int)Math.Ceiling(overlayTotal / (double)pageSize)
                );
                page = Math.Min(page, overlayPages);
                var overlaySkip = checked((page - 1) * pageSize);
                return new VehicleAuthorizationPage(
                    overlay.Skip(overlaySkip).Take(pageSize).ToList(),
                    page,
                    pageSize,
                    overlayTotal
                );
            }

            var totalRecords = await CountByStatusAsync(
                connection,
                null,
                schema,
                status,
                capturedByUserCode,
                allowedSiteCodes,
                currentUserId
            );
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
            page = Math.Min(page, totalPages);
            var skip = checked((long)(page - 1) * pageSize);
            var data = await QueryAsync(
                connection,
                null,
                schema,
                status: status,
                capturedByUserCode: capturedByUserCode,
                allowedSiteCodes: allowedSiteCodes,
                currentUserId: currentUserId,
                skip: skip,
                take: pageSize,
                orderBy: GetQueueOrder(schema, awaiting)
            );

            return new VehicleAuthorizationPage(data, page, pageSize, totalRecords);
        });

    public async Task<IEnumerable<PreVehicleMaster>> GetAuthorizationHistoryAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? capturedByUserCode = null,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) =>
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var historyStatuses = schema.Columns.Contains("Authority_Status")
                ? new[] { "Authorized", "Rejected" }
                : Array.Empty<string>();
            var overlay = await TryLoadHistoryFromProcedureAsync(
                connection,
                schema,
                historyStatuses,
                startDate,
                endDate,
                allowedSiteCodes,
                currentUserId
            );
            if (overlay is not null)
            {
                return overlay;
            }

            var rows = await QueryAsync(
                connection,
                null,
                schema,
                statuses: historyStatuses,
                capturedByUserCode: capturedByUserCode,
                allowedSiteCodes: allowedSiteCodes,
                currentUserId: currentUserId,
                startDate: startDate,
                endDate: endDate
            );
            return (IEnumerable<PreVehicleMaster>)rows;
        });

    private static async Task<bool?> TryConfirmChassisFromDetailsProcedureAsync(
        DbConnection connection,
        string chassisNumber
    )
    {
        if (
            !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_Pre_Vehicle_Details",
                "@chassis_number"
            )
        )
        {
            return null;
        }

        var rows = await ExecuteProcedureRowsAsync(
            connection,
            null,
            "DEV_SEL_Pre_Vehicle_Details",
            new ProcedureParameter("@chassis_number", DbType.String, chassisNumber)
        );
        return rows.Count > 0;
    }

    private static async Task<IReadOnlyList<PreVehicleMaster>?> TryLoadFilterPreVehiclesAsync(
        DbConnection connection,
        VehicleAuthorizationSchema schema,
        string chassisNo,
        IReadOnlySet<short>? allowedSiteCodes
    )
    {
        if (
            !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_FilterPreVehicles",
                "@chassisno"
            )
        )
        {
            return null;
        }

        var procedureRows = await ExecuteProcedureRowsAsync(
            connection,
            null,
            "DEV_SEL_FilterPreVehicles",
            new ProcedureParameter("@chassisno", DbType.String, chassisNo)
        );
        if (procedureRows.Count == 0)
        {
            return [];
        }

        var chassisOrder = ReadQueueChassisNumbers(procedureRows);
        if (chassisOrder.Count == 0)
        {
            return null;
        }

        var leftover = new List<PreVehicleMaster>();
        foreach (var chassis in chassisOrder)
        {
            leftover.AddRange(
                await QueryAsync(
                    connection,
                    null,
                    schema,
                    chassisNumber: chassis,
                    allowedSiteCodes: allowedSiteCodes
                )
            );
        }

        return OrderByProcedureChassis(leftover, chassisOrder);
    }

    private static async Task<IReadOnlyList<PreVehicleMaster>?> TryLoadQueueFromProcedureAsync(
        DbConnection connection,
        VehicleAuthorizationSchema schema,
        string status,
        bool awaiting,
        IReadOnlySet<short>? allowedSiteCodes,
        int? currentUserId
    )
    {
        var procedure = ResolveQueueProcedure(status, awaiting, currentUserId);
        if (procedure is null)
        {
            return null;
        }

        return await LoadVehiclesFromQueueProcedureAsync(
            connection,
            schema,
            procedure,
            status,
            statuses: null,
            startDate: null,
            endDate: null,
            allowedSiteCodes
        );
    }

    private static async Task<IReadOnlyList<PreVehicleMaster>?> TryLoadHistoryFromProcedureAsync(
        DbConnection connection,
        VehicleAuthorizationSchema schema,
        IReadOnlyCollection<string> historyStatuses,
        DateTime? startDate,
        DateTime? endDate,
        IReadOnlySet<short>? allowedSiteCodes,
        int? currentUserId
    )
    {
        if (currentUserId is not > 0)
        {
            return null;
        }

        var procedure = new QueueProcedure(
            "DEV_SEL_RejectedVehicles",
            ["@UserCode"],
            [new ProcedureParameter("@UserCode", DbType.Int32, currentUserId.Value)]
        );
        return await LoadVehiclesFromQueueProcedureAsync(
            connection,
            schema,
            procedure,
            status: null,
            statuses: historyStatuses,
            startDate,
            endDate,
            allowedSiteCodes
        );
    }

    private static QueueProcedure? ResolveQueueProcedure(
        string status,
        bool awaiting,
        int? currentUserId
    )
    {
        if (awaiting)
        {
            if (currentUserId is not > 0)
            {
                return null;
            }

            return new QueueProcedure(
                "DEV_SEL_Pre_Vehicle_Awaiting_Authority",
                ["@UserCode"],
                [new ProcedureParameter("@UserCode", DbType.Int32, currentUserId.Value)]
            );
        }

        if (string.Equals(status, "Authorized", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueProcedure("DEV_SEL_AuthorizedVehicles", [], []);
        }

        if (string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            if (currentUserId is not > 0)
            {
                return null;
            }

            return new QueueProcedure(
                "DEV_SEL_RejectedVehicles",
                ["@UserCode"],
                [new ProcedureParameter("@UserCode", DbType.Int32, currentUserId.Value)]
            );
        }

        return null;
    }

    private static async Task<IReadOnlyList<PreVehicleMaster>?> LoadVehiclesFromQueueProcedureAsync(
        DbConnection connection,
        VehicleAuthorizationSchema schema,
        QueueProcedure procedure,
        string? status,
        IReadOnlyCollection<string>? statuses,
        DateTime? startDate,
        DateTime? endDate,
        IReadOnlySet<short>? allowedSiteCodes
    )
    {
        if (
            !await ProcedureMatchesAsync(
                connection,
                null,
                procedure.Name,
                procedure.ExpectedParameters
            )
        )
        {
            return null;
        }

        var procedureRows = await ExecuteProcedureRowsAsync(
            connection,
            null,
            procedure.Name,
            procedure.Parameters
        );
        if (procedureRows.Count == 0)
        {
            return [];
        }

        var chassisOrder = ReadQueueChassisNumbers(procedureRows);
        if (chassisOrder.Count == 0)
        {
            return null;
        }

        var leftover = await QueryAsync(
            connection,
            null,
            schema,
            status: status,
            statuses: statuses,
            allowedSiteCodes: allowedSiteCodes,
            startDate: startDate,
            endDate: endDate
        );
        return OrderByProcedureChassis(leftover, chassisOrder);
    }

    private static IReadOnlyList<string> ReadQueueChassisNumbers(
        IReadOnlyList<Dictionary<string, object?>> rows
    )
    {
        var chassisOrder = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var chassis = ReadRowString(
                row,
                "Chassis N0",
                "Chassis NO",
                "Chassis No",
                "chassis_number",
                "Chassis_Number"
            );
            if (string.IsNullOrWhiteSpace(chassis) || !seen.Add(chassis))
            {
                continue;
            }

            chassisOrder.Add(chassis);
        }

        return chassisOrder;
    }

    private static IReadOnlyList<PreVehicleMaster> OrderByProcedureChassis(
        IReadOnlyList<PreVehicleMaster> leftover,
        IReadOnlyList<string> chassisOrder
    )
    {
        var byChassis = leftover
            .Where(vehicle => !string.IsNullOrWhiteSpace(vehicle.chassis_number))
            .GroupBy(vehicle => vehicle.chassis_number!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase
            );

        var ordered = new List<PreVehicleMaster>(chassisOrder.Count);
        foreach (var chassis in chassisOrder)
        {
            if (byChassis.TryGetValue(chassis, out var vehicle))
            {
                ordered.Add(vehicle);
            }
        }

        return ordered;
    }

    public async Task<IReadOnlyList<VehicleMaintenanceTypeOption>> GetMaintenanceTypesAsync() =>
        await WithConnectionAsync(async connection =>
        {
            if (
                !await ProcedureMatchesAsync(
                    connection,
                    null,
                    "DEV_SEL_AllMaintenanceTypes"
                )
            )
            {
                return (IReadOnlyList<VehicleMaintenanceTypeOption>)[];
            }

            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "dbo.DEV_SEL_AllMaintenanceTypes";

            var types = new List<VehicleMaintenanceTypeOption>();
            await using var reader = await command.ExecuteReaderAsync();
            var codeOrdinal = FindOrdinal(reader, "Maintenance_TypeId", "maintenance_type_id");
            var nameOrdinal = FindOrdinal(reader, "Name", "name");
            if (codeOrdinal is null || nameOrdinal is null)
            {
                return types;
            }

            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(codeOrdinal.Value) || reader.IsDBNull(nameOrdinal.Value))
                {
                    continue;
                }

                var name = reader.GetValue(nameOrdinal.Value)?.ToString()?.Trim();
                if (
                    !short.TryParse(reader.GetValue(codeOrdinal.Value)?.ToString(), out var code)
                    || string.IsNullOrWhiteSpace(name)
                )
                {
                    continue;
                }

                types.Add(new VehicleMaintenanceTypeOption(code, name));
            }

            return types;
        });

    public async Task<PreVehicleMaster> CreateAsync(PreVehicleMaster vehicleAuth, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(vehicleAuth);

        return await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
            var committed = false;

            try
            {
                var existing = string.IsNullOrWhiteSpace(vehicleAuth.chassis_number)
                    ? null
                    : (
                        await QueryAsync(
                            connection,
                            transaction,
                            schema,
                            chassisNumber: vehicleAuth.chassis_number!
                                .Trim()
                        )
                    ).SingleOrDefault();

                await EnsureLegacyCaptureIdentityAsync(
                    connection,
                    transaction,
                    vehicleAuth,
                    existing
                );
                await EnsureNoVehicleDuplicateAsync(connection, transaction, vehicleAuth);

                // The legacy capture page looks up the original capturer with
                // DEV_Check_UserCodeExists before insert. The user performing
                // the recall is only the action user; never replace ownership
                // merely because a new person edits the capture.
                var capturedByUserId = await ResolveCapturedByUserCodeAsync(
                    connection,
                    transaction,
                    vehicleAuth.chassis_number,
                    existing,
                    currentUserId
                );
                vehicleAuth.captured_by_user_code = ToShortUserCode(capturedByUserId);

                if (
                    existing is not null
                    && string.Equals(
                        existing.Authority_Status,
                        "Authorized",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Vehicle authorization already exists for chassis number: {vehicleAuth.chassis_number}"
                    );
                }

                var legacyCreationProcedure =
                    await GetVehicleCreationProcedureContractAsync(
                        connection,
                        transaction
                    );
                var usedLegacyCreationProcedure = legacyCreationProcedure is not null;

                int tempVmfCode;
                if (legacyCreationProcedure is not null)
                {
                    if (
                        legacyCreationProcedure.IncludesGgNumber
                        && string.IsNullOrWhiteSpace(vehicleAuth.fleet_number)
                    )
                    {
                        throw new ArgumentException(
                            "A legacy GG number is required to capture this vehicle inception record."
                        );
                    }

                    await ExecuteProcedureAsync(
                        connection,
                        transaction,
                        "DEV_INS_New_Vehicle_Master",
                        legacyCreationProcedure.IncludesGgNumber
                            ? new ProcedureParameter(
                                "@ggNumber",
                                DbType.String,
                                vehicleAuth.fleet_number?.Trim().ToUpperInvariant() ?? string.Empty
                            )
                            : null,
                        new ProcedureParameter(
                            "@replaced_gg_number",
                            DbType.String,
                            vehicleAuth.replaced_gg_number
                        ),
                        new ProcedureParameter("@model_code", DbType.Int16, vehicleAuth.model_code),
                        new ProcedureParameter("@colour", DbType.String, vehicleAuth.colour),
                        new ProcedureParameter(
                            "@year_manufactured",
                            DbType.Int16,
                            vehicleAuth.year_manufactured
                        ),
                        new ProcedureParameter(
                            "@chassis_number",
                            DbType.String,
                            vehicleAuth.chassis_number
                        ),
                        new ProcedureParameter(
                            "@engine_number",
                            DbType.String,
                            vehicleAuth.engine_number
                        ),
                        new ProcedureParameter(
                            "@take_on_odo",
                            DbType.Int32,
                            vehicleAuth.take_on_odo
                        ),
                        new ProcedureParameter(
                            "@take_on_date",
                            DbType.DateTime,
                            vehicleAuth.take_on_date
                        ),
                        new ProcedureParameter(
                            "@location_code",
                            DbType.Int16,
                            vehicleAuth.location_code
                        ),
                        new ProcedureParameter(
                            "@vehicle_status_code",
                            DbType.Int16,
                            vehicleAuth.vehicle_status_code
                        ),
                        new ProcedureParameter("@type_code", DbType.Int16, vehicleAuth.type_code),
                        new ProcedureParameter("@vs_code", DbType.Byte, vehicleAuth.vs_code),
                        new ProcedureParameter("@comment", DbType.String, vehicleAuth.comment),
                        legacyCreationProcedure.IncludesFleetNotes
                            ? new ProcedureParameter(
                                "@fleet_notes",
                                DbType.String,
                                vehicleAuth.Fleet_Notes
                            )
                            : null,
                        new ProcedureParameter(
                            "@purchase_amount",
                            DbType.Decimal,
                            vehicleAuth.purchase_amount
                        ),
                        new ProcedureParameter(
                            "@purchase_from",
                            DbType.String,
                            vehicleAuth.purchase_from
                        ),
                        new ProcedureParameter(
                            "@purchase_date",
                            DbType.DateTime,
                            vehicleAuth.purchase_date
                        ),
                        new ProcedureParameter(
                            "@captured_by_user_code",
                            DbType.Int16,
                            capturedByUserId
                        ),
                        new ProcedureParameter(
                            "@action_user_access_code",
                            DbType.Int16,
                            currentUserId
                        ),
                        new ProcedureParameter("@site_code", DbType.Int16, vehicleAuth.site_code),
                        legacyCreationProcedure.IncludesInvoice
                            ? new ProcedureParameter(
                                "@invoiceNumber",
                                DbType.String,
                                vehicleAuth.invoice_number
                            )
                            : null,
                        legacyCreationProcedure.IncludesGpNumber
                            ? new ProcedureParameter(
                                "@gpNumber",
                                DbType.String,
                                vehicleAuth.gp_number
                            )
                            : null,
                        legacyCreationProcedure.IncludesPreviousIdentity
                            ? new ProcedureParameter(
                                "@Old_chassis_number",
                                DbType.String,
                                null
                            )
                            : null,
                        legacyCreationProcedure.IncludesPreviousIdentity
                            ? new ProcedureParameter(
                                "@Old_engine_number",
                                DbType.String,
                                null
                            )
                            : null
                    );
                    var captured = (
                        await QueryAsync(
                            connection,
                            transaction,
                            schema,
                            chassisNumber: vehicleAuth.chassis_number!.Trim()
                        )
                    ).SingleOrDefault();
                    tempVmfCode = captured?.temp_vmf_code
                        ?? throw new InvalidOperationException(
                            "The legacy pre-vehicle capture procedure completed without a readable authorization record."
                        );
                }
                else if (existing is not null)
                {
                    tempVmfCode = existing.temp_vmf_code;
                    await UpdateRowAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode,
                        vehicleAuth,
                        currentUserId
                    );
                }
                else
                {
                    // Explicit compatibility fallback: this is used only when
                    // the original creation procedure is genuinely absent.
                    // Keep it parameterized and retain the legacy pre-vehicle
                    // result shape, while making the missing procedure visible
                    // to logs rather than silently claiming procedure parity.
                    tempVmfCode = await InsertRowAsync(
                        connection,
                        transaction,
                        schema,
                        vehicleAuth,
                        currentUserId
                    );
                }

                await WriteLegacyCaptureSideEffectsAsync(
                    connection,
                    transaction,
                    tempVmfCode,
                    vehicleAuth,
                    currentUserId,
                    writeInitialNote: !usedLegacyCreationProcedure
                );
                await transaction.CommitAsync();
                committed = true;

                return (
                    await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
                ).Single();
            }
            catch
            {
                if (!committed)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        });
    }

    public async Task UpdateAsync(PreVehicleMaster vehicleAuth, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(vehicleAuth);

        // The legacy capture page posts the same
        // DEV_INS_New_Vehicle_Master operation for both a new row and a
        // recalled/rejected row. Reuse the procedure-first path so its status
        // normalization, duplicate checks, and note side effects are retained;
        // CreateAsync's direct row update is only reached when the original
        // procedure is genuinely absent.
        _ = await CreateAsync(vehicleAuth, currentUserId);
    }

    public async Task<VehicleAuthorizationApprovalResult> ApproveAsync(
        int tempVmfCode,
        int authorizedByUserId,
        string? comment = null
    )
    {
        return await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
            var committed = false;
            VehicleAuthorizationApprovalResult approvalResult = new(null, null, null);
            try
            {
                var vehicle = (
                    await QueryAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode: tempVmfCode,
                        lockForUpdate: true
                    )
                ).SingleOrDefault();
                if (vehicle is null)
                {
                    throw new KeyNotFoundException(
                        $"Vehicle authorization with ID {tempVmfCode} not found"
                    );
                }

                if (
                    !string.Equals(
                        vehicle.Authority_Status,
                        "Awaiting Authorization",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Vehicle authorization {tempVmfCode} is not awaiting authorization"
                    );
                }

                approvalResult = await EnsureGgNumbersAvailableForAuthorizationAsync(
                    connection,
                    transaction
                );

                var approvalProcedure = await ProcedureMatchesAsync(
                    connection,
                    transaction,
                    "DEV_INS_VehicleFromPre_Vehicle_Master",
                    "@ChassisNo",
                    "@comment",
                    "@captured_by_user_code"
                );
                if (approvalProcedure)
                {
                    // Promotion inserts vehicle_master and the legacy trigger
                    // creates the first fin.vehicle_tariff for eligible new
                    // vehicles. Do not authorize a vehicle when that
                    // source-backed tariff trigger is missing or disabled.
                    await EnsureEnabledTriggerAsync(
                        connection,
                        transaction,
                        "dbo",
                        VehicleTableName,
                        "TRG_UPSERT_CheckPurchaseAmount"
                    );
                    // This procedure owns the legacy GG allocation and the
                    // promotion into vehicle_master. Do not replace it on a
                    // client's legacy database.
                    await ExecuteProcedureAsync(
                        connection,
                        transaction,
                        "DEV_INS_VehicleFromPre_Vehicle_Master",
                        new ProcedureParameter("@ChassisNo", DbType.String, vehicle.chassis_number),
                        new ProcedureParameter("@comment", DbType.String, comment ?? string.Empty),
                        new ProcedureParameter(
                            "@captured_by_user_code",
                            DbType.Int16,
                            authorizedByUserId
                        )
                    );
                }
                else
                {
                    await EnsureEnabledTriggerAsync(
                        connection,
                        transaction,
                        "dbo",
                        VehicleTableName,
                        "TRG_UPSERT_CheckPurchaseAmount"
                    );
                    await PromoteWithoutLegacyProcedureAsync(
                        connection,
                        transaction,
                        vehicle,
                        authorizedByUserId
                    );
                    await UpdateAuthorizationStatusAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode,
                        "Authorized",
                        authorizedByUserId,
                        comment,
                        null,
                        vehicle.vmf_code
                    );
                    await WriteLegacyAuthorizationNoteAsync(
                        connection,
                        transaction,
                        vehicle.chassis_number,
                        comment,
                        authorizedByUserId
                    );
                }

                // Promotion procedures allocate the VMF on vehicle_master and
                // may only copy that value back to pre_vehicle_master as a
                // side effect. Re-read the authorization row before checking
                // the tariff so we validate the actual promoted vehicle code.
                var promotedVehicle = (
                    await QueryAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode: tempVmfCode
                    )
                ).SingleOrDefault();
                if (promotedVehicle is null)
                {
                    throw new InvalidOperationException(
                        "Vehicle authorization promotion completed without a readable pre-vehicle row."
                    );
                }

                var promotedVmfCode = promotedVehicle.vmf_code;
                if (promotedVmfCode is not > 0 && !string.IsNullOrWhiteSpace(promotedVehicle.chassis_number))
                {
                    await using var vmfCommand = connection.CreateCommand();
                    vmfCommand.Transaction = transaction;
                    vmfCommand.CommandText = $"SELECT TOP (1) [vmf_code] FROM [dbo].[{VehicleTableName}] WHERE [chassis_number] = @chassisNumber";
                    AddParameter(vmfCommand, "@chassisNumber", DbType.String, promotedVehicle.chassis_number.Trim());
                    var rawVmfCode = await vmfCommand.ExecuteScalarAsync();
                    if (rawVmfCode is not null and not DBNull)
                        promotedVmfCode = Convert.ToInt32(rawVmfCode);
                }

                await EnsureCurrentVehicleTariffAsync(
                    connection,
                    transaction,
                    promotedVmfCode,
                    promotedVehicle.year_manufactured,
                    promotedVehicle.type_code,
                    promotedVehicle.model_code
                );
                if (promotedVmfCode is > 0)
                {
                    await UpdateMaintenanceVmfCodeAfterPromotionAsync(
                        connection,
                        transaction,
                        promotedVmfCode.Value,
                        tempVmfCode
                    );
                }
                await transaction.CommitAsync();
                committed = true;
            }
            catch
            {
                if (!committed)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }

            return approvalResult;
        });
    }

    public async Task RejectAsync(
        int tempVmfCode,
        int rejectedByUserId,
        string rejectionReason,
        string? comment = null
    )
    {
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
            var committed = false;
            try
            {
                var vehicle = (
                    await QueryAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode: tempVmfCode,
                        lockForUpdate: true
                    )
                ).SingleOrDefault();
                if (vehicle is null)
                {
                    throw new KeyNotFoundException(
                        $"Vehicle authorization with ID {tempVmfCode} not found"
                    );
                }

                if (
                    !string.Equals(
                        vehicle.Authority_Status,
                        "Awaiting Authorization",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Vehicle authorization {tempVmfCode} is not awaiting authorization"
                    );
                }

                // The legacy rejection procedure changes the pre-vehicle
                // status, stores the rejection comment and actor, and writes
                // the corresponding pre-vehicle note. Keep those side effects
                // database-owned whenever the original procedure exists.
                var legacyComment =
                    $"[Rejected] {(string.IsNullOrWhiteSpace(comment) ? rejectionReason.Trim() : comment.Trim())}";
                var rejectionProcedure = await ProcedureMatchesAsync(
                    connection,
                    transaction,
                    "DEV_UPD_Rejected_PreVehicles",
                    "@ChassisNo",
                    "@comment",
                    "@captured_by_user_code"
                );
                if (rejectionProcedure)
                {
                    await ExecuteProcedureAsync(
                        connection,
                        transaction,
                        "DEV_UPD_Rejected_PreVehicles",
                        new ProcedureParameter(
                            "@ChassisNo",
                            DbType.String,
                            vehicle.chassis_number
                        ),
                        new ProcedureParameter("@comment", DbType.String, legacyComment),
                        new ProcedureParameter(
                            "@captured_by_user_code",
                            DbType.Int16,
                            rejectedByUserId
                        )
                    );
                }
                else
                {
                    await UpdateAuthorizationStatusAsync(
                        connection,
                        transaction,
                        schema,
                        tempVmfCode,
                        "Rejected",
                        rejectedByUserId,
                        legacyComment,
                        rejectionReason,
                        null
                    );
                    await WriteLegacyAuthorizationNoteAsync(
                        connection,
                        transaction,
                        vehicle.chassis_number,
                        legacyComment,
                        rejectedByUserId
                    );
                }
                await transaction.CommitAsync();
                committed = true;
            }
            catch
            {
                if (!committed)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }

            return true;
        });
    }

    public async Task AddCommentAsync(int tempVmfCode, string comment, int modifiedByUserId)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ArgumentException("Comment cannot be empty", nameof(comment));
        }

        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var vehicle = (
                await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
            ).SingleOrDefault();
            if (vehicle is null)
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            var timestampedComment = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {comment.Trim()}";
            var combinedComment = string.IsNullOrWhiteSpace(vehicle.authorization_comment)
                ? timestampedComment
                : $"{vehicle.authorization_comment}\n{timestampedComment}";
            var values = new List<WriteValue>();
            AddValue(
                values,
                schema.Columns,
                "authorization_comment",
                DbType.String,
                combinedComment
            );
            AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
            AddValue(
                values,
                schema.Columns,
                "modified_by_user_code",
                DbType.Int32,
                modifiedByUserId > 0 ? modifiedByUserId : null
            );
            await ExecuteUpdateAsync(connection, null, tempVmfCode, values);

            if (
                await ProcedureMatchesAsync(
                    connection,
                    null,
                    "DEV_INS_PreVehicle_master_Notes",
                    "@comment",
                    "@chassis_number",
                    "@added_by_user_code"
                )
            )
            {
                await ExecuteProcedureAsync(
                    connection,
                    null,
                    "DEV_INS_PreVehicle_master_Notes",
                    new ProcedureParameter("@comment", DbType.String, comment.Trim()),
                    new ProcedureParameter(
                        "@chassis_number",
                        DbType.String,
                        vehicle.chassis_number
                    ),
                    new ProcedureParameter("@added_by_user_code", DbType.Int16, modifiedByUserId)
                );
            }

            return true;
        });
    }

    public async Task DeleteAsync(int tempVmfCode, int currentUserId)
    {
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var values = new List<WriteValue>();
            if (schema.Columns.Contains("is_deleted"))
            {
                AddValue(values, schema.Columns, "is_deleted", DbType.Boolean, true);
            }
            else
            {
                AddValue(values, schema.Columns, "Authority_Status", DbType.String, "Deleted");
            }

            AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
            AddValue(
                values,
                schema.Columns,
                "modified_by_user_code",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
            if (
                values.Count == 0
                || await ExecuteUpdateAsync(connection, null, tempVmfCode, values) == 0
            )
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            return true;
        });
    }

    public async Task ClearFromAuthorityListAsync(int tempVmfCode)
    {
        await WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var vehicle = (
                await QueryAsync(connection, null, schema, tempVmfCode: tempVmfCode)
            ).SingleOrDefault();
            if (vehicle is null)
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            if (
                !string.Equals(
                    vehicle.Authority_Status,
                    "Authorized",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    $"Vehicle authorization {tempVmfCode} is not authorized for printing and inception."
                );
            }

            var chassisNumber = vehicle.chassis_number?.Trim();
            if (string.IsNullOrWhiteSpace(chassisNumber))
            {
                throw new InvalidOperationException(
                    $"Vehicle authorization {tempVmfCode} has no chassis number for DEV_CLR_NewVehicleFromAuthList."
                );
            }

            if (
                await ProcedureMatchesAsync(
                    connection,
                    null,
                    "DEV_CLR_NewVehicleFromAuthList",
                    "@chassisNo"
                )
            )
            {
                await ExecuteProcedureAsync(
                    connection,
                    null,
                    "DEV_CLR_NewVehicleFromAuthList",
                    new ProcedureParameter("@chassisNo", DbType.String, chassisNumber)
                );
                return true;
            }

            if (!schema.Columns.Contains("printed"))
            {
                throw new NotSupportedException(
                    "The legacy procedure DEV_CLR_NewVehicleFromAuthList is unavailable and pre_vehicle_master.printed is not present. No direct-DML fallback was run."
                );
            }

            var values = new List<WriteValue>();
            AddValue(values, schema.Columns, "printed", DbType.String, "Y");
            AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
            if (await ExecuteUpdateAsync(connection, null, tempVmfCode, values) == 0)
            {
                throw new KeyNotFoundException(
                    $"Vehicle authorization with ID {tempVmfCode} not found"
                );
            }

            return true;
        });
    }

    public Task<VehicleAuthorizationPrintSnapshot?> GetPrintSnapshotAsync(
        int tempVmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    ) =>
        WithConnectionAsync(async connection =>
        {
            var schema = await GetSchemaAsync(connection, null);
            var vehicle = (
                await QueryAsync(
                    connection,
                    null,
                    schema,
                    tempVmfCode: tempVmfCode,
                    allowedSiteCodes: allowedSiteCodes,
                    currentUserId: currentUserId
                )
            ).SingleOrDefault();
            if (vehicle is null)
            {
                return null;
            }

            var chassisNumber = vehicle.chassis_number?.Trim();
            var printRow = await ReadPrintVehicleDetailsAsync(connection, chassisNumber);
            var extras = await ReadPrintExtrasAsync(connection, chassisNumber);
            var userRow = await ReadPrintUserDetailsAsync(connection, chassisNumber);
            OverlayPrintVehicleDetails(vehicle, printRow);

            return new VehicleAuthorizationPrintSnapshot(
                vehicle,
                extras,
                GetRowValue(printRow, "Site Name", "site_name", "SiteName"),
                GetRowValue(printRow, "location", "Location"),
                GetRowValue(printRow, "Hired From", "hired_from", "HiredFrom"),
                GetRowValue(printRow, "Hire Type", "hire_type", "HireType"),
                GetRowValue(printRow, "status", "Status"),
                GetRowValue(userRow, "Captured By", "captured_by", "CapturedBy"),
                ParseRowDate(userRow, "date captured", "date_captured", "captured_date"),
                GetRowValue(userRow, "Authorized By", "authorized_by", "AuthorizedBy"),
                ParseRowDate(userRow, "authority_date", "authorization_date", "date authorized")
            );
        });

    public Task<IReadOnlyList<VehicleStatusComment>> GetVehicleStatusCommentsAsync(
        string chassisNumber
    ) =>
        string.IsNullOrWhiteSpace(chassisNumber)
            ? Task.FromResult<IReadOnlyList<VehicleStatusComment>>([])
            : WithConnectionAsync(connection =>
                ReadStatusCommentsAsync(connection, chassisNumber.Trim())
            );

    private async Task<T> WithConnectionAsync<T>(Func<DbConnection, Task<T>> operation)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            return await operation(connection);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<VehicleAuthorizationSchema> GetSchemaAsync(
        DbConnection connection,
        DbTransaction? transaction
    )
    {
        var columns = await GetColumnsAsync(connection, transaction, "dbo", PreVehicleTableName);
        if (!columns.Contains("temp_vmf_code"))
        {
            throw new InvalidOperationException(
                "The pre_vehicle_master compatibility table is not available."
            );
        }

        var modelColumns = await GetColumnsAsync(connection, transaction, "dbo", ModelTableName);
        await using var identityCommand = connection.CreateCommand();
        identityCommand.Transaction = transaction;
        identityCommand.CommandText =
            $"SELECT COLUMNPROPERTY(OBJECT_ID(N'[dbo].[{PreVehicleTableName}]'), N'temp_vmf_code', 'IsIdentity')";
        var identityValue = await identityCommand.ExecuteScalarAsync();
        return new VehicleAuthorizationSchema(
            columns,
            modelColumns,
            Convert.ToInt32(identityValue) == 1
        );
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string schemaName,
        string tableName
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema
              AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, schemaName);
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<int> CountByStatusAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        string status,
        int? capturedByUserCode,
        IReadOnlySet<short>? allowedSiteCodes,
        int? currentUserId
    )
    {
        var hasStatusColumn = schema.Columns.Contains("Authority_Status");
        if (
            !hasStatusColumn
            && !string.Equals(status, "Awaiting Authorization", StringComparison.OrdinalIgnoreCase)
        )
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var conditions = new List<string> { GetNotDeletedFilter("p", schema.Columns) };
        if (hasStatusColumn)
        {
            conditions.Add("[p].[Authority_Status] = @authorityStatus");
            AddParameter(command, "@authorityStatus", DbType.String, status);
        }

        AddUnprintedAuthorizedFilter(conditions, schema.Columns, status);

        if (capturedByUserCode.HasValue)
        {
            var ownerColumn = schema.Columns.Contains("captured_by_user_code")
                ? "captured_by_user_code"
                : schema.Columns.Contains("created_by_user_code")
                    ? "created_by_user_code"
                    : null;
            if (ownerColumn is null)
                return 0;
            conditions.Add($"[p].[{ownerColumn}] = @capturedByUserCode");
            AddParameter(command, "@capturedByUserCode", DbType.Int32, capturedByUserCode.Value);
        }

        AddScopeConditions(conditions, command, schema.Columns, allowedSiteCodes, currentUserId);

        command.CommandText =
            $"SELECT COUNT(1) FROM [dbo].[{PreVehicleTableName}] AS [p] WHERE {string.Join(" AND ", conditions)}";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string GetQueueOrder(VehicleAuthorizationSchema schema, bool awaiting)
    {
        if (awaiting)
        {
            return schema.Columns.Contains("chassis_number")
                ? "[p].[chassis_number] ASC, [p].[temp_vmf_code] ASC"
                : "[p].[temp_vmf_code] ASC";
        }

        if (schema.Columns.Contains("authorization_date"))
        {
            return "[p].[authorization_date] DESC, [p].[temp_vmf_code] DESC";
        }

        return schema.Columns.Contains("date_created")
            ? "[p].[date_created] DESC, [p].[temp_vmf_code] DESC"
            : "[p].[temp_vmf_code] DESC";
    }

    private static async Task<List<PreVehicleMaster>> QueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        int? tempVmfCode = null,
        string? chassisNumber = null,
        string? status = null,
        IReadOnlyCollection<string>? statuses = null,
        int? capturedByUserCode = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        long? skip = null,
        int? take = null,
        string? orderBy = null,
        bool lockForUpdate = false,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    )
    {
        if (
            status is not null
            && !schema.Columns.Contains("Authority_Status")
            && !string.Equals(status, "Awaiting Authorization", StringComparison.OrdinalIgnoreCase)
        )
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(chassisNumber) && !schema.Columns.Contains("chassis_number"))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var conditions = new List<string> { GetNotDeletedFilter("p", schema.Columns) };
        var joins = string.Empty;

        if (tempVmfCode.HasValue)
        {
            conditions.Add("[p].[temp_vmf_code] = @tempVmfCode");
            AddParameter(command, "@tempVmfCode", DbType.Int32, tempVmfCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(chassisNumber))
        {
            conditions.Add("[p].[chassis_number] = @chassisNumber");
            AddParameter(command, "@chassisNumber", DbType.String, chassisNumber.Trim());
        }

        if (schema.Columns.Contains("Authority_Status"))
        {
            if (status is not null)
            {
                conditions.Add("[p].[Authority_Status] = @authorityStatus");
                AddParameter(command, "@authorityStatus", DbType.String, status);
            }
            else if (statuses is not null && statuses.Count > 0)
            {
                var statusParameters = statuses
                    .Select((_, index) => $"@historyStatus{index}")
                    .ToArray();
                conditions.Add(
                    $"[p].[Authority_Status] IN ({string.Join(", ", statusParameters)})"
                );
                for (var index = 0; index < statuses.Count; index++)
                {
                    AddParameter(
                        command,
                        statusParameters[index],
                        DbType.String,
                        statuses.ElementAt(index)
                    );
                }
            }
        }

        AddUnprintedAuthorizedFilter(conditions, schema.Columns, status);

        if (capturedByUserCode.HasValue)
        {
            var ownerColumn = schema.Columns.Contains("captured_by_user_code")
                ? "captured_by_user_code"
                : schema.Columns.Contains("created_by_user_code")
                    ? "created_by_user_code"
                    : null;
            if (ownerColumn is null)
                return [];
            conditions.Add($"[p].[{ownerColumn}] = @capturedByUserCode");
            AddParameter(command, "@capturedByUserCode", DbType.Int32, capturedByUserCode.Value);
        }

        AddScopeConditions(conditions, command, schema.Columns, allowedSiteCodes, currentUserId);

        var dateColumn =
            schema.Columns.Contains("authorization_date") ? "authorization_date"
            : schema.Columns.Contains("date_created") ? "date_created"
            : null;
        if (startDate.HasValue && dateColumn is not null)
        {
            conditions.Add($"[p].[{dateColumn}] >= @startDate");
            AddParameter(command, "@startDate", DbType.DateTime2, startDate.Value);
        }

        if (endDate.HasValue && dateColumn is not null)
        {
            conditions.Add($"[p].[{dateColumn}] <= @endDate");
            AddParameter(command, "@endDate", DbType.DateTime2, endDate.Value);
        }

        var modelProjection =
            schema.ModelColumns.Contains("model_code")
            && schema.ModelColumns.Contains("model_description")
                ? "[m].[model_description] AS [model_description]"
                : "CAST(NULL AS nvarchar(255)) AS [model_description]";
        if (
            schema.ModelColumns.Contains("model_code")
            && schema.ModelColumns.Contains("model_description")
        )
        {
            joins = "LEFT JOIN [dbo].[model] AS [m] ON [m].[model_code] = [p].[model_code]";
        }

        var projection = string.Join(
            ", ",
            SelectedColumns
                .Select(column => GetProjection("p", schema.Columns, column))
                .Append(modelProjection)
        );
        var orderColumn =
            schema.Columns.Contains("authorization_date") ? "authorization_date"
            : schema.Columns.Contains("date_created") ? "date_created"
            : "temp_vmf_code";
        var orderClause = orderBy ?? $"[p].[{orderColumn}] DESC, [p].[temp_vmf_code] DESC";
        var tableHint = lockForUpdate ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
        command.CommandText =
            $"SELECT {projection} FROM [dbo].[{PreVehicleTableName}] AS [p]{tableHint} {joins} WHERE {string.Join(" AND ", conditions)} ORDER BY {orderClause}";
        if (skip.HasValue && take.HasValue)
        {
            command.CommandText += " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
            AddParameter(command, "@skip", DbType.Int64, skip.Value);
            AddParameter(command, "@take", DbType.Int32, take.Value);
        }

        var rows = new List<PreVehicleMaster>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(Map(reader));
        }

        return rows;
    }

    private static void AddScopeConditions(
        ICollection<string> conditions,
        DbCommand command,
        IReadOnlySet<string> columns,
        IReadOnlySet<short>? allowedSiteCodes,
        int? currentUserId
    )
    {
        if (allowedSiteCodes is null)
        {
            return;
        }

        var sites = allowedSiteCodes.Where(site => site > 0).Distinct().ToArray();
        if (sites.Length == 0)
        {
            conditions.Add("1 = 0");
            return;
        }

        var siteColumn = columns.Contains("site_code") ? "site_code" : null;
        var ownerColumns = currentUserId is > 0
            ? new[] { "captured_by_user_code", "created_by_user_code" }
                .Where(columns.Contains)
                .ToArray()
            : [];
        var siteParameters = sites.Select((_, index) => $"@allowedVehicleSite{index}").ToArray();
        var predicates = new List<string>();
        if (siteColumn is not null)
        {
            predicates.Add($"[p].[{siteColumn}] IN ({string.Join(", ", siteParameters)})");
        }
        predicates.AddRange(ownerColumns.Select(column => $"[p].[{column}] = @allowedVehicleOwner"));
        if (predicates.Count == 0)
        {
            conditions.Add("1 = 0");
            return;
        }

        conditions.Add("(" + string.Join(" OR ", predicates) + ")");
        for (var index = 0; index < sites.Length; index++)
        {
            AddParameter(command, siteParameters[index], DbType.Int16, sites[index]);
        }
        if (ownerColumns.Length > 0)
        {
            AddParameter(command, "@allowedVehicleOwner", DbType.Int32, currentUserId);
        }
    }

    private static PreVehicleMaster Map(DbDataReader reader) =>
        new()
        {
            temp_vmf_code = ReadInt32(reader, "temp_vmf_code") ?? 0,
            fleet_number = ReadString(reader, "fleet_number"),
            registration_number = ReadString(reader, "registration_number"),
            replaced_gg_number = ReadString(reader, "replaced_gg_number"),
            model_code = ReadInt16(reader, "model_code") ?? 0,
            Model = ReadModel(reader),
            colour = ReadString(reader, "colour"),
            year_manufactured = ReadInt16(reader, "year_manufactured"),
            chassis_number = ReadString(reader, "chassis_number"),
            engine_number = ReadString(reader, "engine_number"),
            take_on_odo = ReadInt32(reader, "take_on_odo"),
            take_on_date = ReadDateTime(reader, "take_on_date"),
            location_code = ReadInt16(reader, "location_code"),
            vehicle_status_code = ReadInt16(reader, "vehicle_status_code"),
            vehicle_status_date = ReadDateTime(reader, "vehicle_status_date"),
            type_code = ReadInt16(reader, "type_code"),
            vs_code = ReadByte(reader, "vs_code"),
            comment = ReadString(reader, "comment"),
            purchase_amount = ReadDecimal(reader, "purchase_amount"),
            purchase_from = ReadString(reader, "purchase_from"),
            purchase_date = ReadDateTime(reader, "purchase_date"),
            captured_by_user_code = ReadInt16(reader, "captured_by_user_code"),
            action_user_access_code = ReadInt16(reader, "action_user_access_code"),
            site_code = ReadInt16(reader, "site_code"),
            Authority_Status = ReadString(reader, "Authority_Status") ?? "Awaiting Authorization",
            captured_date = ReadDateTime(reader, "captured_date"),
            printed = ReadString(reader, "printed"),
            invoice_number = ReadString(reader, "invoice_number"),
            gp_number = ReadString(reader, "gp_number"),
            Fleet_Notes = ReadString(reader, "Fleet_Notes"),
            damage_status = ReadString(reader, "damage_status"),
            damages_comment = ReadString(reader, "damages_comment"),
            authorized_by_user_code = ReadInt32(reader, "authorized_by_user_code"),
            authorization_date = ReadDateTime(reader, "authorization_date"),
            rejection_reason = ReadString(reader, "rejection_reason"),
            authorization_comment = ReadString(reader, "authorization_comment"),
            vmf_code = ReadInt32(reader, "vmf_code"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code =
                ReadInt32(reader, "created_by_user_code")
                ?? ReadInt16(reader, "captured_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private static FIS.Core.Domain.Entities.Model? ReadModel(DbDataReader reader)
    {
        var description = ReadString(reader, "model_description");
        return string.IsNullOrWhiteSpace(description)
            ? null
            : new FIS.Core.Domain.Entities.Model { model_description = description };
    }

    private static async Task<int> InsertRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        VehicleAuthorizationSchema schema,
        PreVehicleMaster vehicle,
        int currentUserId
    )
    {
        var values = BuildValues(vehicle, schema.Columns, currentUserId, includeCreateAudit: true);
        if (!schema.IsIdentity)
        {
            var code = await GetNextCodeAsync(connection, transaction);
            AddValue(values, schema.Columns, "temp_vmf_code", DbType.Int32, code);
            await ExecuteInsertAsync(connection, transaction, values, outputKey: false);
            return code;
        }

        return await ExecuteInsertAsync(connection, transaction, values, outputKey: true);
    }

    private static async Task<int> ResolveCapturedByUserCodeAsync(
        DbConnection connection,
        DbTransaction transaction,
        string? chassisNumber,
        PreVehicleMaster? existing,
        int currentUserId
    )
    {
        if (
            !string.IsNullOrWhiteSpace(chassisNumber)
            && await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_Check_UserCodeExists",
                "@chassis_no"
            )
        )
        {
            var rows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                "DEV_Check_UserCodeExists",
                new ProcedureParameter("@chassis_no", DbType.String, chassisNumber.Trim())
            );
            var userCode = rows.Count == 0
                ? null
                : ReadRowInt32(rows[0], "User Code", "UserCode", "user_code");
            return userCode is > 0 ? userCode.Value : currentUserId;
        }

        return existing?.captured_by_user_code is > 0
            ? existing.captured_by_user_code.Value
            : currentUserId;
    }

    private static async Task EnsureLegacyCaptureIdentityAsync(
        DbConnection connection,
        DbTransaction transaction,
        PreVehicleMaster vehicle,
        PreVehicleMaster? existing
    )
    {
        var chassisNumber = vehicle.chassis_number?.Trim();
        if (
            !string.IsNullOrWhiteSpace(chassisNumber)
            && await ProcedureMatchesAsync(
                connection,
                transaction,
                "Dev_Val_Chassis_No",
                "@chassis_number"
            )
        )
        {
            var rows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                "Dev_Val_Chassis_No",
                new ProcedureParameter("@chassis_number", DbType.String, chassisNumber)
            );
            var chassisRec = rows.Count == 0 ? 0 : ReadFirstColumnInt(rows[0]);
            var existingChassis = existing?.chassis_number?.Trim();
            if (
                chassisRec == 1
                && !string.Equals(existingChassis, chassisNumber, StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "There is a Vehicle with the VIN/Chassis Number you have entered, please validate and enter another one."
                );
            }
        }

        var engineNumber = vehicle.engine_number?.Trim();
        if (
            !string.IsNullOrWhiteSpace(engineNumber)
            && await ProcedureMatchesAsync(
                connection,
                transaction,
                "Dev_Val_Engine_No",
                "@Engine_number"
            )
        )
        {
            var rows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                "Dev_Val_Engine_No",
                new ProcedureParameter("@Engine_number", DbType.String, engineNumber)
            );
            var engineRec = rows.Count == 0 ? 0 : ReadFirstColumnInt(rows[0]);
            var existingEngine = existing?.engine_number?.Trim();
            if (
                engineRec == 1
                && !string.Equals(existingEngine, engineNumber, StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new InvalidOperationException(
                    "There is a Vehicle with the Engine Number you have entered, please validate and enter another one."
                );
            }
        }

        var replacedGg = vehicle.replaced_gg_number?.Trim().ToUpperInvariant();
        var existingReplaced = existing?.replaced_gg_number?.Trim().ToUpperInvariant();
        if (
            string.IsNullOrWhiteSpace(replacedGg)
            || string.Equals(existingReplaced, replacedGg, StringComparison.OrdinalIgnoreCase)
            || !await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_VAL_Replace_GGNumber",
                "@Replace_GGNumber"
            )
        )
        {
            return;
        }

        var replaceRows = await ExecuteProcedureRowsAsync(
            connection,
            transaction,
            "DEV_VAL_Replace_GGNumber",
            new ProcedureParameter("@Replace_GGNumber", DbType.String, replacedGg)
        );
        var replaceStatus = replaceRows.Count == 0
            ? null
            : ReadRowString(replaceRows[0], "Replace GG Status", "ReplaceGGStatus");
        if (string.Equals(replaceStatus, "in_pre_vehicle_master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cannot replace a Vehicle that is awaiting authorization, please validate and fix before you submit."
            );
        }

        if (
            string.Equals(replaceStatus, "in_block_gg_numbers", StringComparison.OrdinalIgnoreCase)
            || string.Equals(replaceStatus, "not_exists", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "The Replace GG Number you have entered does not exist, please validate and fix before you submit."
            );
        }

        if (string.Equals(replaceStatus, "invalid_vehicle", StringComparison.OrdinalIgnoreCase))
        {
            var vehicleStatus = ReadRowString(replaceRows[0], "vehicle status", "Vehicle Status");
            throw new InvalidOperationException(
                $"The Replace GG Number you have entered is in status [{vehicleStatus}]. Only the following vehicle statuses may be replaced [Stolen, Withdrawn, Sold, Missing or Destroyed]."
            );
        }
    }

    private static async Task<VehicleAuthorizationApprovalResult> EnsureGgNumbersAvailableForAuthorizationAsync(
        DbConnection connection,
        DbTransaction transaction
    )
    {
        if (!await ProcedureMatchesAsync(connection, transaction, "DEV_Check_GGNumAvailabilityStatus"))
        {
            return new VehicleAuthorizationApprovalResult(null, null, null);
        }

        var availabilityRows = await ExecuteProcedureRowsAsync(
            connection,
            transaction,
            "DEV_Check_GGNumAvailabilityStatus"
        );
        if (availabilityRows.Count == 0)
        {
            throw new InvalidOperationException(
                "There are no available GG Numbers in the System, therefore this vehicle will not be accepted."
            );
        }

        var available = ReadRowInt32(
            availabilityRows[0],
            "Availabe GGNumbs",
            "Available GGNumbs",
            "Available_GGNumbs"
        );
        var returnStatus = ReadRowInt32(
            availabilityRows[0],
            "Return Status",
            "ReturnStatus"
        );
        if (available == 0 && returnStatus == 0)
        {
            throw new InvalidOperationException(
                "There are no available GG Numbers in the System, therefore this vehicle will not be accepted."
            );
        }

        var canAuthorize =
            (available > 100 && returnStatus == -1)
            || (available is >= 1 and <= 100 && returnStatus == 1);
        if (!canAuthorize)
        {
            throw new InvalidOperationException(
                "There are no available GG Numbers in the System, therefore this vehicle will not be accepted."
            );
        }

        string? allocatedGg = null;
        if (await ProcedureMatchesAsync(connection, transaction, "DEV_SEL_Allocated_GG_NO"))
        {
            var allocatedRows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                "DEV_SEL_Allocated_GG_NO"
            );
            allocatedGg = allocatedRows.Count == 0
                ? null
                : ReadRowString(allocatedRows[0], "GG_Number", "GG Number");
        }

        return new VehicleAuthorizationApprovalResult(allocatedGg, available, returnStatus);
    }

    private static async Task EnsureNoVehicleDuplicateAsync(
        DbConnection connection,
        DbTransaction transaction,
        PreVehicleMaster vehicle
    )
    {
        var columns = await GetColumnsAsync(connection, transaction, "dbo", VehicleTableName);
        var checks = new List<(string Column, string Parameter, object? Value)>();

        if (
            !string.IsNullOrWhiteSpace(vehicle.chassis_number) && columns.Contains("chassis_number")
        )
        {
            checks.Add(("chassis_number", "@chassisNumber", vehicle.chassis_number.Trim()));
        }

        var engineColumn =
            columns.Contains("engine_number_1") ? "engine_number_1"
            : columns.Contains("engine_number") ? "engine_number"
            : null;
        if (!string.IsNullOrWhiteSpace(vehicle.engine_number) && engineColumn is not null)
        {
            checks.Add((engineColumn, "@engineNumber", vehicle.engine_number.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(vehicle.fleet_number) && columns.Contains("fleet_number"))
        {
            checks.Add(("fleet_number", "@fleetNumber", vehicle.fleet_number.Trim()));
        }

        if (
            !string.IsNullOrWhiteSpace(vehicle.gp_number) && columns.Contains("registration_number")
        )
        {
            checks.Add(("registration_number", "@registrationNumber", vehicle.gp_number.Trim()));
        }

        if (checks.Count == 0 || !columns.Contains("vmf_code"))
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT TOP (1) [v].[vmf_code] FROM [dbo].[{VehicleTableName}] AS [v] WHERE {GetNotDeletedFilter("v", columns)} AND ({string.Join(" OR ", checks.Select(check => $"[v].[{check.Column}] = {check.Parameter}"))})";
        foreach (var check in checks)
        {
            AddParameter(command, check.Parameter, DbType.String, check.Value);
        }

        var existingCode = await command.ExecuteScalarAsync();
        if (existingCode is not null and not DBNull)
        {
            throw new InvalidOperationException(
                $"A vehicle already exists with one of the supplied identifying values (VMF {existingCode})."
            );
        }
    }

    private static async Task UpdateRowAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        int tempVmfCode,
        PreVehicleMaster vehicle,
        int currentUserId
    )
    {
        var values = BuildValues(vehicle, schema.Columns, currentUserId, includeCreateAudit: false);
        if (
            values.Count == 0
            || await ExecuteUpdateAsync(connection, transaction, tempVmfCode, values) == 0
        )
        {
            throw new KeyNotFoundException(
                $"Vehicle authorization with ID {tempVmfCode} not found"
            );
        }
    }

    private static List<WriteValue> BuildValues(
        PreVehicleMaster vehicle,
        IReadOnlySet<string> columns,
        int currentUserId,
        bool includeCreateAudit
    )
    {
        var takeOnDate = vehicle.take_on_date ?? DateTime.Now;
        var purchaseDate = vehicle.purchase_date ?? takeOnDate;
        var values = new List<WriteValue>();
        AddValue(values, columns, "fleet_number", DbType.String, vehicle.fleet_number);
        AddValue(
            values,
            columns,
            "registration_number",
            DbType.String,
            vehicle.registration_number
        );
        AddValue(values, columns, "replaced_gg_number", DbType.String, vehicle.replaced_gg_number);
        AddValue(values, columns, "model_code", DbType.Int16, vehicle.model_code);
        AddValue(values, columns, "colour", DbType.String, vehicle.colour ?? "UNKNOWN");
        AddValue(values, columns, "year_manufactured", DbType.Int16, vehicle.year_manufactured);
        AddValue(values, columns, "chassis_number", DbType.String, vehicle.chassis_number);
        AddValue(values, columns, "engine_number", DbType.String, vehicle.engine_number);
        AddValue(values, columns, "take_on_odo", DbType.Int32, vehicle.take_on_odo ?? 0);
        AddValue(values, columns, "take_on_date", DbType.DateTime2, takeOnDate);
        AddValue(values, columns, "location_code", DbType.Int16, vehicle.location_code ?? 0);
        AddValue(
            values,
            columns,
            "vehicle_status_code",
            DbType.Int16,
            vehicle.vehicle_status_code ?? 0
        );
        AddValue(
            values,
            columns,
            "vehicle_status_date",
            DbType.DateTime2,
            vehicle.vehicle_status_date ?? takeOnDate
        );
        AddValue(values, columns, "type_code", DbType.Int16, vehicle.type_code ?? 1);
        AddValue(values, columns, "vs_code", DbType.Byte, vehicle.vs_code ?? 1);
        AddValue(values, columns, "comment", DbType.String, vehicle.comment ?? string.Empty);
        AddValue(values, columns, "purchase_amount", DbType.Decimal, vehicle.purchase_amount ?? 0m);
        AddValue(
            values,
            columns,
            "purchase_from",
            DbType.String,
            vehicle.purchase_from ?? string.Empty
        );
        AddValue(values, columns, "purchase_date", DbType.DateTime2, purchaseDate);
        AddValue(
            values,
            columns,
            "captured_by_user_code",
            DbType.Int16,
            vehicle.captured_by_user_code ?? ToShortUserCode(currentUserId)
        );
        AddValue(
            values,
            columns,
            "action_user_access_code",
            DbType.Int16,
            vehicle.action_user_access_code ?? ToShortUserCode(currentUserId)
        );
        AddValue(values, columns, "site_code", DbType.Int16, vehicle.site_code);
        AddValue(values, columns, "Authority_Status", DbType.String, "Awaiting Authorization");
        AddValue(
            values,
            columns,
            "captured_date",
            DbType.DateTime2,
            vehicle.captured_date ?? DateTime.Now
        );
        AddValue(values, columns, "printed", DbType.String, vehicle.printed ?? "N");
        AddValue(values, columns, "invoice_number", DbType.String, vehicle.invoice_number);
        AddValue(values, columns, "gp_number", DbType.String, vehicle.gp_number);
        AddValue(values, columns, "Fleet_Notes", DbType.String, vehicle.Fleet_Notes);
        AddValue(values, columns, "damage_status", DbType.String, vehicle.damage_status);
        AddValue(values, columns, "damages_comment", DbType.String, vehicle.damages_comment);

        if (includeCreateAudit)
        {
            AddValue(
                values,
                columns,
                "date_created",
                DbType.DateTime2,
                vehicle.date_created == default ? DateTime.Now : vehicle.date_created
            );
            AddValue(
                values,
                columns,
                "created_by_user_code",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
            AddValue(values, columns, "is_deleted", DbType.Boolean, false);
        }
        else
        {
            AddValue(values, columns, "date_updated", DbType.DateTime2, DateTime.Now);
            AddValue(
                values,
                columns,
                "modified_by_user_code",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : null
            );
        }

        return values;
    }

    private static async Task UpdateAuthorizationStatusAsync(
        DbConnection connection,
        DbTransaction? transaction,
        VehicleAuthorizationSchema schema,
        int tempVmfCode,
        string status,
        int actingUserId,
        string? comment,
        string? rejectionReason,
        int? vmfCode
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, schema.Columns, "Authority_Status", DbType.String, status);
        AddValue(values, schema.Columns, "comment", DbType.String, comment);
        AddValue(
            values,
            schema.Columns,
            "action_user_access_code",
            DbType.Int16,
            ToShortUserCode(actingUserId)
        );
        AddValue(values, schema.Columns, "authorized_by_user_code", DbType.Int32, actingUserId);
        AddValue(values, schema.Columns, "authorization_date", DbType.DateTime2, DateTime.Now);
        AddValue(values, schema.Columns, "authorization_comment", DbType.String, comment);
        AddValue(values, schema.Columns, "rejection_reason", DbType.String, rejectionReason);
        if (vmfCode.HasValue)
        {
            // A legacy promotion procedure may allocate or populate the
            // vehicle identifier itself. Never overwrite that value with
            // NULL when the pre-vehicle row did not have it beforehand.
            AddValue(values, schema.Columns, "vmf_code", DbType.Int32, vmfCode);
        }
        AddValue(values, schema.Columns, "date_updated", DbType.DateTime2, DateTime.Now);
        AddValue(values, schema.Columns, "modified_by_user_code", DbType.Int32, actingUserId);
        if (
            values.Count == 0
            || await ExecuteUpdateAsync(connection, transaction, tempVmfCode, values) == 0
        )
        {
            throw new KeyNotFoundException(
                $"Vehicle authorization with ID {tempVmfCode} not found"
            );
        }
    }

    private static async Task WriteLegacyAuthorizationNoteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string? chassisNumber,
        string? comment,
        int actingUserId
    )
    {
        if (
            string.IsNullOrWhiteSpace(chassisNumber)
            || !await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_INS_PreVehicle_master_Notes",
                "@comment",
                "@chassis_number",
                "@added_by_user_code"
            )
        )
        {
            return;
        }

        await ExecuteProcedureAsync(
            connection,
            transaction,
            "DEV_INS_PreVehicle_master_Notes",
            new ProcedureParameter("@comment", DbType.String, comment ?? string.Empty),
            new ProcedureParameter("@chassis_number", DbType.String, chassisNumber),
            new ProcedureParameter(
                "@added_by_user_code",
                DbType.Int16,
                actingUserId
            )
        );
    }

    private static async Task UpdateMaintenanceVmfCodeAfterPromotionAsync(
        DbConnection connection,
        DbTransaction transaction,
        int vmfCode,
        int tempVmfCode
    )
    {
        // PreCaptureNewVehicleDetails.aspx.vb btnAccept_Click copies the
        // captured maintenance plan from the temp pre-vehicle key onto the
        // promoted vehicle_master code. Absence of the procedure leaves the
        // capture-time DEV_UPD_VehicleMaintenanceOptions row unlinked.
        if (
            !await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_UPD_MaintenanceVmfCode",
                "@vmfCode",
                "@tempVmfCode"
            )
        )
        {
            return;
        }

        await ExecuteProcedureAsync(
            connection,
            transaction,
            "DEV_UPD_MaintenanceVmfCode",
            new ProcedureParameter("@vmfCode", DbType.Int32, vmfCode),
            new ProcedureParameter("@tempVmfCode", DbType.Int32, tempVmfCode)
        );
    }

    private static async Task WriteLegacyCaptureSideEffectsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int tempVmfCode,
        PreVehicleMaster vehicle,
        int currentUserId,
        bool writeInitialNote = true
    )
    {
        if (
            writeInitialNote
            && await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_INS_PreVehicle_master_Notes",
                "@comment",
                "@chassis_number",
                "@added_by_user_code"
            )
            && !string.IsNullOrWhiteSpace(vehicle.comment)
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_INS_PreVehicle_master_Notes",
                new ProcedureParameter("@comment", DbType.String, vehicle.comment),
                new ProcedureParameter("@chassis_number", DbType.String, vehicle.chassis_number),
                new ProcedureParameter("@added_by_user_code", DbType.Int16, currentUserId)
            );
        }

        if (
            await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_INS_Vehicle_Damages",
                "@chassisno",
                "@comment",
                "@userid",
                "@status"
            )
            && (
                !string.IsNullOrWhiteSpace(vehicle.damage_status)
                || !string.IsNullOrWhiteSpace(vehicle.damages_comment)
            )
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_INS_Vehicle_Damages",
                new ProcedureParameter("@chassisno", DbType.String, vehicle.chassis_number),
                new ProcedureParameter(
                    "@comment",
                    DbType.String,
                    vehicle.damages_comment ?? string.Empty
                ),
                new ProcedureParameter("@userid", DbType.Int16, currentUserId),
                new ProcedureParameter("@status", DbType.String, vehicle.damage_status ?? "N")
            );
        }

        if (
            await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_INS_temp_fleet_notes",
                "@chassisno",
                "@fleet_notes"
            )
            && !string.IsNullOrWhiteSpace(vehicle.Fleet_Notes)
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_INS_temp_fleet_notes",
                new ProcedureParameter("@chassisno", DbType.String, vehicle.chassis_number),
                new ProcedureParameter("@fleet_notes", DbType.String, vehicle.Fleet_Notes)
            );
        }

        if (
            await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_INS_NewVehicle_Extras",
                "@chassis_no",
                "@extra_code"
            )
        )
        {
            foreach (var extraCode in vehicle.ExtraCodes.Distinct())
            {
                await ExecuteProcedureAsync(
                    connection,
                    transaction,
                    "DEV_INS_NewVehicle_Extras",
                    new ProcedureParameter("@chassis_no", DbType.String, vehicle.chassis_number),
                    new ProcedureParameter("@extra_code", DbType.Int16, extraCode)
                );
            }
        }

        if (
            vehicle.MaintenanceTypeCode is > 0
            && await ProcedureMatchesAsync(
                connection,
                transaction,
                "DEV_UPD_VehicleMaintenanceOptions",
                "@vmfCode",
                "@tempVmfCode",
                "@maintType",
                "@startDate",
                "@period",
                "@kilos",
                "@maintValue",
                "@userCode"
            )
        )
        {
            await ExecuteProcedureAsync(
                connection,
                transaction,
                "DEV_UPD_VehicleMaintenanceOptions",
                new ProcedureParameter("@vmfCode", DbType.Int32, 0),
                new ProcedureParameter("@tempVmfCode", DbType.Int32, tempVmfCode),
                new ProcedureParameter("@maintType", DbType.Int16, vehicle.MaintenanceTypeCode),
                new ProcedureParameter(
                    "@startDate",
                    DbType.String,
                    vehicle.MaintenanceStartDate?.ToString("yyyy/MM/dd")
                ),
                new ProcedureParameter(
                    "@period",
                    DbType.Int32,
                    vehicle.MaintenancePeriodMonths ?? 0
                ),
                new ProcedureParameter("@kilos", DbType.Int32, vehicle.MaintenanceKilos ?? 0),
                new ProcedureParameter("@maintValue", DbType.Decimal, vehicle.MaintenanceValue),
                new ProcedureParameter("@userCode", DbType.Int32, currentUserId)
            );
        }
    }

    private static async Task PromoteWithoutLegacyProcedureAsync(
        DbConnection connection,
        DbTransaction transaction,
        PreVehicleMaster preVehicle,
        int actingUserId
    )
    {
        var columns = await GetColumnsAsync(connection, transaction, "dbo", VehicleTableName);
        if (!columns.Contains("vmf_code"))
        {
            throw new InvalidOperationException(
                "The vehicle_master compatibility table is not available for authorization."
            );
        }

        var existingCode = await FindVehicleCodeAsync(
            connection,
            transaction,
            columns,
            preVehicle.chassis_number
        );
        if (existingCode.HasValue)
        {
            preVehicle.vmf_code = existingCode.Value;
            return;
        }

        var fleetNumber = preVehicle.fleet_number;
        if (string.IsNullOrWhiteSpace(fleetNumber))
        {
            fleetNumber = await AllocateGgNumberAsync(connection, transaction, columns);
        }

        var values = new List<WriteValue>();
        AddVehicleValue(values, columns, "model_code", DbType.Int16, preVehicle.model_code);
        AddVehicleValue(values, columns, "type_code", DbType.Int16, preVehicle.type_code ?? 1);
        AddVehicleValue(
            values,
            columns,
            "vehicle_status_code",
            DbType.Int16,
            preVehicle.vehicle_status_code ?? 0
        );
        AddVehicleValue(
            values,
            columns,
            "location_code",
            DbType.Int16,
            preVehicle.location_code ?? 0
        );
        AddVehicleValue(values, columns, "fleet_number", DbType.String, fleetNumber);
        AddVehicleValue(
            values,
            columns,
            "registration_number",
            DbType.String,
            preVehicle.registration_number ?? fleetNumber
        );
        AddVehicleValue(
            values,
            columns,
            "take_on_date",
            DbType.DateTime2,
            preVehicle.take_on_date ?? DateTime.Now
        );
        AddVehicleValue(values, columns, "take_on_odo", DbType.Int32, preVehicle.take_on_odo ?? 0);
        AddVehicleValue(values, columns, "current_odo", DbType.Int32, preVehicle.take_on_odo ?? 0);
        AddVehicleValue(
            values,
            columns,
            "engine_number_1",
            DbType.String,
            preVehicle.engine_number
        );
        AddVehicleValue(
            values,
            columns,
            "chassis_number",
            DbType.String,
            preVehicle.chassis_number
        );
        AddVehicleValue(
            values,
            columns,
            "year_manufactured",
            DbType.Int16,
            preVehicle.year_manufactured
        );
        AddVehicleValue(
            values,
            columns,
            "purchase_date",
            DbType.DateTime2,
            preVehicle.purchase_date
        );
        AddVehicleValue(
            values,
            columns,
            "purchase_amount",
            DbType.Decimal,
            preVehicle.purchase_amount
        );
        AddVehicleValue(values, columns, "purchased_from", DbType.String, preVehicle.purchase_from);
        AddVehicleValue(values, columns, "colour", DbType.String, preVehicle.colour);
        AddVehicleValue(
            values,
            columns,
            "previos_gg_number",
            DbType.String,
            preVehicle.replaced_gg_number
        );
        AddVehicleValue(
            values,
            columns,
            "vehicle_status_date",
            DbType.DateTime2,
            preVehicle.vehicle_status_date ?? preVehicle.take_on_date
        );
        AddVehicleValue(
            values,
            columns,
            "user_access_code",
            DbType.Int16,
            preVehicle.captured_by_user_code ?? ToShortUserCode(actingUserId)
        );
        AddVehicleValue(
            values,
            columns,
            "captured_date",
            DbType.DateTime2,
            preVehicle.captured_date ?? DateTime.Now
        );
        AddVehicleValue(values, columns, "vs_code", DbType.Byte, preVehicle.vs_code);
        AddVehicleValue(
            values,
            columns,
            "invoice_number",
            DbType.String,
            preVehicle.invoice_number
        );
        AddVehicleValue(values, columns, "veh_site_code", DbType.Int16, preVehicle.site_code);
        AddVehicleValue(values, columns, "Site_code", DbType.Int16, preVehicle.site_code);
        AddVehicleValue(values, columns, "initial_site_code", DbType.Int16, preVehicle.site_code);
        AddVehicleValue(values, columns, "temp_vmf_code", DbType.Int32, preVehicle.temp_vmf_code);
        AddVehicleValue(values, columns, "date_created", DbType.DateTime2, DateTime.Now);
        AddVehicleValue(values, columns, "created_by_user_code", DbType.Int32, actingUserId);
        AddVehicleValue(values, columns, "is_deleted", DbType.Boolean, false);

        var vmfCode = await ExecuteVehicleInsertAsync(connection, transaction, values);
        preVehicle.vmf_code = vmfCode;
        if (string.IsNullOrWhiteSpace(preVehicle.fleet_number))
        {
            preVehicle.fleet_number = fleetNumber;
        }
    }

    private static async Task<int?> FindVehicleCodeAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        string? chassisNumber
    )
    {
        if (!columns.Contains("chassis_number") || string.IsNullOrWhiteSpace(chassisNumber))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT TOP (1) [vmf_code] FROM [dbo].[{VehicleTableName}] WHERE [chassis_number] = @chassisNumber";
        AddParameter(command, "@chassisNumber", DbType.String, chassisNumber);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task<string?> AllocateGgNumberAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> vehicleColumns
    )
    {
        if (!vehicleColumns.Contains("fleet_number"))
        {
            return null;
        }

        var numberColumns = await GetColumnsAsync(
            connection,
            transaction,
            "dbo",
            GgNumberTableName
        );
        if (!numberColumns.Contains("GG_Number"))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var notDeleted = numberColumns.Contains("is_deleted")
            ? " AND ([n].[is_deleted] = 0 OR [n].[is_deleted] IS NULL)"
            : string.Empty;
        command.CommandText = $"""
            SELECT TOP (1) [n].[GG_Number]
            FROM [dbo].[{GgNumberTableName}] AS [n]
            WHERE [n].[GG_Number] IS NOT NULL{notDeleted}
              AND NOT EXISTS (
                  SELECT 1
                  FROM [dbo].[{VehicleTableName}] AS [v]
                  WHERE [v].[fleet_number] = [n].[GG_Number]
              )
            ORDER BY [n].[GG_Number]
            """;
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? null : result.ToString();
    }

    private static async Task<int> ExecuteVehicleInsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<WriteValue> values
    )
    {
        var insertValues = values
            .Where(value =>
                !string.Equals(value.Column, "vmf_code", StringComparison.OrdinalIgnoreCase)
            )
            .ToArray();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{VehicleTableName}] ({string.Join(", ", insertValues.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[vmf_code] VALUES ({string.Join(", ", insertValues.Select(value => value.Parameter))})";
        AddParameters(command, insertValues);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ExecuteInsertAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<WriteValue> values,
        bool outputKey
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = outputKey
            ? $"INSERT INTO [dbo].[{PreVehicleTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[temp_vmf_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})"
            : $"INSERT INTO [dbo].[{PreVehicleTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        if (outputKey)
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        await command.ExecuteNonQueryAsync();
        return 0;
    }

    private static async Task<int> ExecuteUpdateAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int tempVmfCode,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"UPDATE [dbo].[{PreVehicleTableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [temp_vmf_code] = @tempVmfCode";
        AddParameters(command, values);
        AddParameter(command, "@tempVmfCode", DbType.Int32, tempVmfCode);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetNextCodeAsync(
        DbConnection connection,
        DbTransaction transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"SELECT COALESCE(MAX([temp_vmf_code]), 0) + 1 FROM [dbo].[{PreVehicleTableName}] WITH (UPDLOCK, HOLDLOCK)";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<VehicleCreationProcedureContract?> GetVehicleCreationProcedureContractAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName = "DEV_INS_New_Vehicle_Master"
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [parameterObject].[name]
            FROM [sys].[procedures] AS [procedureObject]
            INNER JOIN [sys].[schemas] AS [schemaObject]
                ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
            LEFT JOIN [sys].[parameters] AS [parameterObject]
                ON [parameterObject].[object_id] = [procedureObject].[object_id]
               AND [parameterObject].[parameter_id] > 0
            WHERE [schemaObject].[name] = N'dbo'
              AND [procedureObject].[name] = @procedureName
            ORDER BY [parameterObject].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);
        var actualParameters = new List<string>();
        var procedureFound = false;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            procedureFound = true;
            if (!reader.IsDBNull(0))
                actualParameters.Add(reader.GetString(0));
        }

        if (!procedureFound)
            return null;

        // The active Master-File capture page and its data-access component
        // both declare this exact legacy signature. @ggNumber is the captured
        // GG number. Keep the earlier no-GG signatures as explicit compatibility
        // variants because older restored databases may still expose them.
        var legacyInvoiceGp = new[]
        {
            "@ggNumber",
            "@replaced_gg_number",
            "@model_code",
            "@colour",
            "@year_manufactured",
            "@chassis_number",
            "@engine_number",
            "@take_on_odo",
            "@take_on_date",
            "@location_code",
            "@vehicle_status_code",
            "@type_code",
            "@vs_code",
            "@comment",
            "@purchase_amount",
            "@purchase_from",
            "@purchase_date",
            "@captured_by_user_code",
            "@action_user_access_code",
            "@site_code",
            "@invoiceNumber",
            "@gpNumber",
        };
        var invoiceGp = legacyInvoiceGp.Skip(1).ToArray();
        var invoice = invoiceGp.Take(invoiceGp.Length - 1).ToArray();
        var baseParameters = invoiceGp.Take(invoiceGp.Length - 2).ToArray();
        // An older archived variant did not expose @ggNumber and instead
        // accepted the recalled vehicle's original identity values.
        var legacyOldIdentity = new[]
        {
            "@replaced_gg_number",
            "@model_code",
            "@colour",
            "@year_manufactured",
            "@chassis_number",
            "@engine_number",
            "@take_on_odo",
            "@take_on_date",
            "@location_code",
            "@vehicle_status_code",
            "@type_code",
            "@vs_code",
            "@comment",
            "@fleet_notes",
            "@purchase_amount",
            "@purchase_from",
            "@purchase_date",
            "@captured_by_user_code",
            "@action_user_access_code",
            "@site_code",
            "@Old_chassis_number",
            "@Old_engine_number",
        };

        bool Matches(IReadOnlyCollection<string> expected) =>
            actualParameters.Count == expected.Count
            && actualParameters.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(expected);

        if (Matches(legacyInvoiceGp))
            return new VehicleCreationProcedureContract(true, true, true, false);
        if (Matches(invoiceGp))
            return new VehicleCreationProcedureContract(false, true, true, false);
        if (Matches(invoice))
            return new VehicleCreationProcedureContract(false, true, false, false);
        if (Matches(baseParameters))
            return new VehicleCreationProcedureContract(false, false, false, false);
        if (Matches(legacyOldIdentity))
            return new VehicleCreationProcedureContract(false, false, false, true, true);

        throw new InvalidOperationException(
            $"The deployed legacy procedure {procedureName} does not match any archived vehicle-capture parameter contract. No direct-DML fallback was run."
        );
    }

    private sealed record VehicleCreationProcedureContract(
        bool IncludesGgNumber,
        bool IncludesInvoice,
        bool IncludesGpNumber,
        bool IncludesFleetNotes,
        bool IncludesPreviousIdentity = false
    );

    private static async Task EnsureEnabledTriggerAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string schemaName,
        string tableName,
        string triggerName
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [tr].[is_disabled]
            FROM [sys].[triggers] AS [tr]
            INNER JOIN [sys].[tables] AS [tb]
                ON [tb].[object_id] = [tr].[parent_id]
            INNER JOIN [sys].[schemas] AS [sc]
                ON [sc].[schema_id] = [tb].[schema_id]
            WHERE [sc].[name] = @schemaName
              AND [tb].[name] = @tableName
              AND [tr].[name] = @triggerName;
            """;
        AddParameter(command, "@schemaName", DbType.String, schemaName);
        AddParameter(command, "@tableName", DbType.String, tableName);
        AddParameter(command, "@triggerName", DbType.String, triggerName);

        var disabled = await command.ExecuteScalarAsync();
        if (disabled is null or DBNull || Convert.ToBoolean(disabled))
        {
            throw new NotSupportedException(
                $"The legacy vehicle tariff trigger workflow is unavailable ({schemaName}.{triggerName} on {tableName}); no vehicle promotion fallback was run."
            );
        }
    }

    private static async Task EnsureCurrentVehicleTariffAsync(
        DbConnection connection,
        DbTransaction transaction,
        int? vmfCode,
        short? yearManufactured,
        short? vehicleTypeCode,
        short? modelCode
    )
    {
        if (vmfCode is not > 0)
        {
            throw new InvalidOperationException(
                "Vehicle authorization completed without a vehicle master code; no tariff-bearing vehicle was promoted."
            );
        }

        if (yearManufactured is not > 0)
        {
            throw new InvalidOperationException(
                $"Vehicle {vmfCode.Value} was promoted without a year manufactured; no legacy tariff can be resolved."
            );
        }

        // The legacy tariff function uses dbo.leasetariff for lease contracts.
        if (vehicleTypeCode == 4)
        {
            await using var leaseCommand = connection.CreateCommand();
            leaseCommand.Transaction = transaction;
            leaseCommand.CommandText = """
                IF OBJECT_ID(N'dbo.leasetariff', N'U') IS NULL
                    SELECT CAST(NULL AS bit);
                ELSE
                    SELECT TOP (1) CAST(1 AS bit)
                    FROM [dbo].[leasetariff] AS [lt]
                    WHERE [lt].[vmf_code] = @vmfCode
                      AND [lt].[fixed_tariff] IS NOT NULL
                      AND [lt].[fixed_tariff] > 0
                      AND [lt].[start_date] <= CONVERT(smalldatetime, GETDATE())
                      AND ([lt].[end_date] IS NULL OR [lt].[end_date] = 0 OR [lt].[end_date] >= CONVERT(smalldatetime, GETDATE()))
                      AND ([lt].[active] = 1 OR [lt].[active] IS NULL);
                """;
            AddParameter(leaseCommand, "@vmfCode", DbType.Int32, vmfCode.Value);
            var hasLeaseTariff = await leaseCommand.ExecuteScalarAsync();
            if (hasLeaseTariff is null or DBNull || !Convert.ToBoolean(hasLeaseTariff))
            {
                throw new InvalidOperationException(
                    $"Vehicle {vmfCode.Value} was promoted without a current lease tariff. Authorization was not committed."
                );
            }

            return;
        }

        // dbo.GetVehicleTariff checks the class tariff first for 2009-and-
        // older vehicles. For 2008/2009 records captured after the 2009
        // tariff cut-over it can then fall through to fin.vehicle_tariff when
        // no class row exists. Check those two sources in that same order;
        // do not force every 2008/2009 vehicle into only one tariff system.
        var canUseLegacyClassTariff = yearManufactured.Value <= 2009 && vehicleTypeCode != 5;
        if (canUseLegacyClassTariff)
        {
            if (modelCode is > 0)
            {
                await using var classTariffCommand = connection.CreateCommand();
                classTariffCommand.Transaction = transaction;
                classTariffCommand.CommandText = """
                    IF OBJECT_ID(N'dbo.tariff', N'U') IS NULL
                        SELECT CAST(NULL AS bit);
                    ELSE
                        SELECT TOP (1) CAST(1 AS bit)
                        FROM [dbo].[tariff] AS [t]
                        INNER JOIN [dbo].[model] AS [m]
                            ON [m].[class_code] = [t].[class_code]
                        WHERE [m].[model_code] = @modelCode
                          AND [t].[year_manufactured] = @tariffYear
                          AND [t].[effective_start_date] <= CONVERT(date, GETDATE())
                          AND ([t].[effective_end_date] IS NULL OR [t].[effective_end_date] >= CONVERT(date, GETDATE()))
                          AND [t].[monthly_fixed_amount] IS NOT NULL
                          AND [t].[monthly_odo_amount] IS NOT NULL;
                    """;
                AddParameter(classTariffCommand, "@modelCode", DbType.Int16, modelCode.Value);
                AddParameter(
                    classTariffCommand,
                    "@tariffYear",
                    DbType.Int16,
                    (short)Math.Max(2002, (int)yearManufactured.Value)
                );
                var hasClassTariff = await classTariffCommand.ExecuteScalarAsync();
                if (hasClassTariff is not null and not DBNull && Convert.ToBoolean(hasClassTariff))
                {
                    return;
                }
            }

            // Before 1 April 2009 the legacy function has no modern fallback;
            // keep the historical class-tariff requirement for that period.
            if (DateTime.Today < new DateTime(2009, 4, 1) || yearManufactured.Value < 2008)
            {
                throw new InvalidOperationException(
                    $"Vehicle {vmfCode.Value} was promoted without a current class tariff. Authorization was not committed."
                );
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF OBJECT_ID(N'fin.vehicle_tariff', N'U') IS NULL
                SELECT CAST(NULL AS bit);
            ELSE
                SELECT CAST(1 AS bit)
                FROM [fin].[vehicle_tariff] AS [vt]
                WHERE [vt].[vmf_code] = @vmfCode
                  AND [vt].[start_date] <= CONVERT(smalldatetime, GETDATE())
                  AND ([vt].[end_date] IS NULL OR [vt].[end_date] >= CONVERT(smalldatetime, GETDATE()))
                  AND [vt].[start_date] >= DATEADD(year, -1, CONVERT(smalldatetime, GETDATE()))
                  AND [vt].[vehicle_fixed_tariff] IS NOT NULL
                  AND [vt].[vehicle_kilometer_tariff] IS NOT NULL;
            """;
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode.Value);
        var hasTariff = await command.ExecuteScalarAsync();
        if (hasTariff is null or DBNull || !Convert.ToBoolean(hasTariff))
        {
            throw new InvalidOperationException(
                $"Vehicle {vmfCode.Value} was promoted without a current vehicle tariff. Authorization was not committed; capture/release the tariff before assigning the vehicle to a client."
            );
        }
    }

    private static async Task<bool> ProcedureMatchesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        params string[] expectedParameters
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [parameterObject].[name]
            FROM [sys].[procedures] AS [procedureObject]
            INNER JOIN [sys].[schemas] AS [schemaObject]
                ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
            LEFT JOIN [sys].[parameters] AS [parameterObject]
                ON [parameterObject].[object_id] = [procedureObject].[object_id]
               AND [parameterObject].[parameter_id] > 0
            WHERE [schemaObject].[name] = N'dbo'
              AND [procedureObject].[name] = @procedureName
            ORDER BY [parameterObject].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);

        var actualParameters = new List<string>();
        var procedureFound = false;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                procedureFound = true;
                if (!reader.IsDBNull(0))
                {
                    actualParameters.Add(reader.GetString(0));
                }
            }
        }

        if (!procedureFound)
            return false;
        if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match its verified parameter contract. No direct-DML fallback was run."
            );
        }

        return true;
    }

    private static async Task ExecuteProcedureAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        params ProcedureParameter?[] parameters
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"dbo.{procedureName}";
        foreach (var parameter in parameters)
        {
            if (parameter is null)
                continue;
            AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The procedure name is selected only from fixed legacy compatibility branches; all values are parameters."
    )]
    private static async Task<List<Dictionary<string, object?>>> ExecuteProcedureRowsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        params ProcedureParameter?[] parameters
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"dbo.{procedureName}";
        foreach (var parameter in parameters)
        {
            if (parameter is null)
                continue;
            AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
        }

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static int? ReadFirstColumnInt(IReadOnlyDictionary<string, object?> row)
    {
        foreach (var value in row.Values)
        {
            return ToInt32(value);
        }

        return null;
    }

    private static int? ReadRowInt32(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return ToInt32(value);
            }
        }

        return null;
    }

    private static string? ReadRowString(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    )
    {
        foreach (var key in keys)
        {
            if (
                row.TryGetValue(key, out var value)
                && value is not null and not DBNull
                && value.ToString()?.Trim() is { Length: > 0 } text
            )
            {
                return text;
            }
        }

        return null;
    }

    private static int? ToInt32(object? value)
    {
        if (value is null or DBNull)
        {
            return null;
        }

        return int.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed
        )
            ? parsed
            : null;
    }

    private static string GetProjection(string alias, IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{alias}].[{column}] AS [{column}]";
        }

        var sqlType =
            StringColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "nvarchar(4000)"
            : DateColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "datetime2"
            : DecimalColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "decimal(18, 2)"
            : ByteColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "tinyint"
            : ShortColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "smallint"
            : IntegerColumns.Contains(column, StringComparer.OrdinalIgnoreCase) ? "int"
            : column.Equals("is_deleted", StringComparison.OrdinalIgnoreCase) ? "bit"
            : "nvarchar(4000)";
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(string alias, IReadOnlySet<string> columns)
    {
        var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : $"[{alias}].";
        return columns.Contains("is_deleted")
            ? $"({prefix}[is_deleted] = 0 OR {prefix}[is_deleted] IS NULL)"
            : "1 = 1";
    }

    private static void AddUnprintedAuthorizedFilter(
        ICollection<string> conditions,
        IReadOnlySet<string> columns,
        string? status
    )
    {
        if (
            !columns.Contains("printed")
            || !string.Equals(status, "Authorized", StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        // Archive print-and-clear marks printed so the row leaves the
        // authorized queue. The modern entity maps printed as char(1);
        // treat Y/1 as already printed on both char and bit stores.
        conditions.Add(
            "([p].[printed] IS NULL OR LTRIM(RTRIM(CONVERT(varchar(10), [p].[printed]))) NOT IN (N'Y', N'y', N'1'))"
        );
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        DbType type,
        object? value
    )
    {
        if (columns.Contains(column))
        {
            values.Add(new WriteValue(column, $"@{column}", type, value));
        }
    }

    private static void AddVehicleValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        DbType type,
        object? value
    ) => AddValue(values, columns, column, type, value);

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

    private static async Task<Dictionary<string, string?>?> ReadPrintVehicleDetailsAsync(
        DbConnection connection,
        string? chassisNumber
    )
    {
        if (
            string.IsNullOrWhiteSpace(chassisNumber)
            || !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_PrintVehicle_Details",
                "@chassis_number"
            )
        )
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "dbo.DEV_SEL_PrintVehicle_Details";
        AddParameter(command, "@chassis_number", DbType.String, chassisNumber);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadRow(reader) : null;
    }

    private static async Task<IReadOnlyList<string>> ReadPrintExtrasAsync(
        DbConnection connection,
        string? chassisNumber
    )
    {
        if (
            string.IsNullOrWhiteSpace(chassisNumber)
            || !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_NewVehicle_Extras",
                "@SearchVal"
            )
        )
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "dbo.DEV_SEL_NewVehicle_Extras";
        AddParameter(command, "@SearchVal", DbType.String, chassisNumber);
        var extras = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var description = ReadAliased(reader, "extra_description", "Extra_Description");
            if (!string.IsNullOrWhiteSpace(description))
            {
                extras.Add(description);
            }
        }

        return extras;
    }

    private static async Task OverlayCaptureExtrasAsync(
        DbConnection connection,
        PreVehicleMaster? vehicle
    )
    {
        if (vehicle is null || string.IsNullOrWhiteSpace(vehicle.chassis_number))
        {
            return;
        }

        if (
            !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_NewVehicle_Extras",
                "@SearchVal"
            )
        )
        {
            return;
        }

        vehicle.ExtraCodes = await ReadCaptureExtraCodesAsync(
            connection,
            vehicle.chassis_number
        );
    }

    private static async Task<List<short>> ReadCaptureExtraCodesAsync(
        DbConnection connection,
        string chassisNumber
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "dbo.DEV_SEL_NewVehicle_Extras";
        AddParameter(command, "@SearchVal", DbType.String, chassisNumber);
        var extras = new List<short>();
        var unresolvedDescriptions = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var extraCode = ReadAliased(reader, "extra_code", "Extra_Code", "ExtraCode");
                if (short.TryParse(extraCode, out var parsed) && parsed > 0)
                {
                    extras.Add(parsed);
                    continue;
                }

                var description = ReadAliased(reader, "extra_description", "Extra_Description");
                if (!string.IsNullOrWhiteSpace(description))
                {
                    unresolvedDescriptions.Add(description);
                }
            }
        }

        foreach (var description in unresolvedDescriptions)
        {
            var resolved = await ResolveExtraCodeByDescriptionAsync(connection, description);
            if (resolved is > 0)
            {
                extras.Add(resolved.Value);
            }
        }

        return extras.Distinct().ToList();
    }

    private static async Task<short?> ResolveExtraCodeByDescriptionAsync(
        DbConnection connection,
        string? description
    )
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var columns = await GetColumnsAsync(connection, null, "dbo", ExtraCodeTableName);
        if (!columns.Contains("extra_code") || !columns.Contains("extra_description"))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) [extra_code]
            FROM [dbo].[extra_codes]
            WHERE LOWER(LTRIM(RTRIM([extra_description]))) = @description
            """;
        AddParameter(
            command,
            "@description",
            DbType.String,
            description.Trim().ToLowerInvariant()
        );
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToInt16(value);
    }

    private static async Task<IReadOnlyList<VehicleStatusComment>> ReadStatusCommentsAsync(
        DbConnection connection,
        string chassisNumber
    )
    {
        if (
            !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_Vehicle_Status_Comments",
                "@chassis_No"
            )
        )
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "dbo.DEV_SEL_Vehicle_Status_Comments";
        AddParameter(command, "@chassis_No", DbType.String, chassisNumber);
        var comments = new List<VehicleStatusComment>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var commentDateText = ReadAliased(
                reader,
                "Comment_Date",
                "comment_date",
                "CommentDate"
            );
            comments.Add(
                new VehicleStatusComment(
                    ReadAliased(reader, "Captured By", "CapturedBy", "captured_by"),
                    DateTime.TryParse(commentDateText, out var commentDate)
                        ? commentDate
                        : null,
                    ReadAliased(
                        reader,
                        "Authority_Status",
                        "Authority Status",
                        "authority_status"
                    ),
                    ReadAliased(reader, "Comment", "comment")
                )
            );
        }

        return comments;
    }

    private static async Task<Dictionary<string, string?>?> ReadPrintUserDetailsAsync(
        DbConnection connection,
        string? chassisNumber
    )
    {
        if (
            string.IsNullOrWhiteSpace(chassisNumber)
            || !await ProcedureMatchesAsync(
                connection,
                null,
                "DEV_SEL_Vehicle_CapturerDetails",
                "@chassis_No"
            )
        )
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "dbo.DEV_SEL_Vehicle_CapturerDetails";
        AddParameter(command, "@chassis_No", DbType.String, chassisNumber);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadRow(reader) : null;
    }

    private static void OverlayPrintVehicleDetails(
        PreVehicleMaster vehicle,
        Dictionary<string, string?>? printRow
    )
    {
        if (printRow is null || printRow.Count == 0)
        {
            return;
        }

        vehicle.fleet_number = GetRowValue(printRow, "GG Number", "fleet_number") ?? vehicle.fleet_number;
        vehicle.registration_number =
            GetRowValue(printRow, "registration_number") ?? vehicle.registration_number;
        vehicle.replaced_gg_number =
            GetRowValue(printRow, "replaced_gg_number") ?? vehicle.replaced_gg_number;
        vehicle.colour = GetRowValue(printRow, "colour") ?? vehicle.colour;
        vehicle.chassis_number = GetRowValue(printRow, "chassis_number") ?? vehicle.chassis_number;
        vehicle.engine_number =
            GetRowValue(printRow, "engine_number_1", "engine_number") ?? vehicle.engine_number;
        vehicle.invoice_number = GetRowValue(printRow, "invoice_number") ?? vehicle.invoice_number;
        vehicle.gp_number = GetRowValue(printRow, "gp_number") ?? vehicle.gp_number;
        vehicle.purchase_from =
            GetRowValue(printRow, "purchased_from", "purchase_from") ?? vehicle.purchase_from;
        vehicle.Fleet_Notes = GetRowValue(printRow, "Fleet_Notes", "fleet_notes") ?? vehicle.Fleet_Notes;
        vehicle.damages_comment =
            GetRowValue(printRow, "damages_comment") ?? vehicle.damages_comment;
        vehicle.authorization_comment =
            GetRowValue(printRow, "comment") ?? vehicle.authorization_comment;
        var modelDescription = GetRowValue(printRow, "model");
        if (!string.IsNullOrWhiteSpace(modelDescription))
        {
            vehicle.Model ??= new FIS.Core.Domain.Entities.Model();
            vehicle.Model.model_description = modelDescription;
        }

        var year = GetRowValue(printRow, "year_manufactured");
        if (short.TryParse(year, out var yearManufactured))
        {
            vehicle.year_manufactured = yearManufactured;
        }

        var odo = GetRowValue(printRow, "take_on_odo");
        if (int.TryParse(odo, out var takeOnOdo))
        {
            vehicle.take_on_odo = takeOnOdo;
        }

        var takeOnDate = ParseRowDate(printRow, "take_on_date");
        if (takeOnDate.HasValue)
        {
            vehicle.take_on_date = takeOnDate;
        }

        var purchaseDate = ParseRowDate(printRow, "purchase_date");
        if (purchaseDate.HasValue)
        {
            vehicle.purchase_date = purchaseDate;
        }

        var purchaseAmount = GetRowValue(printRow, "purchase_amount");
        if (decimal.TryParse(purchaseAmount, out var amount))
        {
            vehicle.purchase_amount = amount;
        }
    }

    private static Dictionary<string, string?> ReadRow(DbDataReader reader)
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            row[reader.GetName(index)] = reader.IsDBNull(index)
                ? null
                : reader.GetValue(index)?.ToString()?.Trim();
        }

        return row;
    }

    private static string? GetRowValue(Dictionary<string, string?>? row, params string[] keys)
    {
        if (row is null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static DateTime? ParseRowDate(Dictionary<string, string?>? row, params string[] keys)
    {
        var value = GetRowValue(row, keys);
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? ReadAliased(DbDataReader reader, params string[] columnNames)
    {
        var ordinal = FindOrdinal(reader, columnNames);
        if (ordinal is null || reader.IsDBNull(ordinal.Value))
        {
            return null;
        }

        return reader.GetValue(ordinal.Value)?.ToString()?.Trim();
    }

    private static short? ToShortUserCode(int userId) =>
        userId is > 0 and <= short.MaxValue ? (short)userId : null;

    private static int? FindOrdinal(DbDataReader reader, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            try
            {
                return reader.GetOrdinal(columnName);
            }
            catch (IndexOutOfRangeException)
            {
                // The legacy procedure's result-set casing/alias varies by
                // database version; continue with the next known alias.
            }
        }

        return null;
    }

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : reader[column]?.ToString();

    private static int? ReadInt32(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static short? ReadInt16(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static byte? ReadByte(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToByte(reader[column]);

    private static decimal? ReadDecimal(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDecimal(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static bool? ReadBoolean(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToBoolean(reader[column]);

    private sealed record VehicleAuthorizationSchema(
        HashSet<string> Columns,
        HashSet<string> ModelColumns,
        bool IsIdentity
    );

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed record ProcedureParameter(string Name, DbType Type, object? Value);

    private sealed record QueueProcedure(
        string Name,
        string[] ExpectedParameters,
        ProcedureParameter[] Parameters
    );
}
