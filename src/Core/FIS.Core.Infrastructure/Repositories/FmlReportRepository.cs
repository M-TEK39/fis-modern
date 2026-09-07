using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads the FML reports without projecting EF entities whose optional modern
/// columns may not exist in the client's original database. The legacy report
/// procedures remain the source of truth when installed; guarded queries are
/// used when the database has only the tables/views needed by the report.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all user values are parameters.")]
public sealed class FmlReportRepository : IFmlReportRepository
{
    private const string VehicleTable = "vehicle_master";
    private const string VehicleSourceTable = "vehicle_source";
    private const string VehicleStatusTable = "vehicle_status";
    private const string VehicleTypeTable = "type";
    private const string ModelTable = "model";
    private const string ClassTable = "class";
    private const string SiteTable = "site";
    private const string LocationTable = "location";
    private const string ContractTable = "contract";
    private const string ContractTypeTable = "Contract_type";
    private const string LeaseTariffTable = "LeaseTariff";
    private const string LeaseTermsTable = "LeaseContractTerms";
    private const string VehicleKilosTable = "VehicleKilos";
    private const string MaintenanceTable = "maintenance_records";
    private const string WesbankView = "AllWesbank";

    private static readonly string[] RequiredVehicleColumns =
    [
        "vmf_code",
        "fleet_number",
        "registration_number",
        "model_code",
        "type_code",
        "vehicle_status_code",
        "vs_code",
        "year_manufactured",
        "purchase_amount"
    ];

    private readonly FisDbContext _context;
    private readonly ILogger<FmlReportRepository> _logger;

    public FmlReportRepository(FisDbContext context, ILogger<FmlReportRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<FmlMaintenanceHistoryReport> GetMaintenanceHistoryAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? financialYear,
        string? vehicleNumber)
        => WithConnectionAsync((connection, transaction) => QueryMaintenanceHistoryAsync(
            connection,
            transaction,
            startDate,
            endDate,
            financialYear,
            vehicleNumber));

    public Task<FmlContractsReport> GetContractsExpiringAsync()
        => WithConnectionAsync((connection, transaction) => QueryContractsAsync(
            connection,
            transaction,
            expired: false));

    public Task<FmlContractsReport> GetExpiredOpenContractsAsync()
        => WithConnectionAsync((connection, transaction) => QueryContractsAsync(
            connection,
            transaction,
            expired: true));

    public Task<FmlVehiclesNoContractsReport> GetVehiclesNoContractsAsync()
        => WithConnectionAsync(QueryVehiclesNoContractsAsync);

    public Task<FmlOverUtilizedReport> GetOverUtilizedAsync(DateTime? startDate, DateTime? endDate)
        => WithConnectionAsync((connection, transaction) => QueryOverUtilizedAsync(
            connection,
            transaction,
            startDate,
            endDate));

