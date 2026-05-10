using System.Data;
using System.Globalization;
using System.Text.Json;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

public interface ILegacyReportResultService
{
    Task<LegacyReportResultDto> GetReportAsync(string reportKey, IDictionary<string, string?> filters, CancellationToken cancellationToken = default);
}

public sealed class LegacyReportResultService : ILegacyReportResultService
{
    private readonly FisDbContext _context;
    private readonly ILogger<LegacyReportResultService> _logger;
    private readonly IReadOnlyDictionary<string, LegacyReportDefinition> _definitions;

    public LegacyReportResultService(FisDbContext context, ILogger<LegacyReportResultService> logger)
    {
        _context = context;
        _logger = logger;
        _definitions = BuildDefinitions();
    }

    public async Task<LegacyReportResultDto> GetReportAsync(string reportKey, IDictionary<string, string?> filters, CancellationToken cancellationToken = default)
    {
        var resolvedKey = ResolveReportKeyAlias(reportKey);

        if (!_definitions.TryGetValue(resolvedKey, out var definition))
        {
            _logger.LogWarning("Legacy report key '{ReportKey}' is not mapped. Returning fallback response.", reportKey);
            return BuildMissingKeyFallback(reportKey, filters);
        }

        if (definition.StoredProcedureItem is not null)
        {
            try
            {
                var storedProcResult = await TryExecuteStoredProcedureAsync(definition, filters, cancellationToken);
                if (storedProcResult is not null)
                {
                    return storedProcResult;
                }
            }
            catch (Exception ex)
            {
                    _logger.LogWarning(ex, "Stored procedure execution failed for legacy report {ReportKey}. Falling back to approximate query.", resolvedKey);
            }
        }

        var fallback = await definition.Fallback(filters, cancellationToken);
        fallback.ReportKey = reportKey;
        fallback.Title = string.IsNullOrWhiteSpace(fallback.Title) ? definition.Title : fallback.Title;
        fallback.LegacyTarget ??= definition.LegacyTarget;
        fallback.IsApproximate = true;
        fallback.ApproximationReason ??= definition.ApproximationReason;
        fallback.TotalCount = fallback.Rows.Count;
        return fallback;
    }

    private static string ResolveReportKeyAlias(string reportKey)
    {
        if (string.IsNullOrWhiteSpace(reportKey))
        {
            return reportKey;
        }

        var key = reportKey.Trim().ToLowerInvariant();
        return key switch
        {
            // Top-level menu keys that should resolve to concrete dynamic definitions
            "asset-verification" => "asset-list",
            "auction" => "vehicle-disposals",
            "class-code" => "tariffs-class-2007",
            "class-code-totals" => "tariffs-class-2007",
            "clearance" => "unallocated-vehicles",
            "fuel-cards" => "wesbank",
            "logbooks" => "vehicle-logs-report",
            "logsheets" => "vehicle-logs-report",
            "registration-certificate-one-vehicle" => "registration-certificates",
            "tariffs-class-codes" => "tariffs-class-2007",
            "tariffs-licence-fees" => "tariffs-fin-year",
            "tariffs-make-model" => "tariffs-fin-year",
            "tariffs-private-taxi" => "tariffs-fin-year",
            "users" => "audit-trail",
            "users-added" => "audit-trail",
            "users-one" => "audit-trail",
            "vehicle-by-barcode" => "vehicles",
            "vehicles-with-history" => "vehicles",
            "vip-pool-utilization-current" => "trips-open-31",
            "vip-pool-utilization-previous" => "trips-open-31",
            "vip-pool-income-current" => "trips-open-31",
            "vip-pool-income-previous" => "trips-open-31",

            // Audit trail variants
            "audit-trail-department" => "audit-trail",
            "audit-trail-site" => "audit-trail",
            "audit-trail-vehicle" => "audit-trail",

            // Management menu variants
            "management-ggmt" => "management",
            "management-incorrect-captured-data" => "management",
            "management-site-info" => "management",

            // Contract report variants
            "contract-summary" => "contracts",
            "contract-trip-authority-dept-site-date" => "contracts",
            "contract-trip-authority-multiple" => "contracts",
            "contract-trip-authority-single" => "contracts",
            "contract-vehicle-multiple" => "contracts",
            "contract-vehicle-single" => "contracts",
            "contracts-checklist" => "contracts",
            "contracts-expiring-by-date" => "contracts",
            "contracts-fleet-reports" => "contracts",
            "contracts-no-distance" => "contracts",
            "contracts-per-dept-period" => "contracts",
            "lease-nom-contract-split" => "contracts",

            // Asset list variants
            "asset-list-by-province" => "asset-list",
            "asset-list-by-department" => "asset-list",
            "asset-list-by-site" => "asset-list",

            // Asset verification variants
            "asset-verification-per-site-province-date" => "asset-verification",
            "asset-verification-not-verified" => "asset-verification",
            "asset-verification-verified-by-date-range" => "asset-verification",

            // Fine report variants
            "fines-one-vehicle" => "fines",
            "fines-appear-date" => "fines",
            "fines-reissue-submission" => "fines",
            "fines-traffic-dept-detail" => "fines",

            // Taxis menu/report variants
            "taxis-future-bookings-my-dept" => "taxis",
            "taxis-history-bookings-period" => "taxis",
            "taxis-requisition-numbers-period" => "taxis",
            "taxis-per-hire-company" => "taxis",
            "taxis-reprint-requisition" => "taxis",
            "taxis-reprint-taxi-log" => "taxis",
            "taxis-fin-general-requisitions" => "taxis-financial",
            "taxis-fin-requisitions-per-department" => "taxis-financial",
            "taxis-fin-outstanding-logsheets" => "taxis-financial",
            "taxis-fin-log-odometer-gg" => "taxis-financial",
            "taxis-fin-cancellations" => "taxis-financial",
            "taxis-fin-no-objective-or-responsibility" => "taxis-financial",

            // Wesbank variants
            "wesbank-one-vehicle" => "wesbank",
            "wesbank-one-vehicle-period" => "wesbank",
            "wesbank-one-dept-site" => "wesbank",
            "wesbank-overfills" => "wesbank",
            "wesbank-multiple-daily-fuels" => "wesbank",

            // Auction variants
            "auction-one-vehicle" => "auction",
            "auction-sale-to-name" => "auction",
            "auction-one-sort-gg" => "auction",
            "auction-one-sort-lot" => "auction",

            // Logbook/logsheet fine-grained variants
            "logbooks-number" => "logbooks",
            "logbooks-one-vehicle" => "logbooks",
            "logsheets-all-outstanding" => "logsheets",
            "logsheets-one-vehicle" => "logsheets",
            "logsheets-vehicle-details-per-rek" => "logsheets",
            "logsheets-vehicle-odo-balance" => "logsheets",

            // Losses report variants
            "losses-one-vehicle" => "losses",
            "losses-outstanding-report" => "losses",
            "losses-with-report" => "losses",
            "losses-site-period-vip-gg-hire" => "losses",

            // Licence variants
            "licences-all-with-model-tare-fee" => "licences",
            "licences-cof-info" => "licences",
            "licences-expire-date" => "licences",
            "licences-site" => "licences",
            "licences-gg-number" => "licences",
            "licences-register-number" => "licences",
            "licences-chassis-number" => "licences",
            "licences-engine-number" => "licences",
            "licences-data-workgroup" => "licences",
            "licences-data-workgroup-latest" => "licences",
            "licences-received-by-ggmt" => "licences",
            "licences-prov-reg-number" => "licences",
            "licences-old-expire-dates" => "licences",
            "licences-make-model-fee" => "licences",
            "licences-month-fees" => "licences",
            "licences-sap-info" => "licences",
            "licences-dept-sites-period" => "licences",

            // Vehicle report variants
            "vehicles-no-trips" => "vehicles",
            "vehicles-no-trips-daterange" => "vehicles",
            "vehicles-per-site" => "vehicles",
            "vehicles-per-department" => "vehicles",
            "vehicles-inservice-per-gg" => "vehicles",
            "vehicles-inservice-per-dept" => "vehicles",
            "vehicles-inservice-wesbank" => "vehicles",
            "vehicles-provincial-numbers" => "vehicles",
            "vehicles-with-barcodes" => "vehicles",
            "vehicles-lpg-converted" => "vehicles",
            "vehicles-replaced-per-dept" => "vehicles",
            "vehicles-older-than-5y-over-120k" => "vehicles",
            "vehicles-older-than-5y-over-120k-period" => "vehicles",
            "vehicles-extended-service" => "vehicles",
            "vehicles-value-inservice" => "vehicles",
            "vehicles-extras" => "vehicles",
            "vehicles-contract-type-site" => "vehicles",
            "vehicles-contract-type-department" => "vehicles",
            "vehicles-contract-type-department-site" => "vehicles",
            "vehicles-universal-selected" => "vehicles",
            "vehicles-selected" => "vehicles",
            "vehicles-els-manual" => "vehicles",
            "vehicle-contract-single" => "vehicles",
            "vehicle-contract-multiple" => "vehicles",
            "vehicle-contract-universal" => "vehicles",
            "vehicle-contract-els-manual" => "vehicles",

            // Department/site variants
            "departments-sites-contact" => "departments-sites",
            "departments-outstanding-logs-combined" => "departments-sites",
            "departments-one-department" => "departments-sites",
            "departments-one-site" => "departments-sites",
            "departments-vehicles-manual-logs" => "departments-sites",
            "departments-vehicles-els" => "departments-sites",

            // Fuel card variants
            "fuelcards-one-vehicle" => "fuel-cards",
            "fuelcards-one-vehicle-handout" => "fuel-cards",
            "fuelcards-expire-date" => "fuel-cards",
            "fuelcards-one-site-expire-date" => "fuel-cards",
            "fuelcards-dept-site-expire-period" => "fuel-cards",
            "fuelcards-dept-site" => "fuel-cards",
            "fuelcards-replace-reason" => "fuel-cards",
            "fuelcards-one-pan" => "fuel-cards",
            "fuelcards-wesbank-new-cards" => "fuel-cards",
            "fuelcards-pool-vehicles" => "fuel-cards",
            "fuelcards-vip-vehicles" => "fuel-cards",

            // Workshop report variants
            "workshop-one-vehicle" => "workshop",
            "workshop-print-job-card" => "workshop",
            "workshop-in-shop" => "workshop",
            "workshop-merchants" => "workshop",

            _ => key
        };
    }

    private static LegacyReportResultDto BuildMissingKeyFallback(string reportKey, IDictionary<string, string?> filters)
    {
        var columns = new List<LegacyReportColumnDto>
        {
            new() { Key = "report_key", Header = "Report Key" },
            new() { Key = "status", Header = "Status" },
            new() { Key = "details", Header = "Details" }
        };

        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["report_key"] = reportKey,
            ["status"] = "Missing API Mapping",
            ["details"] = "This report key is not yet mapped in LegacyReportResultService. Returning non-404 placeholder so navigation can continue."
        };

        if (filters.Count > 0)
        {
            row["details"] += $" Filters: {JsonSerializer.Serialize(filters)}";
        }

