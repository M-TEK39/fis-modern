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
        string searchTerm
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
            };
            command.CommandText =
                $"SELECT TOP (50) {string.Join(", ", projection)} FROM [dbo].[{VehicleTableName}] AS [v] WHERE {predicate} ORDER BY {orderBy}";
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
                        ReadInt16IfAvailable(reader, availableColumns, "vehicle_status_code")
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

    public async Task<ContractVehicleLookup?> GetVehicleForContractAsync(int vmfCode)
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
            };
            command.CommandText =
                $"SELECT TOP (1) {string.Join(", ", projection)} FROM [dbo].[{VehicleTableName}] AS [v] WHERE [v].[vmf_code] = @vmfCode";
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
                ReadInt16IfAvailable(reader, availableColumns, "vehicle_status_code")
            );
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
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

    public async Task<Contract> CreateAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);

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

        var existing = await GetByIdAsync(contract.contract_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Contract with contract_code {contract.contract_code} not found"
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

    public async Task<Contract> ExtendExistingAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.contract_code <= 0)
            throw new ArgumentException("A valid contract code is required.", nameof(contract));
        if (!contract.target_return_date.HasValue)
            throw new ArgumentException("A target return date is required to extend a contract.", nameof(contract));

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_Contract_ExtendExisting",
                "@ContractCode",
                "@VMFCode",
                "@TargetReturnDate",
                "@contract_estimated_overall_km",
                "@Notes",
                "@UserID"
            )
        )
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
                new ProcedureParameter("@UserID", DbType.Int32, currentUserId)
            );
            return await GetByIdAsync(contract.contract_code)
                ?? throw new InvalidOperationException(
                    "The legacy contract-extension procedure removed the selected contract."
                );
        }

        // Compatibility fallback only where the original extension procedure is genuinely absent.
        await UpdateAsync(contract, currentUserId);
        return contract;
    }

    public async Task<Contract> UpdatePendingDecisionAsync(Contract contract, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(contract);
        if (contract.contract_code <= 0)
            throw new ArgumentException("A valid contract code is required.", nameof(contract));
        if (!contract.contract_status_code.HasValue)
            throw new ArgumentException("A contract status is required.", nameof(contract));

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
            return await GetByIdAsync(contract.contract_code)
                ?? throw new InvalidOperationException(
                    "The legacy contract decision procedure removed the selected contract."
                );
        }

        // Compatibility fallback only when the original procedure is genuinely absent.
        await UpdateAsync(contract, currentUserId);
        return contract;
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
                new ProcedureParameter("@UserID", DbType.Int32, currentUserId)
            );
            return await GetByIdAsync(pendingContract.contract_code)
                ?? throw new InvalidOperationException(
                    "The legacy contract activation procedure removed the selected contract."
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

        if (
            !await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_Contract_ReassignExisting",
                "@ExistingContractCode",
                "@VMFCode",
                "@SiteCode",
                "@StartDate",
                "@StartTime",
                "@StartOdometer",
                "@DriverSAID",
                "@DriverName",
                "@Authorisation",
                "@Notes",
                "@UserID",
                "@Responsibility",
                "@Objective",
                "@Project",
                "@Fund",
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
                "@lease_contract_period",
                "@contract_estimated_overall_km"
            )
        )
        {
            throw new NotSupportedException(
                "The legacy contract-reassignment procedure is unavailable; reassignment cannot be approximated."
            );
        }

        await ExecuteLegacyProcedureAsync(
            "DEV_UPD_Contract_ReassignExisting",
            new ProcedureParameter("@ExistingContractCode", DbType.Int32, existingContract.contract_code),
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

        var reassigned = (await GetContractsByVehicleAsync(existingContract.vmf_code))
            .Where(contract =>
                contract.reassigned_from_contract_code == existingContract.contract_code
                && contract.site_code == reassignment.site_code
                && contract.start_date.Date == reassignment.start_date.Date
            )
            .OrderByDescending(contract => contract.contract_code)
            .FirstOrDefault();
        return reassigned
            ?? throw new InvalidOperationException(
                "The legacy contract-reassignment procedure completed without a readable replacement contract."
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
        var contract = await GetByIdAsync(contractCode);
        if (contract == null)
            throw new ArgumentException($"Contract {contractCode} not found");
        if (contract.still_current != "Y")
            throw new InvalidOperationException($"Contract {contractCode} is not currently active");

        contract.still_current = "N";
        contract.end_date = endDate.Date;
        contract.end_time = endDate;
        contract.contract_status_code = 7;
        contract.contract_status_date = endDate;
        if (endOdometer.HasValue)
            contract.end_odometer = endOdometer.Value;
        if (!string.IsNullOrWhiteSpace(notes))
            contract.Notes = notes;
        await UpdateAsync(contract, currentUserId);
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

            if (!procedureFound)
                return false;

            if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} does not match the archived parameter contract. No direct-DML fallback was run."
                );
            }

            return true;
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
            new("contract_type", "@contractType", DbType.String, contract.contract_type ?? "H"),
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
