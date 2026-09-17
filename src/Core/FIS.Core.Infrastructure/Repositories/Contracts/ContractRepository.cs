using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists contract workflows against both the client-era contract table and
/// later expanded versions of that same table. The original schema remains
/// authoritative; columns introduced by later database releases are selected
/// and written only when runtime metadata confirms that they exist.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Contract SQL uses fixed table/column allowlists and parameters for all values; runtime column selection is required for legacy schema compatibility."
)]
public class ContractRepository : IContractRepository
{
    private const string ContractTableName = "contract";
    private const string VehicleTableName = "vehicle_master";
    private const string SiteTableName = "site";

    private static readonly string[] DirectInsertTriggerNames =
    [
        "TRG_INS_CheckDuplicateContract",
        "TRG_INS_ContractJournalDetailRecord",
    ];

    private static readonly string[] DirectUpdateTriggerNames =
    ["TRG_UPD_ContractJournalDetailRecord"];

    private static readonly string[] RequiredContractColumns =
    [
        "contract_code",
        "vmf_code",
        "site_code",
        "start_date",
        "start_time",
        "end_date",
        "end_time",
        "start_odometer",
        "end_odometer",
        "still_current",
        "contract_type",
        "Driver_id",
        "Authorisation",
        "Driver_name",
        "Notes",
        "target_return_date",
        "user_code",
        "Charged_Until",
        "bas_objective_code",
        "bas_responsibility_code",
        "relief_for_contract",
        "locked_for_transfer",
        "hours_used",
        "bas_project_number",
        "journal_detail_code",
        "parent_contract_code",
        "contract_group_code",
        "bas_fund_code",
        "monthly_km",
    ];

    private static readonly string[] OptionalContractColumns =
    [
        "contract_status_code",
        "contract_status_date",
        "vehicle_assessment_code",
        "approver_code",
        "site_driver_code",
        "collector_firstname",
        "collector_surname",
        "collector_sa_id",
        "collector_passportnumber",
        "collector_office_number",
        "collector_cellphone_number",
        "collector_office",
        "collector_designation",
        "relief_vehicle_option",
        "lease_contract_period",
        "contract_estimated_overall_km",
        "intended_start_date",
        "intended_start_time",
        "capture_date",
        "modified_date",
        "reassigned_from_contract_code",
        "backdating_start_date",
        "backdating_requested_date",
        "backdating_requested_by_username",
        "backdating_approved_date",
        "backdating_approved_by_Username",
        "backdating_declined_date",
        "backdating_declined_by_Username",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] VehicleProjectionColumns =
    [
        "fleet_number",
        "registration_number",
        "model_code",
        "current_odo",
        "vehicle_status_code",
        "location_code",
        "year_manufactured",
        "chassis_number",
        "engine_number_1",
        "purchased_from",
        "colour",
    ];

    private static readonly string[] RequiredVehicleColumns =
    [
        "vmf_code",
        "model_code",
        "type_code",
        "vehicle_status_code",
        "location_code",
        "fleet_number",
        "registration_number",
        "current_odo",
    ];

    private static readonly string[] VehicleSearchColumns =
    [
        "fleet_number",
        "registration_number",
        "chassis_number",
        "engine_number_1",
        "invoice_number",
    ];

    private static readonly string[] SiteProjectionColumns =
    [
        "description",
        "Depatrment_code",
        "site_active",
        "res_person",
        "net_address",
        "telephone",
    ];

    private readonly FisDbContext _context;