    private async Task<FmlMaintenanceHistoryReport> QueryMaintenanceHistoryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        DateTime? startDate,
        DateTime? endDate,
        int? financialYear,
        string? vehicleNumber)
    {
        var range = ResolveDateRange(startDate, endDate, financialYear);
        var search = string.IsNullOrWhiteSpace(vehicleNumber) ? null : vehicleNumber.Trim();
        var storedProcedureRows = await TryExecuteStoredProcedureAsync(
            connection,
            transaction,
            "DEV_REP_FMLVehicleMaintenanceHistory",
            command =>
            {
                AddParameter(command, "@start_date", DbType.DateTime, range.Start);
                AddParameter(command, "@end_date", DbType.DateTime, range.End);
                AddParameter(command, "@ggnum", DbType.String, search ?? string.Empty);
                AddParameter(command, "@fin_year", DbType.Int32, financialYear.GetValueOrDefault());
            });

        if (storedProcedureRows is not null)
        {
            var records = storedProcedureRows
                .Select(MapMaintenanceRecord)
                .Where(record => record is not null)
                .Cast<FmlMaintenanceHistoryRecord>()
                .ToList();
            return new FmlMaintenanceHistoryReport(records, records.Sum(record => record.TotalCostOverDateRange ?? 0m));
        }

        var viewColumns = await GetColumnsAsync(connection, WesbankView, transaction);
        var vehicleColumns = await GetColumnsAsync(connection, VehicleTable, transaction);
        var sourceColumns = await GetColumnsAsync(connection, VehicleSourceTable, transaction);
        var statusColumns = await GetColumnsAsync(connection, VehicleStatusTable, transaction);
        var modelColumns = await GetColumnsAsync(connection, ModelTable, transaction);

        if (HasColumns(viewColumns, "vmf_code", "sourcedate", "debit_amount", "cost_category_code") &&
            HasColumns(vehicleColumns, RequiredVehicleColumns) &&
            HasColumns(sourceColumns, "vs_code", "name") &&
            HasColumns(statusColumns, "vehicle_status_code", "status_description") &&
            HasColumns(modelColumns, "model_code", "model_description"))
        {
            return await QueryWesbankMaintenanceAsync(
                connection,
                transaction,
                range,
                search,
                viewColumns,
                vehicleColumns,
                sourceColumns,
                statusColumns,
                modelColumns);
        }

        return await QueryModernMaintenanceAsync(
            connection,
            transaction,
            range,
            search,
            vehicleColumns,
            sourceColumns,
            statusColumns,
            modelColumns,
            await GetColumnsAsync(connection, MaintenanceTable, transaction));
    }

    private async Task<FmlMaintenanceHistoryReport> QueryWesbankMaintenanceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        DateRange range,
        string? search,
        IReadOnlySet<string> viewColumns,
        IReadOnlySet<string> vehicleColumns,
        IReadOnlySet<string> sourceColumns,
        IReadOnlySet<string> statusColumns,
        IReadOnlySet<string> modelColumns)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        AddParameter(command, "@from", DbType.DateTime2, range.Start);
        AddParameter(command, "@to", DbType.DateTime2, range.End.Date.AddDays(1));
        AddParameter(command, "@search", DbType.String, search);

        var statusDate = GetDateExpression("v", vehicleColumns, "vehicle_status_date", "date_updated", "date_created", "take_on_date");
        var categoryDescription = GetTextExpression("w", viewColumns, "Cost Category description", "cost_category_description");
        var conditions = new List<string>
        {
            "[w].[cost_category_code] IN (3, 4, 5, 6)",
            "[w].[sourcedate] >= @from",
            "[w].[sourcedate] < @to",
            "[v].[vs_code] IN (2, 3)",
            "[v].[type_code] = 4",
            "[v].[vehicle_status_code] IN (1, 2, 4, 5, 10)",
            GetNotDeletedFilter("v", vehicleColumns),
            GetNotDeletedFilter("w", viewColumns),
            "(@search IS NULL OR [v].[fleet_number] = @search OR [v].[registration_number] = @search)"
        };

        command.CommandText = $"""
            SELECT [v].[fleet_number] AS [gg_number],
                   [v].[year_manufactured] AS [year_manufactured],
                   [m].[model_description] AS [model_description],
                   [s].[status_description] AS [current_status],
                   {statusDate} AS [current_status_date],
                   [vs].[name] AS [hired_from],
                   {categoryDescription} AS [maintenance_expense_type],
                   SUM(CAST([w].[debit_amount] AS decimal(19, 4))) AS [total_cost]
            FROM [dbo].[{WesbankView}] AS [w]
            INNER JOIN [dbo].[{VehicleTable}] AS [v] ON [w].[vmf_code] = [v].[vmf_code]
            INNER JOIN [dbo].[{VehicleSourceTable}] AS [vs] ON [v].[vs_code] = [vs].[vs_code]
            INNER JOIN [dbo].[{VehicleStatusTable}] AS [s] ON [v].[vehicle_status_code] = [s].[vehicle_status_code]
            INNER JOIN [dbo].[{ModelTable}] AS [m] ON [v].[model_code] = [m].[model_code]
            WHERE {string.Join(" AND ", conditions)}
            GROUP BY [v].[fleet_number], [v].[year_manufactured], [m].[model_description],
                     [s].[status_description], {statusDate}, [vs].[name], {categoryDescription}
            ORDER BY [v].[fleet_number]
            """;

        var records = new List<FmlMaintenanceHistoryRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new FmlMaintenanceHistoryRecord(
                ReadString(reader, "gg_number"),
                ReadInt16(reader, "year_manufactured"),
                ReadString(reader, "model_description"),
                ReadString(reader, "current_status"),
                ReadDateTime(reader, "current_status_date"),
                ReadString(reader, "hired_from"),
                ReadString(reader, "maintenance_expense_type"),
                ReadDecimal(reader, "total_cost")));
        }

        return new FmlMaintenanceHistoryReport(records, records.Sum(record => record.TotalCostOverDateRange ?? 0m));
    }

    private async Task<FmlMaintenanceHistoryReport> QueryModernMaintenanceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        DateRange range,
        string? search,
        IReadOnlySet<string> vehicleColumns,
        IReadOnlySet<string> sourceColumns,
        IReadOnlySet<string> statusColumns,
        IReadOnlySet<string> modelColumns,
        IReadOnlySet<string> maintenanceColumns)
    {
        if (!HasColumns(maintenanceColumns, "vmf_code", "maintenance_date", "maintenance_type", "total_cost") ||
            !HasColumns(vehicleColumns, RequiredVehicleColumns) ||
            !HasColumns(sourceColumns, "vs_code", "name") ||
            !HasColumns(statusColumns, "vehicle_status_code", "status_description") ||
            !HasColumns(modelColumns, "model_code", "model_description"))
        {
            return new FmlMaintenanceHistoryReport([], 0m);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        AddParameter(command, "@from", DbType.DateTime2, range.Start);
        AddParameter(command, "@to", DbType.DateTime2, range.End.Date.AddDays(1));
        AddParameter(command, "@search", DbType.String, search);

        var statusDate = GetDateExpression("v", vehicleColumns, "vehicle_status_date", "date_updated", "date_created", "take_on_date");
        var conditions = new List<string>
        {
            "[r].[maintenance_date] >= @from",
            "[r].[maintenance_date] < @to",
            "[v].[vs_code] IN (2, 3)",
            "[v].[type_code] = 4",
            "[v].[vehicle_status_code] IN (1, 2, 4, 5, 10)",
            GetNotDeletedFilter("r", maintenanceColumns),
            GetNotDeletedFilter("v", vehicleColumns),
            "(@search IS NULL OR [v].[fleet_number] = @search OR [v].[registration_number] = @search)"
        };

        command.CommandText = $"""
            SELECT [v].[fleet_number] AS [gg_number],
                   [v].[year_manufactured] AS [year_manufactured],
                   [m].[model_description] AS [model_description],
                   [s].[status_description] AS [current_status],
                   {statusDate} AS [current_status_date],
                   [vs].[name] AS [hired_from],
                   [r].[maintenance_type] AS [maintenance_expense_type],
                   SUM(CAST([r].[total_cost] AS decimal(19, 4))) AS [total_cost]
            FROM [dbo].[{MaintenanceTable}] AS [r]
            INNER JOIN [dbo].[{VehicleTable}] AS [v] ON [r].[vmf_code] = [v].[vmf_code]
            INNER JOIN [dbo].[{VehicleSourceTable}] AS [vs] ON [v].[vs_code] = [vs].[vs_code]
            INNER JOIN [dbo].[{VehicleStatusTable}] AS [s] ON [v].[vehicle_status_code] = [s].[vehicle_status_code]
            INNER JOIN [dbo].[{ModelTable}] AS [m] ON [v].[model_code] = [m].[model_code]
            WHERE {string.Join(" AND ", conditions)}
            GROUP BY [v].[fleet_number], [v].[year_manufactured], [m].[model_description],
                     [s].[status_description], {statusDate}, [vs].[name], [r].[maintenance_type]
            ORDER BY [v].[fleet_number]
            """;

        var records = new List<FmlMaintenanceHistoryRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new FmlMaintenanceHistoryRecord(
                ReadString(reader, "gg_number"),
                ReadInt16(reader, "year_manufactured"),
                ReadString(reader, "model_description"),
                ReadString(reader, "current_status"),
                ReadDateTime(reader, "current_status_date"),
                ReadString(reader, "hired_from"),
                ReadString(reader, "maintenance_expense_type"),
                ReadDecimal(reader, "total_cost")));
        }

        return new FmlMaintenanceHistoryReport(records, records.Sum(record => record.TotalCostOverDateRange ?? 0m));
    }

    private async Task<FmlContractsReport> QueryContractsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        bool expired)
    {
        var procedure = expired
            ? "DEV_REP_ExpiredFMLContractsStillOpenContractsWithClients"
            : "DEV_REP_FMLContractsExpireInThreeMonths";
        var storedProcedureRows = await TryExecuteStoredProcedureAsync(connection, transaction, procedure, null);
        if (storedProcedureRows is not null)
        {
            var records = storedProcedureRows
                .Select(MapContractRecord)
                .Where(record => record is not null)
                .Cast<FmlContractRecord>()
                .Select((record, index) => record with { RowNumber = record.RowNumber ?? index + 1 })
                .ToList();
            return new FmlContractsReport(records);
        }

        return new FmlContractsReport(await QueryContractFallbackAsync(connection, transaction, expired));
    }

    private async Task<IReadOnlyList<FmlContractRecord>> QueryContractFallbackAsync(
        DbConnection connection,
        DbTransaction? transaction,
        bool expired)
    {
        var vehicleColumns = await GetColumnsAsync(connection, VehicleTable, transaction);
        var contractColumns = await GetColumnsAsync(connection, ContractTable, transaction);
        var sourceColumns = await GetColumnsAsync(connection, VehicleSourceTable, transaction);
        var typeColumns = await GetColumnsAsync(connection, VehicleTypeTable, transaction);
        var modelColumns = await GetColumnsAsync(connection, ModelTable, transaction);
        var siteColumns = await GetColumnsAsync(connection, SiteTable, transaction);
        var tariffColumns = await GetColumnsAsync(connection, LeaseTariffTable, transaction);
        var contractTypeColumns = await GetColumnsAsync(connection, ContractTypeTable, transaction);

        if (!HasColumns(vehicleColumns, RequiredVehicleColumns) ||
            !HasColumns(contractColumns, "vmf_code", "start_date", "target_return_date", "still_current", "contract_type", "site_code") ||
            !HasColumns(sourceColumns, "vs_code", "name") ||
            !HasColumns(typeColumns, "type_code", "type_description") ||
            !HasColumns(modelColumns, "model_code", "model_description") ||
            !HasColumns(siteColumns, "site_code", "description") ||
            !HasColumns(tariffColumns, "vmf_code", "start_date", "end_date", "fixed_tariff"))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        AddParameter(command, "@today", DbType.DateTime2, DateTime.Today);
        AddParameter(command, "@cutoff", DbType.DateTime2, DateTime.Today.AddMonths(3));
        AddParameter(command, "@zeroDate", DbType.DateTime2, new DateTime(1900, 1, 1));

        var contractTargetPredicate = expired
            ? "[c].[target_return_date] < @today"
            : "[c].[target_return_date] >= @today AND [c].[target_return_date] <= @cutoff";
        var tariffActivePredicate = tariffColumns.Contains("active") ? "AND [lt].[active] = 1" : string.Empty;
        var tariffNotDeleted = GetNotDeletedFilter("lt", tariffColumns);
        var contractTypeJoin = HasColumns(contractTypeColumns, "contract_type", "CT_description")
            ? $"LEFT JOIN [dbo].[{ContractTypeTable}] AS [ct] ON [ct].[contract_type] = [c].[contract_type]"
            : string.Empty;
        var contractTypeExpression = HasColumns(contractTypeColumns, "contract_type", "CT_description")
            ? "COALESCE([ct].[CT_description], [c].[contract_type])"
            : "[c].[contract_type]";
        var siteExpression = siteColumns.Contains("department_number")
            ? "CASE WHEN [s].[department_number] IS NULL OR LTRIM(RTRIM(CONVERT(nvarchar(50), [s].[department_number]))) = '' THEN [s].[description] ELSE CONVERT(nvarchar(50), [s].[department_number]) + ': ' + [s].[description] END"
            : "[s].[description]";

        command.CommandText = $"""
            SELECT DISTINCT
                   [v].[fleet_number] AS [gg_number],
                   [v].[registration_number] AS [gp_number],
                   [m].[model_description] AS [model],
                   [v].[year_manufactured] AS [year_model],
                   [vs].[name] AS [hired_from],
                   [t].[type_description] AS [hire_type],
                   [c].[still_current] AS [still_current],
                   [c].[start_date] AS [contract_start_date],
                   [c].[target_return_date] AS [target_return_date],
                   {contractTypeExpression} AS [contract_type],
                   {siteExpression} AS [site_name],
                   [lt].[fixed_tariff] AS [fixed_tariff]
            FROM [dbo].[{VehicleTable}] AS [v]
            INNER JOIN [dbo].[{ModelTable}] AS [m] ON [v].[model_code] = [m].[model_code]
            INNER JOIN [dbo].[{VehicleTypeTable}] AS [t] ON [v].[type_code] = [t].[type_code]
            INNER JOIN [dbo].[{VehicleSourceTable}] AS [vs] ON [v].[vs_code] = [vs].[vs_code]
            INNER JOIN [dbo].[{ContractTable}] AS [c] ON [v].[vmf_code] = [c].[vmf_code]
            INNER JOIN [dbo].[{SiteTable}] AS [s] ON [c].[site_code] = [s].[site_code]
            INNER JOIN [dbo].[{LeaseTariffTable}] AS [lt]
                ON [v].[vmf_code] = [lt].[vmf_code]
               AND [lt].[start_date] <= @today
               AND ([lt].[end_date] IS NULL OR [lt].[end_date] >= @today OR [lt].[end_date] = @zeroDate)
               {tariffActivePredicate}
               AND {tariffNotDeleted}
            {contractTypeJoin}
            WHERE [v].[vs_code] IN (2, 3)
              AND [v].[type_code] = 4
              AND [c].[still_current] = 'Y'
              AND [c].[contract_type] = 'L'
              AND {contractTargetPredicate}
              AND {GetNotDeletedFilter("v", vehicleColumns)}
              AND {GetNotDeletedFilter("c", contractColumns)}
            ORDER BY [v].[fleet_number]
            """;

        var records = new List<FmlContractRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new FmlContractRecord(
                records.Count + 1,
                ReadString(reader, "gg_number"),
                ReadString(reader, "gp_number"),
                ReadString(reader, "model"),
                ReadInt16(reader, "year_model"),
                ReadString(reader, "hired_from"),
                ReadString(reader, "hire_type"),
                ReadString(reader, "still_current"),
                ReadDateTime(reader, "contract_start_date"),
                ReadDateTime(reader, "target_return_date"),
                ReadString(reader, "contract_type"),
                ReadString(reader, "site_name"),
                ReadDecimal(reader, "fixed_tariff")));
        }

        return records;
    }

    private async Task<FmlVehiclesNoContractsReport> QueryVehiclesNoContractsAsync(
        DbConnection connection,
        DbTransaction? transaction)
    {
        var vehicleColumns = await GetColumnsAsync(connection, VehicleTable, transaction);
        var sourceColumns = await GetColumnsAsync(connection, VehicleSourceTable, transaction);
        var statusColumns = await GetColumnsAsync(connection, VehicleStatusTable, transaction);
        var locationTable = await ResolveTableAsync(connection, [LocationTable, "Locations"], transaction);
        var locationColumns = locationTable is null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : await GetColumnsAsync(connection, locationTable, transaction);
        var modelColumns = await GetColumnsAsync(connection, ModelTable, transaction);
        var classColumns = await GetColumnsAsync(connection, ClassTable, transaction);
        var contractColumns = await GetColumnsAsync(connection, ContractTable, transaction);
        var tariffColumns = await GetColumnsAsync(connection, LeaseTariffTable, transaction);

        if (!HasColumns(vehicleColumns, RequiredVehicleColumns) ||
            !vehicleColumns.Contains("location_code") ||
            !HasColumns(sourceColumns, "vs_code", "name") ||
            !HasColumns(statusColumns, "vehicle_status_code", "status_description") ||
            !HasColumns(locationColumns, "location_code", "description") ||
            !HasColumns(modelColumns, "model_code", "model_description", "class_code") ||
            !HasColumns(classColumns, "class_code", "description") ||
            !HasColumns(contractColumns, "vmf_code"))
        {
            return new FmlVehiclesNoContractsReport([]);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var tariffJoin = HasColumns(tariffColumns, "vmf_code")
            ? $"LEFT JOIN (SELECT DISTINCT [vmf_code] FROM [dbo].[{LeaseTariffTable}] WHERE {GetNotDeletedFilter(string.Empty, tariffColumns)}) AS [lt] ON [v].[vmf_code] = [lt].[vmf_code]"
            : string.Empty;
        var tariffPredicate = HasColumns(tariffColumns, "vmf_code") ? "[lt].[vmf_code] IS NULL" : "1 = 1";
        var notExistsContract = $"NOT EXISTS (SELECT 1 FROM [dbo].[{ContractTable}] AS [c] WHERE [c].[vmf_code] = [v].[vmf_code] AND {GetNotDeletedFilter("c", contractColumns)})";

        command.CommandText = $"""
            SELECT [v].[fleet_number] AS [gg_number],
                   [v].[registration_number] AS [registration_number],
                   [vs].[name] AS [hired_from],
                   [vst].[status_description] AS [vehicle_status],
                   [l].[description] AS [location],
                   [v].[year_manufactured] AS [year_model],
                   [m].[model_description] AS [model_description],
                   [cl].[description] AS [class_description],
                   [v].[purchase_amount] AS [purchase_amount]
            FROM [dbo].[{VehicleTable}] AS [v]
            INNER JOIN [dbo].[{VehicleSourceTable}] AS [vs] ON [v].[vs_code] = [vs].[vs_code]
            INNER JOIN [dbo].[{VehicleStatusTable}] AS [vst] ON [v].[vehicle_status_code] = [vst].[vehicle_status_code]
            INNER JOIN [dbo].[{locationTable}] AS [l] ON [l].[location_code] = [v].[location_code]
            INNER JOIN [dbo].[{ModelTable}] AS [m] ON [v].[model_code] = [m].[model_code]
            INNER JOIN [dbo].[{ClassTable}] AS [cl] ON [m].[class_code] = [cl].[class_code]
            {tariffJoin}
            WHERE [v].[vs_code] IN (2, 3)
              AND [v].[type_code] = 4
              AND {tariffPredicate}
              AND {notExistsContract}
              AND {GetNotDeletedFilter("v", vehicleColumns)}
            ORDER BY [v].[fleet_number]
            """;

        var records = new List<FmlVehicleNoContractRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new FmlVehicleNoContractRecord(
                records.Count + 1,
                ReadString(reader, "gg_number"),
                ReadString(reader, "registration_number"),
                ReadString(reader, "hired_from"),
                ReadString(reader, "vehicle_status"),
                ReadString(reader, "location"),
                ReadInt16(reader, "year_model"),
                ReadString(reader, "model_description"),
                ReadString(reader, "class_description"),
                ReadDecimal(reader, "purchase_amount")));
        }

        return new FmlVehiclesNoContractsReport(records);
    }

    private async Task<FmlOverUtilizedReport> QueryOverUtilizedAsync(
        DbConnection connection,
        DbTransaction? transaction,
        DateTime? startDate,
        DateTime? endDate)
    {
        var storedProcedureRows = await TryExecuteStoredProcedureAsync(
            connection,
            transaction,
            "DEV_REP_OverUtilizedFMLVehiclesOnKm",
            command =>
            {
                AddParameter(command, "@start_date", DbType.DateTime, startDate?.Date);
                AddParameter(command, "@end_date", DbType.DateTime, endDate?.Date);
            });

        if (storedProcedureRows is not null)
        {
            var records = storedProcedureRows
                .Select(MapOverUtilizedRecord)
                .Where(record => record is not null)
                .Cast<FmlOverUtilizedRecord>()
                .Select((record, index) => record with { VehicleCounter = record.VehicleCounter ?? index + 1 })
                .ToList();
            return new FmlOverUtilizedReport(records);
        }

        return new FmlOverUtilizedReport(await QueryOverUtilizedFallbackAsync(connection, transaction, startDate, endDate));
    }

    private async Task<IReadOnlyList<FmlOverUtilizedRecord>> QueryOverUtilizedFallbackAsync(
        DbConnection connection,
        DbTransaction? transaction,
        DateTime? startDate,
        DateTime? endDate)
    {
        var vehicleColumns = await GetColumnsAsync(connection, VehicleTable, transaction);
        var sourceColumns = await GetColumnsAsync(connection, VehicleSourceTable, transaction);
        var modelColumns = await GetColumnsAsync(connection, ModelTable, transaction);
        var contractColumns = await GetColumnsAsync(connection, ContractTable, transaction);
        var termsColumns = await GetColumnsAsync(connection, LeaseTermsTable, transaction);
        var kiloColumns = await GetColumnsAsync(connection, VehicleKilosTable, transaction);

        if (!HasColumns(vehicleColumns, RequiredVehicleColumns) ||
            !HasColumns(sourceColumns, "vs_code", "name") ||
            !HasColumns(modelColumns, "model_code", "model_description") ||
            !HasColumns(contractColumns, "vmf_code", "start_date", "still_current") ||
            !HasColumns(termsColumns, "VehicleContractTermID", "vmf_Code", "AgreedKilos", "AgreedTerms") ||
            !HasColumns(kiloColumns, "vmf_code", "start_odo", "end_odo") ||
            !HasAnyColumn(kiloColumns, "Source_Date", "TransactionDate", "date_created", "date_updated"))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var from = startDate?.Date ?? DateTime.Today.AddMonths(-3);
        var to = endDate?.Date ?? DateTime.Today;
        AddParameter(command, "@from", DbType.DateTime2, from);
        AddParameter(command, "@to", DbType.DateTime2, to.AddDays(1));
        AddParameter(command, "@today", DbType.DateTime2, DateTime.Today);

        var kiloDate = GetDateExpression("k", kiloColumns, "Source_Date", "TransactionDate", "date_created", "date_updated");
        var kiloStart = GetColumnExpression("k", kiloColumns, "start_odo", "end_odo");
        var kiloEnd = GetColumnExpression("k", kiloColumns, "end_odo", "start_odo");
        var actualOdo = $"CASE WHEN COALESCE({kiloEnd}, 0) > COALESCE({kiloStart}, 0) THEN COALESCE({kiloEnd}, {kiloStart}) ELSE COALESCE({kiloStart}, {kiloEnd}) END";
        var agreedOverall = termsColumns.Contains("AgreedOverallKilo")
            ? "[lct].[AgreedOverallKilo]"
            : "CAST([lct].[AgreedKilos] * [lct].[AgreedTerms] AS decimal(19, 4))";
        var contractEnd = GetDateExpression("c", contractColumns, "end_date", "charged_until");
        if (contractEnd == "CAST(NULL AS datetime2)")
        {
            contractEnd = "@today";
        }

        command.CommandText = $"""
            SELECT [v].[fleet_number] AS [gg_number],
                   [v].[registration_number] AS [gp_number],
                   [vs].[name] AS [hired_from],
                   MAX({actualOdo}) AS [max_odo_meter],
                   MIN(COALESCE({kiloStart}, {kiloEnd})) AS [min_odo_meter],
                   [lct].[AgreedKilos] AS [agreed_kilos],
                   {agreedOverall} AS [agreed_overall_kilo],
                   [lct].[AgreedTerms] AS [agreed_terms],
                   DATEDIFF(month, [c].[start_date], COALESCE({contractEnd}, @today)) + 1 AS [actual_term],
                   [c].[start_date] AS [contract_start_date],
                   [v].[year_manufactured] AS [year_model],
                   [m].[model_description] AS [model_description],
                   [v].[purchase_amount] AS [purchase_amount]
            FROM [dbo].[{LeaseTermsTable}] AS [lct]
            INNER JOIN [dbo].[{VehicleTable}] AS [v] ON [lct].[vmf_Code] = [v].[vmf_code]
            INNER JOIN [dbo].[{ContractTable}] AS [c]
                ON [c].[vmf_code] = [v].[vmf_code]
               AND [c].[still_current] = 'Y'
            INNER JOIN [dbo].[{VehicleSourceTable}] AS [vs] ON [v].[vs_code] = [vs].[vs_code]
            INNER JOIN [dbo].[{ModelTable}] AS [m] ON [v].[model_code] = [m].[model_code]
            INNER JOIN [dbo].[{VehicleKilosTable}] AS [k]
                ON [k].[vmf_code] = [v].[vmf_code]
               AND {kiloDate} >= @from
               AND {kiloDate} < @to
            WHERE [v].[vs_code] IN (2, 3)
              AND [v].[type_code] = 4
              AND {GetNotDeletedFilter("v", vehicleColumns)}
              AND {GetNotDeletedFilter("c", contractColumns)}
              AND {GetNotDeletedFilter("lct", termsColumns)}
              AND {GetNotDeletedFilter("k", kiloColumns)}
            GROUP BY [v].[fleet_number], [v].[registration_number], [vs].[name],
                     [lct].[AgreedKilos], {agreedOverall}, [lct].[AgreedTerms],
                     [c].[start_date], {contractEnd}, [v].[year_manufactured],
                     [m].[model_description], [v].[purchase_amount]
            ORDER BY [v].[fleet_number]
            """;

        var rawRows = new List<OverUtilizedRawRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rawRows.Add(new OverUtilizedRawRecord(
                ReadString(reader, "gg_number"),
                ReadString(reader, "gp_number"),
                ReadString(reader, "hired_from"),
                ReadDecimal(reader, "max_odo_meter"),
                ReadDecimal(reader, "min_odo_meter"),
                ReadDecimal(reader, "agreed_kilos"),
                ReadDecimal(reader, "agreed_overall_kilo"),
                ReadDecimal(reader, "agreed_terms"),
                ReadDecimal(reader, "actual_term"),
                ReadDateTime(reader, "contract_start_date"),
                ReadInt16(reader, "year_model"),
                ReadString(reader, "model_description"),
                ReadDecimal(reader, "purchase_amount")));
        }

        return rawRows
            .Select((row, index) => BuildOverUtilizedRecord(row, index + 1, from, to))
            .Where(record => record is not null)
            .Cast<FmlOverUtilizedRecord>()
            .ToList();
    }

    private static FmlMaintenanceHistoryRecord? MapMaintenanceRecord(IReadOnlyDictionary<string, object?> row)
    {
        var ggNumber = ReadString(row, "GG Number", "gg_number", "fleet_number");
        var total = ReadDecimal(row, "Total Cost Over Date Range", "total_cost", "TotalCostOverDateRange");
        if (ggNumber is null && total is null)
        {
            return null;
        }

        return new FmlMaintenanceHistoryRecord(
            ggNumber,
            ReadInt16(row, "Year Manufactured", "YearManufactured", "year_manufactured"),
            ReadString(row, "Model Description", "model_description"),
            ReadString(row, "Current Status", "current_status"),
            ReadDateTime(row, "Current Status Date", "current_status_date"),
            ReadString(row, "Hired From", "hired_from"),
            ReadString(row, "Maintenance Expense Type", "maintenance_expense_type"),
            total);
    }

    private static FmlContractRecord? MapContractRecord(IReadOnlyDictionary<string, object?> row)
    {
        var ggNumber = ReadString(row, "GG Number", "gg_number", "fleet_number");
        if (ggNumber is null)
        {
            return null;
        }

        return new FmlContractRecord(
            ReadInt32(row, "No.", "RowNumber", "row_number"),
            ggNumber,
            ReadString(row, "GP Number", "gp_number", "registration_number"),
            ReadString(row, "Model", "model"),
            ReadInt16(row, "Year Model", "year_model"),
            ReadString(row, "Hired From", "hired_from"),
            ReadString(row, "Hire Type", "hire_type"),
            ReadString(row, "Still Current", "still_current"),
            ReadDateTime(row, "Contract Start Date", "contract_start_date"),
            ReadDateTime(row, "Target Return Date", "target_return_date"),
            ReadString(row, "Contract Type", "contract_type"),
            ReadString(row, "Site Name", "site_name"),
            ReadDecimal(row, "fixed_tariff", "Fixed Tariff", "fixedTariff"));
    }

    private static FmlOverUtilizedRecord? MapOverUtilizedRecord(IReadOnlyDictionary<string, object?> row)
    {
        var ggNumber = ReadString(row, "GG Number", "gg_number", "fleet_number");
        if (ggNumber is null)
        {
            return null;
        }

        return new FmlOverUtilizedRecord(
            ReadInt32(row, "Vehicle Counter", "vehicle_counter"),
            ggNumber,
            ReadString(row, "GP Number", "gp_number", "registration_number"),
            ReadString(row, "Hired From", "hired_from"),
            ReadString(row, "Month", "month"),
            ReadDecimal(row, "max_odo_meter", "MaxOdoMeter"),
            ReadDecimal(row, "min_odo_meter", "MinOdoMeter"),
            ReadDecimal(row, "actual_kilos", "ActualKilos"),
            ReadDecimal(row, "AgreedKilos", "agreed_kilos"),
            ReadDecimal(row, "excess_kilos", "ExcessKilos"),
            ReadDecimal(row, "AgreedOverallKilo", "agreed_overall_kilo"),
            ReadDecimal(row, "AgreedTerms", "agreed_terms"),
            ReadDecimal(row, "ActualTerm", "actual_term"),
            ReadDecimal(row, "total_kilos", "TotalKilos"),
            ReadDecimal(row, "total_excess_kilos", "TotalExcessKilos"),
            ReadDecimal(row, "average_monthly_kilos", "AverageMonthlyKilos"),
            ReadString(row, "projected_end_month", "ProjectedEndMonth"),
            ReadDateTime(row, "projected_end_date", "ProjectedEndDate"),
            ReadInt16(row, "Year Manufactured", "YearModel", "year_model"),
            ReadString(row, "Model Description", "model_description"),
            ReadDecimal(row, "Purchase Amount", "purchase_amount"));
    }

    private static FmlOverUtilizedRecord? BuildOverUtilizedRecord(
        OverUtilizedRawRecord row,
        int counter,
        DateTime from,
        DateTime to)
    {
        if (!row.MaxOdoMeter.HasValue || !row.MinOdoMeter.HasValue)
        {
            return null;
        }

        var actualKilos = row.MaxOdoMeter.Value - row.MinOdoMeter.Value;
        var agreedOverall = row.AgreedOverallKilo ??
            (row.AgreedKilos.HasValue && row.AgreedTerms.HasValue
                ? row.AgreedKilos.Value * row.AgreedTerms.Value
                : null);
        var excess = row.AgreedKilos.HasValue && actualKilos > row.AgreedKilos.Value
            ? actualKilos - row.AgreedKilos.Value
            : (decimal?)null;
        var totalExcess = agreedOverall.HasValue && actualKilos > agreedOverall.Value
            ? actualKilos - agreedOverall.Value
            : (decimal?)null;
        if (!excess.HasValue && !totalExcess.HasValue)
        {
            return null;
        }

        var actualTerm = row.ActualTerm.GetValueOrDefault(row.AgreedTerms.GetValueOrDefault(1));
        actualTerm = actualTerm <= 0 ? 1 : actualTerm;
        var averageMonthly = actualKilos / actualTerm;
        DateTime? projectedEndDate = null;
        string? projectedEndMonth = null;
        if (agreedOverall.HasValue && averageMonthly > 0)
        {
            var projectedMonths = Math.Round(agreedOverall.Value / averageMonthly, 0, MidpointRounding.ToEven);
            if (projectedMonths < row.AgreedTerms.GetValueOrDefault(decimal.MaxValue) && projectedMonths <= int.MaxValue)
            {
                projectedEndDate = row.ContractStartDate?.Date.AddMonths((int)projectedMonths);
                projectedEndMonth = projectedMonths.ToString(CultureInfo.InvariantCulture);
            }
        }

        return new FmlOverUtilizedRecord(
            counter,
            row.GgNumber,
            row.GpNumber,
            row.HiredFrom,
            $"{from:yyyy-MM} to {to:yyyy-MM}",
            row.MaxOdoMeter,
            row.MinOdoMeter,
            actualKilos,
            row.AgreedKilos,
            excess,
            agreedOverall,
            row.AgreedTerms,
            actualTerm,
            actualKilos,
            totalExcess,
            averageMonthly,
            projectedEndMonth,
            projectedEndDate,
            row.YearModel,
            row.ModelDescription,
            row.PurchaseAmount);
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>?> TryExecuteStoredProcedureAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        Action<DbCommand>? configure)
    {
        if (!await ObjectExistsAsync(connection, $"dbo.{procedureName}", transaction))
        {
            return null;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            configure?.Invoke(command);

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FML report procedure {ProcedureName} was unavailable; using compatibility query", procedureName);
            return null;
        }
    }

    private async Task<T> WithConnectionAsync<T>(Func<DbConnection, DbTransaction?, Task<T>> operation)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            return await operation(connection, _context.Database.CurrentTransaction?.GetDbTransaction());
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> ObjectExistsAsync(DbConnection connection, string objectName, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@objectName) IS NULL THEN 0 ELSE 1 END";
        AddParameter(command, "@objectName", DbType.String, objectName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string?> ResolveTableAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        DbTransaction? transaction)
    {
        foreach (var candidate in candidates)
        {
            if (await ObjectExistsAsync(connection, $"dbo.{candidate}", transaction))
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
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

    private static bool HasColumns(IReadOnlySet<string> columns, params string[] required)
        => required.All(columns.Contains);

    private static bool HasAnyColumn(IReadOnlySet<string> columns, params string[] candidates)
        => candidates.Any(columns.Contains);

    private static string GetNotDeletedFilter(string alias, IReadOnlySet<string> columns)
    {
        var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : $"[{alias}].";
        return columns.Contains("is_deleted")
            ? $"({prefix}[is_deleted] = 0 OR {prefix}[is_deleted] IS NULL)"
            : "1 = 1";
    }

    private static string GetDateExpression(string alias, IReadOnlySet<string> columns, params string[] candidates)
    {
        var expressions = candidates
            .Where(columns.Contains)
            .Select(column => GetColumnExpression(alias, columns, column))
            .ToArray();
        return expressions.Length switch
        {
            0 => "CAST(NULL AS datetime2)",
            1 => expressions[0],
            _ => $"COALESCE({string.Join(", ", expressions)})"
        };
    }

    private static string GetTextExpression(string alias, IReadOnlySet<string> columns, params string[] candidates)
    {
        var column = candidates.FirstOrDefault(columns.Contains);
        return column is null ? "CAST(NULL AS nvarchar(255))" : GetColumnExpression(alias, columns, column);
    }

    private static string GetColumnExpression(string alias, IReadOnlySet<string> columns, params string[] candidates)
    {
        var column = candidates.FirstOrDefault(columns.Contains);
        if (column is null)
        {
            return "CAST(NULL AS decimal(19, 4))";
        }

        return string.IsNullOrWhiteSpace(alias) ? $"[{column}]" : $"[{alias}].[{column}]";
    }

    private static DateRange ResolveDateRange(DateTime? startDate, DateTime? endDate, int? financialYear)
    {
        if (financialYear is > 0)
        {
            return new DateRange(
                new DateTime(financialYear.Value, 4, 1),
                new DateTime(financialYear.Value + 1, 3, 31));
        }

        var start = startDate?.Date ?? new DateTime(1900, 1, 1);
        var end = endDate?.Date ?? DateTime.Today;
        return end < start ? new DateRange(end, start) : new DateRange(start, end);
    }

    private static object? GetValue(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> row, params string[] keys)
        => GetValue(row, keys)?.ToString()?.Trim() is { Length: > 0 } value ? value : null;

    private static int? ReadInt32(IReadOnlyDictionary<string, object?> row, params string[] keys)
        => ToInt32(GetValue(row, keys));

    private static short? ReadInt16(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        var value = ToInt32(GetValue(row, keys));
        return value.HasValue ? Convert.ToInt16(value.Value, CultureInfo.InvariantCulture) : null;
    }

    private static decimal? ReadDecimal(IReadOnlyDictionary<string, object?> row, params string[] keys)
        => ToDecimal(GetValue(row, keys));

    private static DateTime? ReadDateTime(IReadOnlyDictionary<string, object?> row, params string[] keys)
    {
        var value = GetValue(row, keys);
        if (value is DateTime dateTime)
        {
            return dateTime;
        }

        return DateTime.TryParse(value?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed
            : null;
    }

    private static int? ToInt32(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? ToDecimal(object? value)
    {
        if (value is null || value is DBNull)
        {
            return null;
        }

        return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadString(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : reader[column]?.ToString()?.Trim() is { Length: > 0 } value ? value : null;

    private static short? ReadInt16(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt16(reader[column], CultureInfo.InvariantCulture);

    private static decimal? ReadDecimal(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : ToDecimal(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToDateTime(reader[column], CultureInfo.InvariantCulture);

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record DateRange(DateTime Start, DateTime End);

    private sealed record OverUtilizedRawRecord(
        string? GgNumber,
        string? GpNumber,
        string? HiredFrom,
        decimal? MaxOdoMeter,
        decimal? MinOdoMeter,
        decimal? AgreedKilos,
        decimal? AgreedOverallKilo,
        decimal? AgreedTerms,
        decimal? ActualTerm,
        DateTime? ContractStartDate,
        short? YearModel,
        string? ModelDescription,
        decimal? PurchaseAmount);
}