        return new LegacyReportResultDto
        {
            ReportKey = reportKey,
            Title = $"Report: {reportKey}",
            LegacyTarget = "Legacy mapping pending",
            IsApproximate = true,
            ApproximationReason = "Fallback generated because report key mapping is missing in API.",
            Columns = columns,
            Rows = new List<Dictionary<string, string?>> { row },
            TotalCount = 1
        };
    }

    private async Task<LegacyReportResultDto?> TryExecuteStoredProcedureAsync(
        LegacyReportDefinition definition,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken)
    {
        if (definition.StoredProcedureItem is null)
        {
            return null;
        }

        var parameters = definition.BuildStoredProcedureParameters?.Invoke(filters) ?? Array.Empty<LegacyStoredProcedureParameter>();
        if (parameters.Count == 0 && filters.Any(kvp => !string.Equals(kvp.Key, "view", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kvp.Value)) && definition.BuildStoredProcedureParameters is null)
        {
            return null;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
#pragma warning disable CA2100
            command.CommandText = $"DEV_REP_{definition.StoredProcedureItem}";
#pragma warning restore CA2100
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 180;

            foreach (var parameter in parameters)
            {
                var dbParameter = command.CreateParameter();
                dbParameter.ParameterName = parameter.Name;
                dbParameter.DbType = parameter.DbType;
                dbParameter.Value = parameter.Value ?? DBNull.Value;
                command.Parameters.Add(dbParameter);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await ReadDynamicResultAsync(reader, definition, cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<LegacyReportResultDto> ReadDynamicResultAsync(
        System.Data.Common.DbDataReader reader,
        LegacyReportDefinition definition,
        CancellationToken cancellationToken)
    {
        var columns = new List<LegacyReportColumnDto>();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var header = reader.GetName(index);
            columns.Add(new LegacyReportColumnDto
            {
                Key = header,
                Header = header
            });
        }

        var rows = new List<Dictionary<string, string?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                row[column.Key] = FormatValue(reader[column.Key]);
            }
            rows.Add(row);
        }

        return new LegacyReportResultDto
        {
            ReportKey = definition.Key,
            Title = definition.Title,
            LegacyTarget = definition.LegacyTarget,
            IsApproximate = false,
            ApproximationReason = null,
            Columns = columns,
            Rows = rows,
            TotalCount = rows.Count
        };
    }

    private IReadOnlyDictionary<string, LegacyReportDefinition> BuildDefinitions()
    {
        return new Dictionary<string, LegacyReportDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["asset-list"] = new(
                "asset-list",
                "Asset List: New & In-Service Vehicles",
                "Finance/AssetVehicleReports.aspx",
                null,
                BuildAssetListAsync,
                "Rendered from legacy-compatible vehicle/status/site data because the legacy asset list menu fans into multiple report modes."),

            ["audit-trail"] = new(
                "audit-trail",
                "Audit Trail Reports",
                "Finance/GetFinancialAditTrailReportsDateRange.aspx",
                "ELSAuditTrailReport",
                BuildAuditTrailAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>(),
                ApproximationReason: "Falls back to contract audit log history when the legacy DEV_REP_ELSAuditTrailReport stored procedure is unavailable."),

            ["capture-activity"] = new(
                "capture-activity",
                "Capture Activity",
                "Modern report (no direct legacy equivalent)",
                null,
                BuildCaptureActivityAsync,
                "No direct legacy equivalent exists; this grid is a dynamic modern approximation using capture timestamps across legacy-backed tables."),

            ["contract-history"] = new(
                "contract-history",
                "Contract History",
                "Logs/RPT_Contracts_per_vehicle.aspx",
                null,
                BuildContractHistoryAsync,
                "Legacy page is a composite/folder view. This approximation flattens contract history into a legacy-style dynamic grid."),

            ["contracts"] = new(
                "contracts",
                "Contracts Report",
                "/FISReports/Contracts/Contracts.aspx",
                "VehicleContractsAuditReport",
                BuildContractsAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>(),
                ApproximationReason: "Falls back to contract/site/vehicle data when the legacy contract audit stored procedure is unavailable."),

            ["departments-sites"] = new(
                "departments-sites",
                "Departments and Sites",
                "Department/Department.aspx",
                null,
                BuildDepartmentsSitesAsync,
                "Legacy department screens are custom pages. This approximation projects the legacy department/site tables into a dynamic grid."),

            ["fines"] = new(
                "fines",
                "Fines Reports",
                "Fines/RPTFines.aspx",
                null,
                BuildFinesAsync,
                "Legacy fines report pages are menu-driven and parameterized. This approximation projects fine records with joined vehicle/site/traffic fields."),

            ["losses"] = new(
                "losses",
                "Losses Reports",
                "Losses/RPTLosses.aspx",
                null,
                BuildLossesAsync,
                "Legacy losses report pages are menu-driven and parameterized. This approximation projects loss records with joined vehicle/site/loss-type fields."),

            ["manuals"] = new(
                "manuals",
                "Manuals Menu",
                "Manuals/RPTmanuals.aspx",
                null,
                BuildManualsAsync,
                "Legacy manuals reporting is a navigation menu of manual documents. This result preserves one-to-one menu entries and targets in a dynamic grid."),

            ["high-distance-all"] = new(
                "high-distance-all",
                "High Distance Vehicles (All Departments)",
                "ShowReport.aspx?Item=KiloAudit",
                "KiloAudit",
                BuildHighDistanceAllAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>(),
                ApproximationReason: "Falls back to current/highest odometer readings when the legacy DEV_REP_KiloAudit stored procedure is unavailable."),

            ["high-distance-dept"] = new(
                "high-distance-dept",
                "High Distance Vehicles (Department)",
                "ShowReport.aspx?Item=KiloAudit&DepartmentID=...",
                "KiloAudit",
                BuildHighDistanceDeptAsync,
                BuildStoredProcedureParameters: filters => BuildOptionalParameterList(("@DepartmentID", (object?)GetShort(filters, "dept"), DbType.Int16)),
                ApproximationReason: "Falls back to current/highest odometer readings plus active contract department when the legacy DEV_REP_KiloAudit stored procedure is unavailable."),

            ["incorrect-quantities"] = new(
                "incorrect-quantities",
                "Report to show incorrect calculated quantities",
                "Finance/GeneratedReports.aspx?key=9.3%20Report%20to%20show%20incorrect%20calculated%20quantities",
                null,
                BuildIncorrectQuantitiesAsync,
                "Legacy generated report output is approximated from vehicle odometer and quantity-related fields in vehicle_master."),

            ["licences"] = new(
                "licences",
                "Licence Reports",
                "License/RPTLicence.aspx",
                null,
                BuildLicencesAsync,
                "Legacy licence reports include multiple one-vehicle and grouped variants. This approximation consolidates core licence fields with vehicle, model, status, site, and licence-fee data."),

            ["management"] = new(
                "management",
                "Management Reports",
                "Management_Reports/Management.aspx",
                null,
                BuildManagementAsync,
                "Legacy management report entry is a custom page. This approximation groups legacy vehicle data into a management summary grid."),

            ["previous-fin-year"] = new(
                "previous-fin-year",
                "Previous Fin Year Reports",
                "Finance/PreviousFinYear.aspx",
                null,
                BuildPreviousFinYearMenuAsync,
                "Legacy flow presents two report items. This modern entry returns a selectable summary of those same report options."),

            ["previous-fin-year-manual-logs"] = new(
                "previous-fin-year-manual-logs",
                "Previous Fin Year Manual Logsheet Kilos Captured in Current Fin Year",
                "ShowReport.aspx?Item=PreviousFinYearManualLogsCapturedInCurrentFinYear",
                "PreviousFinYearManualLogsCapturedInCurrentFinYear",
                BuildPreviousFinYearManualLogsAsync,
                "Falls back to Logsheets month/date_created financial-year comparison when the legacy report stored procedure is unavailable."),

            ["previous-fin-year-vip-taxi"] = new(
                "previous-fin-year-vip-taxi",
                "Previous Fin Year VIP & Taxi Requisitions Captured in Current Fin Year",
                "ShowReport.aspx?Item=PreviousFinYearKiloLogsCapturedInCurrentFinYear",
                "PreviousFinYearKiloLogsCapturedInCurrentFinYear",
                BuildPreviousFinYearVipTaxiAsync,
                "Falls back to Taxis date_required/date_created financial-year comparison when the legacy report stored procedure is unavailable."),

            ["registration-certificates"] = new(
                "registration-certificates",
                "Registration Certificates",
                "ScanDocs/Reg_Cert_Menu.aspx",
                null,
                BuildRegistrationCertificatesAsync,
                "Legacy registration certificate flow is menu-driven. This approximation uses vehicle master registration certificate fields."),

            ["tariffs-class-2007"] = new(
                "tariffs-class-2007",
                "Published Tariffs (2007 and Earlier)",
                "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
                "Tariffs",
                BuildTariffsClass2007Async,
                BuildStoredProcedureParameters: filters => BuildOptionalParameterList(("@FinYear", (object?)GetFinancialYear(filters), DbType.Int32)),
                ApproximationReason: "Falls back to legacy tariff rows because the published tariffs stored procedure is not guaranteed to exist in every environment."),

            ["tariffs-fin-year"] = new(
                "tariffs-fin-year",
                "Published Tariffs by Financial Year",
                "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
                "Tariffs",
                BuildTariffsFinYearAsync,
                BuildStoredProcedureParameters: filters => BuildOptionalParameterList(("@FinYear", (object?)GetFinancialYear(filters), DbType.Int32)),
                ApproximationReason: "Falls back to legacy tariff rows because the published tariffs stored procedure is not guaranteed to exist in every environment."),

            ["tariffs-per-vehicle"] = new(
                "tariffs-per-vehicle",
                "Tariffs per Vehicle",
                "ShowReport.aspx?Item=TariffsPerVehicle",
                "TariffsPerVehicle",
                BuildTariffsPerVehicleAsync,
                BuildStoredProcedureParameters: _ => [new LegacyStoredProcedureParameter("@FinYear", 0, DbType.Int32)],
                ApproximationReason: "Falls back to fin.vehicle_tariff rows joined to vehicles when the legacy per-vehicle tariff stored procedure is unavailable."),

            ["taxis"] = new(
                "taxis",
                "Taxi Reports Menu",
                "Taxis/RPTtaxis.aspx",
                null,
                BuildTaxisMenuAsync,
                "Legacy taxis reporting opens from a menu page. This dynamic result preserves the same menu entries and targets."),

            ["taxis-list-per-department"] = new(
                "taxis-list-per-department",
                "Report On All Taxis in various Departments",
                "Taxis/RPT_list_of_taxis_per_department.aspx",
                null,
                BuildTaxisListPerDepartmentAsync,
                "Legacy report lists requisition numbers with department description. This approximation uses taxis + department data."),

            ["taxis-list-inservice-per-department"] = new(
                "taxis-list-inservice-per-department",
                "Report On All Taxis in service in various Departments",
                "Taxis/RPT_list_of_taxis_inservice_per_department.aspx",
                null,
                BuildTaxisListInServicePerDepartmentAsync,
                "Legacy report lists in-service taxi requisitions with department description. This approximation applies in-service vehicle status filtering."),

            ["taxis-financial"] = new(
                "taxis-financial",
                "Financial Reports: Taxis",
                "Taxis/Taxi_Fin_reports.aspx",
                null,
                BuildTaxisFinancialAsync,
                "Legacy taxi financial pages are custom forms. This approximation uses the legacy Taxis table and related department/site data."),

            ["trip-authority"] = new(
                "trip-authority",
                "Trip Authority Report",
                "TripReports.aspx / ShowReport multiple items",
                null,
                BuildTripAuthorityAsync,
                "Legacy trip authority reports fan into multiple custom pages. This approximation flattens trip authority records into a single dynamic grid."),

            ["trips-open-31"] = new(
                "trips-open-31",
                "Trips Open for Over 31 Days",
                "ShowReport.aspx?Item=TripsOpenForOver31Days",
                "TripsOpenForOver31Days",
                BuildTripsOpen31Async,
                BuildStoredProcedureParameters: filters => BuildOptionalParameterList(("@Days", (object?)GetInt(filters, "days"), DbType.Int32)),
                ApproximationReason: "Falls back to trip_authorities issue/expiry dates when the legacy TripsOpenForOver31Days stored procedure is unavailable."),

            ["unallocated-vehicles"] = new(
                "unallocated-vehicles",
                "Unallocated Vehicles",
                "Finance/OpenReport.aspx?Report=VehiclesNoCurrentContractAndFuelTransactions",
                null,
                BuildUnallocatedVehiclesAsync,
                "Approximated from vehicles with no active contract; fuel-transaction parity requires the original finance report pipeline."),

            ["vehicle-additions"] = new(
                "vehicle-additions",
                "Vehicle Additions",
                "SelectStartAndEndDate.aspx?Item=AllVehiclesPurchasedInADateRange",
                "AllVehiclesPurchasedInADateRange",
                BuildVehicleAdditionsAsync,
                BuildStoredProcedureParameters: filters => BuildDateRangeParameters(filters),
                ApproximationReason: "Falls back to purchase-date filtering on vehicle_master when the legacy additions stored procedure is unavailable."),

            ["vehicle-disposals"] = new(
                "vehicle-disposals",
                "Vehicle Disposals",
                "SelectStartAndEndDate.aspx?Item=AllVehiclesDisposedInADateRange",
                "AllVehiclesDisposedInADateRange",
                BuildVehicleDisposalsAsync,
                BuildStoredProcedureParameters: filters => BuildDateRangeParameters(filters),
                ApproximationReason: "Falls back to sold-date filtering on vehicle_master when the legacy disposals stored procedure is unavailable."),

            ["vehicle-info"] = new(
                "vehicle-info",
                "General Vehicle Information Report",
                "Vehicles/RPT_Vehicle_details.aspx",
                null,
                BuildVehicleInfoAsync,
                "Legacy vehicle information report is a composite detail page. This approximation surfaces vehicle-master fields in a dynamic grid."),

            ["vehicle-list-date-range"] = new(
                "vehicle-list-date-range",
                "Vehicle List in Date Range",
                "SelectStartAndEndDate.aspx?Item=FilterAllVehiclesInADateRange",
                "FilterAllVehiclesInADateRange",
                BuildVehicleListDateRangeAsync,
                BuildStoredProcedureParameters: filters => BuildDateRangeParameters(filters),
                ApproximationReason: "Falls back to take-on/sold-date filtering because the legacy date-range stored procedure is unavailable in some environments."),

            ["vehicle-logs-report"] = new(
                "vehicle-logs-report",
                "Vehicle Logs Report",
                "Logs/RPT_logsheet_per_vehicle.aspx",
                null,
                BuildVehicleLogsReportAsync,
                "Legacy vehicle logs report is a folder/iframe composition. This approximation flattens contracts, logbooks and logsheets into one dynamic grid."),

            ["vehicle-status-range"] = new(
                "vehicle-status-range",
                "Vehicle Status Range Report",
                "Vehicles/VehicleStatus.aspx",
                null,
                BuildVehicleStatusRangeAsync,
                "Legacy vehicle status report is a custom page. This approximation uses vehicle_status_history rows."),

            ["vehicle-status-all"] = new(
                "vehicle-status-all",
                "All Vehicle Status",
                "Finance/GeneratedReports.aspx?key=9.2%20All%20Vehicle%20Statuses",
                null,
                BuildVehicleStatusAllAsync,
                "Legacy generated report output is approximated from vehicle master + status + site dimensions."),

            ["vehicles"] = new(
                "vehicles",
                "Vehicle Master List",
                "Vehicles/Vehicles.aspx",
                null,
                BuildVehiclesAsync,
                "Legacy vehicle master screen is custom. This approximation projects vehicle_master columns into a dynamic result grid."),

            ["vehicles-no-tariff"] = new(
                "vehicles-no-tariff",
                "Vehicles with Expired or No Tariffs",
                "ShowReport.aspx?Item=GetAllVehicleWithNoTariffs",
                "GetAllVehicleWithNoTariffs",
                BuildVehiclesNoTariffAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>(),
                ApproximationReason: "Falls back to vehicles that have no active fin.vehicle_tariff row when the legacy stored procedure is unavailable."),

            ["wesbank"] = new(
                "wesbank",
                "Wesbank First Auto Report",
                "Transaction/RPTTransaction.aspx",
                "WesbankExpensesOneProvinceAndAllMonths",
                BuildWesbankAsync,
                BuildStoredProcedureParameters: filters => BuildOptionalParameterList(
                    ("@ProvinceCode", (object?)GetString(filters, "province"), DbType.String),
                    ("@StartDate", (object?)GetDate(filters, "from"), DbType.DateTime),
                    ("@EndDate", (object?)GetDate(filters, "to"), DbType.DateTime)),
                ApproximationReason: "Falls back to journal_detail rows because the original Wesbank report pipeline is not fully represented in the modern API."),

            ["workshop"] = new(
                "workshop",
                "Workshop Report",
                "Workshop/RPTWorkshop.aspx",
                null,
                BuildWorkshopAsync,
                "Legacy workshop reports are menu-driven custom pages. This approximation uses workshop receive/complete rows in a dynamic grid.")
        };
    }

    private async Task<LegacyReportResultDto> BuildAssetListAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";
        var siteCode = GetShort(filters, "site");

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join model in _context.Models.AsNoTracking() on vehicle.model_code equals model.model_code into vehicleModels
            from model in vehicleModels.DefaultIfEmpty()
            join make in _context.Makes.AsNoTracking() on model.make_code equals make.make_code into vehicleMakes
            from make in vehicleMakes.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on vehicle.location_code equals site.Site_code into vehicleSites
            from site in vehicleSites.DefaultIfEmpty()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into vehicleStatuses
            from status in vehicleStatuses.DefaultIfEmpty()
            where !vehicle.is_deleted && (vehicle.vehicle_status_code == 1 || vehicle.vehicle_status_code == 2)
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                Vehicle = JoinVehicleLabel(vehicle.fleet_number, vehicle.registration_number, vehicle.vmf_code),
                Site = site != null ? site.description : null,
                vehicle.current_odo,
                vehicle.highest_km,
                Status = status != null ? status.status_description : null,
                Make = make != null ? make.make_description : null,
                Model = model != null ? model.model_description : null,
                vehicle.year_manufactured,
                vehicle.take_on_date
            };

        if (siteCode.HasValue)
        {
            query = query.Where(row => row.Site != null && row.vmf_code > 0).Where(row => _context.Vehicles.Any(v => v.vmf_code == row.vmf_code && v.location_code == siteCode.Value));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(search, mode, cancellationToken);
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var rows = await query
            .OrderBy(row => row.fleet_number)
            .ThenBy(row => row.registration_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Asset List: New & In-Service Vehicles",
            "Finance/AssetVehicleReports.aspx",
            false,
            null,
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Vehicle", row => row.Vehicle),
            Column("Make", row => row.Make),
            Column("Model", row => row.Model),
            Column("Year", row => row.year_manufactured),
            Column("Status", row => row.Status),
            Column("Site", row => row.Site),
            Column("Current ODO", row => row.current_odo),
            Column("Highest KM", row => row.highest_km),
            Column("Take On Date", row => row.take_on_date));
    }

    private async Task<LegacyReportResultDto> BuildAuditTrailAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var reportType = GetString(filters, "rtype");

        var query = _context.ContractAuditLogs
            .AsNoTracking()
            .Where(log => log.Contract != null || log.contract_code > 0)
            .Select(log => new
            {
                log.id,
                ReportType = "Contract",
                log.action,
                UserId = log.performed_by_user_code.ToString(),
                log.performed_at,
                log.contract_code,
                Field = log.field_changed,
                OldValue = log.old_value,
                NewValue = log.new_value,
                Notes = log.notes
            });

        if (!string.IsNullOrWhiteSpace(reportType) && !string.Equals(reportType, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => row.ReportType == reportType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                row.ReportType.Contains(term) ||
                row.action.Contains(term) ||
                row.UserId.Contains(term) ||
                row.id.ToString().Contains(term) ||
                (row.Field != null && row.Field.Contains(term)) ||
                (row.Notes != null && row.Notes.Contains(term)));
        }

        var rows = await query
            .OrderByDescending(row => row.performed_at)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Audit Trail Reports",
            "Finance/GetFinancialAditTrailReportsDateRange.aspx",
            false,
            null,
            rows,
            Column("Audit ID", row => row.id),
            Column("Report Type", row => row.ReportType),
            Column("Action", row => row.action),
            Column("User ID", row => row.UserId),
            Column("Accessed Date", row => row.performed_at),
            Column("Contract Code", row => row.contract_code),
            Column("Field Changed", row => row.Field),
            Column("Old Value", row => row.OldValue),
            Column("New Value", row => row.NewValue),
            Column("Notes", row => row.Notes));
    }

    private async Task<LegacyReportResultDto> BuildCaptureActivityAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var startDate = GetDate(filters, "from")?.Date ?? DateTime.Today.AddMonths(-1).Date;
        var endDate = GetDate(filters, "to")?.Date ?? DateTime.Today.Date;
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var module = (GetString(filters, "module") ?? "all").Trim();
        var site = GetShort(filters, "site");
        var vehicle = GetString(filters, "vehicle");
        var capturedBy = GetString(filters, "capturedby");

        var contractRows = await (
            from contract in _context.Contracts.AsNoTracking()
            join vehicleMaster in _context.Vehicles.AsNoTracking() on contract.vmf_code equals vehicleMaster.vmf_code into contractVehicles
            from vehicleMaster in contractVehicles.DefaultIfEmpty()
            where !contract.is_deleted && contract.date_created.Date >= startDate && contract.date_created.Date <= endDate
            select new CaptureActivityRow(
                "Contracts",
                contract.contract_code.ToString(),
                vehicleMaster != null ? vehicleMaster.fleet_number : null,
                vehicleMaster != null ? vehicleMaster.registration_number : null,
                contract.site_code,
                contract.created_by_user_code,
                contract.date_created,
                $"Contract {contract.contract_code} ({contract.still_current})"))
            .ToListAsync(cancellationToken);

        var tripRows = await (
            from trip in _context.Trips.AsNoTracking()
            join contract in _context.Contracts.AsNoTracking() on trip.contract_code equals contract.contract_code into tripContracts
            from contract in tripContracts.DefaultIfEmpty()
            join vehicleMaster in _context.Vehicles.AsNoTracking() on contract.vmf_code equals vehicleMaster.vmf_code into tripVehicles
            from vehicleMaster in tripVehicles.DefaultIfEmpty()
            where !trip.is_deleted && trip.date_created.Date >= startDate && trip.date_created.Date <= endDate
            select new CaptureActivityRow(
                "Trip Authority",
                trip.trip_authority_code.ToString(),
                vehicleMaster != null ? vehicleMaster.fleet_number : null,
                vehicleMaster != null ? vehicleMaster.registration_number : null,
                contract != null ? contract.site_code : (short?)null,
                trip.created_by_user_code,
                trip.date_created,
                trip.trip_reason))
            .ToListAsync(cancellationToken);

        var logsheetRows = await (
            from logsheet in _context.Logsheets.AsNoTracking()
            join vehicleMaster in _context.Vehicles.AsNoTracking() on logsheet.vmf_code equals vehicleMaster.vmf_code into logsheetVehicles
            from vehicleMaster in logsheetVehicles.DefaultIfEmpty()
            where !logsheet.is_deleted && logsheet.date_created.Date >= startDate && logsheet.date_created.Date <= endDate
            select new CaptureActivityRow(
                "ELS Logsheet",
                logsheet.log_code.ToString(),
                vehicleMaster != null ? vehicleMaster.fleet_number : null,
                vehicleMaster != null ? vehicleMaster.registration_number : null,
                logsheet.site_code,
                logsheet.created_by_user_code,
                logsheet.date_created,
                $"Month {logsheet.month:yyyy-MM}"))
            .ToListAsync(cancellationToken);

        var logbookRows = await (
            from logbook in _context.Logbooks.AsNoTracking()
            join vehicleMaster in _context.Vehicles.AsNoTracking() on logbook.vmf_code equals vehicleMaster.vmf_code into logbookVehicles
            from vehicleMaster in logbookVehicles.DefaultIfEmpty()
            where !logbook.is_deleted && logbook.date_created.Date >= startDate && logbook.date_created.Date <= endDate
            select new CaptureActivityRow(
                "Logbook",
                logbook.logbookcode.ToString(),
                vehicleMaster != null ? vehicleMaster.fleet_number : null,
                vehicleMaster != null ? vehicleMaster.registration_number : null,
                logbook.site_code,
                logbook.created_by_user_code,
                logbook.date_created,
                logbook.lb_comment))
            .ToListAsync(cancellationToken);

        IEnumerable<CaptureActivityRow> rows = contractRows
            .Concat(tripRows)
            .Concat(logsheetRows)
            .Concat(logbookRows);

        if (!string.Equals(module, "all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(row => string.Equals(row.Module, module, StringComparison.OrdinalIgnoreCase));
        }

        if (site.HasValue)
        {
            rows = rows.Where(row => row.SiteCode == site.Value);
        }

        if (!string.IsNullOrWhiteSpace(vehicle))
        {
            var term = vehicle.Trim();
            rows = rows.Where(row => Contains(row.GgNumber, term) || Contains(row.GpNumber, term));
        }

        if (!string.IsNullOrWhiteSpace(capturedBy))
        {
            var term = capturedBy.Trim();
            rows = rows.Where(row => (row.CapturedByUserCode?.ToString() ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var orderedRows = rows
            .OrderByDescending(row => row.DateCaptured)
            .ThenBy(row => row.Module)
            .Take(5000)
            .ToList();

        return CreateDynamicResult(
            "Capture Activity",
            "Modern report (no direct legacy equivalent)",
            true,
            "No direct legacy equivalent exists; this dynamic result stays modern but no longer uses hard-coded typed table columns.",
            orderedRows,
            Column("Module", row => row.Module),
            Column("Record ID", row => row.RecordId),
            Column("GG Number", row => row.GgNumber),
            Column("GP Number", row => row.GpNumber),
            Column("Site Code", row => row.SiteCode),
            Column("Captured By", row => row.CapturedByUserCode),
            Column("Date Captured", row => row.DateCaptured),
            Column("Description", row => row.Description));
    }

    private async Task<LegacyReportResultDto> BuildContractHistoryAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var vmfCode = GetInt(filters, "vmf");
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        var query =
            from contract in _context.Contracts.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on contract.vmf_code equals vehicle.vmf_code into contractVehicles
            from vehicle in contractVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on contract.site_code equals site.Site_code into contractSites
            from site in contractSites.DefaultIfEmpty()
            where !contract.is_deleted
            select new
            {
                contract.contract_code,
                contract.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                contract.start_date,
                contract.end_date,
                contract.still_current,
                contract.contract_type,
                contract.contract_status_code,
                Site = site != null ? site.description : null,
                contract.Driver_name,
                contract.Authorisation,
                contract.Notes
            };

        if (vmfCode.HasValue)
        {
            query = query.Where(row => row.vmf_code == vmfCode.Value);
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(search, mode, cancellationToken);
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var rows = await query
            .OrderByDescending(row => row.start_date)
            .ThenByDescending(row => row.contract_code)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Contract History",
            "Logs/RPT_Contracts_per_vehicle.aspx",
            true,
            "Legacy contract history is a composite page. This dynamic grid flattens the same legacy-backed contract data.",
            rows,
            Column("Contract Code", row => row.contract_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Site", row => row.Site),
            Column("Start Date", row => row.start_date),
            Column("End Date", row => row.end_date),
            Column("Still Current", row => row.still_current),
            Column("Contract Type", row => row.contract_type),
            Column("Status Code", row => row.contract_status_code),
            Column("Driver Name", row => row.Driver_name),
            Column("Authorisation", row => row.Authorisation),
            Column("Notes", row => row.Notes));
    }

    private async Task<LegacyReportResultDto> BuildContractsAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var stillCurrent = GetString(filters, "current");
        var siteCode = GetShort(filters, "site");
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var statusCode = GetShort(filters, "status");

        var query =
            from contract in _context.Contracts.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on contract.vmf_code equals vehicle.vmf_code into contractVehicles
            from vehicle in contractVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on contract.site_code equals site.Site_code into contractSites
            from site in contractSites.DefaultIfEmpty()
            where !contract.is_deleted
            select new
            {
                contract.contract_code,
                contract.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                SiteCode = contract.site_code,
                Site = site != null ? site.description : null,
                contract.start_date,
                contract.end_date,
                contract.target_return_date,
                contract.contract_type,
                contract.still_current,
                contract.contract_status_code,
                contract.Driver_name,
                contract.Authorisation,
                contract.Notes
            };

        if (!string.IsNullOrWhiteSpace(stillCurrent) && !string.Equals(stillCurrent, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(row => row.still_current == stillCurrent);
        }
        if (siteCode.HasValue)
        {
            query = query.Where(row => row.SiteCode == siteCode.Value);
        }
        if (statusCode.HasValue)
        {
            query = query.Where(row => row.contract_status_code == statusCode.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(row => row.start_date.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(row => row.start_date.Date <= to.Value);
        }

        var rows = await query
            .OrderByDescending(row => row.start_date)
            .ThenBy(row => row.fleet_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Contracts Report",
            "/FISReports/Contracts/Contracts.aspx",
            false,
            null,
            rows,
            Column("Contract Code", row => row.contract_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Site Code", row => row.SiteCode),
            Column("Site", row => row.Site),
            Column("Start Date", row => row.start_date),
            Column("End Date", row => row.end_date),
            Column("Target Return Date", row => row.target_return_date),
            Column("Still Current", row => row.still_current),
            Column("Status Code", row => row.contract_status_code),
            Column("Contract Type", row => row.contract_type),
            Column("Driver Name", row => row.Driver_name),
            Column("Authorisation", row => row.Authorisation),
            Column("Notes", row => row.Notes));
    }

    private async Task<LegacyReportResultDto> BuildDepartmentsSitesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var departmentCode = GetShort(filters, "dept");
        var status = (GetString(filters, "status") ?? "all").Trim();

        var query =
            from department in _context.Departments.AsNoTracking()
            join site in _context.Sites.AsNoTracking() on department.department_code equals site.Depatrment_code into departmentSites
            from site in departmentSites.DefaultIfEmpty()
            where !department.is_deleted && (site == null || !site.is_deleted)
            select new
            {
                department.department_code,
                Department = department.description,
                department.res_person,
                department.net_address,
                department.telephone,
                SiteCode = site != null ? site.Site_code : (short?)null,
                Site = site != null ? site.description : null,
                SiteActive = site != null ? site.site_active : (bool?)null,
                SiteContact = site != null ? site.res_person : null,
                SiteEmail = site != null ? site.net_address : null,
                SiteTelephone = site != null ? site.telephone : null
            };

        if (departmentCode.HasValue)
        {
            query = query.Where(row => row.department_code == departmentCode.Value);
        }

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var active = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(row => row.SiteActive == active);
        }

        var rows = await query
            .OrderBy(row => row.Department)
            .ThenBy(row => row.Site)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Departments and Sites",
            "Department/Department.aspx",
            true,
            "Legacy department maintenance is menu-driven. This result mirrors the underlying legacy department/site columns.",
            rows,
            Column("Department Code", row => row.department_code),
            Column("Department", row => row.Department),
            Column("Responsible Person", row => row.res_person),
            Column("Department Email", row => row.net_address),
            Column("Department Telephone", row => row.telephone),
            Column("Site Code", row => row.SiteCode),
            Column("Site", row => row.Site),
            Column("Site Active", row => row.SiteActive),
            Column("Site Contact", row => row.SiteContact),
            Column("Site Email", row => row.SiteEmail),
            Column("Site Telephone", row => row.SiteTelephone));
    }

    private async Task<LegacyReportResultDto> BuildFinesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var search = GetString(filters, "search");

        var query =
            from fine in _context.Fines.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on fine.vmf_code equals vehicle.vmf_code into fineVehicles
            from vehicle in fineVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on fine.Site_code equals site.Site_code into fineSites
            from site in fineSites.DefaultIfEmpty()
            where !fine.is_deleted
            select new
            {
                fine.Fine_code,
                fine.Offence_date,
                fine.Offence_reference,
                fine.Offence_issuer,
                fine.Fine_amount,
                fine.Pay_due_date,
                fine.Fine_pay_date,
                fine.Receive_gg_date,
                fine.Appear_date,
                fine.Offence_name,
                fine.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                fine.Site_code,
                Site = site != null ? site.description : null
            };

        if (from.HasValue)
        {
            query = query.Where(row => row.Offence_date.HasValue && row.Offence_date.Value.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(row => row.Offence_date.HasValue && row.Offence_date.Value.Date <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                (row.fleet_number != null && row.fleet_number.Contains(term)) ||
                (row.registration_number != null && row.registration_number.Contains(term)) ||
                (row.Offence_reference != null && row.Offence_reference.Contains(term)) ||
                (row.Offence_issuer != null && row.Offence_issuer.Contains(term)) ||
                row.Fine_code.ToString().Contains(term));
        }

        var rows = await query
            .OrderByDescending(row => row.Offence_date)
            .ThenByDescending(row => row.Fine_code)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Fines Reports",
            "Fines/RPTFines.aspx",
            true,
            "Legacy fines reports include multiple per-vehicle/per-department views. This approximation consolidates core fine records with vehicle, site, and traffic department fields.",
            rows,
            Column("Fine Code", row => row.Fine_code),
            Column("Offence Date", row => row.Offence_date),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Offence Reference", row => row.Offence_reference),
            Column("Offence Issuer", row => row.Offence_issuer),
            Column("Fine Amount", row => row.Fine_amount),
            Column("Pay Due Date", row => row.Pay_due_date),
            Column("Fine Pay Date", row => row.Fine_pay_date),
            Column("Receive GG Date", row => row.Receive_gg_date),
            Column("Appear Date", row => row.Appear_date),
            Column("Offence Name", row => row.Offence_name),
            Column("VMF Code", row => row.vmf_code),
            Column("Site Code", row => row.Site_code),
            Column("Site", row => row.Site));
    }

    private async Task<LegacyReportResultDto> BuildLossesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var search = GetString(filters, "search");

        var query =
            from loss in _context.Losses.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on loss.vmf_code equals vehicle.vmf_code into lossVehicles
            from vehicle in lossVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on loss.site_code equals site.Site_code into lossSites
            from site in lossSites.DefaultIfEmpty()
            join lossType in _context.LossTypes.AsNoTracking() on loss.loss_type_code equals lossType.loss_type_code into lossTypeRows
            from lossType in lossTypeRows.DefaultIfEmpty()
            where !loss.is_deleted
            select new
            {
                loss.loss_code,
                loss.loss_date,
                loss.loss_reference,
                loss.loss_amount,
                loss.dept_claim,
                loss.sapd,
                loss.inspector,
                loss.case_number,
                loss.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                loss.site_code,
                Site = site != null ? site.description : null,
                site.Department_number,
                DepartmentCode = site != null ? site.Depatrment_code : (short?)null,
                loss.loss_type_code,
                LossType = lossType != null ? lossType.loss_description : null
            };

        if (from.HasValue)
        {
            query = query.Where(row => row.loss_date.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(row => row.loss_date.Date <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                (row.fleet_number != null && row.fleet_number.Contains(term)) ||
                (row.registration_number != null && row.registration_number.Contains(term)) ||
                (row.loss_reference != null && row.loss_reference.Contains(term)) ||
                (row.case_number != null && row.case_number.Contains(term)) ||
                (row.sapd != null && row.sapd.Contains(term)) ||
                row.loss_code.ToString().Contains(term));
        }

        var rows = await query
            .OrderByDescending(row => row.loss_date)
            .ThenByDescending(row => row.loss_code)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Losses Reports",
            "Losses/RPTLosses.aspx",
            true,
            "Legacy losses reports include by-GG/by-site/by-date views. This approximation consolidates core loss records with vehicle, site, and loss type fields.",
            rows,
            Column("Loss Code", row => row.loss_code),
            Column("Loss Date", row => row.loss_date),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Loss Reference", row => row.loss_reference),
            Column("Case Number", row => row.case_number),
            Column("SAPD", row => row.sapd),
            Column("Inspector", row => row.inspector),
            Column("Loss Amount", row => row.loss_amount),
            Column("Department Claim", row => row.dept_claim),
            Column("VMF Code", row => row.vmf_code),
            Column("Site Code", row => row.site_code),
            Column("Site", row => row.Site),
            Column("Department Number", row => row.Department_number),
            Column("Department Code", row => row.DepartmentCode),
            Column("Loss Type Code", row => row.loss_type_code),
            Column("Loss Type", row => row.LossType));
    }

    private async Task<LegacyReportResultDto> BuildHighDistanceAllAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        return await BuildHighDistanceAsync(filters, null, "All vehicles with high distances (All Departments)", "ShowReport.aspx?Item=KiloAudit", cancellationToken);
    }

    private async Task<LegacyReportResultDto> BuildHighDistanceDeptAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var departmentCode = GetShort(filters, "dept") ?? GetShort(filters, "DepartmentID");
        return await BuildHighDistanceAsync(filters, departmentCode, "All vehicles with high distances (Department)", "ShowReport.aspx?Item=KiloAudit&DepartmentID=...", cancellationToken);
    }

    private async Task<LegacyReportResultDto> BuildHighDistanceAsync(
        IDictionary<string, string?> filters,
        short? departmentCode,
        string title,
        string legacyTarget,
        CancellationToken cancellationToken)
    {
        var threshold = GetInt(filters, "threshold") ?? 5000;

        var activeContracts = _context.Contracts.AsNoTracking().Where(contract => !contract.is_deleted && contract.still_current == "Y");
        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into vehicleStatuses
            from status in vehicleStatuses.DefaultIfEmpty()
            join contract in activeContracts on vehicle.vmf_code equals contract.vmf_code into vehicleContracts
            from contract in vehicleContracts.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on contract.site_code equals site.Site_code into contractSites
            from site in contractSites.DefaultIfEmpty()
            where !vehicle.is_deleted && ((vehicle.highest_km ?? 0) >= threshold || vehicle.current_odo >= threshold)
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                CurrentOdo = vehicle.current_odo,
                HighestKm = vehicle.highest_km,
                Status = status != null ? status.status_description : null,
                DepartmentCode = site != null ? site.Depatrment_code : null,
                Site = site != null ? site.description : null,
                contract.site_code
            };

        if (departmentCode.HasValue)
        {
            query = query.Where(row => row.DepartmentCode == departmentCode.Value);
        }

        var rows = await query
            .OrderByDescending(row => row.HighestKm ?? row.CurrentOdo)
            .ThenBy(row => row.fleet_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            title,
            legacyTarget,
            false,
            null,
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Current ODO", row => row.CurrentOdo),
            Column("Highest KM", row => row.HighestKm),
            Column("Status", row => row.Status),
            Column("Department Code", row => row.DepartmentCode),
            Column("Site Code", row => row.site_code),
            Column("Site", row => row.Site));
    }

    private async Task<LegacyReportResultDto> BuildIncorrectQuantitiesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var rows = await _context.Vehicles.AsNoTracking()
            .Where(vehicle => !vehicle.is_deleted
                && (vehicle.take_on_odo < 0
                    || vehicle.current_odo < 0
                    || vehicle.current_odo < vehicle.take_on_odo
                    || (vehicle.average_consumption.HasValue && vehicle.average_consumption.Value < 0)
                    || (vehicle.highest_km.HasValue && vehicle.highest_km.Value < 0)
                    || (vehicle.km_3month_average.HasValue && vehicle.km_3month_average.Value < 0)))
            .OrderBy(vehicle => vehicle.fleet_number)
            .ThenBy(vehicle => vehicle.registration_number)
            .Take(5000)
            .Select(vehicle => new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.take_on_odo,
                vehicle.current_odo,
                vehicle.highest_km,
                vehicle.average_consumption,
                vehicle.km_3month_average,
                Reason =
                    vehicle.current_odo < vehicle.take_on_odo ? "Current odometer is less than take-on odometer." :
                    vehicle.take_on_odo < 0 ? "Take-on odometer is negative." :
                    vehicle.current_odo < 0 ? "Current odometer is negative." :
                    (vehicle.average_consumption.HasValue && vehicle.average_consumption.Value < 0) ? "Average consumption is negative." :
                    (vehicle.highest_km.HasValue && vehicle.highest_km.Value < 0) ? "Highest KM is negative." :
                    (vehicle.km_3month_average.HasValue && vehicle.km_3month_average.Value < 0) ? "3-month KM average is negative." :
                    "Quantity anomaly detected."
            })
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Report to show incorrect calculated quantities",
            "Finance/GeneratedReports.aspx?key=9.3%20Report%20to%20show%20incorrect%20calculated%20quantities",
            true,
            "Legacy generated report output is approximated from vehicle odometer and quantity-related fields in vehicle_master.",
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Take On ODO", row => row.take_on_odo),
            Column("Current ODO", row => row.current_odo),
            Column("Highest KM", row => row.highest_km),
            Column("Average Consumption", row => row.average_consumption),
            Column("3-Month KM Average", row => row.km_3month_average),
            Column("Reason", row => row.Reason));
    }

    private async Task<LegacyReportResultDto> BuildLicencesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var mode = (GetString(filters, "mode") ?? "GG").Trim().ToUpperInvariant();
        var statusFilter = (GetString(filters, "status") ?? "all").Trim().ToLowerInvariant();
        var locationFilter = (GetString(filters, "location") ?? "all").Trim().ToLowerInvariant();
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking() on vehicle.model_code equals model.model_code into models
            from model in models.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking() on vehicle.type_code equals type.type_code into types
            from type in types.DefaultIfEmpty()
            join garageSite in _context.Sites.AsNoTracking() on vehicle.location_code equals garageSite.Site_code into garageSites
            from garageSite in garageSites.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on vehicle.Licence_receiver_site equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            join fee in _context.LicenseFees.AsNoTracking() on model.licence_fee_code equals fee.licence_fee_code into fees
            from fee in fees.DefaultIfEmpty()
            where !vehicle.is_deleted
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.lic_register_number,
                vehicle.lic_registration_doc,
                vehicle.licence_due_date,
                vehicle.cof_required,
                vehicle.cof_last_done,
                CofAmount = vehicle.Cof_amount,
                LicenceReceiver = vehicle.Licence_receiver,
                LicenceReceiverId = vehicle.Licence_receiver_id,
                LicenceReceiverTel = vehicle.Licence_receiver_tel,
                LicenceReceiverSiteCode = vehicle.Licence_receiver_site,
                LicenceDateTaken = vehicle.Licence_date_taken,
                vehicle.licence_comments,
                vehicle.engine_number_1,
                vehicle.chassis_number,
                vehicle.vehicle_status_code,
                Status = status != null ? status.status_description : null,
                vehicle.location_code,
                Garage = garageSite != null ? garageSite.description : null,
                vehicle.type_code,
                Type = type != null ? type.type_description : null,
                Site = site != null ? site.description : null,
                site.Department_number,
                LicenceDescription = fee != null ? fee.licence_description : null,
                LicenceFee = fee != null ? fee.licence_fee : null
            };

        if (statusFilter == "inservice")
        {
            query = query.Where(row => row.vehicle_status_code == 1);
        }
        else if (statusFilter == "notinservice")
        {
            query = query.Where(row => row.vehicle_status_code != 1);
        }

        if (locationFilter == "jhb")
        {
            query = query.Where(row => row.location_code == 1);
        }
        else if (locationFilter == "pta")
        {
            query = query.Where(row => row.location_code == 2);
        }

        if (from.HasValue)
        {
            query = query.Where(row => row.licence_due_date.HasValue && row.licence_due_date.Value.Date >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(row => row.licence_due_date.HasValue && row.licence_due_date.Value.Date <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = mode switch
            {
                "GP" => query.Where(row => row.registration_number != null && row.registration_number.Contains(term)),
                "REGISTER" => query.Where(row => row.lic_register_number != null && row.lic_register_number.Contains(term)),
                "ENGINE" => query.Where(row => row.engine_number_1 != null && row.engine_number_1.Contains(term)),
                "CHASSIS" => query.Where(row => row.chassis_number != null && row.chassis_number.Contains(term)),
                "VIN" => query.Where(row => row.chassis_number != null && row.chassis_number.Contains(term)),
                _ => query.Where(row => row.fleet_number != null && row.fleet_number.Contains(term))
            };
        }

        var rows = await query
            .OrderBy(row => row.fleet_number)
            .ThenBy(row => row.registration_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Licence Reports",
            "License/RPTLicence.aspx",
            true,
            "Legacy licence module includes many report branches (GG/GP/register/engine/chassis/site/date). This approximation keeps the core licence record fields and lookup modes in one modern dynamic grid.",
            rows,
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Vehicle Register Number", row => row.lic_register_number),
            Column("Register Doc", row => row.lic_registration_doc),
            Column("Licence Due Date", row => row.licence_due_date),
            Column("COF Required", row => row.cof_required),
            Column("COF Last Done", row => row.cof_last_done),
            Column("COF Amount", row => row.CofAmount),
            Column("Licence Receiver", row => row.LicenceReceiver),
            Column("Licence Receiver ID", row => row.LicenceReceiverId),
            Column("Licence Receiver Tel", row => row.LicenceReceiverTel),
            Column("Licence Receiver Site Code", row => row.LicenceReceiverSiteCode),
            Column("Licence Receiver Site", row => row.Site),
            Column("Department Number", row => row.Department_number),
            Column("Licence Date Taken", row => row.LicenceDateTaken),
            Column("Licence Comments", row => row.licence_comments),
            Column("Engine Number", row => row.engine_number_1),
            Column("VIN / Chassis Number", row => row.chassis_number),
            Column("Status Code", row => row.vehicle_status_code),
            Column("Status", row => row.Status),
            Column("Location Code", row => row.location_code),
            Column("Garage", row => row.Garage),
            Column("Type Code", row => row.type_code),
            Column("Type", row => row.Type),
            Column("Licence Description", row => row.LicenceDescription),
            Column("Licence Fee", row => row.LicenceFee),
            Column("VMF Code", row => row.vmf_code));
    }

    private async Task<LegacyReportResultDto> BuildManagementAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var asOf = GetDate(filters, "asof")?.Date ?? DateTime.Today.Date;

        var rows = await (
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            where !vehicle.is_deleted && vehicle.take_on_date.Date <= asOf
            group vehicle by new
            {
                vehicle.vehicle_status_code,
                Status = status != null ? status.status_description : null
            }
            into grouped
            orderby grouped.Key.Status
            select new
            {
                AsOf = asOf,
                grouped.Key.vehicle_status_code,
                grouped.Key.Status,
                VehicleCount = grouped.Count(),
                AverageOdo = grouped.Average(row => (double?)row.current_odo),
                MaxOdo = grouped.Max(row => (int?)row.current_odo),
                AvgPurchaseAmount = grouped.Average(row => (decimal?)row.purchase_amount)
            })
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Management Reports",
            "Management_Reports/Management.aspx",
            true,
            "Legacy management reporting is menu-driven. This dynamic approximation summarizes vehicle counts by status as of the requested date.",
            rows,
            Column("As Of Date", row => row.AsOf),
            Column("Status Code", row => row.vehicle_status_code),
            Column("Status", row => row.Status),
            Column("Vehicle Count", row => row.VehicleCount),
            Column("Average ODO", row => row.AverageOdo),
            Column("Maximum ODO", row => row.MaxOdo),
            Column("Average Purchase Amount", row => row.AvgPurchaseAmount));
    }

    private Task<LegacyReportResultDto> BuildManualsAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var rows = new[]
        {
            new { Sequence = "1", Manual = "General Information", Target = "manuals/RPT_underdev.aspx", Availability = "Under Development" },
            new { Sequence = "2", Manual = "Accident Manual", Target = "Accident/Doc/Doc_Accidents.htm", Availability = "Available" },
            new { Sequence = "3", Manual = "Auction Manual", Target = "Auction/Doc/Doc_Auctions.htm", Availability = "Available" },
            new { Sequence = "4", Manual = "Call Centre Manual", Target = "CallCentre/Doc/Doc_CallCentre.htm", Availability = "Available" },
            new { Sequence = "5", Manual = "Contract Manual", Target = "contracts/Docs/User Documentation for Contracts Module.html", Availability = "Available" },
            new { Sequence = "6", Manual = "Electronic Log Sheet and Trip Authority Training Manual", Target = "Docs/Electronic Log Sheet and Trip Authority Training Manual.doc", Availability = "Available" },
            new { Sequence = "7", Manual = "Financial Manual", Target = "manuals/RPT_underdev.aspx", Availability = "Under Development" },
            new { Sequence = "8", Manual = "Fines Manual", Target = "Fines/Doc/Doc_Fines.htm", Availability = "Available" },
            new { Sequence = "9", Manual = "Fuelcard Manual", Target = "Fuelcard/Doc/Doc_Fuelcards.htm", Availability = "Available" },
            new { Sequence = "10", Manual = "Licence Manual", Target = "License/Doc/DOC_LICENCE.htm", Availability = "Available" },
            new { Sequence = "11", Manual = "Logbook Manual", Target = "Logbook/Doc/Doc_Logbooks.htm", Availability = "Available" },
            new { Sequence = "12", Manual = "Logsheet Manual", Target = "Logs/Doc/Doc_Logsheets.htm", Availability = "Available" },
            new { Sequence = "13", Manual = "Losses Manual", Target = "Losses/Doc/Doc_Losses.htm", Availability = "Available" },
            new { Sequence = "14", Manual = "Private Hire Manual", Target = "Private_Hire/Doc/Doc_PrivateHire.htm", Availability = "Available" },
            new { Sequence = "15", Manual = "Reports Manual", Target = "/Doc/Doc_Reports.htm", Availability = "Available" },
            new { Sequence = "16", Manual = "Taxis Manual", Target = "Taxis/Doc/Doc_taxis.htm", Availability = "Available" },
            new { Sequence = "17", Manual = "Trip Authority Manual", Target = "manuals/RPT_underdev.aspx", Availability = "Under Development" },
            new { Sequence = "18", Manual = "Updating Trip Authorities Manual", Target = "Docs/Doc/Updating Trip Authorities Manual2.htm", Availability = "Available" },
            new { Sequence = "19", Manual = "Troubleshoot Manual", Target = "TS_Log/Doc/Doc_Troubleshoot.htm", Availability = "Available" },
            new { Sequence = "20", Manual = "User Admin Manual", Target = "/Doc/Doc_UserAdmin.htm", Availability = "Available" },
            new { Sequence = "21", Manual = "Validation Data Manual", Target = "Validation/Doc/Doc_ValidationData.htm", Availability = "Available" },
            new { Sequence = "22", Manual = "Vehicle Manual", Target = "manuals/RPT_underdev.aspx", Availability = "Under Development" },
            new { Sequence = "23", Manual = "Workshop Manual", Target = "Workshop/Doc/Doc_Workshop.htm", Availability = "Available" }
        };

        return Task.FromResult(CreateDynamicResult(
            "Manuals Menu",
            "Manuals/RPTmanuals.aspx",
            false,
            null,
            rows,
            Column("Sequence", row => row.Sequence),
            Column("Manual", row => row.Manual),
            Column("Target", row => row.Target),
            Column("Availability", row => row.Availability)));
    }

    private Task<LegacyReportResultDto> BuildPreviousFinYearMenuAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var rows = new[]
        {
            new
            {
                Sequence = "i",
                ReportName = "Manual Logsheet Kilos captured in current Fin Year",
                ReportKey = "previous-fin-year-manual-logs",
                LegacyItem = "PreviousFinYearManualLogsCapturedInCurrentFinYear"
            },
            new
            {
                Sequence = "ii",
                ReportName = "VIP & Taxi requisitions captured in current Fin Year",
                ReportKey = "previous-fin-year-vip-taxi",
                LegacyItem = "PreviousFinYearKiloLogsCapturedInCurrentFinYear"
            }
        };

        return Task.FromResult(CreateDynamicResult(
            "Previous Fin Year Reports",
            "Finance/PreviousFinYear.aspx",
            false,
            null,
            rows,
            Column("Sequence", row => row.Sequence),
            Column("Report Name", row => row.ReportName),
            Column("Report Key", row => row.ReportKey),
            Column("Legacy Item", row => row.LegacyItem)));
    }

    private async Task<LegacyReportResultDto> BuildPreviousFinYearManualLogsAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var currentFinancialYear = GetFinancialYearKey(DateTime.Today);

        var rows = await (
            from logsheet in _context.Logsheets.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on logsheet.vmf_code equals vehicle.vmf_code into logVehicles
            from vehicle in logVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on logsheet.site_code equals site.Site_code into logSites
            from site in logSites.DefaultIfEmpty()
            where !logsheet.is_deleted
                && GetFinancialYearKey(logsheet.date_created) == currentFinancialYear
                && GetFinancialYearKey(logsheet.month) < currentFinancialYear
            orderby logsheet.date_created descending, logsheet.log_code descending
            select new
            {
                logsheet.log_code,
                logsheet.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                logsheet.month,
                logsheet.date_created,
                logsheet.rek_num,
                logsheet.start_odo,
                logsheet.end_odo,
                SiteCode = site != null ? site.Site_code : (short?)null,
                Site = site != null ? site.description : null
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Previous Fin Year Manual Logsheet Kilos Captured in Current Fin Year",
            "ShowReport.aspx?Item=PreviousFinYearManualLogsCapturedInCurrentFinYear",
            false,
            null,
            rows,
            Column("Log Code", row => row.log_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Logsheet Month", row => row.month),
            Column("Captured Date", row => row.date_created),
            Column("Requisition Number", row => row.rek_num),
            Column("Start ODO", row => row.start_odo),
            Column("End ODO", row => row.end_odo),
            Column("Site Code", row => row.SiteCode),
            Column("Site", row => row.Site));
    }

    private async Task<LegacyReportResultDto> BuildPreviousFinYearVipTaxiAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var currentFinancialYear = GetFinancialYearKey(DateTime.Today);

        var rows = await (
            from taxi in _context.Taxis.AsNoTracking()
            join department in _context.Departments.AsNoTracking() on taxi.department_code equals department.department_code into taxiDepartments
            from department in taxiDepartments.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on taxi.site_code equals site.Site_code into taxiSites
            from site in taxiSites.DefaultIfEmpty()
            where !taxi.is_deleted
                && GetFinancialYearKey(taxi.date_created) == currentFinancialYear
                && GetFinancialYearKey(taxi.date_required) < currentFinancialYear
            orderby taxi.date_created descending, taxi.request_id descending
            select new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.official,
                taxi.rank,
                taxi.vmf_code,
                taxi.date_required,
                taxi.date_created,
                taxi.contractor_id,
                DepartmentCode = department != null ? department.department_code : (short?)null,
                Department = department != null ? department.description : null,
                SiteCode = site != null ? site.Site_code : (short?)null,
                Site = site != null ? site.description : null
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Previous Fin Year VIP & Taxi Requisitions Captured in Current Fin Year",
            "ShowReport.aspx?Item=PreviousFinYearKiloLogsCapturedInCurrentFinYear",
            false,
            null,
            rows,
            Column("Request ID", row => row.request_id),
            Column("Requisition Number", row => row.rek_num),
            Column("Official", row => row.official),
            Column("Rank", row => row.rank),
            Column("Vehicle", row => row.vmf_code),
            Column("Date Required", row => row.date_required),
            Column("Captured Date", row => row.date_created),
            Column("Contractor ID", row => row.contractor_id),
            Column("Department Code", row => row.DepartmentCode),
            Column("Department", row => row.Department),
            Column("Site Code", row => row.SiteCode),
            Column("Site", row => row.Site));
    }

    private async Task<LegacyReportResultDto> BuildRegistrationCertificatesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";
        var showSingleVehicle = !string.IsNullOrWhiteSpace(search);

        var query =
            from scanDoc in _context.ScanDocs.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on scanDoc.vmf_code equals vehicle.vmf_code
            where !scanDoc.is_deleted && !vehicle.is_deleted
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                scanDoc.period_begin,
                scanDoc.period_end,
                scanDoc.image,
                DateUploaded = scanDoc.date_updated ?? scanDoc.date_created
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(search, mode, cancellationToken);
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var rows = await query
            .OrderBy(row => row.fleet_number)
            .ThenBy(row => row.period_begin)
            .ThenBy(row => row.registration_number)
            .Take(5000)
            .Select(row => new
            {
                row.vmf_code,
                row.fleet_number,
                row.registration_number,
                row.period_begin,
                row.period_end,
                row.DateUploaded,
                row.image
            })
            .ToListAsync(cancellationToken);

        if (showSingleVehicle)
        {
            return CreateDynamicResult(
                $"Registration Certificate for {search!.Trim().ToUpperInvariant()}",
                "ScanDocs/ListOne2.aspx",
                false,
                null,
                rows,
                Column("From", row => row.period_begin),
                Column("To", row => row.period_end),
                Column("Registration Certificate", row => row.image));
        }

        return CreateDynamicResult(
            "Registration Certificates for All Vehicles",
            "ScanDocs/RPT_ListAll2.aspx",
            false,
            null,
            rows,
            Column("Fleet Number", row => row.fleet_number),
            Column("Registration Number", row => row.registration_number),
            Column("From", row => row.period_begin),
            Column("To", row => row.period_end),
            Column("Date Uploaded", row => row.DateUploaded),
            Column("Registration Certificate", row => row.image));
    }

    private async Task<LegacyReportResultDto> BuildTariffsClass2007Async(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var max = Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000);
        var rows = await _context.Tariffs.AsNoTracking()
            .Where(tariff => !tariff.is_deleted && (tariff.year_manufactured ?? 0) <= 2007)
            .OrderBy(tariff => tariff.class_code)
            .ThenBy(tariff => tariff.year_manufactured)
            .Take(max)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Published Tariffs (2007 and Earlier)",
            "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
            false,
            null,
            rows,
            Column("Tariff Code", row => row.tariff_code),
            Column("Class Code", row => row.class_code),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Monthly Fixed Amount", row => row.monthly_fixed_amount),
            Column("Monthly ODO Amount", row => row.monthly_odo_amount),
            Column("Daily Fixed Amount", row => row.daily_fixed_amount),
            Column("Hourly Fixed Amount", row => row.hourly_fixed_amount),
            Column("Fuel Kilo Tariff", row => row.fuel_kilo_tariff),
            Column("Effective Start Date", row => row.effective_start_date),
            Column("Effective End Date", row => row.effective_end_date));
    }

    private async Task<LegacyReportResultDto> BuildTariffsFinYearAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var max = Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000);

        var query = _context.Tariffs.AsNoTracking().Where(tariff => !tariff.is_deleted);
        if (from.HasValue)
        {
            query = query.Where(tariff => tariff.effective_start_date.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(tariff => tariff.effective_start_date.Date <= to.Value);
        }

        var rows = await query
            .OrderByDescending(tariff => tariff.effective_start_date)
            .ThenBy(tariff => tariff.class_code)
            .Take(max)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Published Tariffs by Financial Year",
            "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
            false,
            null,
            rows,
            Column("Tariff Code", row => row.tariff_code),
            Column("Class Code", row => row.class_code),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Monthly Fixed Amount", row => row.monthly_fixed_amount),
            Column("Monthly ODO Amount", row => row.monthly_odo_amount),
            Column("Fuel Kilo Tariff", row => row.fuel_kilo_tariff),
            Column("Effective Start Date", row => row.effective_start_date),
            Column("Effective End Date", row => row.effective_end_date),
            Column("Approval Status", row => row.tariff_approval_status));
    }

    private async Task<LegacyReportResultDto> BuildTariffsPerVehicleAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var max = Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000);

        var rows = await (
            from tariff in _context.VehicleTariffs.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on tariff.vmf_code equals vehicle.vmf_code into tariffVehicles
            from vehicle in tariffVehicles.DefaultIfEmpty()
            where !tariff.is_deleted && vehicle != null && (vehicle.year_manufactured ?? 0) >= 2008
            orderby vehicle.fleet_number, vehicle.registration_number, tariff.start_date descending
            select new
            {
                tariff.vehicle_tariff_code,
                tariff.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.year_manufactured,
                tariff.parameter_year,
                tariff.start_date,
                tariff.end_date,
                tariff.vehicle_fixed_tariff,
                tariff.vehicle_fixed_daily_tariff,
                tariff.vehicle_kilometer_tariff,
                tariff.fuel_kilo_tariff,
                tariff.maintenance_kilometer_amount,
                tariff.comment
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Tariffs per Vehicle",
            "ShowReport.aspx?Item=TariffsPerVehicle",
            false,
            null,
            rows,
            Column("Vehicle Tariff Code", row => row.vehicle_tariff_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Parameter Year", row => row.parameter_year),
            Column("Start Date", row => row.start_date),
            Column("End Date", row => row.end_date),
            Column("Vehicle Fixed Tariff", row => row.vehicle_fixed_tariff),
            Column("Vehicle Daily Tariff", row => row.vehicle_fixed_daily_tariff),
            Column("Vehicle Kilometer Tariff", row => row.vehicle_kilometer_tariff),
            Column("Fuel Kilo Tariff", row => row.fuel_kilo_tariff),
            Column("Maintenance KM Amount", row => row.maintenance_kilometer_amount),
            Column("Comment", row => row.comment));
    }

    private Task<LegacyReportResultDto> BuildTaxisMenuAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var rows = new[]
        {
            new { Sequence = "1", ReportName = "Future booking made for my department", LegacyTarget = "Taxis/RPT_My_reqs.aspx", ReportKey = "taxis-future-bookings" },
            new { Sequence = "2", ReportName = "History bookings for period", LegacyTarget = "Taxis/RPT_My_reqs2.aspx", ReportKey = "taxis-history-bookings" },
            new { Sequence = "3", ReportName = "List of Requisition Numbers for a period", LegacyTarget = "Taxis/RPT_reqno_taxi_main.aspx", ReportKey = "taxis-requisition-list" },
            new { Sequence = "4", ReportName = "Taxis Per Hire Company", LegacyTarget = "Taxis/RPT_taxis_per_company1_c.aspx", ReportKey = "taxis-per-hire-company" },
            new { Sequence = "5", ReportName = "List Of all Taxis in service in various departments", LegacyTarget = "Taxis/RPT_list_of_taxis_inservice_per_department.aspx", ReportKey = "taxis-list-inservice-per-department" },
            new { Sequence = "6", ReportName = "List Of all Taxis in various departments", LegacyTarget = "Taxis/RPT_list_of_taxis_per_department.aspx", ReportKey = "taxis-list-per-department" },
            new { Sequence = "7", ReportName = "Reprint A Requisition", LegacyTarget = "Taxis/Report_Request_GGVIP_reprint_1_2.aspx", ReportKey = "taxis-reprint-requisition" },
            new { Sequence = "8", ReportName = "Reprint A Taxi Log", LegacyTarget = "Taxis/Report_Reprint_Taxi_Log_1.aspx", ReportKey = "taxis-reprint-log" }
        };

        return Task.FromResult(CreateDynamicResult(
            "Taxi Reports Menu",
            "Taxis/RPTtaxis.aspx",
            false,
            null,
            rows,
            Column("Sequence", row => row.Sequence),
            Column("Report Name", row => row.ReportName),
            Column("Legacy Target", row => row.LegacyTarget),
            Column("Report Key", row => row.ReportKey)));
    }

    private async Task<LegacyReportResultDto> BuildTaxisListPerDepartmentAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");

        var query =
            from taxi in _context.Taxis.AsNoTracking()
            join department in _context.Departments.AsNoTracking() on taxi.department_code equals department.department_code into taxiDepartments
            from department in taxiDepartments.DefaultIfEmpty()
            where !taxi.is_deleted
            select new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.department_code,
                Department = department != null ? department.description : null,
                taxi.vmf_code
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                (row.rek_num != null && row.rek_num.Contains(term)) ||
                (row.Department != null && row.Department.Contains(term)) ||
                (row.vmf_code != null && row.vmf_code.Contains(term)) ||
                row.request_id.ToString().Contains(term));
        }

        var rows = await query
            .OrderBy(row => row.Department)
            .ThenBy(row => row.rek_num)
            .Select(row => new
            {
                row.request_id,
                row.rek_num,
                row.department_code,
                row.Department,
                row.vmf_code
            })
            .Distinct()
            .Take(5000)
            .ToListAsync(cancellationToken);

        var numberedRows = rows
            .Select((row, index) => new
            {
                Number = index + 1,
                row.rek_num,
                row.department_code,
                row.Department,
                row.vmf_code,
                row.request_id
            })
            .ToList();

        return CreateDynamicResult(
            "Report On All Taxis in various Departments",
            "Taxis/RPT_list_of_taxis_per_department.aspx",
            true,
            "Legacy report joins taxis via vehicle/logsheet to derive department text. This approximation uses taxis + department mappings and preserves requisition + department output.",
            numberedRows,
            Column("No.", row => row.Number),
            Column("Requisition Number", row => row.rek_num),
            Column("Department Code", row => row.department_code),
            Column("Department", row => row.Department),
            Column("VMF Code", row => row.vmf_code),
            Column("Request ID", row => row.request_id));
    }

    private async Task<LegacyReportResultDto> BuildTaxisListInServicePerDepartmentAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");

        var query =
            from taxi in _context.Taxis.AsNoTracking()
            join department in _context.Departments.AsNoTracking() on taxi.department_code equals department.department_code into taxiDepartments
            from department in taxiDepartments.DefaultIfEmpty()
            join vehicle in _context.Vehicles.AsNoTracking() on taxi.vmf_code equals vehicle.vmf_code.ToString() into taxiVehicles
            from vehicle in taxiVehicles.DefaultIfEmpty()
            where !taxi.is_deleted && vehicle != null && vehicle.vehicle_status_code == 1
            select new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.department_code,
                Department = department != null ? department.description : null,
                taxi.vmf_code
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                (row.rek_num != null && row.rek_num.Contains(term)) ||
                (row.Department != null && row.Department.Contains(term)) ||
                (row.vmf_code != null && row.vmf_code.Contains(term)) ||
                row.request_id.ToString().Contains(term));
        }

        var rows = await query
            .OrderBy(row => row.Department)
            .ThenBy(row => row.rek_num)
            .Distinct()
            .Take(5000)
            .ToListAsync(cancellationToken);

        var numberedRows = rows
            .Select((row, index) => new
            {
                Number = index + 1,
                row.rek_num,
                row.department_code,
                row.Department,
                row.vmf_code,
                row.request_id
            })
            .ToList();

        return CreateDynamicResult(
            "Report On All Taxis in service in various Departments",
            "Taxis/RPT_list_of_taxis_inservice_per_department.aspx",
            true,
            "Legacy report filters on vehicle_status_code = 1. This approximation applies the same in-service status constraint.",
            numberedRows,
            Column("No.", row => row.Number),
            Column("Requisition Number", row => row.rek_num),
            Column("Department Code", row => row.department_code),
            Column("Department", row => row.Department),
            Column("VMF Code", row => row.vmf_code),
            Column("Request ID", row => row.request_id));
    }

    private async Task<LegacyReportResultDto> BuildTaxisFinancialAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var query =
            from taxi in _context.Taxis.AsNoTracking()
            join department in _context.Departments.AsNoTracking() on taxi.department_code equals department.department_code into taxiDepartments
            from department in taxiDepartments.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on taxi.site_code equals site.Site_code into taxiSites
            from site in taxiSites.DefaultIfEmpty()
            where !taxi.is_deleted
            select new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.official,
                taxi.rank,
                taxi.vmf_code,
                Company = taxi.address_1,
                taxi.date_required,
                Department = department != null ? department.description : null,
                Site = site != null ? site.description : null,
                taxi.contractor_id,
                taxi.flight,
                taxi.address_2,
                taxi.address_3
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                row.request_id.ToString().Contains(term) ||
                (row.rek_num != null && row.rek_num.Contains(term)) ||
                (row.official != null && row.official.Contains(term)) ||
                (row.vmf_code != null && row.vmf_code.Contains(term)) ||
                (row.Company != null && row.Company.Contains(term)));
        }

        var rows = await query
            .OrderByDescending(row => row.date_required)
            .ThenByDescending(row => row.request_id)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Financial Reports: Taxis",
            "Taxis/Taxi_Fin_reports.aspx",
            true,
            "Legacy taxi financial report pages are custom. This dynamic approximation uses the underlying legacy taxi request data.",
            rows,
            Column("Request ID", row => row.request_id),
            Column("Requisition Number", row => row.rek_num),
            Column("Official", row => row.official),
            Column("Rank", row => row.rank),
            Column("Vehicle", row => row.vmf_code),
            Column("Company", row => row.Company),
            Column("Date Required", row => row.date_required),
            Column("Department", row => row.Department),
            Column("Site", row => row.Site),
            Column("Contractor ID", row => row.contractor_id),
            Column("Flight", row => row.flight),
            Column("Address 2", row => row.address_2),
            Column("Address 3", row => row.address_3));
    }

    private async Task<LegacyReportResultDto> BuildTripAuthorityAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;

        var query =
            from trip in _context.Trips.AsNoTracking()
            join contract in _context.Contracts.AsNoTracking() on trip.contract_code equals contract.contract_code into tripContracts
            from contract in tripContracts.DefaultIfEmpty()
            join vehicle in _context.Vehicles.AsNoTracking() on contract.vmf_code equals vehicle.vmf_code into tripVehicles
            from vehicle in tripVehicles.DefaultIfEmpty()
            where !trip.is_deleted
            select new
            {
                trip.trip_authority_code,
                trip.trip_request_number,
                trip.issue_date,
                trip.expiry_date,
                trip.trip_reason,
                trip.approver_name,
                trip.approver_rank,
                trip.approver_tel,
                trip.trip_type_code,
                trip.trip_incident_type_code,
                trip.user_access_code,
                contract.contract_code,
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number
            };

        if (from.HasValue)
        {
            query = query.Where(row => row.issue_date.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(row => row.issue_date.Date <= to.Value);
        }

        var rows = await query
            .OrderByDescending(row => row.issue_date)
            .ThenByDescending(row => row.trip_authority_code)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Trip Authority Report",
            "TripReports.aspx / ShowReport multiple items",
            true,
            "Legacy trip authority reporting fans into multiple pages. This dynamic approximation flattens trip_authorities data into one legacy-style grid.",
            rows,
            Column("Trip Authority Code", row => row.trip_authority_code),
            Column("Trip Request Number", row => row.trip_request_number),
            Column("Issue Date", row => row.issue_date),
            Column("Expiry Date", row => row.expiry_date),
            Column("Trip Reason", row => row.trip_reason),
            Column("Approver Name", row => row.approver_name),
            Column("Approver Rank", row => row.approver_rank),
            Column("Approver Tel", row => row.approver_tel),
            Column("Trip Type Code", row => row.trip_type_code),
            Column("Trip Incident Type Code", row => row.trip_incident_type_code),
            Column("User Access Code", row => row.user_access_code),
            Column("Contract Code", row => row.contract_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number));
    }

    private async Task<LegacyReportResultDto> BuildTripsOpen31Async(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var days = Math.Max(1, GetInt(filters, "days") ?? 31);
        var cutoffDate = DateTime.Today.AddDays(-days);

        var rows = await (
            from trip in _context.Trips.AsNoTracking()
            join contract in _context.Contracts.AsNoTracking() on trip.contract_code equals contract.contract_code into tripContracts
            from contract in tripContracts.DefaultIfEmpty()
            join vehicle in _context.Vehicles.AsNoTracking() on contract.vmf_code equals vehicle.vmf_code into tripVehicles
            from vehicle in tripVehicles.DefaultIfEmpty()
            where !trip.is_deleted
                && trip.issue_date.Date <= cutoffDate
                && (!trip.expiry_date.HasValue || trip.expiry_date >= DateTime.Today.Date)
            orderby trip.issue_date, trip.trip_authority_code
            select new
            {
                trip.trip_authority_code,
                trip.contract_code,
                trip.issue_date,
                trip.expiry_date,
                trip.trip_request_number,
                trip.trip_reason,
                trip.approver_name,
                trip.approver_rank,
                trip.approver_tel,
                trip.trip_type_code,
                trip.trip_incident_type_code,
                trip.end_odo_meter,
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Trips Open for Over 31 Days",
            "ShowReport.aspx?Item=TripsOpenForOver31Days",
            false,
            null,
            rows,
            Column("Trip Authority Code", row => row.trip_authority_code),
            Column("Contract Code", row => row.contract_code),
            Column("Issue Date", row => row.issue_date),
            Column("Expiry Date", row => row.expiry_date),
            Column("Days Open", row => (DateTime.Today - row.issue_date.Date).Days),
            Column("Trip Request Number", row => row.trip_request_number),
            Column("Trip Reason", row => row.trip_reason),
            Column("Approver Name", row => row.approver_name),
            Column("Approver Rank", row => row.approver_rank),
            Column("Approver Tel", row => row.approver_tel),
            Column("Trip Type Code", row => row.trip_type_code),
            Column("Trip Incident Type Code", row => row.trip_incident_type_code),
            Column("End ODO Meter", row => row.end_odo_meter),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number));
    }

    private async Task<LegacyReportResultDto> BuildUnallocatedVehiclesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";
        var siteCode = GetShort(filters, "site");

        var activeVmfCodes = await _context.Contracts.AsNoTracking()
            .Where(contract => !contract.is_deleted && contract.still_current == "Y")
            .Select(contract => contract.vmf_code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on vehicle.location_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            where !vehicle.is_deleted && !activeVmfCodes.Contains(vehicle.vmf_code)
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.current_odo,
                vehicle.take_on_date,
                Status = status != null ? status.status_description : null,
                Site = site != null ? site.description : null,
                vehicle.location_code
            };

        if (siteCode.HasValue)
        {
            query = query.Where(row => row.location_code == siteCode.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = string.Equals(mode, "GP", StringComparison.OrdinalIgnoreCase)
                ? query.Where(row => row.registration_number != null && row.registration_number.Contains(term))
                : query.Where(row => row.fleet_number != null && row.fleet_number.Contains(term));
        }

        var rows = await query
            .OrderBy(row => row.fleet_number)
            .ThenBy(row => row.registration_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Unallocated Vehicles",
            "Finance/OpenReport.aspx?Report=VehiclesNoCurrentContractAndFuelTransactions",
            true,
            "Approximated from vehicles that currently have no active contract. Fuel transaction parity still requires the original finance report pipeline.",
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Status", row => row.Status),
            Column("Site", row => row.Site),
            Column("Current ODO", row => row.current_odo),
            Column("Take On Date", row => row.take_on_date));
    }

    private async Task<LegacyReportResultDto> BuildVehicleAdditionsAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var (from, to) = NormalizeDateRange(filters);
        var rows = await _context.Vehicles.AsNoTracking()
            .Where(vehicle => !vehicle.is_deleted && vehicle.purchase_date.HasValue && vehicle.purchase_date.Value.Date >= from && vehicle.purchase_date.Value.Date <= to)
            .OrderBy(vehicle => vehicle.purchase_date)
            .ThenBy(vehicle => vehicle.fleet_number)
            .Take(5000)
            .Select(vehicle => new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.purchase_date,
                vehicle.purchase_amount,
                vehicle.purchased_from,
                vehicle.invoice_number,
                vehicle.take_on_date,
                vehicle.current_odo
            })
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Vehicle Additions",
            "SelectStartAndEndDate.aspx?Item=AllVehiclesPurchasedInADateRange",
            false,
            null,
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Purchase Date", row => row.purchase_date),
            Column("Purchase Amount", row => row.purchase_amount),
            Column("Purchased From", row => row.purchased_from),
            Column("Invoice Number", row => row.invoice_number),
            Column("Take On Date", row => row.take_on_date),
            Column("Current ODO", row => row.current_odo));
    }

    private async Task<LegacyReportResultDto> BuildVehicleDisposalsAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var (from, to) = NormalizeDateRange(filters);
        var rows = await _context.Vehicles.AsNoTracking()
            .Where(vehicle => !vehicle.is_deleted && vehicle.sold_date.HasValue && vehicle.sold_date.Value.Date >= from && vehicle.sold_date.Value.Date <= to)
            .OrderBy(vehicle => vehicle.sold_date)
            .ThenBy(vehicle => vehicle.fleet_number)
            .Take(5000)
            .Select(vehicle => new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.sold_date,
                vehicle.sold_amount,
                vehicle.sold_to,
                vehicle.current_odo,
                vehicle.purchase_amount
            })
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Vehicle Disposals",
            "SelectStartAndEndDate.aspx?Item=AllVehiclesDisposedInADateRange",
            false,
            null,
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Sold Date", row => row.sold_date),
            Column("Sold Amount", row => row.sold_amount),
            Column("Sold To", row => row.sold_to),
            Column("Current ODO", row => row.current_odo),
            Column("Purchase Amount", row => row.purchase_amount));
    }

    private async Task<LegacyReportResultDto> BuildVehicleInfoAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult<object>(
                "General Vehicle Information Report",
                "Vehicles/RPT_Vehicle_Info.aspx",
                true,
                "Legacy vehicle information is a one-vehicle detail page. Enter a GG, GP, engine, VIN/chassis, or invoice number to mirror the legacy flow.",
                Array.Empty<object>(),
                Column("GG Number", _ => (object?)null));
        }

        var vmfCode = await ResolveVehicleVmfCodeAsync(search, mode, cancellationToken);
        if (!vmfCode.HasValue)
        {
            return CreateDynamicResult<object>(
                "General Vehicle Information Report",
                "Vehicles/RPT_Vehicle_Info.aspx",
                true,
                $"No vehicle matched '{search.Trim()}' for the selected {GetVehicleSearchModeLabel(mode)} search.",
                Array.Empty<object>(),
                Column("GG Number", _ => (object?)null));
        }

        var rows = await (
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking() on vehicle.model_code equals model.model_code into models
            from model in models.DefaultIfEmpty()
            join make in _context.Makes.AsNoTracking() on model.make_code equals make.make_code into makes
            from make in makes.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking() on vehicle.type_code equals type.type_code into types
            from type in types.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on vehicle.location_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            join fuelType in _context.FuelTypes.AsNoTracking() on model.fuel_type_code equals fuelType.fuel_type_code into fuelTypes
            from fuelType in fuelTypes.DefaultIfEmpty()
            join vehicleClass in _context.Classes.AsNoTracking() on model.class_code equals vehicleClass.class_code into vehicleClasses
            from vehicleClass in vehicleClasses.DefaultIfEmpty()
            where !vehicle.is_deleted && vehicle.vmf_code == vmfCode.Value
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                Status = status != null ? status.status_description : null,
                Site = site != null ? site.description : null,
                Make = make != null ? make.make_description : null,
                Model = model != null ? model.model_description : null,
                Type = type != null ? type.type_description : null,
                Class = vehicleClass != null ? vehicleClass.description : null,
                FuelType = fuelType != null ? fuelType.fuel_description : null,
                vehicle.chassis_number,
                vehicle.engine_number_1,
                vehicle.colour,
                vehicle.year_manufactured,
                vehicle.take_on_date,
                vehicle.take_on_odo,
                vehicle.current_odo,
                vehicle.purchase_date,
                vehicle.purchase_amount,
                vehicle.purchased_from,
                vehicle.licence_due_date,
                vehicle.lic_register_number,
                vehicle.optional_extras,
                vehicle.service_last_done,
                vehicle.service_last_odo,
                vehicle.sold_date,
                vehicle.sold_to,
                vehicle.sold_amount,
                vehicle.tare,
                vehicle.gvm,
                Transmission = model != null ? model.transmission : null,
                EngineType = model != null ? model.engine_type : null,
                EngineCapacity = model != null ? model.engine_capacity : null,
                RatedPower = model != null ? model.rated_power : null,
                FuelTankCapacity = model != null ? model.fuel_tank_capacity : null,
                TargetConsumption = model != null ? model.target_consumption : null,
                TargetTyreLife = model != null ? model.target_tyre_life : null
            })
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "General Vehicle Information Report",
            "Vehicles/RPT_Vehicle_Info.aspx",
            true,
                "Legacy vehicle information is a folder-style detail page. This approximation consolidates the core vehicle detail section into a single result grid with legacy-aligned descriptions and fields.",
            rows,
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Status", row => row.Status),
            Column("Site", row => row.Site),
            Column("Make", row => row.Make),
            Column("Model", row => row.Model),
            Column("Type", row => row.Type),
            Column("Class", row => row.Class),
            Column("Fuel Type", row => row.FuelType),
            Column("Chassis Number", row => row.chassis_number),
            Column("Engine Number", row => row.engine_number_1),
            Column("Colour", row => row.colour),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Take On Date", row => row.take_on_date),
            Column("Take On ODO", row => row.take_on_odo),
            Column("Current ODO", row => row.current_odo),
            Column("Purchase Date", row => row.purchase_date),
            Column("Purchase Amount", row => row.purchase_amount),
            Column("Purchased From", row => row.purchased_from),
            Column("Licence Due Date", row => row.licence_due_date),
            Column("Licence Register Number", row => row.lic_register_number),
            Column("Optional Extras", row => row.optional_extras),
            Column("Service Last Done", row => row.service_last_done),
            Column("Service Last ODO", row => row.service_last_odo),
            Column("Sold Date", row => row.sold_date),
            Column("Sold To", row => row.sold_to),
            Column("Sold Amount", row => row.sold_amount),
            Column("Tare", row => row.tare),
            Column("GVM", row => row.gvm),
            Column("Transmission", row => row.Transmission),
            Column("Engine Type", row => row.EngineType),
            Column("Engine Capacity", row => row.EngineCapacity),
            Column("Rated Power", row => row.RatedPower),
            Column("Fuel Tank Capacity", row => row.FuelTankCapacity),
            Column("Target Consumption", row => row.TargetConsumption),
            Column("Target Tyre Life", row => row.TargetTyreLife));
    }

    private async Task<LegacyReportResultDto> BuildVehicleListDateRangeAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var (startDate, endDate) = NormalizeDateRange(filters);
        var statusFilter = GetString(filters, "status");

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            where !vehicle.is_deleted && ((vehicle.take_on_date.Date >= startDate && vehicle.take_on_date.Date <= endDate) || (vehicle.sold_date.HasValue && vehicle.sold_date.Value.Date >= startDate && vehicle.sold_date.Value.Date <= endDate))
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.take_on_date,
                vehicle.sold_date,
                vehicle.current_odo,
                StatusCode = vehicle.vehicle_status_code,
                Status = status != null ? status.status_description : null,
                vehicle.purchase_amount,
                vehicle.year_manufactured
            };

        if (!string.IsNullOrWhiteSpace(statusFilter) && !string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase) && short.TryParse(statusFilter, out var statusCode))
        {
            query = query.Where(row => row.StatusCode == statusCode);
        }

        var rows = await query
            .OrderBy(row => row.take_on_date)
            .ThenBy(row => row.fleet_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Vehicle List in Date Range",
            "SelectStartAndEndDate.aspx?Item=FilterAllVehiclesInADateRange",
            false,
            null,
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Take On Date", row => row.take_on_date),
            Column("Sold Date", row => row.sold_date),
            Column("Current ODO", row => row.current_odo),
            Column("Vehicle Status Code", row => row.StatusCode),
            Column("Status", row => row.Status),
            Column("Purchase Amount", row => row.purchase_amount),
            Column("Year Manufactured", row => row.year_manufactured));
    }

    private async Task<LegacyReportResultDto> BuildVehicleStatusAllAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var rows = await (
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on vehicle.location_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            where !vehicle.is_deleted
            orderby status.status_description, vehicle.fleet_number, vehicle.registration_number
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.vehicle_status_code,
                Status = status != null ? status.status_description : null,
                vehicle.vehicle_status_date,
                vehicle.location_code,
                Site = site != null ? site.description : null,
                vehicle.year_manufactured,
                vehicle.current_odo,
                vehicle.take_on_date,
                vehicle.sold_date
            })
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "All Vehicle Status",
            "Finance/GeneratedReports.aspx?key=9.2%20All%20Vehicle%20Statuses",
            true,
            "Legacy report is generated from a separate report pipeline. This approximation projects the same core vehicle status fields from legacy tables.",
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Vehicle Status Code", row => row.vehicle_status_code),
            Column("Status", row => row.Status),
            Column("Status Date", row => row.vehicle_status_date),
            Column("Location Code", row => row.location_code),
            Column("Site", row => row.Site),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Current ODO", row => row.current_odo),
            Column("Take On Date", row => row.take_on_date),
            Column("Sold Date", row => row.sold_date));
    }

    private async Task<LegacyReportResultDto> BuildVehicleLogsReportAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var vmfCode = GetInt(filters, "vmf");
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        if (!vmfCode.HasValue && !string.IsNullOrWhiteSpace(search))
        {
            vmfCode = string.IsNullOrWhiteSpace(GetString(filters, "mode"))
                ? await ResolveLegacyVehicleVmfCodeAsync(search, cancellationToken)
                : await ResolveVehicleVmfCodeAsync(search, mode, cancellationToken);
        }

        if (!vmfCode.HasValue)
        {
            return CreateDynamicResult<object>(
                "Vehicle Logs Report",
                "Logs/RPT_logsheet_per_vehicle.aspx",
                true,
                "Select a vehicle by GG, GP, engine, or VIN/chassis to mirror the legacy per-vehicle logs page.",
                Array.Empty<object>(),
                Column("Section", _ => (object?)null));
        }

        var contractRows = await (
            from contract in _context.Contracts.AsNoTracking()
            join site in _context.Sites.AsNoTracking() on contract.site_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking() on site.Depatrment_code equals department.department_code into departments
            from department in departments.DefaultIfEmpty()
            where !contract.is_deleted && contract.vmf_code == vmfCode.Value
            select new VehicleLogCompositeRow(
                "Contract Details",
                contract.contract_code.ToString(),
                contract.start_date,
                contract.end_date,
                department != null ? department.Department_number : site != null ? site.Department_number : null,
                department != null ? department.description : null,
                site != null ? site.Site_code.ToString() : null,
                site != null ? site.description : null,
                contract.still_current,
                contract.start_odometer,
                contract.end_odometer,
                contract.Driver_name,
                contract.user_code == null ? null : contract.user_code.ToString(),
                contract.Notes))
            .ToListAsync(cancellationToken);

        var tripRows = await (
            from trip in _context.Trips.AsNoTracking()
            join contract in _context.Contracts.AsNoTracking() on trip.contract_code equals contract.contract_code
            join site in _context.Sites.AsNoTracking() on contract.site_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking() on site.Depatrment_code equals department.department_code into departments
            from department in departments.DefaultIfEmpty()
            where !trip.is_deleted && !contract.is_deleted && contract.vmf_code == vmfCode.Value
            select new VehicleLogCompositeRow(
                "ELS Details",
                trip.trip_authority_code.ToString(),
                trip.issue_date,
                trip.expiry_date,
                department != null ? department.Department_number : site != null ? site.Department_number : null,
                department != null ? department.description : null,
                site != null ? site.Site_code.ToString() : null,
                site != null ? site.description : null,
                trip.trip_type_code.ToString(),
                contract.start_odometer,
                trip.end_odo_meter,
                trip.trip_request_number,
                trip.user_access_code == null ? null : trip.user_access_code.ToString(),
                trip.trip_reason))
            .ToListAsync(cancellationToken);

        var logsheetRows = await (
            from logsheet in _context.Logsheets.AsNoTracking()
            join site in _context.Sites.AsNoTracking() on logsheet.site_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking() on site.Depatrment_code equals department.department_code into departments
            from department in departments.DefaultIfEmpty()
            where !logsheet.is_deleted && logsheet.vmf_code == vmfCode.Value
            select new VehicleLogCompositeRow(
                "Logsheet Details",
                logsheet.log_code.ToString(),
                logsheet.month,
                null,
                department != null ? department.Department_number : site != null ? site.Department_number : null,
                department != null ? department.description : null,
                site != null ? site.Site_code.ToString() : null,
                site != null ? site.description : null,
                logsheet.bund_num != null ? $"Bundle {logsheet.bund_num}" : null,
                logsheet.start_odo,
                logsheet.end_odo,
                logsheet.rek_num,
                logsheet.created_by_user_code == null ? null : logsheet.created_by_user_code.ToString(),
                logsheet.days_used.HasValue ? $"Days used: {logsheet.days_used}" : null))
            .ToListAsync(cancellationToken);

        var rows = contractRows
            .Concat(tripRows)
            .Concat(logsheetRows)
            .OrderByDescending(row => row.StartDate)
            .ThenBy(row => row.Section)
            .Take(5000)
            .ToList();

        return CreateDynamicResult(
            "Vehicle Logs Report",
            "Logs/RPT_logsheet_per_vehicle.aspx",
            true,
            "Legacy vehicle logs report is a folder/iframe composition with separate Contract, ELS, and Logsheet sections. This approximation flattens those sections into one grid while keeping the legacy section labels and columns.",
            rows,
            Column("Section", row => row.Section),
            Column("Record Code", row => row.RecordCode),
            Column("Start Date", row => row.StartDate),
            Column("End Date", row => row.EndDate),
            Column("Department No", row => row.DepartmentNumber),
            Column("Department Description", row => row.DepartmentDescription),
            Column("Site No", row => row.SiteNumber),
            Column("Site Description", row => row.SiteDescription),
            Column("Status / Type", row => row.StatusOrType),
            Column("Start ODO", row => row.StartOdo),
            Column("End ODO", row => row.EndOdo),
            Column("Driver / Requisition", row => row.DriverOrRequisition),
            Column("Captured By User Code", row => row.CapturedByUserCode),
            Column("Notes", row => row.Notes));
    }

    private async Task<LegacyReportResultDto> BuildVehicleStatusRangeAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var statusCode = GetShort(filters, "status");
        var (startDate, endDate) = NormalizeDateRange(filters);

        var query =
            from history in _context.VehicleStatusHistories.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on history.vmf_code equals vehicle.vmf_code into histories
            from vehicle in histories.DefaultIfEmpty()
            where !history.is_deleted && history.status_start_date.Date <= endDate && history.status_end_date.Date >= startDate
            select new
            {
                history.vehicle_status_history_code,
                history.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                history.vehicle_status_code,
                history.vehicle_status_description,
                history.status_start_date,
                history.status_end_date,
                history.date_created
            };

        if (statusCode.HasValue)
        {
            query = query.Where(row => row.vehicle_status_code == statusCode.Value);
        }

        var rows = await query
            .OrderByDescending(row => row.status_start_date)
            .ThenBy(row => row.fleet_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Vehicle Status Range Report",
            "Vehicles/VehicleStatus.aspx",
            true,
            "Legacy vehicle status pages are custom. This dynamic approximation uses vehicle_status_history rows.",
            rows,
            Column("Status History Code", row => row.vehicle_status_history_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Vehicle Status Code", row => row.vehicle_status_code),
            Column("Vehicle Status Description", row => row.vehicle_status_description),
            Column("Status Start Date", row => row.status_start_date),
            Column("Status End Date", row => row.status_end_date),
            Column("Captured On", row => row.date_created));
    }

    private async Task<LegacyReportResultDto> BuildVehiclesAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking() on vehicle.model_code equals model.model_code into models
            from model in models.DefaultIfEmpty()
            join make in _context.Makes.AsNoTracking() on model.make_code equals make.make_code into makes
            from make in makes.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking() on vehicle.type_code equals type.type_code into types
            from type in types.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on vehicle.location_code equals site.Site_code into sites
            from site in sites.DefaultIfEmpty()
            join vehicleClass in _context.Classes.AsNoTracking() on model.class_code equals vehicleClass.class_code into vehicleClasses
            from vehicleClass in vehicleClasses.DefaultIfEmpty()
            where !vehicle.is_deleted
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                Make = make != null ? make.make_description : null,
                Model = model != null ? model.model_description : null,
                Type = type != null ? type.type_description : null,
                Site = site != null ? site.description : null,
                Class = vehicleClass != null ? vehicleClass.description : null,
                vehicle.location_code,
                Status = status != null ? status.status_description : null,
                vehicle.purchase_date,
                vehicle.purchase_amount,
                vehicle.current_odo,
                vehicle.year_manufactured,
                vehicle.take_on_date,
                vehicle.sold_date
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(search, mode, cancellationToken);
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var rows = await query
            .OrderBy(row => row.fleet_number)
            .ThenBy(row => row.registration_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Vehicle Master List",
            "Vehicles/Vehicles.aspx",
            true,
            "Legacy vehicle list reports are menu-driven and highly parameterized. This approximation now uses human-readable make, model, type, site, and status descriptions instead of raw codes.",
            rows,
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Make", row => row.Make),
            Column("Model", row => row.Model),
            Column("Type", row => row.Type),
            Column("Class", row => row.Class),
            Column("Site", row => row.Site),
            Column("Status", row => row.Status),
            Column("Purchase Date", row => row.purchase_date),
            Column("Purchase Amount", row => row.purchase_amount),
            Column("Current ODO", row => row.current_odo),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Take On Date", row => row.take_on_date),
            Column("Sold Date", row => row.sold_date));
    }

    private async Task<LegacyReportResultDto> BuildVehiclesNoTariffAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var max = Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000);
        var activeTariffVmfCodes = await _context.VehicleTariffs.AsNoTracking()
            .Where(tariff => !tariff.is_deleted && (tariff.end_date == null || tariff.end_date >= DateTime.Today))
            .Select(tariff => tariff.vmf_code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rows = await (
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking() on vehicle.vehicle_status_code equals status.vehicle_status_code into statuses
            from status in statuses.DefaultIfEmpty()
            where !vehicle.is_deleted && (vehicle.vehicle_status_code == 1 || vehicle.vehicle_status_code == 2) && !activeTariffVmfCodes.Contains(vehicle.vmf_code)
            orderby vehicle.fleet_number, vehicle.registration_number
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.year_manufactured,
                vehicle.current_odo,
                vehicle.location_code,
                Status = status != null ? status.status_description : null,
                vehicle.take_on_date
            })
            .Take(max)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Vehicles with Expired or No Tariffs",
            "ShowReport.aspx?Item=GetAllVehicleWithNoTariffs",
            false,
            null,
            rows,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Current ODO", row => row.current_odo),
            Column("Location Code", row => row.location_code),
            Column("Status", row => row.Status),
            Column("Take On Date", row => row.take_on_date));
    }

    private async Task<LegacyReportResultDto> BuildWesbankAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var province = GetString(filters, "province");
        var reportMode = GetString(filters, "rmode");

        var query =
            from detail in _context.Set<JournalDetail>().AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on detail.vmf_code equals vehicle.vmf_code into detailVehicles
            from vehicle in detailVehicles.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking() on detail.department_code equals department.department_code into detailDepartments
            from department in detailDepartments.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking() on detail.site_code equals site.Site_code into detailSites
            from site in detailSites.DefaultIfEmpty()
            select new
            {
                detail.journal_detail_id,
                detail.journal_detail_financial_year,
                detail.journal_detail_date,
                detail.journal_detail_date_posted,
                detail.journal_detail_description,
                detail.journal_detail_tariff,
                detail.journal_detail_amount,
                detail.journal_detail_quantity,
                detail.journal_detail_type_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                Department = department != null ? department.description : null,
                Site = site != null ? site.description : null,
                Province = province,
                ReportMode = reportMode
            };

        if (from.HasValue)
        {
            query = query.Where(row => row.journal_detail_date.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(row => row.journal_detail_date.Date <= to.Value);
        }

        var rows = await query
            .OrderByDescending(row => row.journal_detail_date)
            .ThenBy(row => row.fleet_number)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Wesbank First Auto Report",
            "Transaction/RPTTransaction.aspx",
            true,
            "Original Wesbank reports rely on a dedicated finance pipeline. This dynamic fallback uses journal_detail rows, preserving dynamic legacy-style columns while remaining approximate.",
            rows,
            Column("Journal Detail ID", row => row.journal_detail_id),
            Column("Financial Year", row => row.journal_detail_financial_year),
            Column("Transaction Date", row => row.journal_detail_date),
            Column("Posted Date", row => row.journal_detail_date_posted),
            Column("Description", row => row.journal_detail_description),
            Column("Tariff", row => row.journal_detail_tariff),
            Column("Amount", row => row.journal_detail_amount),
            Column("Quantity", row => row.journal_detail_quantity),
            Column("Type Code", row => row.journal_detail_type_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Department", row => row.Department),
            Column("Site", row => row.Site),
            Column("Province Filter", row => row.Province),
            Column("Report Mode", row => row.ReportMode));
    }

    private async Task<LegacyReportResultDto> BuildWorkshopAsync(IDictionary<string, string?> filters, CancellationToken cancellationToken)
    {
        var search = GetString(filters, "search");
        var (startDate, endDate) = NormalizeDateRange(filters);

        var query =
            from workshop in _context.Workshops.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking() on workshop.vmf_code equals vehicle.vmf_code into workshopVehicles
            from vehicle in workshopVehicles.DefaultIfEmpty()
            where !workshop.is_deleted && ((workshop.receive_date.HasValue && workshop.receive_date.Value.Date >= startDate && workshop.receive_date.Value.Date <= endDate)
                || (workshop.complete_time.HasValue && workshop.complete_time.Value.Date >= startDate && workshop.complete_time.Value.Date <= endDate))
            select new
            {
                workshop.ww_code,
                workshop.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                workshop.receive_date,
                workshop.receive_time,
                workshop.complete_time,
                CompleteDate = workshop.complete_time.HasValue ? workshop.complete_time.Value.Date : (DateTime?)null,
                vehicle.current_odo,
                vehicle.model_code,
                DaysInWorkshop = workshop.receive_date.HasValue && workshop.complete_time.HasValue
                    ? EF.Functions.DateDiffDay(workshop.receive_date.Value, workshop.complete_time.Value)
                    : (int?)null
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                row.ww_code.ToString().Contains(term) ||
                (row.vmf_code != null && row.vmf_code.Value.ToString().Contains(term)) ||
                (row.fleet_number != null && row.fleet_number.Contains(term)) ||
                (row.registration_number != null && row.registration_number.Contains(term)));
        }

        var rows = await query
            .OrderByDescending(row => row.receive_date)
            .ThenByDescending(row => row.ww_code)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return CreateDynamicResult(
            "Workshop Report",
            "Workshop/RPTWorkshop.aspx",
            true,
            "Legacy workshop reporting is menu-driven. This dynamic approximation uses workshop receive/complete rows with legacy table columns.",
            rows,
            Column("Workshop Code", row => row.ww_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Receive Date", row => row.receive_date),
            Column("Receive Time", row => row.receive_time),
            Column("Complete Date", row => row.CompleteDate),
            Column("Complete Time", row => row.complete_time),
            Column("Current ODO", row => row.current_odo),
            Column("Model Code", row => row.model_code),
            Column("Days In Workshop", row => row.DaysInWorkshop));
    }

    private static LegacyReportResultDto CreateDynamicResult<T>(
        string title,
        string legacyTarget,
        bool isApproximate,
        string? approximationReason,
        IEnumerable<T> rows,
        params LegacyProjectionColumn[] columns)
    {
        var columnList = columns
            .Select(column => new LegacyReportColumnDto
            {
                Key = column.Header,
                Header = column.Header
            })
            .ToList();

        var rowList = rows
            .Select(row =>
            {
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in columns)
                {
                    values[column.Header] = FormatValue(column.Selector(row!));
                }
                return values;
            })
            .ToList();

        return new LegacyReportResultDto
        {
            Title = title,
            LegacyTarget = legacyTarget,
            IsApproximate = isApproximate,
            ApproximationReason = approximationReason,
            Columns = columnList,
            Rows = rowList,
            TotalCount = rowList.Count
        };
    }

    private static LegacyProjectionColumn Column(string header, Func<dynamic, object?> selector)
        => new(header, row => selector(row));

    private static List<LegacyStoredProcedureParameter> BuildOptionalParameterList(params (string Name, object? Value, DbType DbType)[] parameters)
        => parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => new LegacyStoredProcedureParameter(parameter.Name, parameter.Value, parameter.DbType))
            .ToList();

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildDateRangeParameters(IDictionary<string, string?> filters)
        => BuildOptionalParameterList(
            ("@StartDate", GetDate(filters, "from"), DbType.DateTime),
            ("@EndDate", GetDate(filters, "to"), DbType.DateTime));

    private static (DateTime From, DateTime To) NormalizeDateRange(IDictionary<string, string?> filters)
    {
        var startDate = GetDate(filters, "from")?.Date ?? DateTime.Today.AddMonths(-1).Date;
        var endDate = GetDate(filters, "to")?.Date ?? DateTime.Today.Date;
        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }
        return (startDate, endDate);
    }

    private static string? GetString(IDictionary<string, string?> filters, string key)
        => filters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private async Task<int?> ResolveLegacyVehicleVmfCodeAsync(string? search, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var term = search.Trim();

        return await _context.Vehicles.AsNoTracking()
            .Where(vehicle => !vehicle.is_deleted
                && ((vehicle.fleet_number != null && vehicle.fleet_number == term)
                    || (vehicle.registration_number != null && vehicle.registration_number == term)
                    || (vehicle.chassis_number != null && vehicle.chassis_number == term)
                    || (vehicle.engine_number_1 != null && vehicle.engine_number_1 == term)))
            .OrderBy(vehicle => vehicle.vmf_code)
            .Select(vehicle => (int?)vehicle.vmf_code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> ResolveVehicleVmfCodeAsync(string? search, string? mode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var term = search.Trim();
        var query = _context.Vehicles.AsNoTracking().Where(vehicle => !vehicle.is_deleted);

        query = NormalizeVehicleSearchMode(mode) switch
        {
            "GP" => query.Where(vehicle => vehicle.registration_number != null && vehicle.registration_number == term),
            "ENGINE" => query.Where(vehicle => vehicle.engine_number_1 != null && vehicle.engine_number_1 == term),
            "VIN" => query.Where(vehicle => vehicle.chassis_number != null && vehicle.chassis_number == term),
            "INVOICE" => query.Where(vehicle => vehicle.invoice_number != null && vehicle.invoice_number == term),
            _ => query.Where(vehicle => vehicle.fleet_number != null && vehicle.fleet_number == term)
        };

        return await query
            .OrderBy(vehicle => vehicle.vmf_code)
            .Select(vehicle => (int?)vehicle.vmf_code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<int>> ResolveVehicleVmfCodesAsync(string? search, string? mode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return new List<int>();
        }

        var term = search.Trim();
        var query = _context.Vehicles.AsNoTracking().Where(vehicle => !vehicle.is_deleted);

        query = NormalizeVehicleSearchMode(mode) switch
        {
            "GP" => query.Where(vehicle => vehicle.registration_number != null && vehicle.registration_number.Contains(term)),
            "ENGINE" => query.Where(vehicle => vehicle.engine_number_1 != null && vehicle.engine_number_1.Contains(term)),
            "VIN" => query.Where(vehicle => vehicle.chassis_number != null && vehicle.chassis_number.Contains(term)),
            "INVOICE" => query.Where(vehicle => vehicle.invoice_number != null && vehicle.invoice_number.Contains(term)),
            _ => query.Where(vehicle => vehicle.fleet_number != null && vehicle.fleet_number.Contains(term))
        };

        return await query
            .OrderBy(vehicle => vehicle.vmf_code)
            .Select(vehicle => vehicle.vmf_code)
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeVehicleSearchMode(string? mode)
        => mode?.Trim().ToUpperInvariant() switch
        {
            "GP" => "GP",
            "ENGINE" => "ENGINE",
            "CHASSIS" => "VIN",
            "VIN" => "VIN",
            "INVOICE" => "INVOICE",
            _ => "GG"
        };

    private static string GetVehicleSearchModeLabel(string? mode)
        => NormalizeVehicleSearchMode(mode) switch
        {
            "GP" => "GP number",
            "ENGINE" => "engine number",
            "VIN" => "VIN / chassis number",
            "INVOICE" => "invoice number",
            _ => "GG number"
        };

    private static short? GetShort(IDictionary<string, string?> filters, string key)
        => short.TryParse(GetString(filters, key), out var value) ? value : null;

    private static int? GetInt(IDictionary<string, string?> filters, string key)
        => int.TryParse(GetString(filters, key), out var value) ? value : null;

    private static DateTime? GetDate(IDictionary<string, string?> filters, string key)
        => DateTime.TryParse(GetString(filters, key), out var value) ? value : null;

    private static int GetFinancialYear(IDictionary<string, string?> filters)
    {
        var explicitYear = GetInt(filters, "FinYear") ?? GetInt(filters, "finYear") ?? GetInt(filters, "financialYear");
        if (explicitYear.HasValue && explicitYear.Value > 0)
        {
            return explicitYear.Value;
        }

        var startYear = GetDate(filters, "from")?.Year;
        var endYear = GetDate(filters, "to")?.Year;
        return startYear ?? endYear ?? DateTime.Today.Year;
    }

    private static int GetFinancialYearKey(DateTime date)
        => date.Month >= 4 ? date.Year : date.Year - 1;

    private static bool Contains(string? source, string term)
        => !string.IsNullOrWhiteSpace(source) && source.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string JoinVehicleLabel(string? ggNumber, string? gpNumber, int vmfCode)
        => $"{(string.IsNullOrWhiteSpace(ggNumber) ? "-" : ggNumber)} / {(string.IsNullOrWhiteSpace(gpNumber) ? "-" : gpNumber)} ({vmfCode})";

    private static string GetCertificateStatus(DateTime? dueDate, DateTime today, DateTime dueSoon)
    {
        if (!dueDate.HasValue)
        {
            return "No due date";
        }

        if (dueDate.Value.Date < today)
        {
            return "Expired";
        }

        if (dueDate.Value.Date <= dueSoon)
        {
            return "Due soon";
        }

        return "Valid";
    }

    private static string? FormatValue(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return null;
        }

        return value switch
        {
            DateTime dateTime when dateTime.TimeOfDay == TimeSpan.Zero => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "Yes" : "No",
            decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
            double number => number.ToString("0.##", CultureInfo.InvariantCulture),
            float number => number.ToString("0.##", CultureInfo.InvariantCulture),
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private sealed record LegacyReportDefinition(
        string Key,
        string Title,
        string LegacyTarget,
        string? StoredProcedureItem,
        Func<IDictionary<string, string?>, CancellationToken, Task<LegacyReportResultDto>> Fallback,
        string ApproximationReason = "",
        Func<IDictionary<string, string?>, IReadOnlyList<LegacyStoredProcedureParameter>>? BuildStoredProcedureParameters = null);

    private sealed record LegacyStoredProcedureParameter(string Name, object? Value, DbType DbType);
    private sealed record LegacyProjectionColumn(string Header, Func<object, object?> Selector);
    private sealed record CaptureActivityRow(string Module, string RecordId, string? GgNumber, string? GpNumber, short? SiteCode, int? CapturedByUserCode, DateTime DateCaptured, string? Description);
    private sealed record VehicleLogCompositeRow(
        string Section,
        string RecordCode,
        DateTime? StartDate,
        DateTime? EndDate,
        string? DepartmentNumber,
        string? DepartmentDescription,
        string? SiteNumber,
        string? SiteDescription,
        string? StatusOrType,
        object? StartOdo,
        object? EndOdo,
        string? DriverOrRequisition,
        string? CapturedByUserCode,
        string? Notes);
}

public sealed class LegacyReportResultDto
{
    public string ReportKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? LegacyTarget { get; set; }
    public bool IsApproximate { get; set; }
    public string? ApproximationReason { get; set; }
    public List<LegacyReportColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
    public int TotalCount { get; set; }
}

public sealed class LegacyReportColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
}