    public ContractRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Contract?> GetByIdAsync(int contractCode)
    {
        var (contractColumns, vehicleColumns, siteColumns) = await GetProjectionColumnsAsync();
        return (
            await QueryAsync(
                contractColumns,
                vehicleColumns,
                siteColumns,
                $"[c].[contract_code] = @contractCode AND {GetNotDeletedFilter(contractColumns)}",
                [new QueryParameter("@contractCode", DbType.Int32, contractCode)],
                "[c].[contract_code] DESC",
                take: 1
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<Contract>> GetActiveContractsAsync()
    {
        var (contractColumns, vehicleColumns, siteColumns) = await GetProjectionColumnsAsync();
        return await QueryAsync(
            contractColumns,
            vehicleColumns,
            siteColumns,
            $"[c].[still_current] = 'Y' AND {GetNotDeletedFilter(contractColumns)}",
            [],
            "[c].[start_date] ASC, [c].[contract_code] ASC"
        );
    }

    public async Task<IEnumerable<Contract>> GetContractsByVehicleAsync(int vmfCode)
    {
        var (contractColumns, vehicleColumns, siteColumns) = await GetProjectionColumnsAsync();
        return await QueryAsync(
            contractColumns,
            vehicleColumns,
            siteColumns,
            $"[c].[vmf_code] = @vmfCode AND {GetNotDeletedFilter(contractColumns)}",
            [new QueryParameter("@vmfCode", DbType.Int32, vmfCode)],
            "[c].[start_date] DESC, [c].[contract_code] DESC"
        );
    }

    public async Task<IEnumerable<Contract>> GetAllAsync()
    {
        var (contractColumns, vehicleColumns, siteColumns) = await GetProjectionColumnsAsync();
        return await QueryAsync(
            contractColumns,
            vehicleColumns,
            siteColumns,
            GetNotDeletedFilter(contractColumns),
            [],
            "[c].[start_date] DESC, [c].[contract_code] DESC"
        );
    }

    public async Task<ContractPage> GetPageAsync(ContractPageQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var (contractColumns, vehicleColumns, siteColumns) = await GetProjectionColumnsAsync();
        var filter = BuildFilter(contractColumns, query);
        var total = await ExecuteCountAsync(filter);
        var orderBy =
            contractColumns.Contains("date_created")
                ? "[c].[date_created] DESC, [c].[contract_code] DESC"
            : contractColumns.Contains("capture_date")
                ? "[c].[capture_date] DESC, [c].[contract_code] DESC"
            : "[c].[contract_code] DESC";
        var items = await QueryAsync(
            contractColumns,
            vehicleColumns,
            siteColumns,
            filter.Clause,
            filter.Parameters,
            orderBy,
            skip: (page - 1) * pageSize,
            take: pageSize
        );

        return new ContractPage(items, total, page, pageSize);
    }

    public async Task<IEnumerable<ContractVehicleLookup>> SearchVehiclesForContractsAsync(
        string searchTerm,
        IReadOnlyCollection<short>? allowedSiteCodes = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync(
            VehicleTableName,
            RequiredVehicleColumns
        );
        var searchableColumns = VehicleSearchColumns.Where(availableColumns.Contains).ToArray();
        var predicate =
            searchableColumns.Length == 0
                ? "1 = 0"
                : string.Join(
                    " OR ",
                    searchableColumns.Select(column =>
                        $"LOWER(CONVERT(varchar(250), [v].[{column}])) LIKE @searchTerm"
                    )
                );
        var orderBy = availableColumns.Contains("fleet_number")
            ? "[v].[fleet_number] ASC, [v].[vmf_code] ASC"
            : "[v].[vmf_code] ASC";
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var projection = new[]
            {
                "[v].[vmf_code] AS [vmf_code]",
                GetOptionalProjection(availableColumns, "fleet_number", "v"),
                GetOptionalProjection(availableColumns, "registration_number", "v"),
                GetOptionalProjection(availableColumns, "chassis_number", "v"),
                GetOptionalProjection(availableColumns, "engine_number_1", "v"),
                GetOptionalProjection(availableColumns, "invoice_number", "v"),
                GetOptionalProjection(availableColumns, "vehicle_status_code", "v"),
                GetOptionalProjection(availableColumns, "veh_site_code", "v"),
            };
            var siteFilter = BuildVehicleSiteFilter(availableColumns, allowedSiteCodes, command);
            command.CommandText =
                $"SELECT TOP (50) {string.Join(", ", projection)} FROM [dbo].[{VehicleTableName}] AS [v] WHERE ({predicate}) AND ({siteFilter}) ORDER BY {orderBy}";
            AddParameter(
                command,
                "@searchTerm",
                DbType.String,
                $"%{searchTerm.Trim().ToLowerInvariant()}%"
            );

            var results = new List<ContractVehicleLookup>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(
                    new ContractVehicleLookup(
                        ReadInt32(reader, "vmf_code") ?? 0,
                        ReadStringIfAvailable(reader, availableColumns, "fleet_number"),
                        ReadStringIfAvailable(reader, availableColumns, "registration_number"),
                        ReadStringIfAvailable(reader, availableColumns, "chassis_number"),
                        ReadStringIfAvailable(reader, availableColumns, "engine_number_1"),
                        ReadStringIfAvailable(reader, availableColumns, "invoice_number"),
                        ReadInt16IfAvailable(reader, availableColumns, "vehicle_status_code"),
                        ReadInt16IfAvailable(reader, availableColumns, "veh_site_code")
                    )
                );
            }

            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<ContractVehicleLookup?> GetVehicleForContractAsync(
        int vmfCode,
        IReadOnlyCollection<short>? allowedSiteCodes = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync(
            VehicleTableName,
            RequiredVehicleColumns
        );
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var projection = new[]
            {
                "[v].[vmf_code] AS [vmf_code]",
                GetOptionalProjection(availableColumns, "fleet_number", "v"),
                GetOptionalProjection(availableColumns, "registration_number", "v"),
                GetOptionalProjection(availableColumns, "chassis_number", "v"),
                GetOptionalProjection(availableColumns, "engine_number_1", "v"),
                GetOptionalProjection(availableColumns, "invoice_number", "v"),
                GetOptionalProjection(availableColumns, "vehicle_status_code", "v"),
                GetOptionalProjection(availableColumns, "veh_site_code", "v"),
            };
            var siteFilter = BuildVehicleSiteFilter(availableColumns, allowedSiteCodes, command);
            command.CommandText =
                $"SELECT TOP (1) {string.Join(", ", projection)} FROM [dbo].[{VehicleTableName}] AS [v] WHERE [v].[vmf_code] = @vmfCode AND ({siteFilter})";
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new ContractVehicleLookup(
                ReadInt32(reader, "vmf_code") ?? 0,
                ReadStringIfAvailable(reader, availableColumns, "fleet_number"),
                ReadStringIfAvailable(reader, availableColumns, "registration_number"),
                ReadStringIfAvailable(reader, availableColumns, "chassis_number"),
                ReadStringIfAvailable(reader, availableColumns, "engine_number_1"),
                ReadStringIfAvailable(reader, availableColumns, "invoice_number"),
                ReadInt16IfAvailable(reader, availableColumns, "vehicle_status_code"),
                ReadInt16IfAvailable(reader, availableColumns, "veh_site_code")
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string BuildVehicleSiteFilter(
        IReadOnlySet<string> availableColumns,
        IReadOnlyCollection<short>? allowedSiteCodes,
        DbCommand command
    )
    {
        if (allowedSiteCodes is null)
            return "1 = 1";

        var siteCodes = allowedSiteCodes.Distinct().ToArray();
        if (siteCodes.Length == 0)
            return "1 = 0";

        var siteColumns = new[] { "veh_site_code", "initial_site_code", "default_site" }
            .Where(availableColumns.Contains)
            .Select(column => $"[v].[{column}]")
            .ToArray();
        if (siteColumns.Length == 0)
            return "1 = 0";

        var parameters = siteCodes.Select((siteCode, index) =>
        {
            var parameterName = $"@vehicleSiteCode{index}";
            AddParameter(command, parameterName, DbType.Int16, siteCode);
            return parameterName;
        });

        var siteExpression = siteColumns.Length == 1
            ? siteColumns[0]
            : $"COALESCE({string.Join(", ", siteColumns)})";
        return $"{siteExpression} IN ({string.Join(", ", parameters)})";
    }

    public async Task<Contract?> GetActiveContractByVehicleAsync(int vmfCode)
    {
        var (contractColumns, vehicleColumns, siteColumns) = await GetProjectionColumnsAsync();
        return (
            await QueryAsync(
                contractColumns,
                vehicleColumns,
                siteColumns,
                $"[c].[vmf_code] = @vmfCode AND [c].[still_current] = 'Y' AND {GetNotDeletedFilter(contractColumns)}",
                [new QueryParameter("@vmfCode", DbType.Int32, vmfCode)],
                "[c].[contract_code] DESC",
                take: 1
            )
        ).SingleOrDefault();
    }

    public async Task<bool> HasActiveContractAsync(int vmfCode)
    {
        var contractColumns = await GetAvailableColumnsAsync(
            ContractTableName,
            RequiredContractColumns
        );
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"SELECT COUNT(1) FROM [dbo].[{ContractTableName}] AS [c] WHERE [c].[vmf_code] = @vmfCode AND [c].[still_current] = 'Y' AND {GetNotDeletedFilter(contractColumns)}";
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Executes the legacy ordinary-contract creation workflow. The archived
    /// procedure deliberately inserts a non-current approval row, initializes
    /// its contract group, records status history, and lets the database
    /// journal/trigger chain participate in the same transaction. A direct
    /// EF/SQL insert would bypass those rules, so absence or drift of the
    /// procedure is reported instead of approximated.
    /// </summary>
    public async Task<Contract> CreateForApprovalAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);

        const string procedureName = "DEV_INS_Contract_New_ForApproval";
        var standardParameters = new[]
        {
            "@ContractCode",
            "@VMFCode",
            "@SiteCode",
            "@StartDate",
            "@StartTime",
            "@StartOdometer",
            "@ContractType",
            "@DriverSAID",
            "@DriverName",
            "@Authorisation",
            "@Notes",
            "@TargetReturnDate",
            "@UserID",
            "@Responsibility",
            "@Objective",
            "@Project",
            "@Fund",
            "@ReliefForContract",
            "@contract_status_code",
            "@contract_status_date",
            "@vehicle_assessment_code",
            "@approver_code",
            "@site_driver_code",
            "@collector_firstname",
            "@collector_surname",
            "@collector_sa_id",
            "@collector_passportnumber",
            "@collector_office_number",
            "@collector_cellphone_number",
            "@collector_office",
            "@collector_designation",
            "@relief_vehicle_option",
            "@lease_contract_period",
            "@contract_estimated_overall_km",
        };
        var extendedParameters = standardParameters
            .Concat(
            [
                "@contract_backdating_requestedBy",
                "@contract_backdating_date",
                "@contract_backdating_start_date",
            ]
        )
            .ToArray();

        var deployedParameters = await GetLegacyProcedureParametersAsync(procedureName);
        if (deployedParameters is null)
        {
            throw new NotSupportedException(
                $"The legacy contract approval procedure {procedureName} is unavailable; ordinary contract creation cannot be approximated."
            );
        }

        var hasExtendedBackdatingContract = deployedParameters.SequenceEqual(
            extendedParameters,
            StringComparer.OrdinalIgnoreCase
        );
        if (!hasExtendedBackdatingContract && !deployedParameters.SequenceEqual(
                standardParameters,
                StringComparer.OrdinalIgnoreCase
            ))
        {
            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match the archived parameter contract. No direct-DML fallback was run."
            );
        }

        if (!hasExtendedBackdatingContract && HasBackdatingRequest(contract))
        {
            throw new NotSupportedException(
                $"The deployed legacy procedure {procedureName} has no backdating parameters; the backdating request was not silently dropped."
            );
        }

        // DEV_INS_Contract_New_ForApproval receives the capturer as @UserID.
        // That value is persisted into user_code by the legacy procedure, so
        // never allow a posted/domain value to impersonate another capturer.
        var ownerCode = currentUserId is > 0 and <= short.MaxValue
            ? (short)currentUserId
            : throw new UnauthorizedAccessException(
                "A valid authenticated user is required to capture a contract."
            );
        contract.user_code = ownerCode;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            command.CommandTimeout = 180;

            var contractCode = command.CreateParameter();
            contractCode.ParameterName = "@ContractCode";
            contractCode.DbType = DbType.Int32;
            contractCode.Direction = ParameterDirection.Output;
            command.Parameters.Add(contractCode);

            AddParameter(command, "@VMFCode", DbType.Int32, contract.vmf_code);
            AddParameter(command, "@SiteCode", DbType.Int16, contract.site_code);
            AddParameter(command, "@StartDate", DbType.DateTime, contract.start_date);
            AddParameter(command, "@StartTime", DbType.DateTime, contract.start_time);
            AddParameter(command, "@StartOdometer", DbType.Int32, contract.start_odometer);
            AddParameter(command, "@ContractType", DbType.String, contract.contract_type);
            AddParameter(command, "@DriverSAID", DbType.String, contract.Driver_id);
            AddParameter(command, "@DriverName", DbType.String, contract.Driver_name);
            AddParameter(command, "@Authorisation", DbType.String, contract.Authorisation);
            AddParameter(command, "@Notes", DbType.String, contract.Notes);
            AddParameter(command, "@TargetReturnDate", DbType.DateTime, contract.target_return_date);
            AddParameter(
                command,
                "@UserID",
                DbType.Int32,
                ownerCode
            );
            AddParameter(command, "@Responsibility", DbType.String, contract.bas_responsibility_code);
            AddParameter(command, "@Objective", DbType.String, contract.bas_objective_code);
            AddParameter(command, "@Project", DbType.String, contract.bas_project_number);
            AddParameter(command, "@Fund", DbType.String, contract.bas_fund_code);
            AddParameter(command, "@ReliefForContract", DbType.Int32, contract.relief_for_contract);
            AddParameter(command, "@contract_status_code", DbType.Int16, contract.contract_status_code ?? 1);
            AddParameter(
                command,
                "@contract_status_date",
                DbType.DateTime,
                contract.contract_status_date ?? DateTime.Today
            );
            AddParameter(command, "@vehicle_assessment_code", DbType.Int32, contract.vehicle_assessment_code ?? 0);
            AddParameter(command, "@approver_code", DbType.Int32, contract.approver_code ?? 0);
            AddParameter(command, "@site_driver_code", DbType.Int32, contract.site_driver_code ?? 0);
            AddParameter(command, "@collector_firstname", DbType.String, contract.collector_firstname);
            AddParameter(command, "@collector_surname", DbType.String, contract.collector_surname);
            AddParameter(command, "@collector_sa_id", DbType.String, contract.collector_sa_id);
            AddParameter(command, "@collector_passportnumber", DbType.String, contract.collector_passportnumber);
            AddParameter(command, "@collector_office_number", DbType.String, contract.collector_office_number);
            AddParameter(command, "@collector_cellphone_number", DbType.String, contract.collector_cellphone_number);
            AddParameter(command, "@collector_office", DbType.String, contract.collector_office);
            AddParameter(command, "@collector_designation", DbType.String, contract.collector_designation);
            AddParameter(command, "@relief_vehicle_option", DbType.Boolean, contract.relief_vehicle_option ?? false);
            AddParameter(command, "@lease_contract_period", DbType.Byte, contract.lease_contract_period ?? 0);
            AddParameter(
                command,
                "@contract_estimated_overall_km",
                DbType.Int32,
                contract.contract_estimated_overall_km ?? 0
            );
            if (hasExtendedBackdatingContract)
            {
                var hasBackdatingRequest = HasBackdatingRequest(contract);
                AddParameter(
                    command,
                    "@contract_backdating_requestedBy",
                    DbType.String,
                    hasBackdatingRequest
                        ? contract.backdating_requested_by_username
                            ?? await ResolveLegacyUsernameAsync(currentUserId)
                        : null
                );
                AddParameter(
                    command,
                    "@contract_backdating_date",
                    DbType.DateTime,
                    contract.backdating_requested_date
                );
                AddParameter(
                    command,
                    "@contract_backdating_start_date",
                    DbType.DateTime,
                    contract.backdating_start_date
                );
            }

            await command.ExecuteNonQueryAsync();
            if (contractCode.Value is null || contractCode.Value == DBNull.Value)
                throw new InvalidOperationException(
                    $"The legacy contract approval procedure {procedureName} did not return a contract code."
                );

            var createdCode = Convert.ToInt32(contractCode.Value);
            return await GetByIdAsync(createdCode)
                ?? throw new InvalidOperationException(
                    $"The legacy contract approval procedure {procedureName} returned contract {createdCode}, but it could not be read back."
                );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<Contract> CreateAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);

        // This method is reserved for the modern home-custody compatibility
        // path. If it has to write the legacy table directly, retain the
        // duplicate-contract and journal-trigger transaction that the source
        // database owns; never silently create a non-billable contract row.
        await EnsureLegacyContractTriggersAsync(DirectInsertTriggerNames);

        if (contract.still_current == "Y" && await HasActiveContractAsync(contract.vmf_code))
        {
            throw new InvalidOperationException(
                $"Vehicle {contract.vmf_code} already has an active contract. Only one active contract per vehicle is allowed."
            );
        }

        var availableColumns = await GetAvailableColumnsAsync(
            ContractTableName,
            RequiredContractColumns
        );
        var now = DateTime.UtcNow;
        var values = BuildWriteValues(contract)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();

        AddOptionalValue(
            values,
            availableColumns,
            "capture_date",
            "@captureDate",
            DbType.DateTime2,
            contract.capture_date ?? now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            contract.date_created == default ? now : contract.date_created
        );
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            contract.created_by_user_code ?? (currentUserId > 0 ? currentUserId : null)
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false
        );

        contract.contract_code = await ExecuteInsertAsync(values);
        contract.capture_date ??= now;
        contract.date_created = contract.date_created == default ? now : contract.date_created;
        contract.created_by_user_code ??= currentUserId > 0 ? currentUserId : null;
        contract.is_deleted = false;
        return contract;
    }

    public async Task UpdateAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);

        // The legacy ContractProvider sends ordinary modifications through
        // NEW_DEV_UPD_Contract. Prefer that procedure whenever the connected
        // database exposes the archived parameter contract so its journal,
        // reversal/rebill, validation, and transaction behavior remains the
        // source of truth. A trigger-backed direct update is only the
        // compatibility fallback when the procedure is genuinely absent.
        var hasLegacyUpdateProcedure = await IsLegacyProcedureAvailableAsync(
            "NEW_DEV_UPD_Contract",
            "@vmfCode",
            "@sitecode",
            "@startdate",
            "@starttime",
            "@enddate",
            "@endtime",
            "@startodometer",
            "@endodometer",
            "@stillcurrent",
            "@contracttype",
            "@driverid",
            "@authorisation",
            "@drivername",
            "@notes",
            "@targetreturndate",
            "@usercode",
            "@chargedUntil",
            "@basobjectivecode",
            "@basresponsibilitycode",
            "@basProjectNumber",
            "@reliefForContract",
            "@lockedForTransfer",
            "@hoursUsed",
            "@contractgroupcode"
        );

        var existing = await GetByIdAsync(contract.contract_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Contract with contract_code {contract.contract_code} not found"
            );
        }

        // A posted DTO must never replace the persisted capturer. The
        // manager/approver reassignment endpoint is the only explicit owner
        // transfer workflow.
        contract.user_code = existing.user_code;

        if (contract.still_current == "Y")
        {
            var active = await GetActiveContractByVehicleAsync(contract.vmf_code);
            if (active != null && active.contract_code != contract.contract_code)
            {
                throw new InvalidOperationException(
                    $"Vehicle {contract.vmf_code} already has another active contract. Only one active contract per vehicle is allowed."
                );
            }
        }

        if (hasLegacyUpdateProcedure)
        {
            await ExecuteInLegacyTransactionAsync(
                "FIS_ContractUpdate",
                async () =>
                {
                    await ExecuteLegacyProcedureAsync(
                        "NEW_DEV_UPD_Contract",
                        new ProcedureParameter("@vmfCode", DbType.Int32, contract.vmf_code),
                        new ProcedureParameter("@sitecode", DbType.Int16, contract.site_code),
                        new ProcedureParameter("@startdate", DbType.DateTime, contract.start_date),
                        new ProcedureParameter("@starttime", DbType.DateTime, contract.start_time),
                        new ProcedureParameter("@enddate", DbType.DateTime, contract.end_date),
                        new ProcedureParameter("@endtime", DbType.DateTime, contract.end_time),
                        new ProcedureParameter("@startodometer", DbType.Int32, contract.start_odometer),
                        new ProcedureParameter("@endodometer", DbType.Int32, contract.end_odometer),
                        new ProcedureParameter("@stillcurrent", DbType.String, contract.still_current),
                        new ProcedureParameter("@contracttype", DbType.String, contract.contract_type),
                        new ProcedureParameter("@driverid", DbType.String, contract.Driver_id),
                        new ProcedureParameter("@authorisation", DbType.String, contract.Authorisation),
                        new ProcedureParameter("@drivername", DbType.String, contract.Driver_name),
                        new ProcedureParameter("@notes", DbType.String, contract.Notes),
                        new ProcedureParameter("@targetreturndate", DbType.DateTime, contract.target_return_date),
                        new ProcedureParameter("@usercode", DbType.Int32, contract.user_code),
                        new ProcedureParameter("@chargedUntil", DbType.DateTime, contract.Charged_Until),
                        new ProcedureParameter("@basobjectivecode", DbType.String, contract.bas_objective_code),
                        new ProcedureParameter("@basresponsibilitycode", DbType.String, contract.bas_responsibility_code),
                        new ProcedureParameter("@basProjectNumber", DbType.String, contract.bas_project_number),
                        new ProcedureParameter("@reliefForContract", DbType.Int32, contract.relief_for_contract),
                        new ProcedureParameter("@lockedForTransfer", DbType.Boolean, contract.locked_for_transfer),
                        new ProcedureParameter("@hoursUsed", DbType.Int16, contract.hours_used),
                        new ProcedureParameter("@contractgroupcode", DbType.Int32, contract.contract_group_code)
                    );
                    await RestoreLegacyCapturerAsync(contract.contract_code, existing.user_code);
                    return true;
                }
            );

            return;
        }

        await UpdateDirectAsync(contract, currentUserId, existing);
    }

    /// <summary>
    /// Applies the explicitly permitted direct-DML compatibility fallback for
    /// a contract update. Callers must use this only after proving that the
    /// source operation's stored procedure is genuinely absent. The legacy
    /// journal trigger is checked before the write so the fallback cannot
    /// silently bypass billing/reversal protection.
    /// </summary>
    private async Task UpdateDirectAsync(
        Contract contract,
        int currentUserId,
        Contract existing
    )
    {
        await EnsureLegacyContractTriggersAsync(DirectUpdateTriggerNames);

        var availableColumns = await GetAvailableColumnsAsync(
            ContractTableName,
            RequiredContractColumns
        );
        var now = DateTime.UtcNow;
        var values = BuildWriteValues(contract)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();

        AddOptionalValue(
            values,
            availableColumns,
            "modified_date",
            "@modifiedDate",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );

        await ExecuteUpdateAsync(contract.contract_code, values);
        contract.date_created = existing.date_created;
        contract.capture_date ??= existing.capture_date;
        contract.created_by_user_code ??= existing.created_by_user_code;
        contract.modified_date = now;
        contract.date_updated = now;
        contract.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
    }

    /// <summary>
    /// Runs the archived contract-history mutation.  The legacy procedure
    /// updates the selected contract and any follow-up contract while keeping
    /// billing/journal state in the database transaction.  A generic UPDATE
    /// is deliberately not used when the procedure is unavailable because it
    /// cannot reproduce those linked-row side effects.
    /// </summary>
    public async Task<Contract> UpdateHistoryAsync(
        int contractCode,
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        string fleetNumber
    )
    {
        if (contractCode <= 0)
            throw new ArgumentException("A valid contract code is required.", nameof(contractCode));
        if (string.IsNullOrWhiteSpace(fleetNumber))
            throw new ArgumentException("A fleet number is required.", nameof(fleetNumber));
        if (endDate.Date < startDate.Date)
            throw new ArgumentException("The contract end date cannot be before its start date.", nameof(endDate));
        if (startOdometer < 0 || endOdometer < 0)
            throw new ArgumentException("Contract odometers cannot be negative.");
        if (endOdometer < startOdometer)
            throw new ArgumentException("The contract end odometer cannot be less than its start odometer.");

        var existing = await GetByIdAsync(contractCode)
            ?? throw new InvalidOperationException(
                $"Contract with contract_code {contractCode} not found"
            );

        const string procedureName = "ADM_Change_End_And_Odo_With_FollowUp_Contract";
        var expectedParameters = new[]
        {
            "@contractToUpdate",
            "@newStartDate",
            "@newEndDate",
            "@startOdometer",
            "@endOdometer",
            "@FleetNumber",
        };

        if (!await IsLegacyProcedureAvailableAsync(procedureName, expectedParameters))
        {
            throw new NotSupportedException(
                $"The legacy contract-history procedure {procedureName} is unavailable; no generic contract update was run."
            );
        }

        return await ExecuteInLegacyTransactionAsync(
            "FIS_ContractHistoryBackdating",
            async () =>
            {
                await ExecuteLegacyProcedureAsync(
                    procedureName,
                    new ProcedureParameter("@contractToUpdate", DbType.Int32, contractCode),
                    new ProcedureParameter("@newStartDate", DbType.DateTime, startDate.Date),
                    new ProcedureParameter("@newEndDate", DbType.DateTime, endDate.Date),
                    new ProcedureParameter("@startOdometer", DbType.Int32, startOdometer),
                    new ProcedureParameter("@endOdometer", DbType.Int32, endOdometer),
                    new ProcedureParameter("@FleetNumber", DbType.String, fleetNumber.Trim())
                );

                await RestoreLegacyCapturerAsync(contractCode, existing.user_code);

                return await GetByIdAsync(contractCode)
                    ?? throw new InvalidOperationException(
                        $"The legacy contract-history procedure {procedureName} completed, but contract {contractCode} could not be read back."
                    );
            }
        );
    }

    /// <summary>
    /// Updates a pending contract through the legacy approval-phase procedure.
    /// The procedure preserves the persisted status/current flags, writes the
    /// approval history, and owns its transaction and trigger behavior.
    /// </summary>
    public async Task<Contract> UpdatePendingForApprovalAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.contract_code <= 0)
            throw new ArgumentException("A valid contract code is required.", nameof(contract));

        // Never trust a posted DTO for ownership. Resolve the persisted
        // capturer before invoking the procedure because @UserID is also
        // written to user_code by the legacy implementation.
        var persistedContract = await GetByIdAsync(contract.contract_code)
            ?? throw new InvalidOperationException(
                $"Contract with contract_code {contract.contract_code} not found"
            );
        contract.user_code = persistedContract.user_code;

        const string procedureName = "DEV_UPD_Contract_New_ApprovalPhase";
        var expectedParameters = new[]
        {
            "@ContractCode",
            "@VMFCode",
            "@SiteCode",
            "@StartDate",
            "@StartTime",
            "@StartOdometer",
            "@ContractType",
            "@DriverSAID",
            "@DriverName",
            "@Authorisation",
            "@Notes",
            "@TargetReturnDate",
            "@UserID",
            "@Responsibility",
            "@Objective",
            "@Project",
            "@Fund",
            "@ReliefForContract",
            "@contract_status_code",
            "@contract_status_date",
            "@vehicle_assessment_code",
            "@approver_code",
            "@site_driver_code",
            "@collector_firstname",
            "@collector_surname",
            "@collector_sa_id",
            "@collector_passportnumber",
            "@collector_office_number",
            "@collector_cellphone_number",
            "@collector_office",
            "@collector_designation",
            "@relief_vehicle_option",
            "@lease_contract_period",
            "@contract_estimated_overall_km",
        };

        if (!await IsLegacyProcedureAvailableAsync(procedureName, expectedParameters))
        {
            throw new NotSupportedException(
                $"The legacy pending-contract procedure {procedureName} is unavailable; pending contract edits cannot be approximated."
            );
        }

        return await ExecuteInLegacyTransactionAsync(
            "FIS_ContractPendingApproval",
            async () =>
            {
                await ExecuteLegacyProcedureAsync(
                    procedureName,
            new ProcedureParameter("@ContractCode", DbType.Int32, contract.contract_code),
            new ProcedureParameter("@VMFCode", DbType.Int32, contract.vmf_code),
            new ProcedureParameter("@SiteCode", DbType.Int16, contract.site_code),
            new ProcedureParameter("@StartDate", DbType.DateTime, contract.start_date),
            new ProcedureParameter("@StartTime", DbType.DateTime, contract.start_time),
            new ProcedureParameter("@StartOdometer", DbType.Int32, contract.start_odometer),
            new ProcedureParameter("@ContractType", DbType.String, contract.contract_type),
            new ProcedureParameter("@DriverSAID", DbType.String, contract.Driver_id),
            new ProcedureParameter("@DriverName", DbType.String, contract.Driver_name),
            new ProcedureParameter("@Authorisation", DbType.String, contract.Authorisation),
            new ProcedureParameter("@Notes", DbType.String, contract.Notes),
            new ProcedureParameter("@TargetReturnDate", DbType.DateTime, contract.target_return_date),
            // The legacy procedure uses @UserID for both its action/status
            // history actor and the row's user_code. Pass the authenticated
            // actor so history is truthful, then restore the original owner
            // immediately after the procedure completes.
            new ProcedureParameter("@UserID", DbType.Int32, currentUserId),
            new ProcedureParameter("@Responsibility", DbType.String, contract.bas_responsibility_code),
            new ProcedureParameter("@Objective", DbType.String, contract.bas_objective_code),
            new ProcedureParameter("@Project", DbType.String, contract.bas_project_number),
            new ProcedureParameter("@Fund", DbType.String, contract.bas_fund_code),
            new ProcedureParameter("@ReliefForContract", DbType.Int32, contract.relief_for_contract),
            new ProcedureParameter("@contract_status_code", DbType.Int16, contract.contract_status_code ?? 1),
            new ProcedureParameter("@contract_status_date", DbType.DateTime, contract.contract_status_date ?? DateTime.Today),
            new ProcedureParameter("@vehicle_assessment_code", DbType.Int32, contract.vehicle_assessment_code ?? 0),
            new ProcedureParameter("@approver_code", DbType.Int32, contract.approver_code ?? 0),
            new ProcedureParameter("@site_driver_code", DbType.Int32, contract.site_driver_code ?? 0),
            new ProcedureParameter("@collector_firstname", DbType.String, contract.collector_firstname),
            new ProcedureParameter("@collector_surname", DbType.String, contract.collector_surname),
            new ProcedureParameter("@collector_sa_id", DbType.String, contract.collector_sa_id),
            new ProcedureParameter("@collector_passportnumber", DbType.String, contract.collector_passportnumber),
            new ProcedureParameter("@collector_office_number", DbType.String, contract.collector_office_number),
            new ProcedureParameter("@collector_cellphone_number", DbType.String, contract.collector_cellphone_number),
            new ProcedureParameter("@collector_office", DbType.String, contract.collector_office),
            new ProcedureParameter("@collector_designation", DbType.String, contract.collector_designation),
            new ProcedureParameter("@relief_vehicle_option", DbType.Boolean, contract.relief_vehicle_option ?? false),
            new ProcedureParameter("@lease_contract_period", DbType.Byte, contract.lease_contract_period ?? 0),
                    new ProcedureParameter("@contract_estimated_overall_km", DbType.Int32, contract.contract_estimated_overall_km ?? 0)
                );
                await RestoreLegacyCapturerAsync(contract.contract_code, contract.user_code);

                return await GetByIdAsync(contract.contract_code)
                    ?? throw new InvalidOperationException(
                        $"The legacy pending-contract procedure {procedureName} completed, but the contract could not be read back."
                    );
            }
        );
    }

    public async Task<Contract> ExtendExistingAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.contract_code <= 0)
            throw new ArgumentException("A valid contract code is required.", nameof(contract));
        if (!contract.target_return_date.HasValue)
            throw new ArgumentException("A target return date is required to extend a contract.", nameof(contract));

        // Read the persisted row before invoking the legacy procedure.  An
        // extension changes the target return date (and optional notes/km)
        // only; it must never close the contract or move its billing cursor.
        // Keeping this snapshot also prevents a stale caller payload from
        // becoming the new capturer.
        var beforeExtension = await GetByIdAsync(contract.contract_code)
            ?? throw new InvalidOperationException(
                $"Contract with contract_code {contract.contract_code} not found"
            );
        if (!string.Equals(beforeExtension.still_current, "Y", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Contract {contract.contract_code} is not currently active and cannot be extended."
            );
        }

        contract.user_code = beforeExtension.user_code;

        var hasOptionalExtensionChanges =
            !string.Equals(contract.Notes, beforeExtension.Notes, StringComparison.Ordinal)
            || contract.contract_estimated_overall_km != beforeExtension.contract_estimated_overall_km;
        var hasLegacyTargetReturnDateProcedure = false;
        if (!hasOptionalExtensionChanges)
        {
            try
            {
                hasLegacyTargetReturnDateProcedure = await IsLegacyProcedureAvailableAsync(
                    "NEW_DEV_UPD_Contract_TargetReturnDate",
                    "@contractcode",
                    "@targetreturndate"
                );
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("does not match the archived parameter contract", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "The deployed legacy target-return-date procedure is incompatible; no direct-DML fallback was run.",
                    exception
                );
            }
        }

        var hasLegacyExtensionProcedure = false;
        if (!hasLegacyTargetReturnDateProcedure || hasOptionalExtensionChanges)
        {
            try
            {
                hasLegacyExtensionProcedure = await IsLegacyProcedureAvailableAsync(
                    "DEV_UPD_Contract_ExtendExisting",
                    "@ContractCode",
                    "@VMFCode",
                    "@TargetReturnDate",
                    "@contract_estimated_overall_km",
                    "@Notes",
                    "@UserID"
                );
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("does not match the archived parameter contract", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    "The deployed legacy contract-extension procedure is incompatible; no direct-DML fallback was run.",
                    exception
                );
            }
        }

        // The archived extension procedure and the explicit modern ownership
        // correction must commit as one unit. If the deployed procedure
        // unexpectedly changes a billing boundary, roll the whole operation
        // back before reporting a dependency failure to the caller.
        var existingTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction is null;
        var extensionTransaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;
        const string savepointName = "FIS_ContractExtension";
        if (!ownsTransaction)
        {
            await existingTransaction!.CreateSavepointAsync(savepointName);
        }

        try
        {
            if (hasLegacyTargetReturnDateProcedure && !hasOptionalExtensionChanges)
            {
                // The archived GGFleet Provider exposes a separate target-date
                // operation with only these two parameters. Use it first when
                // this request is a pure target-return-date extension.
                await ExecuteLegacyProcedureAsync(
                    "NEW_DEV_UPD_Contract_TargetReturnDate",
                    new ProcedureParameter("@contractcode", DbType.Int32, contract.contract_code),
                    new ProcedureParameter("@targetreturndate", DbType.DateTime, contract.target_return_date.Value.Date)
                );
                await RestoreLegacyCapturerAsync(contract.contract_code, beforeExtension.user_code);
            }
            else if (hasLegacyExtensionProcedure)
            {
                await ExecuteLegacyProcedureAsync(
                    "DEV_UPD_Contract_ExtendExisting",
                    new ProcedureParameter("@ContractCode", DbType.Int32, contract.contract_code),
                    new ProcedureParameter("@VMFCode", DbType.Int32, contract.vmf_code),
                    new ProcedureParameter(
                        "@TargetReturnDate",
                        DbType.DateTime,
                        contract.target_return_date.Value
                    ),
                    new ProcedureParameter(
                        "@contract_estimated_overall_km",
                        DbType.Int32,
                        contract.contract_estimated_overall_km
                    ),
                    new ProcedureParameter("@Notes", DbType.String, contract.Notes),
                    // Preserve the legacy procedure's action/audit identity;
                    // the exact-row correction below prevents that identity
                    // from replacing the contract's original owner.
                    new ProcedureParameter("@UserID", DbType.Int32, currentUserId)
                );
                await RestoreLegacyCapturerAsync(contract.contract_code, beforeExtension.user_code);
            }
            else if (hasLegacyTargetReturnDateProcedure)
            {
                // The archived GGFleet Provider exposes a separate target-date
                // operation with only these two parameters. Do not use it when
                // the caller is also trying to change notes or estimated km,
                // because that would silently discard those submitted fields.
                if (!string.Equals(contract.Notes, beforeExtension.Notes, StringComparison.Ordinal)
                    || contract.contract_estimated_overall_km != beforeExtension.contract_estimated_overall_km)
                {
                    throw new NotSupportedException(
                        "The legacy target-return-date procedure does not accept notes or estimated-kilometre changes; no submitted contract fields were dropped."
                    );
                }

                await ExecuteLegacyProcedureAsync(
                    "NEW_DEV_UPD_Contract_TargetReturnDate",
                    new ProcedureParameter("@contractcode", DbType.Int32, contract.contract_code),
                    new ProcedureParameter("@targetreturndate", DbType.DateTime, contract.target_return_date.Value.Date)
                );
                await RestoreLegacyCapturerAsync(contract.contract_code, beforeExtension.user_code);
            }
            else
            {
                // Extending an active contract is a billing operation in the
                // legacy system: its procedure preserves Charged_Until,
                // journal ownership and still_current while applying the new
                // target return date. A generic EF update cannot reproduce
                // those trigger/procedure semantics, so fail closed when the
                // source procedure is not deployed.
                throw new NotSupportedException(
                    "The legacy contract-extension procedure is unavailable; active contract extension cannot be approximated with direct DML."
                );
            }

            var updated = await GetByIdAsync(contract.contract_code)
                ?? throw new InvalidOperationException(
                    "The contract-extension operation removed the selected contract."
                );
            EnsureExtensionBillingInvariant(beforeExtension, updated, contract.target_return_date.Value);

            if (extensionTransaction is not null)
                await extensionTransaction.CommitAsync();
            return updated;
        }
        catch
        {
            if (extensionTransaction is not null)
                await extensionTransaction.RollbackAsync();
            else if (existingTransaction is not null)
                await existingTransaction.RollbackToSavepointAsync(savepointName);
            throw;
        }
        finally
        {
            if (extensionTransaction is not null)
                await extensionTransaction.DisposeAsync();
        }
    }

    private static void EnsureExtensionBillingInvariant(
        Contract before,
        Contract after,
        DateTime expectedTargetReturnDate
    )
    {
        var differences = new List<string>();
        if (!string.Equals(after.still_current, "Y", StringComparison.OrdinalIgnoreCase))
            differences.Add("still_current was changed from Y");
        if (!SameDate(before.end_date, after.end_date))
            differences.Add("end_date was changed");
        if (!SameDate(before.Charged_Until, after.Charged_Until))
            differences.Add("Charged_Until was changed");
        if (before.journal_detail_code != after.journal_detail_code)
            differences.Add("journal_detail_code was changed");
        if (before.user_code != after.user_code)
            differences.Add("user_code/capturer was changed");
        if (!SameDate(after.target_return_date, expectedTargetReturnDate))
            differences.Add("target_return_date was not persisted");

        if (differences.Count > 0)
        {
            throw new LegacyContractExtensionBillingInvariantException(
                $"The contract extension changed billing ownership/state unexpectedly: {string.Join(", ", differences)}. The extension was not accepted as a compatible operation."
            );
        }
    }

    private static bool SameDate(DateTime? left, DateTime? right) =>
        left.HasValue == right.HasValue
        && (!left.HasValue || left.Value.Date == right!.Value.Date);

    public async Task<Contract> UpdatePendingDecisionAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.contract_code <= 0)
            throw new ArgumentException("A valid contract code is required.", nameof(contract));
        if (!contract.contract_status_code.HasValue)
            throw new ArgumentException("A contract status is required.", nameof(contract));

        // Approval decisions must retain the owner captured on the database
        // row even when a caller sends a stale or substituted user_code.
        var persistedContract = await GetByIdAsync(contract.contract_code)
            ?? throw new InvalidOperationException(
                $"Contract with contract_code {contract.contract_code} not found"
            );
        contract.user_code = persistedContract.user_code;

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_Contract_New_ApproveDeclineOrCancel",
                "@ContractCode",
                "@VMFCode",
                "@SiteCode",
                "@StartDate",
                "@StartTime",
                "@StartOdometer",
                "@ContractType",
                "@DriverSAID",
                "@DriverName",
                "@Authorisation",
                "@Notes",
                "@TargetReturnDate",
                "@UserID",
                "@Responsibility",
                "@Objective",
                "@Project",
                "@Fund",
                "@ReliefForContract",
                "@contract_status_code",
                "@contract_status_date",
                "@vehicle_assessment_code",
                "@approver_code",
                "@site_driver_code",
                "@collector_firstname",
                "@collector_surname",
                "@collector_sa_id",
                "@collector_passportnumber",
                "@collector_office_number",
                "@collector_cellphone_number",
                "@collector_office",
                "@collector_designation",
                "@relief_vehicle_option",
                "@lease_contract_period",
                "@contract_estimated_overall_km"
            )
        )
        {
            return await ExecuteInLegacyTransactionAsync(
                "FIS_ContractPendingDecision",
                async () =>
                {
                    await ExecuteLegacyProcedureAsync(
                        "DEV_UPD_Contract_New_ApproveDeclineOrCancel",
                new ProcedureParameter("@ContractCode", DbType.Int32, contract.contract_code),
                new ProcedureParameter("@VMFCode", DbType.Int32, contract.vmf_code),
                new ProcedureParameter("@SiteCode", DbType.Int16, contract.site_code),
                new ProcedureParameter("@StartDate", DbType.DateTime, contract.start_date),
                new ProcedureParameter("@StartTime", DbType.DateTime, contract.start_time),
                new ProcedureParameter("@StartOdometer", DbType.Int32, contract.start_odometer),
                new ProcedureParameter("@ContractType", DbType.String, contract.contract_type),
                new ProcedureParameter("@DriverSAID", DbType.String, contract.Driver_id),
                new ProcedureParameter("@DriverName", DbType.String, contract.Driver_name),
                new ProcedureParameter("@Authorisation", DbType.String, contract.Authorisation),
                new ProcedureParameter("@Notes", DbType.String, contract.Notes),
                new ProcedureParameter(
                    "@TargetReturnDate",
                    DbType.DateTime,
                    contract.target_return_date
                ),
                // The procedure records @UserID in status history and also
                // temporarily writes it to user_code. Use the real actor for
                // the history, then restore the original capturer below.
                new ProcedureParameter("@UserID", DbType.Int32, currentUserId),
                new ProcedureParameter(
                    "@Responsibility",
                    DbType.String,
                    contract.bas_responsibility_code
                ),
                new ProcedureParameter("@Objective", DbType.String, contract.bas_objective_code),
                new ProcedureParameter("@Project", DbType.String, contract.bas_project_number),
                new ProcedureParameter("@Fund", DbType.String, contract.bas_fund_code),
                new ProcedureParameter(
                    "@ReliefForContract",
                    DbType.Int32,
                    contract.relief_for_contract
                ),
                new ProcedureParameter(
                    "@contract_status_code",
                    DbType.Int16,
                    contract.contract_status_code.Value
                ),
                new ProcedureParameter(
                    "@contract_status_date",
                    DbType.DateTime,
                    contract.contract_status_date ?? DateTime.Today
                ),
                new ProcedureParameter(
                    "@vehicle_assessment_code",
                    DbType.Int32,
                    contract.vehicle_assessment_code ?? 0
                ),
                new ProcedureParameter("@approver_code", DbType.Int32, contract.approver_code ?? 0),
                new ProcedureParameter(
                    "@site_driver_code",
                    DbType.Int32,
                    contract.site_driver_code ?? 0
                ),
                new ProcedureParameter(
                    "@collector_firstname",
                    DbType.String,
                    contract.collector_firstname
                ),
                new ProcedureParameter(
                    "@collector_surname",
                    DbType.String,
                    contract.collector_surname
                ),
                new ProcedureParameter("@collector_sa_id", DbType.String, contract.collector_sa_id),
                new ProcedureParameter(
                    "@collector_passportnumber",
                    DbType.String,
                    contract.collector_passportnumber
                ),
                new ProcedureParameter(
                    "@collector_office_number",
                    DbType.String,
                    contract.collector_office_number
                ),
                new ProcedureParameter(
                    "@collector_cellphone_number",
                    DbType.String,
                    contract.collector_cellphone_number
                ),
                new ProcedureParameter(
                    "@collector_office",
                    DbType.String,
                    contract.collector_office
                ),
                new ProcedureParameter(
                    "@collector_designation",
                    DbType.String,
                    contract.collector_designation
                ),
                new ProcedureParameter(
                    "@relief_vehicle_option",
                    DbType.Boolean,
                    contract.relief_vehicle_option ?? false
                ),
                new ProcedureParameter(
                    "@lease_contract_period",
                    DbType.Byte,
                    contract.lease_contract_period ?? 0
                ),
                        new ProcedureParameter(
                            "@contract_estimated_overall_km",
                            DbType.Int32,
                            contract.contract_estimated_overall_km ?? 0
                        )
                    );
                    await RestoreLegacyCapturerAsync(contract.contract_code, contract.user_code);
                    await ApplyBackdatingDecisionAsync(contract, currentUserId);
                    return await GetByIdAsync(contract.contract_code)
                        ?? throw new InvalidOperationException(
                            "The legacy contract decision procedure removed the selected contract."
                        );
                }
            );
        }

        throw new NotSupportedException(
            "The legacy pending-contract decision procedure is unavailable; approval, decline, or cancellation cannot be approximated with direct DML."
        );
    }

    public async Task<Contract> ActivatePendingAsync(
        Contract pendingContract,
        int existingContractCode,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(pendingContract);
        if (pendingContract.contract_code <= 0)
            throw new ArgumentException("A valid pending contract code is required.", nameof(pendingContract));

        var persistedPendingContract = await GetByIdAsync(pendingContract.contract_code)
            ?? throw new InvalidOperationException(
                $"Contract with contract_code {pendingContract.contract_code} not found"
            );
        pendingContract.user_code = persistedPendingContract.user_code;

        var existingContract = existingContractCode > 0
            ? await GetByIdAsync(existingContractCode)
            : null;

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_Contract_NewActivate",
                "@ExistingContractCode",
                "@PendingContractCode",
                "@VMFCode",
                "@StartDate",
                "@StartTime",
                "@StartOdometer",
                "@TargetReturnDate",
                "@Notes",
                "@UserID"
            )
        )
        {
            return await ExecuteInLegacyTransactionAsync(
                "FIS_ContractActivation",
                async () =>
                {
                    await ExecuteLegacyProcedureAsync(
                        "DEV_UPD_Contract_NewActivate",
                new ProcedureParameter("@ExistingContractCode", DbType.Int32, existingContractCode),
                new ProcedureParameter(
                    "@PendingContractCode",
                    DbType.Int32,
                    pendingContract.contract_code
                ),
                new ProcedureParameter("@VMFCode", DbType.Int32, pendingContract.vmf_code),
                new ProcedureParameter("@StartDate", DbType.DateTime, pendingContract.start_date),
                new ProcedureParameter("@StartTime", DbType.DateTime, pendingContract.start_time),
                new ProcedureParameter(
                    "@StartOdometer",
                    DbType.Int32,
                    pendingContract.start_odometer
                ),
                new ProcedureParameter(
                    "@TargetReturnDate",
                    DbType.DateTime,
                    pendingContract.target_return_date
                ),
                new ProcedureParameter("@Notes", DbType.String, pendingContract.Notes),
                        // The v2.1.18 activation procedure pre-creates monthly
                        // contract segments and applies @UserID to every row
                        // in the group. Pass the persisted capturer here so an
                        // approver cannot silently become owner of the whole
                        // contract chain. The API audit log records the actor.
                        new ProcedureParameter("@UserID", DbType.Int32, pendingContract.user_code)
                    );
                    await RestoreLegacyCapturerAsync(pendingContract.contract_code, pendingContract.user_code);
                    if (existingContract is not null)
                        await RestoreLegacyCapturerAsync(existingContract.contract_code, existingContract.user_code);
                    return await GetByIdAsync(pendingContract.contract_code)
                        ?? throw new InvalidOperationException(
                            "The legacy contract activation procedure removed the selected contract."
                        );
                }
            );
        }

        // There is no safe direct-DML activation equivalent: activation closes
        // the predecessor, writes status history, and may split lease periods.
        throw new NotSupportedException(
            "The legacy contract-activation procedure is unavailable; activation cannot be approximated."
        );
    }

    public async Task<Contract> ReassignExistingAsync(
        Contract existingContract,
        Contract reassignment,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(existingContract);
        ArgumentNullException.ThrowIfNull(reassignment);
        if (existingContract.contract_code <= 0 || existingContract.vmf_code <= 0)
            throw new ArgumentException("A valid active contract is required.", nameof(existingContract));
        if (reassignment.user_code is not > 0)
            throw new ArgumentException(
                "A valid capturer is required for contract reassignment.",
                nameof(reassignment)
            );

        var reassignContractCodeParameter = await ResolveReassignProcedureContractAsync();
        if (reassignContractCodeParameter is null)
        {
            throw new NotSupportedException(
                "The legacy contract-reassignment procedure is unavailable; reassignment cannot be approximated."
            );
        }

        return await ExecuteInLegacyTransactionAsync(
            "FIS_ContractReassignment",
            async () =>
            {
                var existingContractCodes = (await GetContractsByVehicleAsync(existingContract.vmf_code))
                    .Select(contract => contract.contract_code)
                    .ToHashSet();

                await ExecuteLegacyProcedureAsync(
                    "DEV_UPD_Contract_ReassignExisting",
            new ProcedureParameter(reassignContractCodeParameter, DbType.Int32, existingContract.contract_code),
            new ProcedureParameter("@VMFCode", DbType.Int32, existingContract.vmf_code),
            new ProcedureParameter("@SiteCode", DbType.Int16, reassignment.site_code),
            new ProcedureParameter("@StartDate", DbType.DateTime, reassignment.start_date),
            new ProcedureParameter("@StartTime", DbType.DateTime, reassignment.start_time),
            new ProcedureParameter(
                "@StartOdometer",
                DbType.Int32,
                reassignment.start_odometer
            ),
            new ProcedureParameter("@DriverSAID", DbType.String, reassignment.Driver_id),
            new ProcedureParameter("@DriverName", DbType.String, reassignment.Driver_name),
            new ProcedureParameter(
                "@Authorisation",
                DbType.String,
                reassignment.Authorisation
            ),
            new ProcedureParameter("@Notes", DbType.String, reassignment.Notes),
            // The legacy procedure uses @UserID for both action history and
            // row ownership. Pass the manager performing the reassignment so
            // the legacy status history is truthful; the exact owner columns
            // are corrected after the procedure in this same transaction.
            new ProcedureParameter("@UserID", DbType.Int32, currentUserId),
            new ProcedureParameter(
                "@Responsibility",
                DbType.String,
                reassignment.bas_responsibility_code
            ),
            new ProcedureParameter("@Objective", DbType.String, reassignment.bas_objective_code),
            new ProcedureParameter("@Project", DbType.String, reassignment.bas_project_number),
            new ProcedureParameter("@Fund", DbType.String, reassignment.bas_fund_code),
            new ProcedureParameter(
                "@approver_code",
                DbType.Int32,
                reassignment.approver_code ?? 0
            ),
            new ProcedureParameter(
                "@site_driver_code",
                DbType.Int32,
                reassignment.site_driver_code ?? 0
            ),
            new ProcedureParameter(
                "@collector_firstname",
                DbType.String,
                reassignment.collector_firstname
            ),
            new ProcedureParameter(
                "@collector_surname",
                DbType.String,
                reassignment.collector_surname
            ),
            new ProcedureParameter("@collector_sa_id", DbType.String, reassignment.collector_sa_id),
            new ProcedureParameter(
                "@collector_passportnumber",
                DbType.String,
                reassignment.collector_passportnumber
            ),
            new ProcedureParameter(
                "@collector_office_number",
                DbType.String,
                reassignment.collector_office_number
            ),
            new ProcedureParameter(
                "@collector_cellphone_number",
                DbType.String,
                reassignment.collector_cellphone_number
            ),
            new ProcedureParameter(
                "@collector_office",
                DbType.String,
                reassignment.collector_office
            ),
            new ProcedureParameter(
                "@collector_designation",
                DbType.String,
                reassignment.collector_designation
            ),
            new ProcedureParameter(
                "@lease_contract_period",
                DbType.Byte,
                reassignment.lease_contract_period ?? 0
            ),
                    new ProcedureParameter(
                        "@contract_estimated_overall_km",
                        DbType.Int32,
                        reassignment.contract_estimated_overall_km ?? 0
                    )
                );

                // The procedure temporarily applies the manager's ID to both
                // rows. Ownership transfer is explicit here, so restore the
                // original contract's capturer and assign the selected new
                // owner to the replacement row.
                await RestoreLegacyCapturerAsync(existingContract.contract_code, existingContract.user_code);

                var contractsAfterReassignment = (await GetContractsByVehicleAsync(existingContract.vmf_code)).ToList();
                var linkedReplacement = contractsAfterReassignment
                    .Where(contract =>
                        contract.reassigned_from_contract_code == existingContract.contract_code
                        && contract.contract_code != existingContract.contract_code
                    )
                    .ToList();
                var newlyInsertedReplacement = contractsAfterReassignment
                    .Where(contract => !existingContractCodes.Contains(contract.contract_code))
                    .Where(contract =>
                        contract.site_code == reassignment.site_code
                        && contract.start_date.Date == reassignment.start_date.Date
                        && contract.start_odometer == reassignment.start_odometer
                        && string.Equals(contract.still_current, "Y", StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
                var candidates = linkedReplacement.Count == 1
                    ? linkedReplacement
                    : newlyInsertedReplacement;
                if (candidates.Count != 1)
                {
                    throw new InvalidOperationException(
                        "The legacy contract-reassignment procedure completed without exactly one readable replacement contract."
                    );
                }

                var reassigned = candidates[0];
                await RestoreLegacyCapturerAsync(reassigned.contract_code, reassignment.user_code);
                return await GetByIdAsync(reassigned.contract_code)
                    ?? throw new InvalidOperationException(
                        "The legacy contract-reassignment procedure completed, but the replacement contract could not be read back."
                    );
            }
        );
    }

    public Task DeleteAsync(int contractCode, int currentUserId) =>
        Task.FromException(
            new NotSupportedException(
                "Legacy contract records cannot be deleted. Use the authorised contract lifecycle actions instead."
            )
        );

    public async Task EndContractAsync(
        int contractCode,
        DateTime endDate,
        int currentUserId,
        int? endOdometer = null,
        string? notes = null
    )
    {
        var beforeClose = await GetByIdAsync(contractCode);
        if (beforeClose == null)
            throw new ArgumentException($"Contract {contractCode} not found");
        if (!string.Equals(beforeClose.still_current, "Y", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Contract {contractCode} is not currently active");

        // The archived provider closes contracts through
        // NEW_DEV_UPD_Contract_CLOSE. Prefer that procedure whenever the
        // restored database exposes the exact legacy contract. Only use the
        // trigger-backed direct update when the procedure genuinely does not
        // exist; a present-but-drifted procedure is a hard compatibility
        // failure rather than permission to guess at billing DML.
        bool hasLegacyCloseProcedure;
        try
        {
            hasLegacyCloseProcedure = await IsLegacyProcedureAvailableAsync(
                "NEW_DEV_UPD_Contract_CLOSE",
                "@contractcode",
                "@enddate",
                "@endodometer"
            );
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("does not match the archived parameter contract", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "The legacy contract-closure procedure is incompatible; no direct-DML close fallback was run.",
                exception
            );
        }

        if (!hasLegacyCloseProcedure && !await HasLegacyContractCloseTriggerAsync())
        {
            throw new NotSupportedException(
                "The legacy contract-closure workflow is unavailable; no direct-DML close fallback was run."
            );
        }

        var existingTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction is null;
        var closeTransaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;
        const string savepointName = "FIS_ContractClose";
        if (!ownsTransaction)
        {
            await existingTransaction!.CreateSavepointAsync(savepointName);
        }

        try
        {
            if (hasLegacyCloseProcedure)
            {
                await ExecuteLegacyProcedureAsync(
                    "NEW_DEV_UPD_Contract_CLOSE",
                    new ProcedureParameter("@contractcode", DbType.Int32, contractCode),
                    new ProcedureParameter("@enddate", DbType.DateTime, endDate.Date),
                    new ProcedureParameter("@endodometer", DbType.Int32, endOdometer ?? 0)
                );
            }
            else
            {
                var contract = beforeClose;
                contract.still_current = "N";
                contract.end_date = endDate.Date;
                contract.end_time = endDate;
                contract.contract_status_code = 7;
                contract.contract_status_date = endDate;
                if (endOdometer.HasValue)
                    contract.end_odometer = endOdometer.Value;
                if (!string.IsNullOrWhiteSpace(notes))
                    contract.Notes = notes;

                // The dedicated close procedure is absent, so use the
                // trigger-guarded compatibility write directly. Calling the
                // general NEW_DEV_UPD_Contract procedure here would silently
                // substitute a different mutation contract for closure.
                await UpdateDirectAsync(contract, currentUserId, beforeClose);
            }

            var afterClose = await GetByIdAsync(contractCode)
                ?? throw new InvalidOperationException(
                    $"The legacy contract-close workflow removed contract {contractCode}."
                );
            EnsureContractCloseBillingInvariant(beforeClose, afterClose, endDate, endOdometer);

            if (closeTransaction is not null)
                await closeTransaction.CommitAsync();
        }
        catch
        {
            if (closeTransaction is not null)
                await closeTransaction.RollbackAsync();
            else if (existingTransaction is not null)
                await existingTransaction.RollbackToSavepointAsync(savepointName);
            throw;
        }
        finally
        {
            if (closeTransaction is not null)
                await closeTransaction.DisposeAsync();
        }
    }

    private async Task<bool> HasLegacyContractCloseTriggerAsync()
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
                SELECT 1
                FROM [sys].[triggers] AS [tr]
                INNER JOIN [sys].[tables] AS [tb]
                    ON [tb].[object_id] = [tr].[parent_id]
                INNER JOIN [sys].[schemas] AS [sc]
                    ON [sc].[schema_id] = [tb].[schema_id]
                WHERE [sc].[name] = N'dbo'
                  AND [tb].[name] = N'contract'
                  AND [tr].[name] = N'TRG_UPD_ContractJournalDetailRecord'
                  AND [tr].[is_disabled] = 0;
                """;
            return await command.ExecuteScalarAsync() is not null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task EnsureLegacyContractTriggersAsync(IReadOnlyCollection<string> requiredTriggers)
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
                SELECT [tr].[name], [tr].[is_disabled]
                FROM [sys].[triggers] AS [tr]
                INNER JOIN [sys].[tables] AS [tb]
                    ON [tb].[object_id] = [tr].[parent_id]
                INNER JOIN [sys].[schemas] AS [sc]
                    ON [sc].[schema_id] = [tb].[schema_id]
                WHERE [sc].[name] = N'dbo'
                  AND [tb].[name] = N'contract'
                  AND [tr].[name] IN
                  (
                      N'TRG_INS_CheckDuplicateContract',
                      N'TRG_INS_ContractJournalDetailRecord',
                      N'TRG_UPD_ContractJournalDetailRecord'
                  );
                """;
            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.GetBoolean(1))
                    enabled.Add(reader.GetString(0));
            }

            var missing = requiredTriggers.Where(trigger => !enabled.Contains(trigger)).ToArray();
            if (missing.Length > 0)
            {
                throw new NotSupportedException(
                    $"The legacy contract-trigger workflow is unavailable ({string.Join(", ", missing)}); no direct-DML fallback was run."
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void EnsureContractCloseBillingInvariant(
        Contract before,
        Contract after,
        DateTime expectedEndDate,
        int? expectedEndOdometer
    )
    {
        var differences = new List<string>();
        if (!string.Equals(after.still_current, "N", StringComparison.OrdinalIgnoreCase))
            differences.Add("still_current was not changed to N");
        if (!SameDate(after.end_date, expectedEndDate))
            differences.Add("end_date was not persisted");
        if (expectedEndOdometer.HasValue && after.end_odometer != expectedEndOdometer)
            differences.Add("end_odometer was not persisted");
        if (!SameDate(after.Charged_Until, expectedEndDate))
            differences.Add("Charged_Until was not aligned to the close date");
        if (before.user_code != after.user_code)
            differences.Add("user_code/capturer was changed");

        if (differences.Count > 0)
        {
            throw new LegacyContractClosureBillingInvariantException(
                $"The legacy contract-close workflow did not preserve the billing boundary: {string.Join(", ", differences)}. The close was not accepted."
            );
        }
    }

    private async Task<(
        HashSet<string> Contract,
        HashSet<string> Vehicle,
        HashSet<string> Site
    )> GetProjectionColumnsAsync() =>
        (
            await GetAvailableColumnsAsync(ContractTableName, RequiredContractColumns),
            await GetAvailableColumnsAsync(VehicleTableName),
            await GetAvailableColumnsAsync(SiteTableName)
        );

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Identifiers are fixed or allowlisted runtime columns and values are parameters."
    )]
    private async Task<List<Contract>> QueryAsync(
        IReadOnlySet<string> contractColumns,
        IReadOnlySet<string> vehicleColumns,
        IReadOnlySet<string> siteColumns,
        string predicate,
        IReadOnlyCollection<QueryParameter> parameters,
        string orderBy,
        int? skip = null,
        int? take = null
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var projection = RequiredContractColumns
                .Select(column => $"[c].[{column}] AS [{column}]")
                .Concat(
                    OptionalContractColumns.Select(column =>
                        GetOptionalProjection(contractColumns, column, "c")
                    )
                )
                .Concat(
                    VehicleProjectionColumns.Select(column =>
                        GetOptionalProjection(vehicleColumns, column, "v", $"vehicle_{column}")
                    )
                )
                .Concat(
                    SiteProjectionColumns.Select(column =>
                        GetOptionalProjection(siteColumns, column, "s", $"site_{column}")
                    )
                )
                .ToArray();
            var paging =
                skip.HasValue && take.HasValue
                    ? $"OFFSET {skip.Value} ROWS FETCH NEXT {take.Value} ROWS ONLY"
                    : string.Empty;

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{ContractTableName}] AS [c]
                LEFT JOIN [dbo].[{VehicleTableName}] AS [v] ON [v].[vmf_code] = [c].[vmf_code]
                LEFT JOIN [dbo].[{SiteTableName}] AS [s] ON [s].[Site_code] = [c].[site_code]
                WHERE {predicate}
                ORDER BY {orderBy}
                {paging}
                """;
            AddParameters(command, parameters);

            var results = new List<Contract>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapContract(reader, contractColumns, vehicleColumns, siteColumns));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<int> ExecuteCountAsync(QueryFilter filter)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"SELECT COUNT(1) FROM [dbo].[{ContractTableName}] AS [c] WHERE {filter.Clause}";
            AddParameters(command, filter.Parameters);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"INSERT INTO [dbo].[{ContractTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[contract_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ExecuteUpdateAsync(int contractCode, IReadOnlyList<WriteValue> values)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"UPDATE [dbo].[{ContractTableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [contract_code] = @contractCode";
            AddParameters(command, values);
            AddParameter(command, "@contractCode", DbType.Int32, contractCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<bool> IsLegacyProcedureAvailableAsync(
        string procedureName,
        params string[] expectedParameters
    )
    {
        var actualParameters = await GetLegacyProcedureParametersAsync(procedureName);
        if (actualParameters is null)
            return false;

        if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match the archived parameter contract. No direct-DML fallback was run."
            );
        }

        return true;
    }

    private async Task<IReadOnlyList<string>?> GetLegacyProcedureParametersAsync(
        string procedureName
    )
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
                SELECT [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                LEFT JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
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

            return procedureFound ? actualParameters : null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<string?> ResolveLegacyUsernameAsync(int currentUserId)
    {
        if (currentUserId <= 0)
            return null;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT TOP (1)
                    COALESCE(
                        NULLIF(LTRIM(RTRIM([name])), ''),
                        NULLIF(LTRIM(RTRIM([E_Mail])), ''),
                        CONVERT(varchar(20), [user_access_code])
                    )
                FROM [dbo].[user_access_old1]
                WHERE [user_access_code] = @userAccessCode
                """;
            AddParameter(command, "@userAccessCode", DbType.Int32, currentUserId);
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ApplyBackdatingDecisionAsync(Contract contract, int currentUserId)
    {
        if (!HasBackdatingRequest(contract))
        {
            return;
        }

        const string procedureName = "DEV_UPD_Contract_BackDating_RequestedApproveDecline";
        var expectedParameters = new[]
        {
            "@ContractCode",
            "@Approved_Date",
            "@Approved_By_Username",
            "@Declined_Date",
            "@Declined_Username",
        };
        if (!await IsLegacyProcedureAvailableAsync(procedureName, expectedParameters))
        {
            throw new NotSupportedException(
                $"The legacy backdating decision procedure {procedureName} is unavailable; no approval decision was reported as successful."
            );
        }

        var actorUsername =
            contract.backdating_approved_by_username
            ?? contract.backdating_declined_by_username
            ?? await ResolveLegacyUsernameAsync(currentUserId);
        var isApproval = contract.contract_status_code is 2 or 3;
        // The legacy data-layer method invokes the backdating procedure after
        // every approval-phase decision. New decline metadata is supplied for
        // status 4; statuses 5/6 preserve any existing request audit values
        // rather than inventing a decline event the archived caller did not
        // record.
        var isDeclineForCorrection = contract.contract_status_code is 4;

        await ExecuteLegacyProcedureAsync(
            procedureName,
            new ProcedureParameter("@ContractCode", DbType.Int32, contract.contract_code),
            new ProcedureParameter(
                "@Approved_Date",
                DbType.DateTime,
                isApproval
                    ? contract.backdating_approved_date
                        ?? contract.backdating_requested_date
                        ?? DateTime.Now
                    : null
            ),
            new ProcedureParameter(
                "@Approved_By_Username",
                DbType.String,
                isApproval
                    ? contract.backdating_approved_by_username ?? actorUsername
                    : null
            ),
            new ProcedureParameter(
                "@Declined_Date",
                DbType.DateTime,
                isDeclineForCorrection
                    ? contract.backdating_declined_date ?? DateTime.Now
                    : contract.backdating_declined_date
            ),
            new ProcedureParameter(
                "@Declined_Username",
                DbType.String,
                isDeclineForCorrection
                    ? contract.backdating_declined_by_username ?? actorUsername
                    : contract.backdating_declined_by_username
            )
        );
    }

    private static bool HasBackdatingRequest(Contract contract) =>
        IsMeaningfulBackdatingDate(contract.backdating_start_date)
        || IsMeaningfulBackdatingDate(contract.backdating_requested_date);

    private static bool IsMeaningfulBackdatingDate(DateTime? value) =>
        value.HasValue && value.Value.Date > new DateTime(1900, 1, 1);

    private async Task<string?> ResolveReassignProcedureContractAsync()
    {
        const string procedureName = "DEV_UPD_Contract_ReassignExisting";
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                INNER JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
                WHERE [schemaObject].[name] = N'dbo'
                  AND [procedureObject].[name] = @procedureName
                  AND [parameterObject].[parameter_id] > 0
                ORDER BY [parameterObject].[parameter_id]
                """;
            AddParameter(command, "@procedureName", DbType.String, procedureName);

            var actual = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    actual.Add(reader.GetString(0));
            }

            if (actual.Count == 0)
            {
                await using var existsCommand = connection.CreateCommand();
                existsCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
                AddParameter(existsCommand, "@procedureName", DbType.String, $"dbo.{procedureName}");
                var objectId = await existsCommand.ExecuteScalarAsync();
                return objectId is null or DBNull ? null : throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} exposes no parameters. No direct-DML fallback was run."
                );
            }

            var currentContract = new[]
            {
                "@ExistingContractCode", "@VMFCode", "@SiteCode", "@StartDate", "@StartTime",
                "@StartOdometer", "@DriverSAID", "@DriverName", "@Authorisation", "@Notes", "@UserID",
                "@Responsibility", "@Objective", "@Project", "@Fund", "@approver_code", "@site_driver_code",
                "@collector_firstname", "@collector_surname", "@collector_sa_id", "@collector_passportnumber",
                "@collector_office_number", "@collector_cellphone_number", "@collector_office",
                "@collector_designation", "@lease_contract_period", "@contract_estimated_overall_km",
            };
            var archivedContract = currentContract.ToArray();
            archivedContract[0] = "@ContractCode";

            if (actual.SequenceEqual(currentContract, StringComparer.OrdinalIgnoreCase))
                return "@ExistingContractCode";
            if (actual.SequenceEqual(archivedContract, StringComparer.OrdinalIgnoreCase))
                return "@ContractCode";

            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match either archived parameter contract. No direct-DML fallback was run."
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ExecuteLegacyProcedureAsync(
        string procedureName,
        params ProcedureParameter[] parameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            command.CommandTimeout = 0;
            foreach (var parameter in parameters)
                AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<T> ExecuteInLegacyTransactionAsync<T>(
        string savepointName,
        Func<Task<T>> operation
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savepointName);
        ArgumentNullException.ThrowIfNull(operation);

        var existingTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;

        if (!ownsTransaction)
        {
            await existingTransaction!.CreateSavepointAsync(savepointName);
        }

        try
        {
            var result = await operation();
            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            else if (existingTransaction is not null)
            {
                await existingTransaction.RollbackToSavepointAsync(savepointName);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Legacy procedures receive one @UserID value and use it both for the
    /// action audit and for the contract's capturer/user_code. The modern
    /// workflow keeps those identities separate: the approving or editing
    /// actor is audited by the API, while the original capturer remains the
    /// contract owner until an explicit reassignment. Restore only the
    /// owner column after the procedure has completed its own transaction.
    /// </summary>
    private async Task RestoreLegacyCapturerAsync(int contractCode, short? capturerCode)
    {
        if (contractCode <= 0)
            return;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            // The legacy procedure can receive the action actor as @UserID,
            // so restore ownership only on the row that was acted on. Do not
            // broaden this to contract_group_code/parent_contract_code: those
            // rows are historical or related contracts and may have different
            // original capturers. Ownership changes between rows are explicit
            // reassignment workflows, never an approval/extension side effect.
            command.CommandText = $"""
                UPDATE [dbo].[{ContractTableName}]
                SET [user_code] = @capturerCode
                WHERE [contract_code] = @contractCode
                """;
            AddParameter(
                command,
                "@capturerCode",
                DbType.Int16,
                capturerCode.HasValue ? capturerCode.Value : DBNull.Value
            );
            AddParameter(command, "@contractCode", DbType.Int32, contractCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string>? requiredColumns = null
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            if (requiredColumns is not null)
            {
                var missing = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
                if (missing.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"The required Contract compatibility columns are not available on {tableName}: {string.Join(", ", missing)}"
                    );
                }
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static QueryFilter BuildFilter(
        IReadOnlySet<string> availableColumns,
        ContractPageQuery query
    )
    {
        var clauses = new List<string> { GetNotDeletedFilter(availableColumns) };
        var parameters = new List<QueryParameter>();

        if (query.StatusCode.HasValue)
        {
            if (availableColumns.Contains("contract_status_code"))
            {
                clauses.Add("[c].[contract_status_code] = @statusCode");
                parameters.Add(
                    new QueryParameter("@statusCode", DbType.Int16, query.StatusCode.Value)
                );
            }
            else if (query.StatusCode == 3)
            {
                clauses.Add("[c].[still_current] = 'Y'");
            }
            else if (query.StatusCode is 5 or 6 or 7)
            {
                clauses.Add("[c].[still_current] = 'N'");
            }
        }

        if (query.SiteCode.HasValue)
        {
            clauses.Add("[c].[site_code] = @siteCode");
            parameters.Add(new QueryParameter("@siteCode", DbType.Int16, query.SiteCode.Value));
        }
        if (query.AllowedSiteCodes is { Count: > 0 })
        {
            var siteParameters = query
                .AllowedSiteCodes.Distinct()
                .Select((siteCode, index) => new QueryParameter($"@allowedSiteCode{index}", DbType.Int16, siteCode))
                .ToArray();
            clauses.Add($"[c].[site_code] IN ({string.Join(", ", siteParameters.Select(parameter => parameter.Name))})");
            parameters.AddRange(siteParameters);
        }
        else if (query.AllowedSiteCodes is not null)
        {
            clauses.Add("1 = 0");
        }
        if (query.OwnerUserCode is > 0)
        {
            clauses.Add(
                availableColumns.Contains("created_by_user_code")
                    ? "([c].[user_code] = @ownerUserCode OR ([c].[user_code] IS NULL AND [c].[created_by_user_code] = @ownerUserCode))"
                    : "[c].[user_code] = @ownerUserCode"
            );
            parameters.Add(new QueryParameter("@ownerUserCode", DbType.Int32, query.OwnerUserCode.Value));
        }
        if (!string.IsNullOrWhiteSpace(query.StillCurrent))
        {
            clauses.Add("[c].[still_current] = @stillCurrent");
            parameters.Add(
                new QueryParameter("@stillCurrent", DbType.String, query.StillCurrent.Trim())
            );
        }
        if (query.StartDateFrom.HasValue)
        {
            clauses.Add("[c].[start_date] >= @startDateFrom");
            parameters.Add(
                new QueryParameter(
                    "@startDateFrom",
                    DbType.DateTime2,
                    query.StartDateFrom.Value.Date
                )
            );
        }
        if (query.StartDateTo.HasValue)
        {
            clauses.Add("[c].[start_date] <= @startDateTo");
            parameters.Add(
                new QueryParameter("@startDateTo", DbType.DateTime2, query.StartDateTo.Value.Date)
            );
        }
        if (query.VmfCode.HasValue)
        {
            clauses.Add("[c].[vmf_code] = @vmfCode");
            parameters.Add(new QueryParameter("@vmfCode", DbType.Int32, query.VmfCode.Value));
        }

        return new QueryFilter(string.Join(" AND ", clauses), parameters);
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> availableColumns) =>
        availableColumns.Contains("is_deleted") ? "ISNULL([c].[is_deleted], 0) = 0" : "1 = 1";

    private static Contract MapContract(
        DbDataReader reader,
        IReadOnlySet<string> contractColumns,
        IReadOnlySet<string> vehicleColumns,
        IReadOnlySet<string> siteColumns
    )
    {
        var startDate = ReadDateTime(reader, "start_date") ?? DateTime.MinValue;
        var stillCurrent = ReadString(reader, "still_current");
        var statusCode =
            ReadInt16IfAvailable(reader, contractColumns, "contract_status_code")
            ?? (
                string.Equals(stillCurrent, "Y", StringComparison.OrdinalIgnoreCase)
                    ? (short)3
                    : (short)7
            );
        var captureDate = ReadDateTimeIfAvailable(reader, contractColumns, "capture_date");
        var dateCreated =
            ReadDateTimeIfAvailable(reader, contractColumns, "date_created")
            ?? captureDate
            ?? startDate;
        var createdBy =
            ReadInt32IfAvailable(reader, contractColumns, "created_by_user_code")
            ?? ReadInt16(reader, "user_code");

        var contract = new Contract
        {
            contract_code = ReadInt32(reader, "contract_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            site_code = ReadInt16(reader, "site_code") ?? 0,
            start_date = startDate,
            start_time = ReadDateTimeOrTime(reader, "start_time") ?? startDate,
            end_date = ReadDateTime(reader, "end_date"),
            end_time = ReadDateTimeOrTime(reader, "end_time"),
            start_odometer = ReadInt32(reader, "start_odometer") ?? 0,
            end_odometer = ReadInt32(reader, "end_odometer"),
            still_current = stillCurrent,
            contract_type = ReadString(reader, "contract_type"),
            Driver_id = ReadString(reader, "Driver_id"),
            Authorisation = ReadString(reader, "Authorisation"),
            Driver_name = ReadString(reader, "Driver_name"),
            Notes = ReadString(reader, "Notes"),
            target_return_date = ReadDateTime(reader, "target_return_date"),
            user_code = ReadInt16(reader, "user_code"),
            Charged_Until = ReadDateTime(reader, "Charged_Until"),
            bas_objective_code = ReadString(reader, "bas_objective_code"),
            bas_responsibility_code = ReadString(reader, "bas_responsibility_code"),
            relief_for_contract = ReadInt32(reader, "relief_for_contract"),
            locked_for_transfer = ReadBoolean(reader, "locked_for_transfer") ?? false,
            hours_used = ReadInt16(reader, "hours_used"),
            bas_project_number = ReadString(reader, "bas_project_number"),
            journal_detail_code = ReadGuid(reader, "journal_detail_code"),
            parent_contract_code = ReadInt32(reader, "parent_contract_code"),
            contract_group_code = ReadInt32(reader, "contract_group_code"),
            bas_fund_code = ReadString(reader, "bas_fund_code"),
            monthly_km = ReadInt32(reader, "monthly_km"),
            contract_status_code = statusCode,
            contract_status_date =
                ReadDateTimeIfAvailable(reader, contractColumns, "contract_status_date")
                ?? (statusCode == 7 ? ReadDateTime(reader, "end_date") : null),
            vehicle_assessment_code = ReadInt32IfAvailable(
                reader,
                contractColumns,
                "vehicle_assessment_code"
            ),
            approver_code = ReadInt32IfAvailable(reader, contractColumns, "approver_code"),
            site_driver_code = ReadInt32IfAvailable(reader, contractColumns, "site_driver_code"),
            collector_firstname = ReadStringIfAvailable(
                reader,
                contractColumns,
                "collector_firstname"
            ),
            collector_surname = ReadStringIfAvailable(reader, contractColumns, "collector_surname"),
            collector_sa_id = ReadStringIfAvailable(reader, contractColumns, "collector_sa_id"),
            collector_passportnumber = ReadStringIfAvailable(
                reader,
                contractColumns,
                "collector_passportnumber"
            ),
            collector_office_number = ReadStringIfAvailable(
                reader,
                contractColumns,
                "collector_office_number"
            ),
            collector_cellphone_number = ReadStringIfAvailable(
                reader,
                contractColumns,
                "collector_cellphone_number"
            ),
            collector_office = ReadStringIfAvailable(reader, contractColumns, "collector_office"),
            collector_designation = ReadStringIfAvailable(
                reader,
                contractColumns,
                "collector_designation"
            ),
            relief_vehicle_option = ReadBooleanIfAvailable(
                reader,
                contractColumns,
                "relief_vehicle_option"
            ),
            lease_contract_period = ReadByteIfAvailable(
                reader,
                contractColumns,
                "lease_contract_period"
            ),
            contract_estimated_overall_km = ReadInt32IfAvailable(
                reader,
                contractColumns,
                "contract_estimated_overall_km"
            ),
            intended_start_date = ReadDateTimeIfAvailable(
                reader,
                contractColumns,
                "intended_start_date"
            ),
            intended_start_time = ReadTimeIfAvailable(
                reader,
                contractColumns,
                "intended_start_time"
            ),
            capture_date = captureDate,
            modified_date = ReadDateTimeIfAvailable(reader, contractColumns, "modified_date"),
            reassigned_from_contract_code = ReadInt32IfAvailable(
                reader,
                contractColumns,
                "reassigned_from_contract_code"
            ),
            backdating_start_date = ReadDateTimeIfAvailable(
                reader,
                contractColumns,
                "backdating_start_date"
            ),
            backdating_requested_date = ReadDateTimeIfAvailable(
                reader,
                contractColumns,
                "backdating_requested_date"
            ),
            backdating_requested_by_username = ReadStringIfAvailable(
                reader,
                contractColumns,
                "backdating_requested_by_username"
            ),
            backdating_approved_date = ReadDateTimeIfAvailable(
                reader,
                contractColumns,
                "backdating_approved_date"
            ),
            backdating_approved_by_username = ReadStringIfAvailable(
                reader,
                contractColumns,
                "backdating_approved_by_Username"
            ),
            backdating_declined_date = ReadDateTimeIfAvailable(
                reader,
                contractColumns,
                "backdating_declined_date"
            ),
            backdating_declined_by_username = ReadStringIfAvailable(
                reader,
                contractColumns,
                "backdating_declined_by_Username"
            ),
            date_created = dateCreated,
            date_updated = ReadDateTimeIfAvailable(reader, contractColumns, "date_updated"),
            created_by_user_code = createdBy,
            modified_by_user_code = ReadInt32IfAvailable(
                reader,
                contractColumns,
                "modified_by_user_code"
            ),
            is_deleted = ReadBooleanIfAvailable(reader, contractColumns, "is_deleted") ?? false,
        };

        if (
            HasOrdinal(reader, "vehicle_fleet_number")
            || HasOrdinal(reader, "vehicle_registration_number")
        )
        {
            contract.Vehicle = new Vehicle
            {
                vmf_code = contract.vmf_code,
                fleet_number = ReadStringIfAvailable(
                    reader,
                    vehicleColumns,
                    "vehicle_fleet_number"
                ),
                registration_number = ReadStringIfAvailable(
                    reader,
                    vehicleColumns,
                    "vehicle_registration_number"
                ),
                model_code =
                    ReadInt16IfAvailable(reader, vehicleColumns, "vehicle_model_code") ?? 0,
                current_odo =
                    ReadInt32IfAvailable(reader, vehicleColumns, "vehicle_current_odo") ?? 0,
                vehicle_status_code =
                    ReadInt16IfAvailable(reader, vehicleColumns, "vehicle_vehicle_status_code")
                    ?? 0,
                location_code =
                    ReadInt16IfAvailable(reader, vehicleColumns, "vehicle_location_code") ?? 0,
                year_manufactured = ReadInt16IfAvailable(
                    reader,
                    vehicleColumns,
                    "vehicle_year_manufactured"
                ),
                chassis_number = ReadStringIfAvailable(
                    reader,
                    vehicleColumns,
                    "vehicle_chassis_number"
                ),
                engine_number_1 = ReadStringIfAvailable(
                    reader,
                    vehicleColumns,
                    "vehicle_engine_number_1"
                ),
                purchased_from = ReadStringIfAvailable(
                    reader,
                    vehicleColumns,
                    "vehicle_purchased_from"
                ),
                colour = ReadStringIfAvailable(reader, vehicleColumns, "vehicle_colour"),
            };
        }

        if (HasOrdinal(reader, "site_description"))
        {
            contract.Site = new Site
            {
                Site_code = contract.site_code,
                description = ReadStringIfAvailable(reader, siteColumns, "site_description"),
                Depatrment_code = ReadInt16IfAvailable(reader, siteColumns, "site_Depatrment_code"),
                site_active =
                    ReadBooleanIfAvailable(reader, siteColumns, "site_site_active") ?? true,
                res_person = ReadStringIfAvailable(reader, siteColumns, "site_res_person"),
                net_address = ReadStringIfAvailable(reader, siteColumns, "site_net_address"),
                telephone = ReadStringIfAvailable(reader, siteColumns, "site_telephone"),
            };
        }

        return contract;
    }

    private static List<WriteValue> BuildWriteValues(Contract contract) =>
        [
            new("vmf_code", "@vmfCode", DbType.Int32, contract.vmf_code),
            new("site_code", "@siteCode", DbType.Int16, contract.site_code),
            new("start_date", "@startDate", DbType.DateTime2, contract.start_date.Date),
            new("start_time", "@startTime", DbType.Time, contract.start_time.TimeOfDay),
            new("end_date", "@endDate", DbType.DateTime2, contract.end_date?.Date),
            new("end_time", "@endTime", DbType.Time, contract.end_time?.TimeOfDay),
            new("start_odometer", "@startOdometer", DbType.Int32, contract.start_odometer),
            new("end_odometer", "@endOdometer", DbType.Int32, contract.end_odometer),
            new("still_current", "@stillCurrent", DbType.String, contract.still_current ?? "N"),
            // The client-era contract table requires a real legacy type. A
            // missing value must not be filled with modern demo type H because
            // that can change billing semantics or violate the FK on the
            // restored database.
            new(
                "contract_type",
                "@contractType",
                DbType.String,
                contract.contract_type
                    ?? throw new InvalidOperationException(
                        "A legacy contract type is required for direct contract persistence."
                    )
            ),
            new("Driver_id", "@driverId", DbType.String, contract.Driver_id),
            new("Authorisation", "@authorisation", DbType.String, contract.Authorisation),
            new("Driver_name", "@driverName", DbType.String, contract.Driver_name),
            new("Notes", "@notes", DbType.String, contract.Notes),
            new(
                "target_return_date",
                "@targetReturnDate",
                DbType.DateTime2,
                contract.target_return_date?.Date
            ),
            new("user_code", "@userCode", DbType.Int16, contract.user_code),
            new("Charged_Until", "@chargedUntil", DbType.DateTime2, contract.Charged_Until),
            new(
                "bas_objective_code",
                "@basObjectiveCode",
                DbType.String,
                contract.bas_objective_code
            ),
            new(
                "bas_responsibility_code",
                "@basResponsibilityCode",
                DbType.String,
                contract.bas_responsibility_code
            ),
            new(
                "relief_for_contract",
                "@reliefForContract",
                DbType.Int32,
                contract.relief_for_contract
            ),
            new(
                "locked_for_transfer",
                "@lockedForTransfer",
                DbType.Boolean,
                contract.locked_for_transfer
            ),
            new("hours_used", "@hoursUsed", DbType.Int16, contract.hours_used),
            new(
                "bas_project_number",
                "@basProjectNumber",
                DbType.String,
                contract.bas_project_number
            ),
            new(
                "journal_detail_code",
                "@journalDetailCode",
                DbType.Guid,
                contract.journal_detail_code
            ),
            new(
                "parent_contract_code",
                "@parentContractCode",
                DbType.Int32,
                contract.parent_contract_code
            ),
            new(
                "contract_group_code",
                "@contractGroupCode",
                DbType.Int32,
                contract.contract_group_code
            ),
            new("bas_fund_code", "@basFundCode", DbType.String, contract.bas_fund_code),
            new("monthly_km", "@monthlyKm", DbType.Int32, contract.monthly_km),
            new(
                "contract_status_code",
                "@contractStatusCode",
                DbType.Int16,
                contract.contract_status_code
            ),
            new(
                "contract_status_date",
                "@contractStatusDate",
                DbType.DateTime2,
                contract.contract_status_date
            ),
            new(
                "vehicle_assessment_code",
                "@vehicleAssessmentCode",
                DbType.Int32,
                contract.vehicle_assessment_code
            ),
            new("approver_code", "@approverCode", DbType.Int32, contract.approver_code),
            new("site_driver_code", "@siteDriverCode", DbType.Int32, contract.site_driver_code),
            new(
                "collector_firstname",
                "@collectorFirstname",
                DbType.String,
                contract.collector_firstname
            ),
            new(
                "collector_surname",
                "@collectorSurname",
                DbType.String,
                contract.collector_surname
            ),
            new("collector_sa_id", "@collectorSaId", DbType.String, contract.collector_sa_id),
            new(
                "collector_passportnumber",
                "@collectorPassport",
                DbType.String,
                contract.collector_passportnumber
            ),
            new(
                "collector_office_number",
                "@collectorOfficeNumber",
                DbType.String,
                contract.collector_office_number
            ),
            new(
                "collector_cellphone_number",
                "@collectorCellphone",
                DbType.String,
                contract.collector_cellphone_number
            ),
            new("collector_office", "@collectorOffice", DbType.String, contract.collector_office),
            new(
                "collector_designation",
                "@collectorDesignation",
                DbType.String,
                contract.collector_designation
            ),
            new(
                "relief_vehicle_option",
                "@reliefVehicleOption",
                DbType.Boolean,
                contract.relief_vehicle_option
            ),
            new(
                "lease_contract_period",
                "@leaseContractPeriod",
                DbType.Byte,
                contract.lease_contract_period
            ),
            new(
                "contract_estimated_overall_km",
                "@contractEstimatedOverallKm",
                DbType.Int32,
                contract.contract_estimated_overall_km
            ),
            new(
                "intended_start_date",
                "@intendedStartDate",
                DbType.DateTime2,
                contract.intended_start_date?.Date
            ),
            new(
                "intended_start_time",
                "@intendedStartTime",
                DbType.Time,
                contract.intended_start_time
            ),
            new("capture_date", "@captureDate", DbType.DateTime2, contract.capture_date),
            new("modified_date", "@modifiedDate", DbType.DateTime2, contract.modified_date),
            new(
                "reassigned_from_contract_code",
                "@reassignedFromContractCode",
                DbType.Int32,
                contract.reassigned_from_contract_code
            ),
        ];

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (availableColumns.Contains(column))
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static string GetOptionalProjection(
        IReadOnlySet<string> columns,
        string column,
        string alias,
        string? outputColumn = null
    )
    {
        var output = outputColumn ?? column;
        return columns.Contains(column)
            ? $"[{alias}].[{column}] AS [{output}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{output}]";
    }

    private static string GetSqlType(string column) =>
        column switch
        {
            "contract_status_code" => "smallint",
            "vehicle_assessment_code"
            or "approver_code"
            or "site_driver_code"
            or "reassigned_from_contract_code" => "int",
            "relief_vehicle_option" or "is_deleted" or "site_active" => "bit",
            "lease_contract_period" => "tinyint",
            "intended_start_time" => "time",
            "contract_status_date"
            or "capture_date"
            or "modified_date"
            or "date_created"
            or "date_updated" => "datetime2",
            "journal_detail_code" => "uniqueidentifier",
            _ when column.EndsWith("_code", StringComparison.OrdinalIgnoreCase) => "smallint",
            _ => "varchar(1)",
        };

    private static void AddParameters(DbCommand command, IEnumerable<QueryParameter> parameters)
    {
        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
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
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.Trim();
    }

    private static string? ReadStringIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string outputColumn
    ) => HasOrdinal(reader, outputColumn) ? ReadString(reader, outputColumn) : null;

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeOrTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value is TimeSpan time ? DateTime.Today.Add(time) : Convert.ToDateTime(value);
    }

    private static TimeSpan? ReadTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadTime(reader, column) : null;

    private static TimeSpan? ReadTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value is TimeSpan time ? time : Convert.ToDateTime(value).TimeOfDay;
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadDateTime(reader, column) : null;

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => HasOrdinal(reader, column) ? ReadInt32(reader, column) : null;

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static short? ReadInt16IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => HasOrdinal(reader, column) ? ReadInt16(reader, column) : null;

    private static byte? ReadByteIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (!columns.Contains(column))
            return null;
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToByte(reader.GetValue(ordinal));
    }

    private static Guid? ReadGuid(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value is Guid guid ? guid : Guid.Parse(Convert.ToString(value)!);
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => HasOrdinal(reader, column) ? ReadBoolean(reader, column) : null;

    private static bool HasOrdinal(DbDataReader reader, string column)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private sealed record QueryParameter(string Name, DbType Type, object? Value);

    private sealed record QueryFilter(string Clause, IReadOnlyList<QueryParameter> Parameters);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed record ProcedureParameter(string Name, DbType Type, object? Value);
}

/// <summary>
/// Raised when an extension procedure or its compatibility fallback changes
/// fields that define billing/ownership. The caller must not report success for
/// an extension that also closed or recreated the contract unexpectedly.
/// </summary>
public sealed class LegacyContractExtensionBillingInvariantException : InvalidOperationException
{
    public LegacyContractExtensionBillingInvariantException(string message)
        : base(message) { }
}

/// <summary>
/// Raised when the legacy contract-close trigger does not leave the closed
/// row and its billing cursor in a consistent state. The caller must not
/// report success for a close that could leave revenue under- or over-billed.
/// </summary>
public sealed class LegacyContractClosureBillingInvariantException : InvalidOperationException
{
    public LegacyContractClosureBillingInvariantException(string message)
        : base(message) { }
}
