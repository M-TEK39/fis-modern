using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

public interface ILegacyReportResultService
{
    Task<LegacyReportResultDto> GetReportAsync(
        string reportKey,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken = default
    );

    Task<LegacyReportResultDto> GetPagedReportAsync(
        string reportKey,
        IDictionary<string, string?> filters,
        int page = 1,
        int pageSize = 24,
        bool includeAll = false,
        CancellationToken cancellationToken = default
    );
}

public sealed class LegacyReportResultService : ILegacyReportResultService
{
    private const string InternalPageFilter = "__legacyReportPage";
    private const string InternalPageSizeFilter = "__legacyReportPageSize";
    private const string InternalIncludeAllFilter = "__legacyReportIncludeAll";
    private const string InternalFineSiteScopeFilter = "__fineAllowedSiteCodes";
    private const string InternalReportSiteScopeFilter = "__reportAllowedSiteCodes";

    private readonly FisDbContext _context;
    private readonly IAssetVerificationRepository _assetVerificationRepository;
    private readonly IWorkshopRepository _workshopRepository;
    private readonly IWorkshopMerchantRepository _workshopMerchantRepository;
    private readonly ITaxiRepository _taxiRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogbookRepository _logbookRepository;
    private readonly ILogsheetRepository _logsheetRepository;
    private readonly ILogger<LegacyReportResultService> _logger;
    private readonly IReadOnlyDictionary<string, LegacyReportDefinition> _definitions;

    public LegacyReportResultService(
        FisDbContext context,
        IAssetVerificationRepository assetVerificationRepository,
        IWorkshopRepository workshopRepository,
        IWorkshopMerchantRepository workshopMerchantRepository,
        ITaxiRepository taxiRepository,
        IVehicleRepository vehicleRepository,
        ILogbookRepository logbookRepository,
        ILogsheetRepository logsheetRepository,
        ILogger<LegacyReportResultService> logger
    )
    {
        _context = context;
        _assetVerificationRepository = assetVerificationRepository;
        _workshopRepository = workshopRepository;
        _workshopMerchantRepository = workshopMerchantRepository;
        _taxiRepository = taxiRepository;
        _vehicleRepository = vehicleRepository;
        _logbookRepository = logbookRepository;
        _logsheetRepository = logsheetRepository;
        _logger = logger;
        _definitions = BuildDefinitions();
    }

    public async Task<LegacyReportResultDto> GetReportAsync(
        string reportKey,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken = default
    )
    {
        // Keep the legacy entry point safe for any future caller: table-shaped
        // reports default to the same 24-row database page as the HTTP route.
        // Full result sets must opt into GetPagedReportAsync(includeAll: true),
        // which is reserved for the explicit export path.
        return await GetReportCoreAsync(
            reportKey,
            filters,
            new LegacyReportPagination(1, 24, false),
            cancellationToken
        );
    }

    public async Task<LegacyReportResultDto> GetPagedReportAsync(
        string reportKey,
        IDictionary<string, string?> filters,
        int page = 1,
        int pageSize = 24,
        bool includeAll = false,
        CancellationToken cancellationToken = default
    )
    {
        var compatibilityFilters = new Dictionary<string, string?>(
            filters,
            StringComparer.OrdinalIgnoreCase
        );
        compatibilityFilters.Remove("page");
        compatibilityFilters.Remove("pageSize");
        compatibilityFilters.Remove("includeAll");

        return await GetReportCoreAsync(
            reportKey,
            compatibilityFilters,
            new LegacyReportPagination(page, pageSize, includeAll),
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> GetReportCoreAsync(
        string reportKey,
        IDictionary<string, string?> filters,
        LegacyReportPagination? pagination,
        CancellationToken cancellationToken
    )
    {
        var resolvedKey = ResolveReportKeyAlias(reportKey);

        if (!_definitions.TryGetValue(resolvedKey, out var definition))
        {
            _logger.LogWarning("Legacy report key '{ReportKey}' is not mapped.", reportKey);
            throw new KeyNotFoundException($"Legacy report key '{reportKey}' is not mapped.");
        }

        // Legacy procedures return the authoritative rows and do not expose a
        // compatible count/page contract. Execute the procedure for every
        // request, then paginate the exact result in memory. Falling back to
        // a table projection merely because the caller asked for a page can
        // omit journal/reversal/allocation rows from an operational report.
        if (definition.StoredProcedureItem is not null || definition.StoredProcedureName is not null)
        {
            try
            {
                var storedProcResult = await TryExecuteStoredProcedureAsync(
                    definition,
                    filters,
                    cancellationToken
                );
                if (storedProcResult is not null)
                {
                    // Keep the key the caller requested.  The definition key is
                    // canonical, but the legacy menu exposes separate keys for
                    // the province/department/site variants.
                    storedProcResult.ReportKey = reportKey;
                    return ApplyPagination(storedProcResult, pagination);
                }

                // A report with a named legacy procedure has an authoritative
                // database contract. Do not silently replace its journal,
                // reversal, allocation, or status semantics with a table
                // approximation merely because that procedure is absent on a
                // particular database.
                throw new LegacyReportProcedureUnavailableException(GetStoredProcedureName(definition));
            }
            catch (Exception ex)
                when (ex is LegacyReportProcedureUnavailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Stored procedure execution failed for legacy report {ReportKey}; no compatibility fallback will be used.",
                    resolvedKey
                );
                throw new LegacyReportProcedureExecutionException(GetStoredProcedureName(definition), ex);
            }
        }

        var fallbackFilters = AddInternalPaginationFilters(filters, pagination);
        var fallback = await definition.Fallback(fallbackFilters, cancellationToken);
        fallback.ReportKey = reportKey;
        fallback.Title = string.IsNullOrWhiteSpace(fallback.Title)
            ? definition.Title
            : fallback.Title;
        fallback.LegacyTarget ??= definition.LegacyTarget;
        fallback.IsApproximate =
            fallback.IsApproximate || !string.IsNullOrWhiteSpace(definition.ApproximationReason);
        if (fallback.IsApproximate)
        {
            fallback.ApproximationReason ??= definition.ApproximationReason;
        }
        else
        {
            fallback.ApproximationReason = null;
        }
        return ApplyPagination(fallback, pagination);
    }

    private static LegacyReportResultDto ApplyPagination(
        LegacyReportResultDto result,
        LegacyReportPagination? pagination
    )
    {
        if (result.IsDatabasePaged)
        {
            return result;
        }

        result.TotalCount = result.Rows.Count;

        if (pagination is null)
        {
            result.Page = 1;
            result.PageSize = Math.Max(1, result.TotalCount);
            return result;
        }

        var requestedPage = Math.Max(1, pagination.Page);
        var pageSize = Math.Clamp(pagination.PageSize, 1, 100);
        if (pagination.IncludeAll)
        {
            result.Page = 1;
            result.PageSize = Math.Max(1, result.TotalCount);
            return result;
        }

        result.PageSize = pageSize;
        result.Page = Math.Min(requestedPage, result.TotalPages);
        var skip = checked((result.Page - 1) * pageSize);
        result.Rows = result.Rows.Skip(skip).Take(pageSize).ToList();
        return result;
    }

    private static IDictionary<string, string?> AddInternalPaginationFilters(
        IDictionary<string, string?> filters,
        LegacyReportPagination? pagination
    )
    {
        var result = new Dictionary<string, string?>(filters, StringComparer.OrdinalIgnoreCase);
        if (pagination is null)
        {
            return result;
        }

        result[InternalPageFilter] = pagination.Page.ToString(CultureInfo.InvariantCulture);
        result[InternalPageSizeFilter] = pagination.PageSize.ToString(CultureInfo.InvariantCulture);
        result[InternalIncludeAllFilter] = pagination.IncludeAll
            ? bool.TrueString
            : bool.FalseString;
        return result;
    }

    private static LegacyReportPagination? GetFallbackPagination(
        IDictionary<string, string?> filters
    )
    {
        if (!GetInt(filters, InternalPageFilter).HasValue)
        {
            return null;
        }

        return new LegacyReportPagination(
            GetInt(filters, InternalPageFilter) ?? 1,
            GetInt(filters, InternalPageSizeFilter) ?? 24,
            bool.TryParse(GetString(filters, InternalIncludeAllFilter), out var includeAll)
                && includeAll
        );
    }

    private static LegacyReportPageWindow GetPageWindow(
        LegacyReportPagination? pagination,
        int totalCount
    )
    {
        if (pagination is null || pagination.IncludeAll)
        {
            return new LegacyReportPageWindow(1, Math.Max(1, totalCount), 0, false);
        }

        var pageSize = Math.Clamp(pagination.PageSize, 1, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Min(Math.Max(1, pagination.Page), totalPages);
        var skip = checked((long)(page - 1) * pageSize);
        return new LegacyReportPageWindow(page, pageSize, skip, true);
    }

    private static LegacyReportResultDto CreateDatabasePagedResult<T>(
        string title,
        string legacyTarget,
        bool isApproximate,
        string? approximationReason,
        IEnumerable<T> rows,
        int totalCount,
        LegacyReportPageWindow pageWindow,
        params LegacyProjectionColumn[] columns
    )
    {
        var result = CreateDynamicResult(
            title,
            legacyTarget,
            isApproximate,
            approximationReason,
            rows,
            columns
        );
        result.TotalCount = totalCount;
        result.Page = pageWindow.Page;
        result.PageSize = pageWindow.PageSize;
        result.IsDatabasePaged = pageWindow.IsPaged;
        return result;
    }

    private async Task<LegacyReportDatabasePage<T>> MaterializeDatabasePageAsync<T>(
        IQueryable<T> query,
        Func<IQueryable<T>, IOrderedQueryable<T>> order,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken,
        int? legacyUnpagedLimit = null
    )
    {
        var pagination = GetFallbackPagination(filters);
        if (pagination is null || pagination.IncludeAll)
        {
            var orderedQuery = order(query);
            var rows = legacyUnpagedLimit.HasValue
                ? await orderedQuery.Take(legacyUnpagedLimit.Value).ToListAsync(cancellationToken)
                : await orderedQuery.ToListAsync(cancellationToken);
            return new LegacyReportDatabasePage<T>(
                rows,
                rows.Count,
                new LegacyReportPageWindow(1, Math.Max(1, rows.Count), 0, false)
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageWindow = GetPageWindow(pagination, totalCount);
        var pagedRows = await order(query)
            .Skip((int)pageWindow.Skip)
            .Take(pageWindow.PageSize)
            .ToListAsync(cancellationToken);
        return new LegacyReportDatabasePage<T>(pagedRows, totalCount, pageWindow);
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
            "asset-verification" => "asset-verification",
            "auction" => "auction",
            "class-code" => "class-code",
            "class-code-totals" => "class-code-totals",
            "clearance" => "clearance-universal",
            "fuel-cards" => "fuel-cards",
            "registration-certificate-one-vehicle" => "registration-certificate-one-vehicle",
            "tariffs-class-codes" => "tariffs-class-codes",
            "tariffs-licence-fees" => "tariffs-licence-fees",
            "tariffs-make-model" => "tariffs-make-model",
            "tariffs-private-taxi" => "tariffs-private-taxi",
            "users" => "users",
            "users-added" => "users-added",
            "users-one" => "users-one",
            "vehicle-by-barcode" => "vehicle-by-barcode",
            "vehicles-with-history" => "vehicles-with-history",

            // Audit trail variants. AuditTrail.aspx filters by Mode; they are
            // not the ELS-only stored procedure used by the generic key.
            "audit-trail-department" => "audit-trail-department",
            "audit-trail-site" => "audit-trail-site",
            "audit-trail-vehicle" => "audit-trail-vehicle",

            // Management menu variants. Management.aspx opens distinct screens
            // gated by Management Reports; they are not the generic menu list.
            "management-ggmt" => "management-ggmt",
            "management-incorrect-captured-data" => "management-incorrect-captured-data",
            "management-site-info" => "management-site-info",
            "ggmt-management" => "management-ggmt",
            "incorrect-captured-data" => "management-incorrect-captured-data",
            "fis-site-management-info" => "management-site-info",

            // Contract report variants. Contracts.aspx opens distinct screens.
            "contract-summary" => "contract-summary",
            "contract-trip-authority-dept-site-date" => "contract-trip-authority-dept-site-date",
            "contract-trip-authority-multiple" => "contract-trip-authority-multiple",
            "contract-trip-authority-single" => "contract-trip-authority-single",
            "contract-vehicle-multiple" => "contract-vehicle-multiple",
            "contract-vehicle-single" => "contract-vehicle-single",
            "contracts-checklist" => "contracts-checklist",
            "contracts-expiring-by-date" => "contracts-expiring-by-date",
            "contracts-fleet-reports" => "contracts-fleet-reports",
            "contracts-no-distance" => "contracts-no-distance",
            "contracts-per-dept-period" => "contracts-per-dept-period",
            "lease-nom-contract-split" => "lease-nom-contract-split",
            "trip-authorities-single" => "contract-trip-authority-single",
            "trip-authorities-multiple" => "contract-trip-authority-multiple",
            "trip-authorities-by-dept-site-date" => "contract-trip-authority-dept-site-date",
            "lease-nom-split" => "lease-nom-contract-split",

            // Asset list variants.  These are separate legacy report keys and
            // must not collapse into the all-assets procedure: the legacy page
            // sends SearchType 1/2/3 and id to its filtered procedure.
            "asset-list-by-province" => "asset-list-by-province",
            "asset-list-by-department" => "asset-list-by-department",
            "asset-list-by-site" => "asset-list-by-site",
            "all-departments" => "asset-list",
            "by-province" => "asset-list-by-province",
            "by-department" => "asset-list-by-department",
            "by-site" => "asset-list-by-site",

            // Asset verification variants
            "asset-verification-per-site-province-date" =>
                "asset-verification-per-site-province-date",
            "asset-verification-not-verified" => "asset-verification-not-verified",
            "asset-verification-verified-by-date-range" =>
                "asset-verification-verified-by-date-range",
            "per-site-province-date" => "asset-verification-per-site-province-date",
            "not-verified" => "asset-verification-not-verified",
            "verified-by-date-range" => "asset-verification-verified-by-date-range",

            // Fine report variants. RPTFines.aspx opens distinct Crystal/HTML
            // screens; they are not the generic fines list.
            "fines-one-vehicle" => "fines-one-vehicle",
            "fines-appear-date" => "fines-appear-date",
            "fines-reissue-submission" => "fines-reissue-submission",
            "fines-traffic-dept-detail" => "fines-traffic-dept-detail",
            "dept-site-period" => "fines-dept-site-period",

            // Taxis menu/report variants. RPTtaxis.aspx and Taxi_Fin_reports.aspx
            // open distinct pages; they are not the generic taxi list.
            "taxis-future-bookings-my-dept" => "taxis-future-bookings-my-dept",
            "taxis-history-bookings-period" => "taxis-history-bookings-period",
            "taxis-requisition-numbers-period" => "taxis-requisition-numbers-period",
            "taxis-per-hire-company" => "taxis-per-hire-company",
            "taxis-list-inservice-per-department" => "taxis-list-inservice-per-department",
            "taxis-list-per-department" => "taxis-list-per-department",
            "taxis-reprint-requisition" => "taxis-reprint-requisition",
            "taxis-reprint-taxi-log" => "taxis-reprint-taxi-log",
            "taxis-fin-general-requisitions" => "taxis-fin-general-requisitions",
            "taxis-fin-requisitions-per-department" => "taxis-fin-requisitions-per-department",
            "taxis-fin-outstanding-logsheets" => "taxis-fin-outstanding-logsheets",
            "taxis-fin-log-odometer-gg" => "taxis-fin-log-odometer-gg",
            "taxis-fin-cancellations" => "taxis-fin-cancellations",
            "taxis-fin-no-objective-or-responsibility" => "taxis-fin-no-objective-or-responsibility",
            "future-bookings-my-dept" => "taxis-future-bookings-my-dept",
            "history-bookings-period" => "taxis-history-bookings-period",
            "requisition-numbers-period" => "taxis-requisition-numbers-period",
            "list-inservice" => "taxis-list-inservice-per-department",
            "list-all" => "taxis-list-per-department",
            "reprint-requisition" => "taxis-reprint-requisition",
            "reprint-taxi-log" => "taxis-reprint-taxi-log",
            "general-requisitions" => "taxis-fin-general-requisitions",
            "requisitions-per-department" => "taxis-fin-requisitions-per-department",
            "outstanding-logsheets" => "taxis-fin-outstanding-logsheets",
            "log-odometer-gg" => "taxis-fin-log-odometer-gg",
            "cancellations" => "taxis-fin-cancellations",
            "no-objective-or-responsibility" => "taxis-fin-no-objective-or-responsibility",

            // Wesbank variants. Regional FinanceRegionWesbankMenu.aspx opens
            // distinct date/province filter screens, not one expenses SP.
            "wesbank-one-vehicle" => "wesbank-one-vehicle",
            "wesbank-one-vehicle-period" => "wesbank-one-vehicle-period",
            "wesbank-one-dept-site" => "wesbank-one-dept-site",
            "wesbank-overfills" => "wesbank-overfills",
            "wesbank-multiple-daily-fuels" => "wesbank-multiple-daily-fuels",
            "one-vehicle-period" => "wesbank-one-vehicle-period",
            "one-dept-site" => "wesbank-one-dept-site",
            "overfills" => "wesbank-overfills",
            "multiple-daily-fuels" => "wesbank-multiple-daily-fuels",

            // Auction variants. RPTAuction.aspx opens distinct Crystal screens.
            "auction-one-vehicle" => "auction-one-vehicle",
            "auction-sale-to-name" => "auction-sale-to-name",
            "auction-one-sort-gg" => "auction-one-sort-gg",
            "auction-one-sort-lot" => "auction-one-sort-lot",
            "sale-to-name" => "auction-sale-to-name",
            "one-auction-sort-gg" => "auction-one-sort-gg",
            "one-auction-sort-lot" => "auction-one-sort-lot",

            // Logbook/logsheet fine-grained variants
            "logbooks-number" => "logbooks-number",
            "logbooks-one-vehicle" => "logbooks-one-vehicle",
            "logsheets-all-outstanding" => "logsheets-all-outstanding",
            "logsheets-one-vehicle" => "logsheets-one-vehicle",
            "logsheets-vehicle-details-per-rek" => "logsheets-vehicle-details-per-rek",
            "logsheets-vehicle-odo-balance" => "logsheets-vehicle-odo-balance",
            "logbook-number" => "logbooks-number",
            "all-outstanding" => "logsheets-all-outstanding",
            "vehicle-details-per-rek" => "logsheets-vehicle-details-per-rek",
            "vehicle-odo-balance" => "logsheets-vehicle-odo-balance",
            "per-dept-site" => "logsheets",
            "period" => "workshop",

            // Losses report variants. losses.aspx opens distinct screens.
            "losses-one-vehicle" => "losses-one-vehicle",
            "losses-all-losses-sorted" => "losses-all-losses-sorted",
            "losses-outstanding-report" => "losses-outstanding-report",
            "losses-with-report" => "losses-with-report",
            "losses-site-period-vip-gg-hire" => "losses-site-period-vip-gg-hire",
            "all-losses-sorted" => "losses-all-losses-sorted",
            "outstanding-report" => "losses-outstanding-report",
            "with-report" => "losses-with-report",
            "site-period-vip-gg-hire" => "losses-site-period-vip-gg-hire",

            // Licence variants. These are distinct License/*.htm and *.aspx
            // screens, not the generic licence list.
            "licences-all-with-model-tare-fee" => "licences-all-with-model-tare-fee",
            "licences-cof-info" => "licences-cof-info",
            "licences-expire-date" => "licences-expire-date",
            "licences-site" => "licences-site",
            "licences-gg-number" => "licences-gg-number",
            "licences-register-number" => "licences-register-number",
            "licences-chassis-number" => "licences-chassis-number",
            "licences-engine-number" => "licences-engine-number",
            "licences-data-workgroup" => "licences-data-workgroup",
            "licences-data-workgroup-latest" => "licences-data-workgroup-latest",
            "licences-received-by-ggmt" => "licences-received-by-ggmt",
            "licences-prov-reg-number" => "licences-prov-reg-number",
            "licences-old-expire-dates" => "licences-old-expire-dates",
            "licences-make-model-fee" => "licences-make-model-fee",
            "licences-month-fees" => "licences-month-fees",
            "licences-sap-info" => "licences-sap-info",
            "licences-dept-sites-period" => "licences-dept-sites-period",
            "gg-number" => "licences-gg-number",
            "prov-reg-number" => "licences-prov-reg-number",
            "register-number" => "licences-register-number",
            "engine-number" => "licences-engine-number",
            "chassis-number" => "licences-chassis-number",
            "all-with-model-tare-fee" => "licences-all-with-model-tare-fee",
            "dept-sites-period" => "licences-dept-sites-period",
            "cof-info" => "licences-cof-info",
            "old-expire-dates" => "licences-old-expire-dates",
            "make-model-fee" => "licences-make-model-fee",
            "data-workgroup" => "licences-data-workgroup",
            "data-workgroup-latest" => "licences-data-workgroup-latest",
            "received-by-ggmt" => "licences-received-by-ggmt",
            "sap-info" => "licences-sap-info",
            "month-fees" => "licences-month-fees",

            // Vehicle report variants. TripReports.aspx ShowReport Items are
            // distinct procedures; remaining vehicle keys are distinct screens
            // and must not collapse into the generic vehicle_master list.
            "vehicles-no-trips" => "vehicles-no-trips",
            "vehicles-no-trips-daterange" => "vehicles-no-trips-daterange",
            "vehicles-per-site" => "vehicles-per-site",
            "vehicles-per-department" => "vehicles-per-department",
            "vehicles-inservice-per-gg" => "vehicles-inservice-per-gg",
            "vehicles-inservice-per-dept" => "vehicles-inservice-per-dept",
            "vehicles-inservice-wesbank" => "vehicles-inservice-wesbank",
            "vehicles-provincial-numbers" => "vehicles-provincial-numbers",
            "vehicles-with-barcodes" => "vehicles-with-barcodes",
            "vehicles-lpg-converted" => "vehicles-lpg-converted",
            "vehicles-replaced-per-dept" => "vehicles-replaced-per-dept",
            "vehicles-older-than-5y-over-120k" => "vehicles-older-than-5y-over-120k",
            "vehicles-older-than-5y-over-120k-period" => "vehicles-older-than-5y-over-120k-period",
            "vehicles-extended-service" => "vehicles-extended-service",
            "vehicles-value-inservice" => "vehicles-value-inservice",
            "vehicles-extras" => "vehicles-extras",
            "vehicles-contract-type-site" => "vehicles-contract-type-site",
            "vehicles-contract-type-department" => "vehicles-contract-type-department",
            "vehicles-contract-type-department-site" => "vehicles-contract-type-department-site",
            "vehicles-selected" => "vehicles-selected",
            "vehicles-els-manual" => "vehicles-els-manual",
            "vehicle-contract-single" => "vehicle-contract-single",
            "vehicle-contract-multiple" => "vehicle-contract-multiple",
            "vehicle-contract-els-manual" => "vehicle-contract-els-manual",
            "all-vehicles" => "all-vehicles",
            "all-users" => "users",
            "one-user" => "users-one",
            "vehicle-by-number" => "vehicle-by-number",
            "inservice-per-gg" => "vehicles-inservice-per-gg",
            "inservice-per-dept" => "vehicles-inservice-per-dept",
            "inservice-wesbank" => "vehicles-inservice-wesbank",
            "provincial-numbers" => "vehicles-provincial-numbers",
            "all-with-barcodes" => "vehicles-with-barcodes",
            "lpg-converted" => "vehicles-lpg-converted",
            "replaced-per-dept" => "vehicles-replaced-per-dept",
            "older-than-5y-over-120k" => "vehicles-older-than-5y-over-120k",
            "older-than-5y-over-120k-period" => "vehicles-older-than-5y-over-120k-period",
            "extended-service" => "vehicles-extended-service",
            "value-inservice" => "vehicles-value-inservice",
            "vehicle-extras" => "vehicles-extras",
            "selected-vehicles" => "vehicles-selected",

            // TripReports.aspx ShowReport / SelectVehicleTrip items are distinct
            // procedures. Do not collapse them into the generic trip-authority key.
            "available-vehicles-site" => "available-vehicles-site",
            "unavailable-vehicles-site" => "unavailable-vehicles-site",
            "users-per-site" => "users-per-site",
            "users-per-department" => "users-per-department",
            "users-all-departments" => "users-all-departments",
            "trips-per-user-all" => "trips-per-user-all",
            "trips-per-user-department" => "trips-per-user-department",
            "trips-for-vehicle" => "trips-for-vehicle",
            "drivers-for-vehicle" => "drivers-for-vehicle",
            "vehicle-utilisation-driver" => "vehicle-utilisation-driver",
            "vehicle-utilisation" => "vehicle-utilisation",
            "trip-count-last-3-months" => "trip-count-last-3-months",
            "trips-open-over-31" => "trips-open-31",
            "trip-authorities-over-25000" => "trip-authorities-over-25000",
            "trip-authorities-over-3500-per-day" => "trip-authorities-over-3500-per-day",
            "els-manual-kilo" => "els-manual-kilo",
            "high-distance-department" => "high-distance-dept",
            "high-distance-all" => "high-distance-all",

            // Department.aspx opens distinct Crystal/HTML screens.
            "departments-sites-contact" => "departments-sites-contact",
            "departments-outstanding-logs-combined" => "departments-outstanding-logs-combined",
            "departments-one-department" => "departments-one-department",
            "departments-one-site" => "departments-one-site",
            "departments-vehicles-manual-logs" => "departments-vehicles-manual-logs",
            "departments-vehicles-els" => "departments-vehicles-els",
            "one-department" => "departments-one-department",
            "one-site" => "departments-one-site",
            "all-dept-site-count" => "departments-sites",
            "all-dept-site-contact" => "departments-sites-contact",
            "outstanding-logs-combined" => "departments-outstanding-logs-combined",
            "vehicles-on-manual-logs" => "departments-vehicles-manual-logs",
            "vehicles-on-els" => "departments-vehicles-els",

            // Fuelcard/RPTFuelcard.aspx opens distinct Crystal/HTML screens.
            "fuelcards-one-vehicle" => "fuelcards-one-vehicle",
            "fuelcards-one-vehicle-handout" => "fuelcards-one-vehicle-handout",
            "fuelcards-expire-date" => "fuelcards-expire-date",
            "fuelcards-one-site-expire-date" => "fuelcards-one-site-expire-date",
            "fuelcards-dept-site-expire-period" => "fuelcards-dept-site-expire-period",
            "fuelcards-dept-site" => "fuelcards-dept-site",
            "fuelcards-replace-reason" => "fuelcards-replace-reason",
            "fuelcards-one-pan" => "fuelcards-one-pan",
            "fuelcards-wesbank-new-cards" => "fuelcards-wesbank-new-cards",
            "fuelcards-pool-vehicles" => "fuelcards-pool-vehicles",
            "fuelcards-vip-vehicles" => "fuelcards-vip-vehicles",
            "one-pan" => "fuelcards-one-pan",
            "one-vehicle-handout" => "fuelcards-one-vehicle-handout",
            "replace-reason" => "fuelcards-replace-reason",
            "dept-site-expire-period" => "fuelcards-dept-site-expire-period",
            "dept-site" => "fuelcards-dept-site",
            "one-site-expire-date" => "fuelcards-one-site-expire-date",
            "pool-vehicles" => "fuelcards-pool-vehicles",
            "vip-vehicles" => "fuelcards-vip-vehicles",
            "wesbank-new-cards" => "fuelcards-wesbank-new-cards",

            // Workshop report variants
            "workshop-one-vehicle" => "workshop-one-vehicle",
            "workshop-print-job-card" => "workshop-print-job-card",
            "workshop-in-shop" => "workshop-in-shop",
            "workshop-merchants" => "workshop-merchants",
            "print-job-card" => "workshop-print-job-card",
            "in-workshop" => "workshop-in-shop",
            "all-merchants" => "workshop-merchants",

            // Direct clearances shorthand from report menu

            // Tariff menu shorthands
            "class-codes-with-tariffs" => "tariffs-class-codes",
            "licence-fees" => "tariffs-licence-fees",
            "make-model-with-tariffs" => "tariffs-make-model",
            "private-taxi-tariffs" => "tariffs-private-taxi",
            "nom-vehicles-without-tariffs" => "nom-vehicles-without-tariff",

            _ => key,
        };
    }

    private async Task<LegacyReportResultDto?> TryExecuteStoredProcedureAsync(
        LegacyReportDefinition definition,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        if (definition.StoredProcedureItem is null && definition.StoredProcedureName is null)
        {
            return null;
        }

        var parameters =
            definition.BuildStoredProcedureParameters?.Invoke(filters)
            ?? Array.Empty<LegacyStoredProcedureParameter>();
        if (
            parameters.Count == 0
            && filters.Any(kvp =>
                !string.Equals(kvp.Key, "view", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kvp.Value)
            )
            && definition.BuildStoredProcedureParameters is null
            && definition.StoredProcedureName is null
        )
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
            var procedureName = GetStoredProcedureName(definition);
            await using (var existsCommand = connection.CreateCommand())
            {
                existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
                var procedureParameter = existsCommand.CreateParameter();
                procedureParameter.ParameterName = "@procedureName";
                procedureParameter.DbType = DbType.String;
                procedureParameter.Value = procedureName;
                existsCommand.Parameters.Add(procedureParameter);
                if (await existsCommand.ExecuteScalarAsync(cancellationToken) is null or DBNull)
                {
                    return null;
                }
            }

            await using var command = connection.CreateCommand();
#pragma warning disable CA2100
            command.CommandText = procedureName;
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

    private static string GetStoredProcedureName(LegacyReportDefinition definition) =>
        definition.StoredProcedureName
        ?? $"dbo.DEV_REP_{definition.StoredProcedureItem}";

    private static async Task<LegacyReportResultDto> ReadDynamicResultAsync(
        System.Data.Common.DbDataReader reader,
        LegacyReportDefinition definition,
        CancellationToken cancellationToken
    )
    {
        var columns = new List<LegacyReportColumnDto>();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var header = reader.GetName(index);
            columns.Add(new LegacyReportColumnDto { Key = header, Header = header });
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
            TotalCount = rows.Count,
        };
    }

    private IReadOnlyDictionary<string, LegacyReportDefinition> BuildDefinitions()
    {
        return new Dictionary<string, LegacyReportDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["asset-verification-per-site-province-date"] = new(
                "asset-verification-per-site-province-date",
                "Report Per Site / Province / Verification Date",
                "Asset_Verification/RPT_asset_verification_2a.aspx",
                null,
                BuildAssetVerificationPerSiteProvinceDateAsync,
                "Rendered through the compatibility asset-verification repository so client-era columns remain available when expanded fields are absent."
            ),

            ["asset-verification-not-verified"] = new(
                "asset-verification-not-verified",
                "Vehicles Not Verified",
                "Asset_Verification/RPT_Not_Verified.aspx",
                null,
                BuildAssetVerificationNotVerifiedAsync,
                "Uses the legacy active-vehicle/current-contract rule and compatibility verification lookup without requiring modern Asset_Verification columns."
            ),

            ["asset-verification-verified-by-date-range"] = new(
                "asset-verification-verified-by-date-range",
                "Vehicles Verified By Date Range - Excel Report",
                "Asset_Verification/RPT_vehicles_verified_by_date_2.aspx",
                null,
                BuildAssetVerificationVerifiedByDateRangeAsync,
                "Rendered through the compatibility asset-verification repository so the date range works with both client-era and expanded verification fields."
            ),

            ["asset-list"] = new(
                "asset-list",
                "Asset List: New & In-Service Vehicles",
                "Finance/AssetVehicleReports.aspx",
                "AllNewAndInServiceVehicles",
                BuildAssetListAsync,
                "Rendered from the legacy all-assets procedure; the filtered menu entries use the companion procedure below.",
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["asset-list-by-province"] = new(
                "asset-list-by-province",
                "Asset List: New & In-Service Vehicles By Province",
                "FISReports/GetVehicleInserviceReports.aspx?Mode=Province",
                "FilterNewAndInServiceVehicles",
                BuildAssetListByProvinceAsync,
                "Rendered from the legacy filtered asset-list procedure (SearchType=1).",
                BuildAssetListProvinceParameters
            ),

            ["asset-list-by-department"] = new(
                "asset-list-by-department",
                "Asset List: New & In-Service Vehicles By Department",
                "FISReports/GetVehicleInserviceReports.aspx?Mode=Department",
                "FilterNewAndInServiceVehicles",
                BuildAssetListByDepartmentAsync,
                "Rendered from the legacy filtered asset-list procedure (SearchType=2).",
                BuildAssetListDepartmentParameters
            ),

            ["asset-list-by-site"] = new(
                "asset-list-by-site",
                "Asset List: New & In-Service Vehicles By Site",
                "FISReports/GetVehicleInserviceReports.aspx?Mode=Site",
                "FilterNewAndInServiceVehicles",
                BuildAssetListBySiteAsync,
                "Rendered from the legacy filtered asset-list procedure (SearchType=3).",
                BuildAssetListSiteParameters
            ),

            ["audit-trail"] = new(
                "audit-trail",
                "Audit Trail Reports",
                "Finance/GetFinancialAditTrailReportsDateRange.aspx",
                "ELSAuditTrailReport",
                BuildAuditTrailAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),
            ["audit-trail-department"] = DistinctArchivedScreen(
                "audit-trail-department",
                "Audit Trail grouped by Department in a date range",
                "Finance/GetFinancialAditTrailReportsDateRange.aspx?Mode=Department",
                "AuditTrail.aspx item 1 is a Mode=Department date-range screen. It is not the generic ELS audit-trail procedure."
            ),
            ["audit-trail-site"] = DistinctArchivedScreen(
                "audit-trail-site",
                "Audit Trail grouped by Site in a date range",
                "Finance/GetFinancialAditTrailReportsDateRange.aspx?Mode=Site",
                "AuditTrail.aspx item 2 is a Mode=Site date-range screen. It is not the generic ELS audit-trail procedure."
            ),
            ["audit-trail-vehicle"] = DistinctArchivedScreen(
                "audit-trail-vehicle",
                "Audit Trail grouped per Vehicle in a date range",
                "Finance/GetFinancialAditTrailReportsDateRange.aspx?Mode=Vehicle",
                "AuditTrail.aspx item 3 is a Mode=Vehicle date-range screen. It is not the generic ELS audit-trail procedure."
            ),
            ["users"] = DistinctArchivedScreen(
                "users",
                "Users Report Menu",
                "Users/Users.aspx",
                "FIS_Report.aspx item 20 opens Users/Users.aspx. It is not the audit-trail procedure."
            ),
            ["users-one"] = DistinctArchivedScreen(
                "users-one",
                "Information On One User",
                "Users/RPT_One_User.aspx",
                "Users.aspx item 1 is a one-user Crystal/HTML screen. It is not the audit-trail procedure."
            ),
            ["users-added"] = DistinctArchivedScreen(
                "users-added",
                "Information On All Users Accounts Added On The System",
                "Users/RPT_main_users_added.aspx",
                "Users.aspx item 3 is the added-accounts screen. It is not the audit-trail procedure."
            ),

            ["capture-activity"] = new(
                "capture-activity",
                "Capture Activity",
                "Modern report (no direct legacy equivalent)",
                null,
                BuildCaptureActivityAsync,
                "No direct legacy equivalent exists; this grid is a dynamic modern approximation using capture timestamps across legacy-backed tables."
            ),

            ["contract-history"] = new(
                "contract-history",
                "Contract History",
                "Logs/RPT_Contracts_per_vehicle.aspx",
                null,
                BuildContractHistoryAsync,
                "Legacy page is a composite/folder view. This approximation flattens contract history into a legacy-style dynamic grid."
            ),

            ["contracts"] = new(
                "contracts",
                "Contracts Report",
                "/FISReports/Contracts/Contracts.aspx",
                "VehicleContractsAuditReport",
                BuildContractsAsync,
                "The archived legacy source exposes this as a custom OpenReport_2/ActiveReports item but does not provide a matching stored procedure definition. This compatibility result is a labelled table projection and is not the operational invoice/billing report.",
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),
            ["contract-vehicle-single"] = DistinctArchivedScreen(
                "contract-vehicle-single",
                "Information On A Vehicle Contracts",
                "FISReports/Contracts/RPT_Vehicle_Contracts_1.aspx",
                "Contracts.aspx item 1 is a one-vehicle contract screen. It is not VehicleContractsAuditReport."
            ),
            ["contract-vehicle-multiple"] = DistinctArchivedScreen(
                "contract-vehicle-multiple",
                "Information On Multiple Vehicle Contracts",
                "FISReports/Contracts/RPT_Multiple_Contracts_1.aspx",
                "Contracts.aspx item 2 is a multiple-vehicle contract screen. It is not VehicleContractsAuditReport."
            ),
            ["contracts-expiring-by-date"] = DistinctArchivedScreen(
                "contracts-expiring-by-date",
                "Information On All Contracts That Will Expire By a Specified Date",
                "FISReports/Contracts/RPT_Daily_Contracts_1.aspx",
                "Contracts.aspx item 4 is the expiry-by-date screen. It is not VehicleContractsAuditReport."
            ),
            ["contract-summary"] = DistinctArchivedScreen(
                "contract-summary",
                "Contract summarized report",
                "FISReports/Contracts/Contract_Report.aspx",
                "Contracts.aspx item 5 is the summarized contract screen. It is not VehicleContractsAuditReport."
            ),
            ["contract-trip-authority-single"] = DistinctArchivedScreen(
                "contract-trip-authority-single",
                "Vehicle Trip Authorities per GG or GP Number",
                "FISReports/Contracts/RPT_Vehicle_Trip_1.aspx",
                "Contracts.aspx item 6 is a trip-authority screen. It is not VehicleContractsAuditReport."
            ),
            ["contract-trip-authority-multiple"] = DistinctArchivedScreen(
                "contract-trip-authority-multiple",
                "Multiple Vehicle Trip Authorities per GG or GP Number",
                "FISReports/Contracts/RPT_Multiple_Vehicle_Trip_1.aspx",
                "Contracts.aspx item 7 is a multiple trip-authority screen. It is not VehicleContractsAuditReport."
            ),
            ["contract-trip-authority-dept-site-date"] = DistinctArchivedScreen(
                "contract-trip-authority-dept-site-date",
                "Vehicle Trip Authorities per Dept, Site or Date",
                "FISReports/Contracts/RPT_Vehicle_Trip_3.aspx",
                "Contracts.aspx item 8 is a dept/site/date trip-authority screen. It is not VehicleContractsAuditReport."
            ),
            ["contracts-checklist"] = DistinctArchivedScreen(
                "contracts-checklist",
                "Daily & Monthly Contract check list report",
                "FISReports/Contracts/check_list.aspx",
                "Contracts.aspx item 9 is the checklist screen. It is not VehicleContractsAuditReport."
            ),
            ["contracts-no-distance"] = DistinctArchivedScreen(
                "contracts-no-distance",
                "Vehicle Contracts with NO distance traveled",
                "FISReports/Contracts/RPT_no_dist_1.aspx",
                "Contracts.aspx item 10 is the no-distance screen. It is not VehicleContractsAuditReport."
            ),
            ["contracts-per-dept-period"] = DistinctArchivedScreen(
                "contracts-per-dept-period",
                "Contracts for a Department for period",
                "FISReports/Contracts/RPT_PerDept_Period.aspx",
                "Contracts.aspx item 11 is the department-period screen. It is not VehicleContractsAuditReport."
            ),
            ["contracts-fleet-reports"] = DistinctArchivedScreen(
                "contracts-fleet-reports",
                "Permanent Section - Contracts Fleet Reports",
                "FISReports/FleetReportsMenu.aspx",
                "Contracts.aspx item 12 opens the GFleet permanent-section menu. It is not VehicleContractsAuditReport."
            ),
            ["lease-nom-contract-split"] = DistinctArchivedScreen(
                "lease-nom-contract-split",
                "Lease Vehicles Split Report for Vehicles not on Contract and those on Contract",
                "FISReports/LeaseNOMVehicleContract/NOMVehicleContractReport.aspx",
                "Contracts.aspx NOM item 1 is the lease split screen. It is not VehicleContractsAuditReport."
            ),

            ["departments-sites"] = new(
                "departments-sites",
                "Departments and Sites",
                "Department/Department.aspx",
                null,
                BuildDepartmentsSitesAsync,
                ""
            ),
            ["departments-one-department"] = DistinctArchivedScreen(
                "departments-one-department",
                "One Department",
                "Department/rpt_single_dept_1.aspx",
                "Department.aspx item 1 is the one-department Crystal screen. It is not the all-departments list."
            ),
            ["departments-one-site"] = DistinctArchivedScreen(
                "departments-one-site",
                "One Site",
                "Department/rpt_single_site_1.aspx",
                "Department.aspx item 2 is the one-site Crystal screen. It is not the all-departments list."
            ),
            ["departments-sites-contact"] = DistinctArchivedScreen(
                "departments-sites-contact",
                "All Departments & Sites with contact details",
                "Department/rpt_all_sites.aspx",
                "Department.aspx item 4 is the contact-details screen. It is not the vehicle-count list."
            ),
            ["departments-vehicles-els"] = DistinctArchivedScreen(
                "departments-vehicles-els",
                "List of Vehicles on ELS",
                "Department/RPT_els_dept_period_main.aspx",
                "Department.aspx item 5 is the ELS vehicle screen. It is not the all-departments list."
            ),
            ["departments-vehicles-manual-logs"] = DistinctArchivedScreen(
                "departments-vehicles-manual-logs",
                "List of Vehicles on Manual LOGSHEETS",
                "Department/RPT_logs_dept_period_main.aspx",
                "Department.aspx item 6 is the manual-logsheet screen. It is not the all-departments list."
            ),
            ["departments-outstanding-logs-combined"] = DistinctArchivedScreen(
                "departments-outstanding-logs-combined",
                "Outstanding Logs per Dept : ELS and Manual Logs Combined",
                "Department/RPT_uits_els_en_logs_dept_period_main.aspx",
                "Department.aspx item 7 is the combined outstanding-logs screen. It is not the all-departments list."
            ),

            ["fines"] = new(
                "fines",
                "Fines Reports",
                "Fines/RPTFines.aspx",
                null,
                BuildFinesAsync,
                ""
            ),
            ["fines-one-vehicle"] = DistinctFineScreen(
                "fines-one-vehicle",
                "Fines Report on ONE Vehicle",
                "fines/RPT_one_num_main_Fines.htm"
            ),
            ["fines-dept-site-period"] = DistinctFineScreen(
                "fines-dept-site-period",
                "Fines Report, for a Dept / Site, for a period",
                "fines/selectfines.htm"
            ),
            ["fines-appear-date"] = DistinctFineScreen(
                "fines-appear-date",
                "Fines Report on Appear Date",
                "fines/RPT_app_date_main_Fines.htm"
            ),
            ["fines-reissue-submission"] = DistinctFineScreen(
                "fines-reissue-submission",
                "Submission to Re-Issue Fine in Transport Officer's Name",
                "fines/RPT_letter_main_Fines.aspx"
            ),
            ["fines-traffic-dept-detail"] = DistinctFineScreen(
                "fines-traffic-dept-detail",
                "Traffic Dept Detail",
                "fines/RPT_traffic_all_report.aspx"
            ),

            ["losses"] = new(
                "losses",
                "Losses Reports",
                "Losses/losses.aspx",
                null,
                BuildLossesAsync,
                ""
            ),
            ["losses-one-vehicle"] = DistinctArchivedScreen(
                "losses-one-vehicle",
                "Losses for one vehicle",
                "losses/RPT_loss_per_vehicle.htm",
                "losses.aspx item 1 is a one-vehicle Crystal/HTML screen. It is not the generic losses list."
            ),
            ["losses-all-losses-sorted"] = DistinctArchivedScreen(
                "losses-all-losses-sorted",
                "All Losses Sorted By Loss type, Department number GG number or Loss Date",
                "losses/RPT_All_Losses_menu.aspx",
                "losses.aspx item 2 opens the all-losses sort menu. It is not the generic losses list."
            ),
            ["losses-outstanding-report"] = DistinctArchivedScreen(
                "losses-outstanding-report",
                "Losses where Report from Department is outstanding",
                "losses/RPT_NoReport.aspx",
                "losses.aspx item 3 is the outstanding-report screen. It is not the generic losses list."
            ),
            ["losses-with-report"] = DistinctArchivedScreen(
                "losses-with-report",
                "Losses where Reports from Departments were supplied",
                "losses/RPT_WithReport.aspx",
                "losses.aspx item 4 is the with-report screen. It is not the generic losses list."
            ),
            ["losses-site-period-vip-gg-hire"] = DistinctArchivedScreen(
                "losses-site-period-vip-gg-hire",
                "Losses Report for a Site, for a Period, for VIP/GG, for Hire Type",
                "losses/RPT_dept_periodVIP_main_losses.aspx",
                "losses.aspx item 5 is the site/period/VIP screen. It is not the generic losses list."
            ),

            ["manuals"] = new(
                "manuals",
                "Manuals Menu",
                "Manuals/RPTmanuals.aspx",
                null,
                BuildManualsAsync,
                "Legacy manuals reporting is a navigation menu of manual documents. This result preserves one-to-one menu entries and targets in a dynamic grid."
            ),

            ["high-distance-all"] = new(
                "high-distance-all",
                "High Distance Vehicles (All Departments)",
                "ShowReport.aspx?Item=KiloAudit",
                "KiloAudit",
                BuildHighDistanceAllAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["high-distance-dept"] = new(
                "high-distance-dept",
                "High Distance Vehicles (Department)",
                "ShowReport.aspx?Item=KiloAudit&DepartmentID=...",
                "KiloAudit",
                BuildHighDistanceDeptAsync,
                BuildStoredProcedureParameters: filters =>
                    BuildOptionalParameterList(
                        ("@DepartmentID", (object?)GetShort(filters, "dept"), DbType.Int16)
                    )
            ),

            ["incorrect-quantities"] = new(
                "incorrect-quantities",
                "Report to show incorrect calculated quantities",
                "Finance/GeneratedReports.aspx?key=9.3%20Report%20to%20show%20incorrect%20calculated%20quantities",
                null,
                BuildIncorrectQuantitiesAsync,
                "Legacy generated report output is approximated from vehicle odometer and quantity-related fields in vehicle_master."
            ),

            ["licences"] = new(
                "licences",
                "Licence Reports",
                "License/RPTLicence.aspx",
                null,
                BuildLicencesAsync,
                "Legacy licence reports include multiple one-vehicle and grouped variants. This approximation consolidates core licence fields with vehicle, model, status, site, and licence-fee data."
            ),

            ["logbooks"] = new(
                "logbooks",
                "Logbook Report for a Department/Site and Period",
                "Logbook/RPT_dept_period_report_Logbk.aspx",
                null,
                BuildLogbookDepartmentPeriodAsync,
                "Uses the legacy Logbook report joins and filters. The client database remains authoritative for any procedure/view-backed report variant."
            ),

            ["logbooks-one-vehicle"] = new(
                "logbooks-one-vehicle",
                "Logbook Report on One Vehicle",
                "Logbook/RPT_one_num_report_Logbk.aspx",
                null,
                BuildLogbookOneVehicleAsync,
                "Uses the legacy Logbook report joins and exact GG/GP vehicle filter."
            ),

            ["logbooks-number"] = new(
                "logbooks-number",
                "Logbook Report on a Logbook Number",
                "Logbook/RPT_one_lbnum_report_Logbk.aspx",
                null,
                BuildLogbookNumberAsync,
                "Uses the legacy Logbook report joins and contains matching on begin_num."
            ),

            ["logsheets"] = new(
                "logsheets",
                "Logsheet Report per Department or Site",
                "Logs/RPT_Logs_per_Dept_1.aspx",
                null,
                BuildLogsheetDepartmentOrSiteAsync,
                "Uses the legacy active-contract, permanent-vehicle, twelve-month logsheet report path."
            ),

            ["logsheets-one-vehicle"] = new(
                "logsheets-one-vehicle",
                "Log Report on One Vehicle",
                "Logs/RPT_Logs_per_vehicle.aspx",
                null,
                BuildLogsheetOneVehicleAsync,
                "Uses the legacy latest-logsheet vehicle report projection and odometer-gap calculation."
            ),

            ["logsheets-vehicle-odo-balance"] = new(
                "logsheets-vehicle-odo-balance",
                "Report per Vehicle - Log ODO Balance",
                "Logs/RPT_logs_per_vehicle_2.aspx",
                null,
                BuildLogsheetOdoBalanceAsync,
                "Uses the legacy vehicle lookup and sequential logsheet odometer balance calculation."
            ),

            ["logsheets-vehicle-details-per-rek"] = new(
                "logsheets-vehicle-details-per-rek",
                "Vehicle Details per Requisition Number",
                "Logs/RPT_logsheet_per_rek.aspx",
                null,
                BuildLogsheetPerRequisitionAsync,
                "Uses the legacy requisition contains search and vehicle/logsheet projection."
            ),

            ["logsheets-all-outstanding"] = new(
                "logsheets-all-outstanding",
                "All Outstanding Logsheets",
                "Logs/RPT_uits_els_en_logs_geld_main.aspx",
                null,
                BuildLogsheetsAllOutstandingAsync,
                "The legacy report is owned by DEV_SEL_All_Logs; no direct-DML or table approximation is used when that procedure is unavailable.",
                StoredProcedureName: "dbo.DEV_SEL_All_Logs"
            ),

            ["management"] = new(
                "management",
                "Management Reports",
                "Management_Reports/Management.aspx",
                null,
                BuildManagementAsync,
                ""
            ),
            ["management-ggmt"] = DistinctArchivedScreen(
                "management-ggmt",
                "GGMT Management Reports",
                "Management_Reports/management2.aspx",
                "Management.aspx item 1 is the GGMT management submenu. It is not the generic management list."
            ),
            ["management-incorrect-captured-data"] = DistinctArchivedScreen(
                "management-incorrect-captured-data",
                "Report On Incorrect Captured Data",
                "Management_Reports/incorrect.aspx",
                "Management.aspx item 2 is the incorrect-captured-data submenu. It is not the generic management list."
            ),
            ["management-site-info"] = DistinctArchivedScreen(
                "management-site-info",
                "FIS Site Management Information",
                "Management_Reports/System_Info/SysInfo_menu.aspx",
                "Management.aspx item 3 is the site-management information submenu. It is not the generic management list."
            ),

            ["previous-fin-year"] = new(
                "previous-fin-year",
                "Previous Fin Year Reports",
                "Finance/PreviousFinYear.aspx",
                null,
                BuildPreviousFinYearMenuAsync,
                "Legacy flow presents two report items. This modern entry returns a selectable summary of those same report options."
            ),

            ["previous-fin-year-manual-logs"] = new(
                "previous-fin-year-manual-logs",
                "Previous Fin Year Manual Logsheet Kilos Captured in Current Fin Year",
                "ShowReport.aspx?Item=PreviousFinYearManualLogsCapturedInCurrentFinYear",
                "PreviousFinYearManualLogsCapturedInCurrentFinYear",
                BuildPreviousFinYearManualLogsAsync,
                ""
            ),

            ["previous-fin-year-vip-taxi"] = new(
                "previous-fin-year-vip-taxi",
                "Previous Fin Year VIP & Taxi Requisitions Captured in Current Fin Year",
                "ShowReport.aspx?Item=PreviousFinYearKiloLogsCapturedInCurrentFinYear",
                "PreviousFinYearKiloLogsCapturedInCurrentFinYear",
                BuildPreviousFinYearVipTaxiAsync,
                ""
            ),

            ["registration-certificates"] = new(
                "registration-certificates",
                "Registration Certificates",
                "ScanDocs/Reg_Cert_Menu.aspx",
                null,
                BuildRegistrationCertificatesAsync,
                "Legacy registration certificate flow is menu-driven. This approximation uses vehicle master registration certificate fields."
            ),
            ["registration-certificate-one-vehicle"] = DistinctArchivedScreen(
                "registration-certificate-one-vehicle",
                "Registration Certificate for One Vehicle",
                "scandocs/RPT_ListOne.aspx",
                "Reg_Cert_Menu.aspx item 2 is the one-vehicle certificate screen. It is not the all-vehicles certificate list."
            ),

            ["asset-verification"] = DistinctArchivedScreen(
                "asset-verification",
                "Asset Verification Reports Menu",
                "Asset_Verification/RPT_Asset_Verification.aspx",
                "FIS_Report.aspx item 3 opens the asset-verification menu. It is not the asset-list procedure."
            ),

            ["class-code"] = DistinctArchivedScreen(
                "class-code",
                "Class Codes, Make & Model",
                "Class_Code/RPT_C_M_M.aspx",
                "Class_Code.aspx item 1 is a class/make/model Crystal screen. It is not published tariffs."
            ),
            ["class-code-totals"] = DistinctArchivedScreen(
                "class-code-totals",
                "Class Codes & Totals",
                "Vehicles/Test_1.aspx",
                "Class_Code.aspx item 2 is a class-code totals screen. It is not published tariffs."
            ),

            ["tariffs-class-codes"] = DistinctArchivedScreen(
                "tariffs-class-codes",
                "Class codes with tariffs",
                "validation/RPT_Class_Tariff.aspx",
                "RPTtariffs.aspx item 1 is the class-tariff Crystal screen. It is not ShowReport Item=Tariffs."
            ),
            ["tariffs-licence-fees"] = DistinctArchivedScreen(
                "tariffs-licence-fees",
                "Licence fees",
                "validation/RPT_licence_Tariff.aspx",
                "RPTtariffs.aspx item 2 is the licence-fee Crystal screen. It is not ShowReport Item=Tariffs."
            ),
            ["tariffs-make-model"] = DistinctArchivedScreen(
                "tariffs-make-model",
                "Make Model with tariffs",
                "validation/RPT_Model_Tariff.aspx",
                "RPTtariffs.aspx item 3 is the make/model tariff Crystal screen. It is not ShowReport Item=Tariffs."
            ),
            ["tariffs-private-taxi"] = DistinctArchivedScreen(
                "tariffs-private-taxi",
                "Private Taxi Tariffs",
                "validation/RPT_Taxi_Tariff.aspx",
                "RPTtariffs.aspx item 4 is the private-taxi tariff Crystal screen. It is not ShowReport Item=Tariffs."
            ),

            ["fuel-cards"] = DistinctArchivedScreen(
                "fuel-cards",
                "FuelCard Reports Menu",
                "fuelcard/RPTFuelcard.aspx",
                "FIS_Report.aspx item 10 opens Fuelcard/RPTFuelcard.aspx. It is not Wesbank First Auto."
            ),
            ["fuelcards-one-vehicle"] = DistinctArchivedScreen(
                "fuelcards-one-vehicle",
                "Fuelcard Report for a Vehicle",
                "fuelcard/RPT_one_vehicle_main_FuelCard.htm",
                "RPTFuelcard.aspx item 1 is a one-vehicle fuelcard screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-one-pan"] = DistinctArchivedScreen(
                "fuelcards-one-pan",
                "Fuelcard Report for a PAN Number",
                "fuelcard/RPT_one_pan_main_FuelCard.htm",
                "RPTFuelcard.aspx item 2 is a PAN fuelcard screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-one-vehicle-handout"] = DistinctArchivedScreen(
                "fuelcards-one-vehicle-handout",
                "Fuelcard Handout Report",
                "fuelcard/RPT_one_veh_handout_main_FuelCard.htm",
                "RPTFuelcard.aspx item 3 is the handout screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-expire-date"] = DistinctArchivedScreen(
                "fuelcards-expire-date",
                "Fuelcard Report for an Expire Date",
                "fuelcard/RPT_expdate_main_FuelCard.aspx",
                "RPTFuelcard.aspx item 5 is the expire-date screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-replace-reason"] = DistinctArchivedScreen(
                "fuelcards-replace-reason",
                "Fuelcard Report for a Replace Reason",
                "fuelcard/RPT_replace_reason_main_FuelCard.htm",
                "RPTFuelcard.aspx item 6 is the replace-reason screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-dept-site-expire-period"] = DistinctArchivedScreen(
                "fuelcards-dept-site-expire-period",
                "Fuelcard Report, for a Department / Site, for an Expire Period",
                "fuelcard/RPT_dept_period_main_FuelCard.aspx",
                "RPTFuelcard.aspx item 7 is the department/site expire-period screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-dept-site"] = DistinctArchivedScreen(
                "fuelcards-dept-site",
                "Fuelcard Report for a Department / Site",
                "fuelcard/RPT_dept_main_FuelCard.htm",
                "RPTFuelcard.aspx item 8 is the department/site fuelcard screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-one-site-expire-date"] = DistinctArchivedScreen(
                "fuelcards-one-site-expire-date",
                "Fuelcard Report for a Dept Site and Expire Date",
                "fuelcard/RPT_one_site_expdate_main_FuelCard.htm",
                "RPTFuelcard.aspx item 9 is the site expire-date screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-pool-vehicles"] = DistinctArchivedScreen(
                "fuelcards-pool-vehicles",
                "Fuelcard Report for POOL Vehicles",
                "fuelcard/RPT_pool_main_FuelCard.htm",
                "RPTFuelcard.aspx item 10 is the pool-vehicle fuelcard screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-vip-vehicles"] = DistinctArchivedScreen(
                "fuelcards-vip-vehicles",
                "Fuelcard Report for VIP Vehicles",
                "fuelcard/RPT_vip_main_FuelCard.htm",
                "RPTFuelcard.aspx item 11 is the VIP fuelcard screen. It is not Wesbank First Auto."
            ),
            ["fuelcards-wesbank-new-cards"] = DistinctArchivedScreen(
                "fuelcards-wesbank-new-cards",
                "Wesbank Application for New FuelCards",
                "fuelcard/RPT_wesbank_main_FuelCard.aspx",
                "RPTFuelcard.aspx item 12 is the new-card application screen. It is not Wesbank First Auto expenses."
            ),

            ["tariffs-class-2007"] = new(
                "tariffs-class-2007",
                "Published Tariffs (2007 and Earlier)",
                "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
                "Tariffs",
                BuildTariffsClass2007Async,
                BuildStoredProcedureParameters: filters =>
                    BuildOptionalParameterList(
                        ("@FinYear", (object?)GetFinancialYear(filters), DbType.Int32)
                    )
            ),

            ["tariffs-fin-year"] = new(
                "tariffs-fin-year",
                "Published Tariffs by Financial Year",
                "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
                "Tariffs",
                BuildTariffsFinYearAsync,
                BuildStoredProcedureParameters: filters =>
                    BuildOptionalParameterList(
                        ("@FinYear", (object?)GetFinancialYear(filters), DbType.Int32)
                    )
            ),

            ["tariffs-per-vehicle"] = new(
                "tariffs-per-vehicle",
                "Tariffs per Vehicle",
                "ShowReport.aspx?Item=TariffsPerVehicle",
                "TariffsPerVehicle",
                BuildTariffsPerVehicleAsync,
                BuildStoredProcedureParameters: _ =>
                    [new LegacyStoredProcedureParameter("@FinYear", 0, DbType.Int32)]
            ),

            ["taxis"] = new(
                "taxis",
                "Taxi Reports Menu",
                "Taxis/RPTtaxis.aspx",
                null,
                BuildTaxisMenuAsync,
                "Legacy taxis reporting opens from a menu page. This dynamic result preserves the same menu entries and targets."
            ),

            ["taxis-list-per-department"] = new(
                "taxis-list-per-department",
                "Report On All Taxis in various Departments",
                "Taxis/RPT_list_of_taxis_per_department.aspx",
                null,
                BuildTaxisListPerDepartmentAsync,
                "Legacy report lists requisition numbers with department description. This approximation uses taxis + department data."
            ),

            ["taxis-list-inservice-per-department"] = new(
                "taxis-list-inservice-per-department",
                "Report On All Taxis in service in various Departments",
                "Taxis/RPT_list_of_taxis_inservice_per_department.aspx",
                null,
                BuildTaxisListInServicePerDepartmentAsync,
                "Legacy report lists in-service taxi requisitions with department description. This approximation applies in-service vehicle status filtering."
            ),

            ["taxis-financial"] = new(
                "taxis-financial",
                "Financial Reports: Taxis",
                "Taxis/Taxi_Fin_reports.aspx",
                null,
                BuildTaxisFinancialAsync,
                "Legacy taxi financial pages are custom forms. This approximation uses the legacy Taxis table and related department/site data."
            ),
            ["taxis-future-bookings-my-dept"] = DistinctArchivedScreen(
                "taxis-future-bookings-my-dept",
                "Future booking made for my department.",
                "Taxis/RPT_My_reqs.aspx",
                "RPTtaxis.aspx item 1 is the future-bookings screen. It is not the generic taxi list."
            ),
            ["taxis-history-bookings-period"] = DistinctArchivedScreen(
                "taxis-history-bookings-period",
                "History bookings for period",
                "Taxis/RPT_My_reqs2.aspx",
                "RPTtaxis.aspx item 2 is the history-bookings screen. It is not the generic taxi list."
            ),
            ["taxis-requisition-numbers-period"] = DistinctArchivedScreen(
                "taxis-requisition-numbers-period",
                "List of Requisition Numbers for a period",
                "Taxis/RPT_reqno_taxi_main.aspx",
                "RPTtaxis.aspx item 3 is the requisition-number screen. It is not the generic taxi list."
            ),
            ["taxis-per-hire-company"] = DistinctArchivedScreen(
                "taxis-per-hire-company",
                "Taxis Per Hire Company",
                "Taxis/RPT_taxis_per_company1_c.aspx",
                "RPTtaxis.aspx item 4 is the hire-company screen. It is not the generic taxi list."
            ),
            ["taxis-reprint-requisition"] = DistinctArchivedScreen(
                "taxis-reprint-requisition",
                "Reprint A Requisition",
                "Taxis/Report_Request_GGVIP_reprint_1_2.aspx",
                "RPTtaxis.aspx item 7 is the requisition reprint screen. It is not the generic taxi list."
            ),
            ["taxis-reprint-taxi-log"] = DistinctArchivedScreen(
                "taxis-reprint-taxi-log",
                "Reprint A Taxi Log",
                "Taxis/Report_Reprint_Taxi_Log_1.aspx",
                "RPTtaxis.aspx item 8 is the taxi-log reprint screen. It is not the generic taxi list."
            ),
            ["taxis-fin-general-requisitions"] = DistinctArchivedScreen(
                "taxis-fin-general-requisitions",
                "General Reports: Requisitions",
                "Taxis/RPT_General_1.aspx",
                "Taxi_Fin_reports.aspx item 1 is the general requisitions screen. It is not the generic taxi-financial projection."
            ),
            ["taxis-fin-requisitions-per-department"] = DistinctArchivedScreen(
                "taxis-fin-requisitions-per-department",
                "Requisitions per Department",
                "Taxis/RPT_dept_Invoices_1.aspx",
                "Taxi_Fin_reports.aspx item 2 is the department requisitions screen. It is not the generic taxi-financial projection."
            ),
            ["taxis-fin-outstanding-logsheets"] = DistinctArchivedScreen(
                "taxis-fin-outstanding-logsheets",
                "All Outstanding Log sheets",
                "Taxis/No_log.aspx",
                "Taxi_Fin_reports.aspx item 3 is the outstanding logsheets screen. It is not the generic taxi-financial projection."
            ),
            ["taxis-fin-log-odometer-gg"] = DistinctArchivedScreen(
                "taxis-fin-log-odometer-gg",
                "Log Odometer (GG Vehicles)",
                "Private_Hire/RPT_taxi_log_odo.aspx",
                "Taxi_Fin_reports.aspx item 4 is the GG odometer screen. It is not the generic taxi-financial projection."
            ),
            ["taxis-fin-cancellations"] = DistinctArchivedScreen(
                "taxis-fin-cancellations",
                "Cancellations",
                "Private_Hire/RPT_cancel.aspx",
                "Taxi_Fin_reports.aspx item 5 is the cancellations screen. It is not the generic taxi-financial projection."
            ),
            ["taxis-fin-no-objective-or-responsibility"] = DistinctArchivedScreen(
                "taxis-fin-no-objective-or-responsibility",
                "Taxi log sheets with no Objective or Responsibility codes",
                "Taxis/RPTnoObjOrResp.aspx",
                "Taxi_Fin_reports.aspx item 6 is the missing-BAS-code screen. It is not the generic taxi-financial projection."
            ),

            ["trip-authority"] = new(
                "trip-authority",
                "Trip Authority Report",
                "TripReports.aspx / ShowReport multiple items",
                null,
                BuildTripAuthorityAsync,
                ""
            ),
            ["available-vehicles-site"] = DistinctShowReportItem(
                "available-vehicles-site",
                "Available vehicles at the user's site",
                "VehiclesPerSite",
                "TripReports.aspx executes DEV_REP_VehiclesPerSite. It is not the generic trip-authority projection."
            ),
            ["unavailable-vehicles-site"] = DistinctShowReportItem(
                "unavailable-vehicles-site",
                "Unavailable vehicles at the user's site",
                "AuthVehiclesPerSite",
                "TripReports.aspx executes DEV_REP_AuthVehiclesPerSite. It is not the generic trip-authority projection."
            ),
            ["users-per-site"] = DistinctShowReportItem(
                "users-per-site",
                "Registered Users at the user's site",
                "UserDetailsPerSite",
                "TripReports.aspx executes DEV_REP_UserDetailsPerSite. It is not the generic trip-authority projection."
            ),
            ["users-per-department"] = DistinctShowReportItem(
                "users-per-department",
                "Registered Users in the user's department",
                "UserDetailsPerDepartment",
                "TripReports.aspx executes DEV_REP_UserDetailsPerDepartment. It is not the generic trip-authority projection."
            ),
            ["users-all-departments"] = DistinctShowReportItem(
                "users-all-departments",
                "All Registered Users on the FIS",
                "UserDetails",
                "TripReports.aspx executes DEV_REP_UserDetails. It is not the generic trip-authority projection."
            ),
            ["trips-per-user-all"] = DistinctShowReportItem(
                "trips-per-user-all",
                "All trips issued per user",
                "TripCountPerUser",
                "TripReports.aspx executes DEV_REP_TripCountPerUser with no department filter. It is not the generic trip-authority projection."
            ),
            ["trips-per-user-department"] = DistinctShowReportItem(
                "trips-per-user-department",
                "All trips issued per user in the user's department",
                "TripCountPerUser",
                "TripReports.aspx executes DEV_REP_TripCountPerUser with FilterID. It is not the generic trip-authority projection."
            ),
            ["trips-for-vehicle"] = DistinctShowReportItem(
                "trips-for-vehicle",
                "All trips for a vehicle",
                "VehicleTrips",
                "TripReports.aspx SelectVehicleTrip Item=VehicleTrips. It is not the generic trip-authority projection."
            ),
            ["drivers-for-vehicle"] = DistinctShowReportItem(
                "drivers-for-vehicle",
                "All drivers for a vehicle",
                "VehicleDrivers",
                "TripReports.aspx SelectVehicleTrip Item=VehicleDrivers. It is not the generic trip-authority projection."
            ),
            ["vehicle-utilisation-driver"] = DistinctShowReportItem(
                "vehicle-utilisation-driver",
                "All vehicles for a driver",
                "VehicleUtilisationByDriverID",
                "TripReports.aspx SelectDriver Item=VehicleUtilisationByDriverID. It is not the generic trip-authority projection."
            ),
            ["vehicle-utilisation"] = DistinctShowReportItem(
                "vehicle-utilisation",
                "Vehicle utilisation for a vehicle",
                "VehicleUtilisation",
                "TripReports.aspx SelectVehicleTrip Item=VehicleUtilisation. It is not the generic trip-authority projection."
            ),
            ["trip-count-last-3-months"] = DistinctArchivedScreen(
                "trip-count-last-3-months",
                "Trip count last 3 months",
                "FISReports/TripReports.aspx",
                "This trip-authority key has no matching archived ShowReport Item in TripReports.aspx. No generic trip-authority projection was substituted."
            ),
            ["trip-authorities-over-25000"] = DistinctArchivedScreen(
                "trip-authorities-over-25000",
                "Max Kilos exceeding 25000",
                "Finance/KiloMaxPerDep_Site_DateRange.aspx?Mode=DSR&Report=AllRoutesOver25000KM",
                "TripReports.aspx opens the 25000km kilo-max screen. It is not the generic trip-authority projection."
            ),
            ["trip-authorities-over-3500-per-day"] = DistinctArchivedScreen(
                "trip-authorities-over-3500-per-day",
                "Max Kilos per Day exceeding 3500 per trip",
                "Finance/KiloMaxPerDep_Site_DateRange.aspx?Mode=DSR&Report=AllDayTripsOver3500KM",
                "TripReports.aspx opens the 3500km/day kilo-max screen. It is not the generic trip-authority projection."
            ),
            ["els-manual-kilo"] = DistinctArchivedScreen(
                "els-manual-kilo",
                "Electronic and Manual Logsheet Report",
                "FISReports/GetReportBetweenStartAndEndDate.aspx?Report=ELSLogReport",
                "TripReports.aspx opens ELSLogReport. It is not the generic trip-authority projection."
            ),

            ["driver-information-finyear"] = new(
                "driver-information-finyear",
                "Driver Information over a Financial Year",
                "ShowReport.aspx?Item=gFleetVehicleUsers",
                "gFleetVehicleUsers",
                BuildDriverInformationAsync,
                "The legacy report is executed when available. If the stored procedure is missing, this fallback reads the compatible trip, contract, vehicle, and trip-driver tables.",
                BuildDriverInformationStoredProcedureParameters
            ),

            ["trips-open-31"] = new(
                "trips-open-31",
                "Trips Open for Over 31 Days",
                "ShowReport.aspx?Item=TripsOpenForOver31Days",
                "TripsOpenForOver31Days",
                BuildTripsOpen31Async,
                BuildStoredProcedureParameters: filters =>
                    BuildOptionalParameterList(
                        ("@Days", (object?)GetInt(filters, "days"), DbType.Int32)
                    )
            ),

            ["unallocated-vehicles"] = new(
                "unallocated-vehicles",
                "Unallocated Vehicles",
                "Finance/OpenReport.aspx?Report=VehiclesNoCurrentContractAndFuelTransactions",
                null,
                BuildUnallocatedVehiclesAsync,
                "Approximated from vehicles with no active contract; fuel-transaction parity requires the original finance report pipeline."
            ),

            ["vehicle-additions"] = new(
                "vehicle-additions",
                "Vehicle Additions",
                "SelectStartAndEndDate.aspx?Item=AllVehiclesPurchasedInADateRange",
                "AllVehiclesPurchasedInADateRange",
                BuildVehicleAdditionsAsync,
                BuildStoredProcedureParameters: filters => BuildDateRangeParameters(filters)
            ),

            ["vehicle-disposals"] = new(
                "vehicle-disposals",
                "Vehicle Disposals",
                "SelectStartAndEndDate.aspx?Item=AllVehiclesDisposedInADateRange",
                "AllVehiclesDisposedInADateRange",
                BuildVehicleDisposalsAsync,
                BuildStoredProcedureParameters: filters => BuildDateRangeParameters(filters)
            ),

            ["vehicle-info"] = new(
                "vehicle-info",
                "General Vehicle Information Report",
                "Vehicles/RPT_Vehicle_details.aspx",
                null,
                BuildVehicleInfoAsync,
                ""
            ),

            ["vehicle-list-date-range"] = new(
                "vehicle-list-date-range",
                "Vehicle List in Date Range",
                "SelectStartAndEndDate.aspx?Item=FilterAllVehiclesInADateRange",
                "FilterAllVehiclesInADateRange",
                BuildVehicleListDateRangeAsync,
                BuildStoredProcedureParameters: filters => BuildDateRangeParameters(filters)
            ),

            ["vehicle-logs-report"] = new(
                "vehicle-logs-report",
                "Vehicle Logs Report",
                "Logs/RPT_logsheet_per_vehicle.aspx",
                null,
                BuildVehicleLogsReportAsync,
                ""
            ),

            ["vehicle-status-range"] = new(
                "vehicle-status-range",
                "Vehicle Status Range Report",
                "Vehicles/VehicleStatus.aspx",
                null,
                BuildVehicleStatusRangeAsync,
                "Legacy vehicle status report is a custom page. This approximation uses vehicle_status_history rows."
            ),

            ["vehicle-status-all"] = new(
                "vehicle-status-all",
                "All Vehicle Status",
                "Finance/GeneratedReports.aspx?key=9.2%20All%20Vehicle%20Statuses",
                null,
                BuildVehicleStatusAllAsync,
                "Legacy generated report output is approximated from vehicle master + status + site dimensions."
            ),

            ["vehicles"] = new(
                "vehicles",
                "Vehicle Master List",
                "Vehicles/Vehicles.aspx",
                null,
                BuildVehiclesAsync,
                "Legacy vehicle master screen is custom. This approximation projects vehicle_master columns into a dynamic result grid."
            ),

            ["vehicles-no-tariff"] = new(
                "vehicles-no-tariff",
                "Vehicles with Expired or No Tariffs",
                "ShowReport.aspx?Item=GetAllVehicleWithNoTariffs",
                "GetAllVehicleWithNoTariffs",
                BuildVehiclesNoTariffAsync,
                BuildStoredProcedureParameters: _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-no-trips"] = new(
                "vehicles-no-trips",
                "Vehicles with no trips (Any Department)",
                "ShowReport.aspx?Item=AllVehiclesNoTripsPerDepartment",
                "AllVehiclesNoTripsPerDepartment",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx item 3.5 executes DEV_REP_AllVehiclesNoTripsPerDepartment. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-no-trips-daterange"] = new(
                "vehicles-no-trips-daterange",
                "Vehicles with no trips between a start and end date",
                "ShowReport.aspx?Item=AllVehiclesNoTripsPerDepartmentBetweenStartAndEndDate",
                "AllVehiclesNoTripsPerDepartmentBetweenStartAndEndDate",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx item 3.6 executes DEV_REP_AllVehiclesNoTripsPerDepartmentBetweenStartAndEndDate. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-per-site"] = new(
                "vehicles-per-site",
                "Registered Vehicles in site",
                "ShowReport.aspx?Item=AllVehiclesPerSite",
                "AllVehiclesPerSite",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx executes DEV_REP_AllVehiclesPerSite. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-per-department"] = new(
                "vehicles-per-department",
                "Registered Vehicles in department",
                "ShowReport.aspx?Item=AllVehiclesPerDepartment",
                "AllVehiclesPerDepartment",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx executes DEV_REP_AllVehiclesPerDepartment. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-contract-type-site"] = new(
                "vehicles-contract-type-site",
                "Vehicles in site by Contract Type",
                "ShowReport.aspx?Item=CountVehiclesPerTypePerSite",
                "CountVehiclesPerTypePerSite",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx executes DEV_REP_CountVehiclesPerTypePerSite. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-contract-type-department"] = new(
                "vehicles-contract-type-department",
                "Vehicles in department by Contract Type",
                "ShowReport.aspx?Item=CountVehiclesPerTypePerDepartment",
                "CountVehiclesPerTypePerDepartment",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx executes DEV_REP_CountVehiclesPerTypePerDepartment. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-contract-type-department-site"] = new(
                "vehicles-contract-type-department-site",
                "Vehicles in department by Site and by Contract Type",
                "ShowReport.aspx?Item=CountVehiclesPerTypePerDepartmentPerSite",
                "CountVehiclesPerTypePerDepartmentPerSite",
                BuildUnmappedUniversalReportAsync,
                "TripReports.aspx executes DEV_REP_CountVehiclesPerTypePerDepartmentPerSite. It is not the generic vehicle list.",
                _ => Array.Empty<LegacyStoredProcedureParameter>()
            ),

            ["vehicles-inservice-per-gg"] = DistinctVehicleScreen(
                "vehicles-inservice-per-gg",
                "In-service vehicles per GG number"
            ),
            ["vehicles-inservice-per-dept"] = DistinctVehicleScreen(
                "vehicles-inservice-per-dept",
                "In-service vehicles per department"
            ),
            ["vehicles-inservice-wesbank"] = DistinctVehicleScreen(
                "vehicles-inservice-wesbank",
                "In-service Wesbank vehicles"
            ),
            ["vehicles-provincial-numbers"] = DistinctVehicleScreen(
                "vehicles-provincial-numbers",
                "Vehicles with provincial numbers"
            ),
            ["vehicles-with-barcodes"] = DistinctVehicleScreen(
                "vehicles-with-barcodes",
                "Vehicles with barcodes"
            ),
            ["vehicles-lpg-converted"] = DistinctVehicleScreen(
                "vehicles-lpg-converted",
                "LPG converted vehicles"
            ),
            ["vehicles-replaced-per-dept"] = DistinctVehicleScreen(
                "vehicles-replaced-per-dept",
                "Replaced vehicles per department"
            ),
            ["vehicles-older-than-5y-over-120k"] = DistinctVehicleScreen(
                "vehicles-older-than-5y-over-120k",
                "Vehicles older than 5 years over 120k km"
            ),
            ["vehicles-older-than-5y-over-120k-period"] = DistinctVehicleScreen(
                "vehicles-older-than-5y-over-120k-period",
                "Vehicles older than 5 years over 120k km for a period"
            ),
            ["vehicles-extended-service"] = DistinctVehicleScreen(
                "vehicles-extended-service",
                "Vehicles on extended service"
            ),
            ["vehicles-value-inservice"] = DistinctVehicleScreen(
                "vehicles-value-inservice",
                "Value of in-service vehicles"
            ),
            ["vehicles-extras"] = DistinctVehicleScreen(
                "vehicles-extras",
                "Vehicle extras"
            ),
            ["vehicles-selected"] = DistinctVehicleScreen(
                "vehicles-selected",
                "Selected vehicles"
            ),
            ["vehicles-els-manual"] = DistinctVehicleScreen(
                "vehicles-els-manual",
                "Vehicle report - Contracts, ELS and Manual Logs"
            ),
            ["vehicle-contract-single"] = DistinctVehicleScreen(
                "vehicle-contract-single",
                "Single vehicle contract report"
            ),
            ["vehicle-contract-multiple"] = DistinctVehicleScreen(
                "vehicle-contract-multiple",
                "Multiple vehicle contract report"
            ),
            ["vehicle-contract-els-manual"] = DistinctVehicleScreen(
                "vehicle-contract-els-manual",
                "Vehicle contract ELS and manual logs"
            ),
            ["vehicle-by-barcode"] = DistinctArchivedScreen(
                "vehicle-by-barcode",
                "Information On A Vehicle BY barcode",
                "Vehicles/RPT_vehicle_barcode.aspx",
                "Vehicles.aspx item 2 is the barcode lookup screen. It is not the generic vehicle_master list."
            ),
            ["vehicles-with-history"] = DistinctArchivedScreen(
                "vehicles-with-history",
                "Information On Vehicles With A History",
                "Vehicles/RPT_History_Of_Vehicles.aspx",
                "Vehicles.aspx item 4 is the vehicle-history screen. It is not the generic vehicle_master list."
            ),
            ["all-vehicles"] = DistinctArchivedScreen(
                "all-vehicles",
                "Information On All Vehicles",
                "Vehicles/RPT_List_Of_Vehicles.aspx",
                "Vehicles.aspx item 3 is the all-vehicles Crystal screen. It is not the generic vehicle_master list."
            ),
            ["vehicle-by-number"] = DistinctArchivedScreen(
                "vehicle-by-number",
                "Information On A Vehicle BY GG, Registration, Engine or Chassis Number",
                "Vehicles/Vehicle_Report.aspx",
                "Vehicles.aspx item 1 is the one-vehicle lookup screen. It is not the generic vehicle_master list."
            ),

            ["licences-gg-number"] = DistinctLicenceScreen(
                "licences-gg-number",
                "Licence Report for a GG Number",
                "License/RPT_1gg_main_lic.htm"
            ),
            ["licences-prov-reg-number"] = DistinctLicenceScreen(
                "licences-prov-reg-number",
                "Licence Report for a Prov Reg Number",
                "License/RPT_1gp_main_lic.htm"
            ),
            ["licences-register-number"] = DistinctLicenceScreen(
                "licences-register-number",
                "Licence Report for a Register Number",
                "License/RPT_1rg_main_lic.htm"
            ),
            ["licences-engine-number"] = DistinctLicenceScreen(
                "licences-engine-number",
                "Licence Report for an Engine Number",
                "License/RPT_1eng_main_lic.htm"
            ),
            ["licences-chassis-number"] = DistinctLicenceScreen(
                "licences-chassis-number",
                "Licence Report for a Chassis Number",
                "License/RPT_1cha_main_lic.htm"
            ),
            ["licences-site"] = DistinctLicenceScreen(
                "licences-site",
                "Licence Report for a Site",
                "License/RPT_1dept_main_lic.htm"
            ),
            ["licences-all-with-model-tare-fee"] = DistinctLicenceScreen(
                "licences-all-with-model-tare-fee",
                "Licence Report on ALL Vehicles",
                "License/RPT_all_main_lic.htm"
            ),
            ["licences-dept-sites-period"] = DistinctLicenceScreen(
                "licences-dept-sites-period",
                "Licence report for department/site and period",
                "License/RPT_dept_period_main_lic.aspx"
            ),
            ["licences-expire-date"] = DistinctLicenceScreen(
                "licences-expire-date",
                "Report on Licence EXPIRE DATE",
                "License/RPT_dept_period_main_lic.aspx"
            ),
            ["licences-month-fees"] = DistinctLicenceScreen(
                "licences-month-fees",
                "Licence month fees",
                "License/RPT_monthfees_main_lic.htm"
            ),
            ["licences-old-expire-dates"] = DistinctLicenceScreen(
                "licences-old-expire-dates",
                "Old licence expire dates",
                "License/RPT_olddat_main_lic.aspx"
            ),
            ["licences-sap-info"] = DistinctLicenceScreen(
                "licences-sap-info",
                "Licence SAP info",
                "License/RPT_sap_main_lic.htm"
            ),
            ["licences-cof-info"] = DistinctLicenceScreen(
                "licences-cof-info",
                "Licence COF info",
                "License/RPT_cof_main_lic.htm"
            ),
            ["licences-make-model-fee"] = DistinctLicenceScreen(
                "licences-make-model-fee",
                "Licence make/model fee",
                "License/RPT_model_licfees.aspx"
            ),
            ["licences-data-workgroup"] = DistinctLicenceScreen(
                "licences-data-workgroup",
                "Data Workgroup - Report for Each Or Group of Vehicle",
                "License/RPT_wgone_main.htm"
            ),
            ["licences-data-workgroup-latest"] = DistinctLicenceScreen(
                "licences-data-workgroup-latest",
                "Data Workgroup - Only Latest Report for Each Or Group of Vehicle",
                "License/RPT_wgonelst_main.htm"
            ),
            ["licences-received-by-ggmt"] = DistinctLicenceScreen(
                "licences-received-by-ggmt",
                "Licences received by GGMT",
                "License/RPT_1ggrec_main_lic.htm"
            ),

            ["permanent-contracts-without-tariff"] = new(
                "permanent-contracts-without-tariff",
                "Permanent contracts without assigned tariffs",
                "GFleetReports/VehiclesonContractWithoutTariffs.aspx",
                null,
                BuildPermanentContractsWithoutTariffAsync,
                "The GFleet report VehiclesonContractWithoutTariffs executes dbo.Dev_Rep_Permanentcontractswithouttariffs. It is not the Reports.aspx GetAllVehicleWithNoTariffs item.",
                _ => Array.Empty<LegacyStoredProcedureParameter>(),
                "dbo.Dev_Rep_Permanentcontractswithouttariffs"
            ),

            ["vehicles-universal-selected"] = new(
                "vehicles-universal-selected",
                "Universal report for selected vehicles",
                "Vehicles/RPT_Univ_Selected_Veh_GetReg.aspx",
                null,
                BuildUnmappedUniversalReportAsync,
                "The archived universal selected-vehicle report builds a session collection and a custom field selection. It is not the generic vehicle list."
            ),

            ["vehicle-contract-universal"] = new(
                "vehicle-contract-universal",
                "Information On All Vehicles Contracts (Universal Report)",
                "FISReports/Contracts/RPT_Contracts_Report_1.aspx",
                null,
                BuildUnmappedUniversalReportAsync,
                "The archived all-contracts universal report is a custom filter screen, not the generic vehicle list."
            ),

            ["clearance-universal"] = new(
                "clearance-universal",
                "Clearance Universal Report",
                "Clearance/RPT_Clear_Univ.aspx",
                null,
                BuildUnmappedUniversalReportAsync,
                "The archived clearance universal report is a merchant/date filter screen, not the generic clearance list."
            ),

            ["nom-vehicles-without-tariff"] = new(
                "nom-vehicles-without-tariff",
                "NOM Vehicles Without Tariffs",
                "ShowReportWithLineNumbers.aspx?Item=NOMVehiclesWithoutTariffs",
                "NOMVehiclesWithoutTariffs",
                BuildNomVehiclesWithoutTariffAsync,
                "The legacy NOM report procedure is authoritative. If it is absent, no different table projection is substituted because its lease, contract, contact, and province joins are part of the report contract."
            ),

            ["wesbank"] = new(
                "wesbank",
                "Wesbank First Auto Report",
                "Transaction/RPTTransaction.aspx",
                "WesbankExpensesOneProvinceAndAllMonths",
                BuildWesbankAsync,
                BuildStoredProcedureParameters: filters =>
                    BuildOptionalParameterList(
                        ("@ProvinceCode", (object?)GetString(filters, "province"), DbType.String),
                        ("@StartDate", (object?)GetDate(filters, "from"), DbType.DateTime),
                        ("@EndDate", (object?)GetDate(filters, "to"), DbType.DateTime)
                    )
            ),
            ["wesbank-one-vehicle"] = DistinctArchivedScreen(
                "wesbank-one-vehicle",
                "Summary Wesbank Expenses Reports – All Inclusive Summary Expenses Reports",
                "Finance/GetWesbankReportsDates.aspx",
                "FinanceRegionWesbankMenu.aspx item a is a date-range summary screen. It is not WesbankExpensesOneProvinceAndAllMonths."
            ),
            ["wesbank-one-vehicle-period"] = DistinctArchivedScreen(
                "wesbank-one-vehicle-period",
                "Summary Wesbank Expenses Reports – Selection on Summary Expenses Reports",
                "Finance/GetWesbankReportsDatesAndProvince.aspx",
                "FinanceRegionWesbankMenu.aspx item b is a province/date summary screen. It is not WesbankExpensesOneProvinceAndAllMonths."
            ),
            ["wesbank-one-dept-site"] = DistinctArchivedScreen(
                "wesbank-one-dept-site",
                "Detailed Wesbank Expenses Reports – All Inclusive Detailed Expenses Reports",
                "Finance/GetWesbankReportsDatesProvince.aspx",
                "FinanceRegionWesbankMenu.aspx item c is a detailed expenses screen. It is not WesbankExpensesOneProvinceAndAllMonths."
            ),
            ["wesbank-overfills"] = DistinctArchivedScreen(
                "wesbank-overfills",
                "Detailed Wesbank Expenses Reports – Selection on Detailed Expenses Reports",
                "Finance/GetWesbankReportsDatesAndProvince2.aspx",
                "FinanceRegionWesbankMenu.aspx item d is a detailed selection screen. It is not WesbankExpensesOneProvinceAndAllMonths."
            ),
            ["wesbank-multiple-daily-fuels"] = DistinctArchivedScreen(
                "wesbank-multiple-daily-fuels",
                "Wesbank multiple daily fuels",
                "Finance/FinanceRegionWesbankMenu.aspx",
                "This Wesbank key is a distinct archived expenses screen. It is not WesbankExpensesOneProvinceAndAllMonths."
            ),
            ["auction"] = DistinctArchivedScreen(
                "auction",
                "Auction Reports Menu",
                "Auction/RPTAuction.aspx",
                "RPTAuction.aspx is the auction reports menu. It is not the vehicle-disposals list."
            ),
            ["auction-one-vehicle"] = DistinctArchivedScreen(
                "auction-one-vehicle",
                "Auction Report on ONE Vehicle",
                "auction/RPT_one_num_main_auction.htm",
                "RPTAuction.aspx item 1 is a one-vehicle Crystal/HTML screen. It is not the vehicle-disposals list."
            ),
            ["auction-sale-to-name"] = DistinctArchivedScreen(
                "auction-sale-to-name",
                "Auction Report on Sale to Name",
                "auction/RPT_one_name_main_auction.aspx",
                "RPTAuction.aspx item 3 is the sale-to-name screen. It is not the vehicle-disposals list."
            ),
            ["auction-one-sort-gg"] = DistinctArchivedScreen(
                "auction-one-sort-gg",
                "Auction Report of ONE Auction - Sort by GG Number",
                "auction/RPT_one_auct1_main_auction.aspx",
                "RPTAuction.aspx item 4 is the GG-number sort screen. It is not the vehicle-disposals list."
            ),
            ["auction-one-sort-lot"] = DistinctArchivedScreen(
                "auction-one-sort-lot",
                "Auction Report of ONE Auction - Sort by LOT Number",
                "auction/RPT_one_auct2_main_auction.aspx",
                "RPTAuction.aspx item 5 is the lot-number sort screen. It is not the vehicle-disposals list."
            ),

            ["workshop"] = new(
                "workshop",
                "Workshop Report",
                "Workshop/RPTWorkshop.aspx",
                null,
                BuildWorkshopPeriodAsync,
                "Legacy workshop reporting is menu-driven. This compatibility result preserves the period report filters and legacy Workshop fields."
            ),

            ["workshop-one-vehicle"] = new(
                "workshop-one-vehicle",
                "Workshop Report on One Vehicle",
                "WorkShop/RPT_ww_one_num_report.aspx",
                null,
                BuildWorkshopOneVehicleAsync,
                "The legacy report uses a wildcard vehicle-number search. The modern result keeps that behavior against the compatible vehicle and Workshop repositories."
            ),

            ["workshop-print-job-card"] = new(
                "workshop-print-job-card",
                "Print a Workshop Job Card",
                "WorkShop/RPT_ww_printjob_report.aspx",
                null,
                BuildWorkshopPrintJobCardAsync,
                "The legacy job-card report joins optional vehicle and Workshop fields. The modern result preserves the available compatible fields without requiring expanded columns."
            ),

            ["workshop-in-shop"] = new(
                "workshop-in-shop",
                "List of Vehicles Still in Workshop",
                "WorkShop/RPT_ww_inshop_report.aspx",
                null,
                BuildWorkshopInShopAsync,
                "The legacy report identifies open job cards with job_close = N. When that legacy flag is absent, the compatibility path uses the incomplete date state."
            ),

            ["workshop-merchants"] = new(
                "workshop-merchants",
                "List of All Merchants",
                "WorkShop/RPT_merch_report.aspx",
                null,
                BuildWorkshopMerchantsAsync,
                "The result reads the Workshop-specific wwmerchant table through its guarded legacy-schema repository."
            ),
        };
    }

    private enum AssetListSearchType
    {
        Province = 1,
        Department = 2,
        Site = 3,
        All = 4,
    }

    private Task<LegacyReportResultDto> BuildAssetListByProvinceAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) => BuildAssetListAsync(filters, cancellationToken, AssetListSearchType.Province);

    private Task<LegacyReportResultDto> BuildAssetListByDepartmentAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) => BuildAssetListAsync(filters, cancellationToken, AssetListSearchType.Department);

    private Task<LegacyReportResultDto> BuildAssetListBySiteAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) => BuildAssetListAsync(filters, cancellationToken, AssetListSearchType.Site);

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildAssetListProvinceParameters(
        IDictionary<string, string?> filters
    ) => BuildAssetListFilterParameters(filters, AssetListSearchType.Province);

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildAssetListDepartmentParameters(
        IDictionary<string, string?> filters
    ) => BuildAssetListFilterParameters(filters, AssetListSearchType.Department);

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildAssetListSiteParameters(
        IDictionary<string, string?> filters
    ) => BuildAssetListFilterParameters(filters, AssetListSearchType.Site);

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildAssetListFilterParameters(
        IDictionary<string, string?> filters,
        AssetListSearchType searchType
    )
    {
        var id = GetAssetListFilterId(filters, searchType);
        return new[]
        {
            new LegacyStoredProcedureParameter("@SearchType", (int)searchType, DbType.Int32),
            // Passing DBNull is intentional.  The legacy procedure returns no
            // rows when a filtered route is called without its required id.
            new LegacyStoredProcedureParameter(
                "@id",
                id.HasValue ? id.Value : DBNull.Value,
                DbType.Int32
            ),
        };
    }

    private async Task<LegacyReportResultDto> BuildAssetListAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) => await BuildAssetListAsync(filters, cancellationToken, null);

    private async Task<LegacyReportResultDto> BuildAssetListAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken,
        AssetListSearchType? forcedSearchType
    )
    {
        if (GetFallbackPagination(filters) is { IncludeAll: false })
        {
            return await BuildPagedAssetListAsync(filters, forcedSearchType, cancellationToken);
        }

        var search = GetString(filters, "search") ?? GetString(filters, "txtNum");
        var mode = GetString(filters, "mode") ?? "GG";
        var searchType = forcedSearchType ?? ResolveAssetListSearchType(filters);
        var filterId = GetAssetListFilterId(filters, searchType);

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join model in _context.Models.AsNoTracking()
                on vehicle.model_code equals model.model_code
                into vehicleModels
            from model in vehicleModels.DefaultIfEmpty()
            join vehicleClass in _context.Classes.AsNoTracking()
                on model.class_code equals vehicleClass.class_code
                into vehicleClasses
            from vehicleClass in vehicleClasses.DefaultIfEmpty()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into vehicleStatuses
            from status in vehicleStatuses.DefaultIfEmpty()
            join source in _context.VehicleSources.AsNoTracking()
                on vehicle.vs_code equals (byte?)source.vs_code
                into vehicleSources
            from source in vehicleSources.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking()
                on vehicle.type_code equals type.type_code
                into vehicleTypes
            from type in vehicleTypes.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on vehicle.location_code equals site.Site_code
                into vehicleSites
            from site in vehicleSites.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking()
                on site.Depatrment_code equals department.department_code
                into vehicleDepartments
            from department in vehicleDepartments.DefaultIfEmpty()
            join province in _context.Provinces.AsNoTracking()
                on site.province_code equals (byte?)province.province_code
                into vehicleProvinces
            from province in vehicleProvinces.DefaultIfEmpty()
            where
                // Legacy report status contract: 0 = New, 1 = In-Service.
                vehicle.vehicle_status_code >= 0
                && vehicle.vehicle_status_code <= 1
            select new
            {
                vehicle.vmf_code,
                vehicle.registration_number,
                vehicle.fleet_number,
                vehicle.colour,
                vehicle.engine_number_1,
                vehicle.chassis_number,
                vehicle.barcode,
                vehicle.year_manufactured,
                vehicle.purchase_date,
                vehicle.purchase_amount,
                Status = status != null ? status.status_description : null,
                ModelDescription = model != null ? model.model_description : null,
                // class_number was introduced after the client-era schema.
                // class_code is the required legacy key and gives the same
                // identifying prefix when a newer class_number is absent.
                ClassCode = vehicleClass != null ? (short?)vehicleClass.class_code : null,
                ClassDescription = vehicleClass != null ? vehicleClass.description : null,
                SourceName = source != null ? source.name : null,
                HireType = type != null ? type.type_description : null,
                SiteCode = site != null ? (short?)site.Site_code : null,
                SiteName = site != null ? site.description : null,
                SiteDepartmentNumber = site != null ? site.Department_number : null,
                SiteResponsiblePerson = site != null ? site.res_person : null,
                SiteTelephone = site != null ? site.telephone : null,
                SiteNetAddress = site != null ? site.net_address : null,
                DepartmentCode = site != null ? site.Depatrment_code : null,
                DepartmentNumber = department != null ? department.Department_number : null,
                DepartmentDescription = department != null ? department.description : null,
                ProvinceCode = site != null ? (byte?)site.province_code : null,
                ProvinceName = province != null ? province.province_name : null,
            };

        List<AssetListFallbackVehicle> vehicles;
        if (searchType != AssetListSearchType.All && !filterId.HasValue)
        {
            vehicles = new List<AssetListFallbackVehicle>();
        }
        else
        {
            if (searchType == AssetListSearchType.Province)
            {
                query = query.Where(row => row.ProvinceCode == (byte?)filterId!.Value);
            }
            else if (searchType == AssetListSearchType.Department)
            {
                query = query.Where(row => row.DepartmentCode == (short?)filterId!.Value);
            }
            else if (searchType == AssetListSearchType.Site)
            {
                query = query.Where(row => row.SiteCode == (short?)filterId!.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(
                    search,
                    mode,
                    cancellationToken
                );
                query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
            }

            var projectedVehicles = await query
                .OrderBy(row => row.ProvinceName)
                .ThenBy(row => row.SiteName)
                .ThenBy(row => row.registration_number)
                .ThenBy(row => row.vmf_code)
                .ToListAsync(cancellationToken);

            vehicles = projectedVehicles
                .Select(row => new AssetListFallbackVehicle(
                    row.registration_number,
                    row.fleet_number,
                    row.Status ?? (row.vmf_code > 0 ? "Unknown" : null),
                    row.ModelDescription,
                    row.colour,
                    JoinLegacyClassName(
                        row.ClassCode?.ToString(CultureInfo.InvariantCulture),
                        row.ClassDescription
                    ),
                    row.engine_number_1,
                    row.chassis_number,
                    row.barcode,
                    row.year_manufactured,
                    row.purchase_date,
                    row.purchase_amount,
                    row.SourceName,
                    string.IsNullOrWhiteSpace(row.HireType) ? "Empty" : row.HireType,
                    row.vmf_code,
                    row.SiteCode,
                    row.SiteName,
                    row.SiteDepartmentNumber,
                    row.SiteResponsiblePerson,
                    row.SiteTelephone,
                    row.SiteNetAddress,
                    row.DepartmentNumber,
                    row.DepartmentDescription,
                    row.ProvinceName
                ))
                .ToList();
        }

        var contracts = new List<AssetListFallbackContract>();
        if (vehicles.Count > 0)
        {
            // Keep the compatibility query bounded to the assets in this
            // report.  Chunking avoids SQL Server's 2,100-parameter limit
            // while ensuring a print/export never scans unrelated contracts.
            foreach (
                var vmfCodeChunk in vehicles
                    .Select(vehicle => vehicle.VmfCode)
                    .Distinct()
                    .Chunk(1000)
            )
            {
                var chunkContracts = await _context
                    .Contracts.AsNoTracking()
                    .Where(contract =>
                        (contract.still_current == "y" || contract.still_current == "n")
                        && vmfCodeChunk.Contains(contract.vmf_code)
                    )
                    .Select(contract => new AssetListFallbackContract(
                        contract.vmf_code,
                        contract.still_current,
                        contract.contract_code,
                        contract.contract_type,
                        contract.start_date,
                        contract.end_date,
                        contract.target_return_date,
                        contract.Charged_Until,
                        contract.site_code,
                        contract.user_code
                    ))
                    .ToListAsync(cancellationToken);

                contracts.AddRange(chunkContracts);
            }
        }

        var contractTypeDescriptions =
            contracts.Count == 0
                ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                : await _context
                    .ContractTypes.AsNoTracking()
                    .ToDictionaryAsync(
                        contractType => contractType.type_name,
                        contractType => contractType.CT_description,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken
                    );

        var rows = new List<AssetListFallbackRow>();
        foreach (var vehicle in vehicles)
        {
            var vehicleContracts = contracts
                .Where(contract => contract.VmfCode == vehicle.VmfCode)
                .ToList();
            var openContracts = vehicleContracts
                .Where(contract => IsCurrentContract(contract.StillCurrent))
                .ToList();
            var selectedContracts =
                openContracts.Count > 0
                    ? openContracts
                    : vehicleContracts
                        .Where(contract => !IsCurrentContract(contract.StillCurrent))
                        .OrderByDescending(contract => contract.ContractCode)
                        .Take(1)
                        .ToList();

            if (selectedContracts.Count == 0)
            {
                rows.Add(CreateAssetListFallbackRow(vehicle, null, contractTypeDescriptions));
            }
            else
            {
                rows.AddRange(
                    selectedContracts.Select(contract =>
                        CreateAssetListFallbackRow(vehicle, contract, contractTypeDescriptions)
                    )
                );
            }
        }

        return CreateDynamicResult(
            GetAssetListFallbackTitle(searchType),
            GetAssetListFallbackTarget(searchType),
            true,
            "The legacy asset-list stored procedure was unavailable; the compatibility query preserves its status, source, location, contract selection, columns, and order.",
            rows.OrderBy(row => row.Province)
                .ThenBy(row => row.SiteName)
                .ThenBy(row => row.Registration)
                .ThenBy(row => row.VmfCode),
            Column("registration", row => row.Registration),
            Column("ggnumber", row => row.GgNumber),
            Column("vehicle status", row => row.VehicleStatus),
            Column("model description", row => row.ModelDescription),
            Column("colour", row => row.Colour),
            Column("class description", row => row.ClassDescription),
            Column("engine number", row => row.EngineNumber),
            Column("chassis number", row => row.ChassisNumber),
            Column("bar code", row => row.BarCode),
            Column("year manufactured", row => row.YearManufactured),
            Column("datepurchased", row => row.DatePurchased),
            Column("purchaseamount", row => row.PurchaseAmount),
            Column("Sourced Via", row => row.SourcedVia),
            Column("Hire Type", row => row.HireType),
            Column("vmf_code", row => row.VmfCode),
            Column("contract stillcurrent", row => row.ContractStillCurrent),
            Column("contract_code", row => row.ContractCode),
            Column("contract_type", row => row.ContractType),
            Column("contract startdate", row => row.ContractStartDate),
            Column("contract enddate", row => row.ContractEndDate),
            Column("expected return date", row => row.ExpectedReturnDate),
            Column("contract chargeduntil", row => row.ContractChargedUntil),
            Column("contract period/months", row => row.ContractPeriodMonths),
            Column("months used", row => row.MonthsUsed),
            Column("months -remaining / +Exceeded", row => row.MonthsRemainingOrExceeded),
            Column("contract lastmodifiedby", row => row.ContractLastModifiedBy),
            Column("departmentname", row => row.DepartmentName),
            Column("sitename", row => row.SiteName),
            Column("site responsible person", row => row.SiteResponsiblePerson),
            Column("province", row => row.Province),
            Column("Tariff:(dailypool / permanent / monthlyLease)", row => row.FixedTariff),
            Column("kilo tariff", row => row.KiloTariff)
        );
    }

    private async Task<LegacyReportResultDto> BuildPagedAssetListAsync(
        IDictionary<string, string?> filters,
        AssetListSearchType? forcedSearchType,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search") ?? GetString(filters, "txtNum");
        var mode = GetString(filters, "mode") ?? "GG";
        var searchType = forcedSearchType ?? ResolveAssetListSearchType(filters);
        var filterId = GetAssetListFilterId(filters, searchType);
        if (searchType != AssetListSearchType.All && !filterId.HasValue)
        {
            return CreateDatabasePagedResult(
                GetAssetListFallbackTitle(searchType),
                GetAssetListFallbackTarget(searchType),
                true,
                "The filtered asset-list report needs its legacy scope identifier.",
                Array.Empty<AssetListFallbackRow>(),
                0,
                GetPageWindow(GetFallbackPagination(filters), 0),
                AssetListColumns
            );
        }

        var contracts = _context
            .Contracts.AsNoTracking()
            .Where(contract =>
                (contract.still_current == "y" || contract.still_current == "n")
            );
        var currentContracts = contracts.Where(contract => contract.still_current == "y");
        var historicContracts = contracts.Where(contract => contract.still_current == "n");

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join model in _context.Models.AsNoTracking()
                on vehicle.model_code equals model.model_code
                into vehicleModels
            from model in vehicleModels.DefaultIfEmpty()
            join vehicleClass in _context.Classes.AsNoTracking()
                on model.class_code equals vehicleClass.class_code
                into vehicleClasses
            from vehicleClass in vehicleClasses.DefaultIfEmpty()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into vehicleStatuses
            from status in vehicleStatuses.DefaultIfEmpty()
            join source in _context.VehicleSources.AsNoTracking()
                on vehicle.vs_code equals (byte?)source.vs_code
                into vehicleSources
            from source in vehicleSources.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking()
                on vehicle.type_code equals type.type_code
                into vehicleTypes
            from type in vehicleTypes.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on vehicle.location_code equals site.Site_code
                into vehicleSites
            from site in vehicleSites.DefaultIfEmpty()
            join department in _context.Departments.AsNoTracking()
                on site.Depatrment_code equals department.department_code
                into vehicleDepartments
            from department in vehicleDepartments.DefaultIfEmpty()
            join province in _context.Provinces.AsNoTracking()
                on site.province_code equals (byte?)province.province_code
                into vehicleProvinces
            from province in vehicleProvinces.DefaultIfEmpty()
            join currentContract in currentContracts
                on vehicle.vmf_code equals currentContract.vmf_code
                into vehicleCurrentContracts
            from currentContract in vehicleCurrentContracts.DefaultIfEmpty()
            from historicContract in historicContracts
                .Where(contract => contract.vmf_code == vehicle.vmf_code && currentContract == null)
                .OrderByDescending(contract => contract.contract_code)
                .Take(1)
                .DefaultIfEmpty()
            where
                vehicle.vehicle_status_code >= 0
                && vehicle.vehicle_status_code <= 1
            select new
            {
                vehicle.vmf_code,
                vehicle.registration_number,
                vehicle.fleet_number,
                vehicle.colour,
                vehicle.engine_number_1,
                vehicle.chassis_number,
                vehicle.barcode,
                vehicle.year_manufactured,
                vehicle.purchase_date,
                vehicle.purchase_amount,
                Status = status != null ? status.status_description : null,
                ModelDescription = model != null ? model.model_description : null,
                ClassCode = vehicleClass != null ? (short?)vehicleClass.class_code : null,
                ClassDescription = vehicleClass != null ? vehicleClass.description : null,
                SourceName = source != null ? source.name : null,
                HireType = type != null ? type.type_description : null,
                SiteCode = site != null ? (short?)site.Site_code : null,
                SiteName = site != null ? site.description : null,
                SiteDepartmentNumber = site != null ? site.Department_number : null,
                SiteResponsiblePerson = site != null ? site.res_person : null,
                SiteTelephone = site != null ? site.telephone : null,
                SiteNetAddress = site != null ? site.net_address : null,
                DepartmentNumber = department != null ? department.Department_number : null,
                DepartmentDescription = department != null ? department.description : null,
                ProvinceCode = site != null ? (byte?)site.province_code : null,
                ProvinceName = province != null ? province.province_name : null,
                ContractStillCurrent = currentContract != null ? currentContract.still_current
                : historicContract != null ? historicContract.still_current
                : null,
                ContractCode = currentContract != null ? (int?)currentContract.contract_code
                : historicContract != null ? historicContract.contract_code
                : null,
                ContractTypeCode = currentContract != null ? currentContract.contract_type
                : historicContract != null ? historicContract.contract_type
                : null,
                ContractStartDate = currentContract != null ? (DateTime?)currentContract.start_date
                : historicContract != null ? historicContract.start_date
                : null,
                ContractEndDate = currentContract != null ? currentContract.end_date
                : historicContract != null ? historicContract.end_date
                : null,
                ContractExpectedReturnDate = currentContract != null
                    ? currentContract.target_return_date
                : historicContract != null ? historicContract.target_return_date
                : null,
                ContractChargedUntil = currentContract != null ? currentContract.Charged_Until
                : historicContract != null ? historicContract.Charged_Until
                : null,
            };

        if (searchType == AssetListSearchType.Province)
        {
            query = query.Where(row => row.ProvinceCode == (byte?)filterId!.Value);
        }
        else if (searchType == AssetListSearchType.Department)
        {
            query = query.Where(row =>
                row.SiteCode.HasValue
                && _context.Sites.Any(site =>
                    site.Site_code == row.SiteCode.Value
                    && site.Depatrment_code == (short?)filterId!.Value
                )
            );
        }
        else if (searchType == AssetListSearchType.Site)
        {
            query = query.Where(row => row.SiteCode == (short?)filterId!.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(
                search,
                mode,
                cancellationToken
            );
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.ProvinceName)
                    .ThenBy(row => row.SiteName)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.ContractCode)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken
        );
        var contractTypeCodes = page
            .Rows.Select(row => row.ContractTypeCode)
            .Where(typeCode => !string.IsNullOrWhiteSpace(typeCode))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var contractTypes =
            contractTypeCodes.Length == 0
                ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                : await _context
                    .ContractTypes.AsNoTracking()
                    .Where(type => contractTypeCodes.Contains(type.type_name))
                    .ToDictionaryAsync(
                        type => type.type_name,
                        type => type.CT_description,
                        StringComparer.OrdinalIgnoreCase,
                        cancellationToken
                    );
        var rows = page.Rows.Select(row =>
            CreateAssetListFallbackRow(
                new AssetListFallbackVehicle(
                    row.registration_number,
                    row.fleet_number,
                    row.Status ?? (row.vmf_code > 0 ? "Unknown" : null),
                    row.ModelDescription,
                    row.colour,
                    JoinLegacyClassName(
                        row.ClassCode?.ToString(CultureInfo.InvariantCulture),
                        row.ClassDescription
                    ),
                    row.engine_number_1,
                    row.chassis_number,
                    row.barcode,
                    row.year_manufactured,
                    row.purchase_date,
                    row.purchase_amount,
                    row.SourceName,
                    string.IsNullOrWhiteSpace(row.HireType) ? "Empty" : row.HireType,
                    row.vmf_code,
                    row.SiteCode,
                    row.SiteName,
                    row.SiteDepartmentNumber,
                    row.SiteResponsiblePerson,
                    row.SiteTelephone,
                    row.SiteNetAddress,
                    row.DepartmentNumber,
                    row.DepartmentDescription,
                    row.ProvinceName
                ),
                row.ContractCode.HasValue
                    ? new AssetListFallbackContract(
                        row.vmf_code,
                        row.ContractStillCurrent,
                        row.ContractCode.Value,
                        row.ContractTypeCode,
                        row.ContractStartDate ?? default,
                        row.ContractEndDate,
                        row.ContractExpectedReturnDate,
                        row.ContractChargedUntil,
                        row.SiteCode ?? default,
                        null
                    )
                    : null,
                contractTypes
            )
        );

        return CreateDatabasePagedResult(
            GetAssetListFallbackTitle(searchType),
            GetAssetListFallbackTarget(searchType),
            true,
            "The legacy asset-list stored procedure was unavailable; the compatibility query preserves its status, source, location, contract selection, columns, and order.",
            rows,
            page.TotalCount,
            page.PageWindow,
            AssetListColumns
        );
    }

    private static readonly LegacyProjectionColumn[] AssetListColumns =
    [
        Column("registration", row => row.Registration),
        Column("ggnumber", row => row.GgNumber),
        Column("vehicle status", row => row.VehicleStatus),
        Column("model description", row => row.ModelDescription),
        Column("colour", row => row.Colour),
        Column("class description", row => row.ClassDescription),
        Column("engine number", row => row.EngineNumber),
        Column("chassis number", row => row.ChassisNumber),
        Column("bar code", row => row.BarCode),
        Column("year manufactured", row => row.YearManufactured),
        Column("datepurchased", row => row.DatePurchased),
        Column("purchaseamount", row => row.PurchaseAmount),
        Column("Sourced Via", row => row.SourcedVia),
        Column("Hire Type", row => row.HireType),
        Column("vmf_code", row => row.VmfCode),
        Column("contract stillcurrent", row => row.ContractStillCurrent),
        Column("contract_code", row => row.ContractCode),
        Column("contract_type", row => row.ContractType),
        Column("contract startdate", row => row.ContractStartDate),
        Column("contract enddate", row => row.ContractEndDate),
        Column("expected return date", row => row.ExpectedReturnDate),
        Column("contract chargeduntil", row => row.ContractChargedUntil),
        Column("contract period/months", row => row.ContractPeriodMonths),
        Column("months used", row => row.MonthsUsed),
        Column("months -remaining / +Exceeded", row => row.MonthsRemainingOrExceeded),
        Column("contract lastmodifiedby", row => row.ContractLastModifiedBy),
        Column("departmentname", row => row.DepartmentName),
        Column("sitename", row => row.SiteName),
        Column("site responsible person", row => row.SiteResponsiblePerson),
        Column("province", row => row.Province),
        Column("Tariff:(dailypool / permanent / monthlyLease)", row => row.FixedTariff),
        Column("kilo tariff", row => row.KiloTariff),
    ];

    private static string GetAssetListFallbackTitle(AssetListSearchType searchType) =>
        searchType switch
        {
            AssetListSearchType.Province => "Asset List: New & In-Service Vehicles By Province",
            AssetListSearchType.Department => "Asset List: New & In-Service Vehicles By Department",
            AssetListSearchType.Site => "Asset List: New & In-Service Vehicles By Site",
            _ => "Asset List: New & In-Service Vehicles",
        };

    private static string GetAssetListFallbackTarget(AssetListSearchType searchType) =>
        searchType switch
        {
            AssetListSearchType.Province =>
                "FISReports/GetVehicleInserviceReports.aspx?Mode=Province",
            AssetListSearchType.Department =>
                "FISReports/GetVehicleInserviceReports.aspx?Mode=Department",
            AssetListSearchType.Site => "FISReports/GetVehicleInserviceReports.aspx?Mode=Site",
            _ => "Finance/AssetVehicleReports.aspx",
        };

    private async Task<LegacyReportResultDto> BuildAssetVerificationPerSiteProvinceDateAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var siteCode = GetShort(filters, "site") ?? GetShort(filters, "cmbDeptNumber");
        var province = GetString(filters, "province") ?? GetString(filters, "cmbDeptName");
        var from = GetDate(filters, "from") ?? GetDate(filters, "sverdate");
        var to = GetDate(filters, "to") ?? GetDate(filters, "everdate");
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _assetVerificationRepository.GetReportPageAsync(
                new AssetVerificationReportPageQuery(
                    AssetVerificationReportMode.PerSiteProvinceDate,
                    siteCode,
                    province,
                    from,
                    to,
                    pagination.Page,
                    pagination.PageSize
                ),
                cancellationToken
            );
            return CreateDatabasePagedResult(
                "Report Per Site / Province / Verification Date",
                "Asset_Verification/RPT_asset_verification_2a.aspx",
                true,
                null,
                page.Items,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("Asset Verification Code", row => row.AssetVerificationCode),
                Column("Vehicle Reg No", row => row.VehicleRegNo),
                Column("Department", row => row.DepartmentName),
                Column("Site", row => row.SiteName),
                Column("Province", row => row.Province),
                Column("Vehicle Make", row => row.VehicleMake),
                Column("Vehicle Model", row => row.VehicleModel),
                Column("Licence Expiry Date", row => row.LicenceExpiryDate),
                Column("Last Verified", row => row.DateLastVerified),
                Column("Status", row => row.Status),
                Column("Responsible Manager", row => row.ResponsibleManager),
                Column("Current KM", row => row.CurrentKm)
            );
        }
        var rows = await GetAssetVerificationReportRowsAsync(cancellationToken);

        var filteredRows = rows.Where(row => !siteCode.HasValue || row.SiteCode == siteCode)
            .Where(row =>
                string.IsNullOrWhiteSpace(province)
                || string.Equals(
                    row.Province?.Trim(),
                    province.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Where(row => !from.HasValue || (row.DateLastVerified?.Date >= from.Value.Date))
            .Where(row => !to.HasValue || (row.DateLastVerified?.Date <= to.Value.Date))
            .OrderBy(row => row.DepartmentName)
            .ThenBy(row => row.SiteName)
            .ThenBy(row => row.VehicleRegNo)
            .Take(5000)
            .ToList();

        return CreateDynamicResult(
            "Report Per Site / Province / Verification Date",
            "Asset_Verification/RPT_asset_verification_2a.aspx",
            true,
            null,
            filteredRows,
            Column("Asset Verification Code", row => row.AssetVerificationCode),
            Column("Vehicle Reg No", row => row.VehicleRegNo),
            Column("Department", row => row.DepartmentName),
            Column("Site", row => row.SiteName),
            Column("Province", row => row.Province),
            Column("Vehicle Make", row => row.VehicleMake),
            Column("Vehicle Model", row => row.VehicleModel),
            Column("Licence Expiry Date", row => row.LicenceExpiryDate),
            Column("Last Verified", row => row.DateLastVerified),
            Column("Status", row => row.Status),
            Column("Responsible Manager", row => row.ResponsibleManager),
            Column("Current KM", row => row.CurrentKm)
        );
    }

    private async Task<LegacyReportResultDto> BuildAssetVerificationNotVerifiedAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _assetVerificationRepository.GetReportPageAsync(
                new AssetVerificationReportPageQuery(
                    AssetVerificationReportMode.NotVerified,
                    Page: pagination.Page,
                    PageSize: pagination.PageSize
                ),
                cancellationToken
            );
            return CreateDatabasePagedResult(
                "Vehicles Not Verified",
                "Asset_Verification/RPT_Not_Verified.aspx",
                true,
                null,
                page.Items,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("VMF Code", row => row.VmfCode),
                Column("GG Number", row => row.FleetNumber),
                Column("GP Number", row => row.RegistrationNumber),
                Column("Department", row => row.DepartmentName),
                Column("Site", row => row.SiteName),
                Column("Site Code", row => row.SiteCode)
            );
        }
        var records = (await _assetVerificationRepository.GetAllAsync()).ToList();
        var verifiedVmfCodes = records
            .Where(record => record.vmf_code.HasValue && record.vmf_code.Value > 0)
            .Select(record => record.vmf_code!.Value)
            .ToHashSet();
        var verifiedVehicleNumbers = records
            .Select(record => NormalizeAssetIdentifier(record.vehicle_reg_no))
            .Where(identifier => identifier is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var vehicles = await _context
            .Vehicles.AsNoTracking()
            .Where(vehicle => vehicle.vehicle_status_code == 1)
            .Select(vehicle => new AssetVerificationVehicleLookup(
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.licence_due_date
            ))
            .ToListAsync(cancellationToken);

        var currentContracts = await (
            from contract in _context.Contracts.AsNoTracking()
            join site in _context.Sites.AsNoTracking() on contract.site_code equals site.Site_code
            join department in _context.Departments.AsNoTracking()
                on site.Depatrment_code equals department.department_code
            where contract.still_current == "Y"
            select new AssetVerificationContractLookup(
                contract.vmf_code,
                contract.site_code,
                site.description,
                department.description
            )
        ).ToListAsync(cancellationToken);

        var contractByVehicle = currentContracts
            .GroupBy(contract => contract.VmfCode)
            .ToDictionary(group => group.Key, group => group.First());

        var rows = new List<AssetVerificationNotVerifiedRow>();
        foreach (var vehicle in vehicles)
        {
            if (
                !contractByVehicle.TryGetValue(vehicle.VmfCode, out var contract)
                || verifiedVmfCodes.Contains(vehicle.VmfCode)
                || IsVerifiedVehicleNumber(verifiedVehicleNumbers, vehicle.FleetNumber)
                || IsVerifiedVehicleNumber(verifiedVehicleNumbers, vehicle.RegistrationNumber)
            )
            {
                continue;
            }

            rows.Add(
                new AssetVerificationNotVerifiedRow(
                    vehicle.VmfCode,
                    vehicle.FleetNumber,
                    vehicle.RegistrationNumber,
                    contract.DepartmentName,
                    contract.SiteName,
                    contract.SiteCode
                )
            );
        }

        return CreateDynamicResult(
            "Vehicles Not Verified",
            "Asset_Verification/RPT_Not_Verified.aspx",
            true,
            null,
            rows.OrderBy(row => row.DepartmentName).ThenBy(row => row.FleetNumber).Take(5000),
            Column("VMF Code", row => row.VmfCode),
            Column("GG Number", row => row.FleetNumber),
            Column("GP Number", row => row.RegistrationNumber),
            Column("Department", row => row.DepartmentName),
            Column("Site", row => row.SiteName),
            Column("Site Code", row => row.SiteCode)
        );
    }

    private async Task<LegacyReportResultDto> BuildAssetVerificationVerifiedByDateRangeAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var from = GetDate(filters, "from") ?? GetDate(filters, "startDate");
        var to = GetDate(filters, "to") ?? GetDate(filters, "endDate");
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _assetVerificationRepository.GetReportPageAsync(
                new AssetVerificationReportPageQuery(
                    AssetVerificationReportMode.VerifiedByDateRange,
                    FromDate: from,
                    ToDate: to,
                    Page: pagination.Page,
                    PageSize: pagination.PageSize
                ),
                cancellationToken
            );
            return CreateDatabasePagedResult(
                "Vehicles Verified By Date Range - Excel Report",
                "Asset_Verification/RPT_vehicles_verified_by_date_2.aspx",
                true,
                null,
                page.Items,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("Vehicle Reg No", row => row.VehicleRegNo),
                Column("Department", row => row.DepartmentName),
                Column("Site", row => row.SiteName),
                Column("Province", row => row.Province),
                Column("Vehicle Make", row => row.VehicleMake),
                Column("Vehicle Model", row => row.VehicleModel),
                Column("Licence Expiry Date", row => row.LicenceExpiryDate),
                Column("Barcode", row => row.Barcode),
                Column("Last Verified", row => row.DateLastVerified),
                Column("Status", row => row.Status)
            );
        }
        var rows = await GetAssetVerificationReportRowsAsync(cancellationToken);

        var filteredRows = rows.Where(row => row.DateLastVerified.HasValue)
            .Where(row => !from.HasValue || row.DateLastVerified!.Value.Date >= from.Value.Date)
            .Where(row => !to.HasValue || row.DateLastVerified!.Value.Date <= to.Value.Date)
            .OrderBy(row => row.DepartmentName)
            .ThenBy(row => row.DateLastVerified)
            .ThenBy(row => row.VehicleRegNo)
            .Take(5000)
            .ToList();

        return CreateDynamicResult(
            "Vehicles Verified By Date Range - Excel Report",
            "Asset_Verification/RPT_vehicles_verified_by_date_2.aspx",
            true,
            null,
            filteredRows,
            Column("Vehicle Reg No", row => row.VehicleRegNo),
            Column("Department", row => row.DepartmentName),
            Column("Site", row => row.SiteName),
            Column("Province", row => row.Province),
            Column("Vehicle Make", row => row.VehicleMake),
            Column("Vehicle Model", row => row.VehicleModel),
            Column("Licence Expiry Date", row => row.LicenceExpiryDate),
            Column("Barcode", row => row.Barcode),
            Column("Last Verified", row => row.DateLastVerified),
            Column("Status", row => row.Status)
        );
    }

    private async Task<List<AssetVerificationReportRow>> GetAssetVerificationReportRowsAsync(
        CancellationToken cancellationToken
    )
    {
        var records = (await _assetVerificationRepository.GetAllAsync()).ToList();
        var vehicles = await _context
            .Vehicles.AsNoTracking()
            .Select(vehicle => new AssetVerificationVehicleLookup(
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.licence_due_date
            ))
            .ToListAsync(cancellationToken);
        var vehiclesByVmf = vehicles
            .GroupBy(vehicle => vehicle.VmfCode)
            .ToDictionary(group => group.Key, group => group.First());
        var vehiclesByFleet = vehicles
            .Where(vehicle => NormalizeAssetIdentifier(vehicle.FleetNumber) is not null)
            .GroupBy(
                vehicle => NormalizeAssetIdentifier(vehicle.FleetNumber)!,
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase
            );
        var vehiclesByRegistration = vehicles
            .Where(vehicle => NormalizeAssetIdentifier(vehicle.RegistrationNumber) is not null)
            .GroupBy(
                vehicle => NormalizeAssetIdentifier(vehicle.RegistrationNumber)!,
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase
            );

        return records
            .Select(record =>
            {
                AssetVerificationVehicleLookup? vehicle = null;
                if (record.vmf_code.HasValue)
                {
                    vehiclesByVmf.TryGetValue(record.vmf_code.Value, out vehicle);
                }

                if (
                    vehicle is null
                    && vehiclesByFleet.TryGetValue(
                        NormalizeAssetIdentifier(record.vehicle_reg_no) ?? string.Empty,
                        out var fleetMatch
                    )
                )
                {
                    vehicle = fleetMatch;
                }

                if (
                    vehicle is null
                    && vehiclesByRegistration.TryGetValue(
                        NormalizeAssetIdentifier(record.vehicle_reg_no) ?? string.Empty,
                        out var registrationMatch
                    )
                )
                {
                    vehicle = registrationMatch;
                }

                return new AssetVerificationReportRow(
                    record.asset_verification_code,
                    record.vehicle_reg_no ?? vehicle?.RegistrationNumber ?? vehicle?.FleetNumber,
                    record.vmf_code ?? vehicle?.VmfCode,
                    record.department_name,
                    record.site_name,
                    record.site_code,
                    record.province,
                    record.vehicle_make,
                    record.vehicle_model,
                    record.licence_expiry_date ?? vehicle?.LicenceDueDate,
                    record.date_last_verified ?? record.verification_date,
                    record.verification_status
                        ?? (
                            (record.date_last_verified ?? record.verification_date).HasValue
                                ? "Verified"
                                : "Pending"
                        ),
                    record.responsible_manager,
                    record.current_km,
                    record.barcode
                );
            })
            .ToList();
    }

    private static bool IsVerifiedVehicleNumber(
        ISet<string> verifiedNumbers,
        string? vehicleNumber
    ) =>
        NormalizeAssetIdentifier(vehicleNumber) is { } normalized
        && verifiedNumbers.Contains(normalized);

    private static string? NormalizeAssetIdentifier(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToSqlContainsPattern(string value) =>
        $"%{value.Replace("~", "~~", StringComparison.Ordinal).Replace("%", "~%", StringComparison.Ordinal).Replace("_", "~_", StringComparison.Ordinal).Replace("[", "~[", StringComparison.Ordinal)}%";

    private async Task<LegacyReportResultDto> BuildAuditTrailAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        var reportType = GetString(filters, "rtype");

        var query = _context
            .ContractAuditLogs.AsNoTracking()
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
                Notes = log.notes,
            });

        if (
            !string.IsNullOrWhiteSpace(reportType)
            && !string.Equals(reportType, "all", StringComparison.OrdinalIgnoreCase)
        )
        {
            query = query.Where(row => row.ReportType == reportType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                row.ReportType.Contains(term)
                || row.action.Contains(term)
                || row.UserId.Contains(term)
                || row.id.ToString().Contains(term)
                || (row.Field != null && row.Field.Contains(term))
                || (row.Notes != null && row.Notes.Contains(term))
            );
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows => rows.OrderByDescending(row => row.performed_at).ThenByDescending(row => row.id),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Audit Trail Reports",
            "Finance/GetFinancialAditTrailReportsDateRange.aspx",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("Audit ID", row => row.id),
            Column("Report Type", row => row.ReportType),
            Column("Action", row => row.action),
            Column("User ID", row => row.UserId),
            Column("Accessed Date", row => row.performed_at),
            Column("Contract Code", row => row.contract_code),
            Column("Field Changed", row => row.Field),
            Column("Old Value", row => row.OldValue),
            Column("New Value", row => row.NewValue),
            Column("Notes", row => row.Notes)
        );
    }

    private async Task<LegacyReportResultDto> BuildCaptureActivityAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
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

        var logsheetColumns = await GetReportTableColumnsAsync("Logsheets", cancellationToken);
        var logbookColumns = await GetReportTableColumnsAsync("logbook", cancellationToken);
        var sources = new List<string>
        {
            """
                SELECT
                    N'Contracts' AS [Module],
                    CONVERT(nvarchar(32), c.[contract_code]) AS [Record ID],
                    v.[fleet_number] AS [GG Number],
                    v.[registration_number] AS [GP Number],
                    c.[site_code] AS [Site Code],
                    c.[created_by_user_code] AS [Captured By],
                    c.[date_created] AS [Date Captured],
                    CONCAT(N'Contract ', c.[contract_code], N' (', c.[still_current], N')') AS [Description],
                    CONCAT(N'C-', c.[contract_code]) AS [__RowKey]
                FROM [dbo].[contract] AS c
                LEFT JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = c.[vmf_code]
                WHERE c.[is_deleted] = 0
                """,
            """
                SELECT
                    N'Trip Authority' AS [Module],
                    CONVERT(nvarchar(32), t.[trip_authority_code]) AS [Record ID],
                    v.[fleet_number] AS [GG Number],
                    v.[registration_number] AS [GP Number],
                    c.[site_code] AS [Site Code],
                    t.[created_by_user_code] AS [Captured By],
                    t.[date_created] AS [Date Captured],
                    t.[trip_reason] AS [Description],
                    CONCAT(N'T-', t.[trip_authority_code]) AS [__RowKey]
                FROM [dbo].[trip_authorities] AS t
                LEFT JOIN [dbo].[contract] AS c ON c.[contract_code] = t.[contract_code]
                LEFT JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = c.[vmf_code]
                WHERE t.[is_deleted] = 0
                """,
        };

        if (logsheetColumns.Contains("date_created"))
        {
            sources.Add(
                $"""
                SELECT
                    N'ELS Logsheet' AS [Module],
                    CONVERT(nvarchar(32), l.[log_code]) AS [Record ID],
                    v.[fleet_number] AS [GG Number],
                    v.[registration_number] AS [GP Number],
                    l.[site_code] AS [Site Code],
                    {OptionalReportColumn(
                    logsheetColumns,
                    "l",
                    "created_by_user_code",
                    "Captured By",
                    "int"
                )},
                    l.[date_created] AS [Date Captured],
                    CONCAT(N'Month ', CONVERT(char(7), l.[month], 120)) AS [Description],
                    CONCAT(N'E-', l.[log_code]) AS [__RowKey]
                FROM [dbo].[Logsheets] AS l
                LEFT JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
                WHERE {(
                    logsheetColumns.Contains("is_deleted")
                        ? "COALESCE(l.[is_deleted], 0) = 0"
                        : "1 = 1"
                )}
                """
            );
        }

        if (logbookColumns.Contains("date_created"))
        {
            sources.Add(
                $"""
                SELECT
                    N'Logbook' AS [Module],
                    CONVERT(nvarchar(32), l.[logbookcode]) AS [Record ID],
                    v.[fleet_number] AS [GG Number],
                    v.[registration_number] AS [GP Number],
                    l.[site_code] AS [Site Code],
                    {OptionalReportColumn(
                    logbookColumns,
                    "l",
                    "created_by_user_code",
                    "Captured By",
                    "int"
                )},
                    l.[date_created] AS [Date Captured],
                    l.[lb_comment] AS [Description],
                    CONCAT(N'L-', l.[logbookcode]) AS [__RowKey]
                FROM [dbo].[logbook] AS l
                LEFT JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
                WHERE {(
                    logbookColumns.Contains("is_deleted")
                        ? "COALESCE(l.[is_deleted], 0) = 0"
                        : "1 = 1"
                )}
                """
            );
        }

        var predicates = new List<string>
        {
            "activity.[Date Captured] >= @captureStart",
            "activity.[Date Captured] < @captureEndExclusive",
        };
        var parameters = new List<ReportParameter>
        {
            new("@captureStart", DbType.DateTime, startDate),
            new("@captureEndExclusive", DbType.DateTime, endDate.AddDays(1)),
        };
        if (!string.Equals(module, "all", StringComparison.OrdinalIgnoreCase))
        {
            predicates.Add("activity.[Module] = @captureModule");
            parameters.Add(new("@captureModule", DbType.String, module));
        }
        if (site.HasValue)
        {
            predicates.Add("activity.[Site Code] = @captureSite");
            parameters.Add(new("@captureSite", DbType.Int16, site.Value));
        }
        if (!string.IsNullOrWhiteSpace(vehicle))
        {
            predicates.Add(
                "(activity.[GG Number] LIKE @captureVehicle ESCAPE '~' OR activity.[GP Number] LIKE @captureVehicle ESCAPE '~')"
            );
            parameters.Add(
                new("@captureVehicle", DbType.String, ToSqlContainsPattern(vehicle.Trim()))
            );
        }
        if (!string.IsNullOrWhiteSpace(capturedBy))
        {
            predicates.Add(
                "CONVERT(nvarchar(32), activity.[Captured By]) LIKE @capturedBy ESCAPE '~'"
            );
            parameters.Add(
                new("@capturedBy", DbType.String, ToSqlContainsPattern(capturedBy.Trim()))
            );
        }

        var sql = $"""
            SELECT
                activity.[Module],
                activity.[Record ID],
                activity.[GG Number],
                activity.[GP Number],
                activity.[Site Code],
                activity.[Captured By],
                activity.[Date Captured],
                activity.[Description],
                activity.[__RowKey]
            FROM (
                {string.Join("\nUNION ALL\n", sources)}
            ) AS activity
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY activity.[Date Captured] DESC, activity.[Module], activity.[__RowKey]
            """;
        var result = await ExecutePagedRawReportQueryAsync(
            "capture-activity",
            "Capture Activity",
            "Modern report (no direct legacy equivalent)",
            sql,
            parameters,
            filters,
            cancellationToken
        );
        result.IsApproximate = true;
        result.ApproximationReason =
            "No direct legacy equivalent exists; this dynamic result merges compatible capture timestamps across legacy-backed tables.";
        return result;
    }

    private async Task<LegacyReportResultDto> BuildContractHistoryAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var vmfCode = GetInt(filters, "vmf");
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        var query =
            from contract in _context.Contracts.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking()
                on contract.vmf_code equals vehicle.vmf_code
                into contractVehicles
            from vehicle in contractVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on contract.site_code equals site.Site_code
                into contractSites
            from site in contractSites.DefaultIfEmpty()
            where true
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
                contract_status_code = (short?)null,
                Site = site != null ? site.description : null,
                contract.Driver_name,
                contract.Authorisation,
                contract.Notes,
            };

        if (vmfCode.HasValue)
        {
            query = query.Where(row => row.vmf_code == vmfCode.Value);
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(
                search,
                mode,
                cancellationToken
            );
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderByDescending(row => row.start_date)
                    .ThenByDescending(row => row.contract_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Contract History",
            "Logs/RPT_Contracts_per_vehicle.aspx",
            true,
            "Legacy contract history is a composite page. This dynamic grid flattens the same legacy-backed contract data.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("Notes", row => row.Notes)
        );
    }

    private async Task<LegacyReportResultDto> BuildContractsAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var stillCurrent = GetString(filters, "current");
        var siteCode = GetShort(filters, "site");
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var statusCode = GetShort(filters, "status");

        var query =
            from contract in _context.Contracts.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking()
                on contract.vmf_code equals vehicle.vmf_code
                into contractVehicles
            from vehicle in contractVehicles.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on contract.site_code equals site.Site_code
                into contractSites
            from site in contractSites.DefaultIfEmpty()
            where true
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
                contract_status_code = (short?)null,
                contract.Driver_name,
                contract.Authorisation,
                contract.Notes,
            };

        if (
            !string.IsNullOrWhiteSpace(stillCurrent)
            && !string.Equals(stillCurrent, "all", StringComparison.OrdinalIgnoreCase)
        )
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

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderByDescending(row => row.start_date)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.contract_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Contracts Report",
            "/FISReports/Contracts/Contracts.aspx",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("Notes", row => row.Notes)
        );
    }

    private Task<LegacyReportResultDto> BuildDepartmentsSitesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Section = "One Department/Site Reports",
                Sequence = "1",
                Report = "One Department",
                Target = "Department/rpt_single_dept_1.aspx",
            },
            new
            {
                Section = "One Department/Site Reports",
                Sequence = "2",
                Report = "One Site",
                Target = "Department/rpt_single_site_1.aspx",
            },
            new
            {
                Section = "All Department/Site Reports",
                Sequence = "3",
                Report = "All Departments & Site with number vehicles",
                Target = "Department/RPT_All_Department_Status.aspx",
            },
            new
            {
                Section = "All Department/Site Reports",
                Sequence = "4",
                Report = "All Departments & Sites with contact details",
                Target = "Department/rpt_all_sites.aspx",
            },
            new
            {
                Section = "Other Report",
                Sequence = "5",
                Report = "List of Vehicles on ELS",
                Target = "Department/RPT_els_dept_period_main.aspx",
            },
            new
            {
                Section = "Other Report",
                Sequence = "6",
                Report = "List of Vehicles on Manual LOGSHEETS",
                Target = "Department/RPT_logs_dept_period_main.aspx",
            },
            new
            {
                Section = "Other Report",
                Sequence = "7",
                Report = "Outstanding Logs per Dept : ELS and Manual Logs Combined",
                Target = "Department/RPT_uits_els_en_logs_dept_period_main.aspx",
            },
            new
            {
                Section = "Navigation",
                Sequence = "R",
                Report = "Return To Main Page",
                Target = "FISReports/FIS_Report.aspx",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Departments and Sites",
                "Department/Department.aspx",
                false,
                null,
                rows,
                Column("Section", row => row.Section),
                Column("Sequence", row => row.Sequence),
                Column("Report", row => row.Report),
                Column("Target", row => row.Target)
            )
        );
    }

    private Task<LegacyReportResultDto> BuildFinesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var mode = GetString(filters, "mode")?.Trim().ToLowerInvariant();
        return mode switch
        {
            "one-vehicle" => BuildFineDetailReportAsync(
                filters,
                "Fines Report on ONE Vehicle",
                "fines/RPT_one_num_report_Fines.aspx",
                cancellationToken
            ),
            "appear-date" => BuildFineDetailReportAsync(
                filters,
                "Due Date To Appear In Court Report",
                "fines/RPT_app_date_report_Fines.aspx",
                cancellationToken
            ),
            "fine-detail" => BuildFineDetailReportAsync(
                filters,
                "Fine Detail",
                "fines/RPT_letter_report_Fines.aspx",
                cancellationToken
            ),
            "reissue-submission" => BuildFineReissueReportAsync(filters, cancellationToken),
            "traffic-dept-detail" => BuildTrafficDeptReportAsync(filters, cancellationToken),
            "dept-site-period" => BuildFineSummaryReportAsync(
                filters,
                "Fines Report for a Department (or Site)",
                "fines/RPT_dept_period_report_Fines.aspx",
                cancellationToken
            ),
            "vehicle-period" => BuildFineSummaryReportAsync(
                filters,
                "Fines Report per Vehicle",
                "fines/RPT_finepervehicle_report.aspx",
                cancellationToken
            ),
            "metro-period" => BuildFineSummaryReportAsync(
                filters,
                "Fines Report per Metro",
                "fines/RPT_metro_report.aspx",
                cancellationToken
            ),
            "all" => BuildFineSummaryReportAsync(
                filters,
                "A List of all Fines",
                "fines/RPT_FinesAll.aspx",
                cancellationToken
            ),
            _ => Task.FromResult(CreateFinesMenuResult()),
        };
    }

    private static LegacyReportResultDto CreateFinesMenuResult()
    {
        var rows = new[]
        {
            new
            {
                Section = "One Vehicle Fines Reports",
                Sequence = "1",
                Report = "Fines Report on ONE Vehicle",
                Target = "fines/RPT_one_num_main_Fines.htm",
            },
            new
            {
                Section = "Other Fines Reports",
                Sequence = "2",
                Report = "Fines Report, for a Dept / Site, for a period",
                Target = "fines/selectfines.htm",
            },
            new
            {
                Section = "Other Fines Reports",
                Sequence = "3",
                Report = "Fines Report on Appear Date",
                Target = "fines/RPT_app_date_main_Fines.htm",
            },
            new
            {
                Section = "Other Fines Reports",
                Sequence = "4",
                Report = "Submission to Re-Issue Fine in Transport Officer's Name",
                Target = "fines/RPT_letter_main_Fines.aspx",
            },
            new
            {
                Section = "Other Fines Reports",
                Sequence = "5",
                Report = "Traffic Dept Detail",
                Target = "fines/RPT_traffic_all_report.aspx",
            },
            new
            {
                Section = "Navigation",
                Sequence = "R",
                Report = "Return To Main Page",
                Target = "FISReports/FIS_Report.aspx",
            },
        };

        return CreateDynamicResult(
            "Fines Reports",
            "Fines/RPTFines.aspx",
            false,
            null,
            rows,
            Column("Section", row => row.Section),
            Column("Sequence", row => row.Sequence),
            Column("Report", row => row.Report),
            Column("Target", row => row.Target)
        );
    }

    private async Task<LegacyReportResultDto> BuildFineDetailReportAsync(
        IDictionary<string, string?> filters,
        string title,
        string legacyTarget,
        CancellationToken cancellationToken
    )
    {
        var fineColumns = await GetReportTableColumnsAsync("Fines", cancellationToken);
        var trafficDeptColumns = await GetReportTableColumnsAsync(
            "Traffic_Dept",
            cancellationToken
        );
        var predicates = BuildFineLivePredicates(fineColumns);
        var parameters = new List<ReportParameter>();
        ApplyFineSiteScope(predicates, parameters, filters);
        var mode = GetString(filters, "mode")?.Trim().ToLowerInvariant();

        if (mode == "fine-detail")
        {
            var fineCode = GetInt(filters, "fineCode") ?? GetInt(filters, "FCode");
            if (fineCode.HasValue)
            {
                predicates.Add("f.[Fine_code] = @fineCode");
                parameters.Add(new("@fineCode", DbType.Int32, fineCode.Value));
            }
        }
        else if (mode == "appear-date")
        {
            var appearDate =
                GetDate(filters, "appearDate")
                ?? GetDate(filters, "date")
                ?? GetDate(filters, "xdat");
            if (appearDate.HasValue)
            {
                predicates.Add("f.[Appear_date] = @appearDate");
                parameters.Add(new("@appearDate", DbType.DateTime2, appearDate.Value.Date));
            }
        }
        else
        {
            var vmfCode = GetInt(filters, "vmf") ?? GetInt(filters, "vmfCode");
            if (vmfCode.HasValue)
            {
                predicates.Add("f.[vmf_code] = @vmfCode");
                parameters.Add(new("@vmfCode", DbType.Int32, vmfCode.Value));
            }
        }

        var sql =
            $"{BuildFineDetailSelect(fineColumns, trafficDeptColumns)} WHERE {string.Join(" AND ", predicates)} ORDER BY f.[Offence_date], f.[Receive_gg_date] DESC, f.[Fine_code]";
        return await ExecuteFineReportQueryAsync(
            title,
            legacyTarget,
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildFineReissueReportAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var fineColumns = await GetReportTableColumnsAsync("Fines", cancellationToken);
        var predicates = BuildFineLivePredicates(fineColumns);
        var parameters = new List<ReportParameter>();
        ApplyFineSiteScope(predicates, parameters, filters);
        var vmfCode = GetInt(filters, "vmf") ?? GetInt(filters, "vmfCode");
        if (vmfCode.HasValue)
        {
            predicates.Add("f.[vmf_code] = @vmfCode");
            parameters.Add(new("@vmfCode", DbType.Int32, vmfCode.Value));
        }

        var sql =
            $"{BuildFineReissueSelect(fineColumns)} WHERE {string.Join(" AND ", predicates)} ORDER BY f.[Offence_date], f.[Receive_gg_date] DESC, f.[Fine_code]";
        return await ExecuteFineReportQueryAsync(
            "Submission to Re-Issue a Traffic Fine",
            "fines/RPT_letter_main_Fines.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildTrafficDeptReportAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var trafficDeptColumns = await GetReportTableColumnsAsync(
            "Traffic_Dept",
            cancellationToken
        );
        var predicates = trafficDeptColumns.Contains("is_deleted")
            ? "WHERE [is_deleted] = 0"
            : string.Empty;
        var sql =
            $"{BuildTrafficDeptSelect(trafficDeptColumns)} {predicates} ORDER BY [Traf_name], [Traffic_dept_code]";
        return await ExecuteFineReportQueryAsync(
            "Traffic Dept Information",
            "fines/RPT_traffic_all_report.aspx",
            sql,
            Array.Empty<ReportParameter>(),
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildFineSummaryReportAsync(
        IDictionary<string, string?> filters,
        string title,
        string legacyTarget,
        CancellationToken cancellationToken
    )
    {
        var fineColumns = await GetReportTableColumnsAsync("Fines", cancellationToken);
        var predicates = BuildFineLivePredicates(fineColumns);
        var parameters = new List<ReportParameter>();
        ApplyFineSiteScope(predicates, parameters, filters);
        var mode = GetString(filters, "mode")?.Trim().ToLowerInvariant();

        var startDate =
            GetDate(filters, "from") ?? GetDate(filters, "startDate") ?? GetDate(filters, "BDAT");
        var endDate =
            GetDate(filters, "to") ?? GetDate(filters, "endDate") ?? GetDate(filters, "EDAT");
        if (startDate.HasValue)
        {
            predicates.Add("f.[Offence_date] >= @startDate");
            parameters.Add(new("@startDate", DbType.DateTime2, startDate.Value.Date));
        }
        if (endDate.HasValue)
        {
            predicates.Add("f.[Offence_date] <= @endDate");
            parameters.Add(new("@endDate", DbType.DateTime2, endDate.Value.Date));
        }

        if (mode == "dept-site-period")
        {
            var siteCode = GetShort(filters, "site") ?? GetShort(filters, "siteCode");
            if (siteCode.HasValue)
            {
                predicates.Add("f.[Site_code] = @siteCode");
                parameters.Add(new("@siteCode", DbType.Int16, siteCode.Value));
            }

            var department = GetString(filters, "dept") ?? GetString(filters, "department");
            if (!string.IsNullOrWhiteSpace(department))
            {
                predicates.Add("s.[Department_number] LIKE @department");
                parameters.Add(new("@department", DbType.String, $"%{department.Trim()}%"));
            }
        }
        else if (mode == "vehicle-period")
        {
            var vmfCode = GetInt(filters, "vmf") ?? GetInt(filters, "vmfCode");
            if (vmfCode.HasValue)
            {
                predicates.Add("f.[vmf_code] = @vmfCode");
                parameters.Add(new("@vmfCode", DbType.Int32, vmfCode.Value));
            }
        }
        else if (mode == "metro-period")
        {
            var issuer = GetString(filters, "issuer") ?? GetString(filters, "offenceIssuer");
            if (!string.IsNullOrWhiteSpace(issuer))
            {
                predicates.Add("f.[Offence_issuer] = @issuer");
                parameters.Add(new("@issuer", DbType.String, issuer.Trim()));
            }
        }

        var sql =
            $"{BuildFineSummarySelect()} WHERE {string.Join(" AND ", predicates)} ORDER BY s.[Department_number], v.[fleet_number], f.[Offence_date] DESC, f.[Fine_code]";
        return await ExecuteFineReportQueryAsync(
            title,
            legacyTarget,
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private static string BuildFineDetailSelect(
        IReadOnlySet<string> fineColumns,
        IReadOnlySet<string> trafficDeptColumns
    )
    {
        return $"""
            SELECT
                f.[Fine_code] AS [Fine Code],
                f.[vmf_code] AS [VMF Code],
                v.[fleet_number] AS [GG Number],
                v.[registration_number] AS [Prov Reg Number],
                vt.[type_description] AS [Hire Type],
                f.[Offence_date] AS [Date of Offence],
                f.[Offence_reference] AS [Reference Number],
                f.[Offence_issuer] AS [Issued By],
                td.[Traf_name] AS [Traffic Dept],
                f.[Fine_amount] AS [Amount of Fine],
                f.[Pay_due_date] AS [Due Date of Payment],
                f.[Appear_date] AS [Due Date to Appear in Court],
                {OptionalReportColumn(fineColumns, "f", "Document_type", "Document Type")},
                f.[Receive_gg_date] AS [Date Received at GMT],
                f.[Issuer_notify_date] AS [Date of Notification to Issuer],
                f.[Notify_dept_date] AS [Date of Notification to Dept],
                s.[description] AS [Dept],
                s.[Department_number] AS [Dept Code],
                s.[res_person] AS [Dept Responsible Person],
                s.[telephone] AS [Dept Telephone],
                s.[fax] AS [Dept Fax],
                s.[net_address] AS [Dept Email],
                s.[address1] AS [Dept Address1],
                s.[address2] AS [Dept Address2],
                s.[address3] AS [Dept Address3],
                s.[postal_code] AS [Dept Postal Code],
                {OptionalReportColumn(
                fineColumns,
                "f",
                "Dept_person_name",
                "Name of Responsable Person at Dept"
            )},
                {OptionalReportColumn(
                fineColumns,
                "f",
                "Dept_person_id",
                "ID of Responsable Person at Dept"
            )},
                f.[Offence_name] AS [Name of Offender],
                f.[Fine_pay_date] AS [Date Fine Paid],
                f.[Withdraw_date] AS [Date Withdrawn],
                {OptionalReportColumn(
                fineColumns,
                "f",
                "Traffic_dept_code",
                "Traffic Dept Code",
                "smallint"
            )},
                td.[Traf_res_person] AS [Traffic Dept Responsible Person],
                td.[Traf_post_address1] AS [Traffic Dept Postal Address1],
                td.[Traf_post_address2] AS [Traffic Dept Postal Address2],
                td.[Traf_post_code] AS [Traffic Dept Postal Code],
                td.[Traf_telephone] AS [Traffic Dept Telephone],
                td.[Traf_fax] AS [Traffic Dept Fax],
                {OptionalReportColumn(trafficDeptColumns, "td", "Traf_cell", "Traffic Dept Cell")},
                td.[Traf_email] AS [Traffic Dept Email]
            FROM [dbo].[Fines] f
            INNER JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = f.[vmf_code]
            INNER JOIN [dbo].[site] s ON s.[Site_code] = f.[Site_code]
            LEFT JOIN [dbo].[type] vt ON vt.[type_code] = v.[type_code]
            LEFT JOIN [dbo].[Traffic_Dept] td ON {(
                fineColumns.Contains("Traffic_dept_code")
                    ? "f.[Traffic_dept_code] = td.[Traffic_dept_code]"
                    : "1 = 0"
            )}
            """;
    }

    private static string BuildFineReissueSelect(IReadOnlySet<string> fineColumns)
    {
        return $"""
            SELECT
                f.[Fine_code] AS [Fine Code],
                f.[Offence_date] AS [Offence Date],
                {OptionalReportColumn(fineColumns, "f", "Document_type", "Doc Type")},
                f.[Receive_gg_date] AS [Date at GMT],
                v.[registration_number] AS [GP Number],
                v.[fleet_number] AS [GG Number],
                vt.[type_description] AS [Hire Type]
            FROM [dbo].[Fines] f
            INNER JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = f.[vmf_code]
            LEFT JOIN [dbo].[type] vt ON vt.[type_code] = v.[type_code]
            """;
    }

    private static string BuildFineSummarySelect()
    {
        return """
            SELECT
                f.[Fine_code] AS [__RowKey],
                v.[registration_number] AS [Prov Reg Number],
                v.[fleet_number] AS [GG Number],
                f.[Offence_date] AS [Offence Date],
                f.[Offence_reference] AS [Reference],
                f.[Offence_issuer] AS [Issuer],
                f.[Fine_amount] AS [Fine Amount],
                s.[Department_number] AS [Dept / Site Number],
                s.[description] AS [Dept / Site]
            FROM [dbo].[Fines] f
            INNER JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = f.[vmf_code]
            INNER JOIN [dbo].[site] s ON s.[Site_code] = f.[Site_code]
            """;
    }

    private static string BuildTrafficDeptSelect(IReadOnlySet<string> trafficDeptColumns)
    {
        return $"""
            SELECT
                [Traffic_dept_code] AS [Traffic Dept Code],
                [Traf_name] AS [Traffic Dept Name],
                [Traf_res_person] AS [Responsable Person],
                [Traf_post_address1] AS [Postal Address1],
                [Traf_post_address2] AS [Postal Address2],
                [Traf_post_code] AS [Postal Code],
                [Traf_telephone] AS [Telephone],
                [Traf_fax] AS [Fax],
                {OptionalReportColumn(trafficDeptColumns, "", "Traf_cell", "Cell")},
                [Traf_email] AS [Email]
            FROM [dbo].[Traffic_Dept]
            """;
    }

    private static List<string> BuildFineLivePredicates(IReadOnlySet<string> fineColumns) =>
        new List<string> { fineColumns.Contains("is_deleted") ? "f.[is_deleted] = 0" : "1 = 1" };

    private static void ApplyFineSiteScope(
        ICollection<string> predicates,
        ICollection<ReportParameter> parameters,
        IDictionary<string, string?> filters
    )
    {
        if (!filters.TryGetValue(InternalFineSiteScopeFilter, out var rawScope))
        {
            return;
        }

        var siteCodes = (rawScope ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => short.TryParse(value, out var code) ? (short?)code : null)
            .Where(code => code is > 0)
            .Select(code => code!.Value)
            .Distinct()
            .ToArray();
        if (siteCodes.Length == 0)
        {
            predicates.Add("1 = 0");
            return;
        }

        var placeholders = siteCodes
            .Select((_, index) => $"@fineScopeSite{index}")
            .ToArray();
        predicates.Add($"f.[Site_code] IN ({string.Join(", ", placeholders)})");
        for (var index = 0; index < siteCodes.Length; index++)
        {
            parameters.Add(new ReportParameter(placeholders[index], DbType.Int16, siteCodes[index]));
        }
    }

    private static string BuildReportSiteScope(
        ICollection<ReportParameter> parameters,
        IDictionary<string, string?> filters,
        string siteExpression
    )
    {
        if (!filters.TryGetValue(InternalReportSiteScopeFilter, out var rawScope))
        {
            return "1 = 1";
        }

        var siteCodes = (rawScope ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => short.TryParse(value, out var code) ? (short?)code : null)
            .Where(code => code is > 0)
            .Select(code => code!.Value)
            .Distinct()
            .ToArray();
        if (siteCodes.Length == 0)
        {
            return "1 = 0";
        }

        var placeholders = siteCodes
            .Select((_, index) => $"@reportScopeSite{index}")
            .ToArray();
        for (var index = 0; index < siteCodes.Length; index++)
        {
            parameters.Add(
                new ReportParameter(placeholders[index], DbType.Int16, siteCodes[index])
            );
        }

        return $"{siteExpression} IN ({string.Join(", ", placeholders)})";
    }

    private static IReadOnlySet<short>? GetReportSiteScope(
        IDictionary<string, string?> filters
    )
    {
        if (!filters.TryGetValue(InternalReportSiteScopeFilter, out var rawScope))
            return null;

        return (rawScope ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => short.TryParse(value, out var code) ? (short?)code : null)
            .Where(code => code is > 0)
            .Select(code => code!.Value)
            .ToHashSet();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is assembled only from fixed report templates and allowlisted runtime column projections; all report values are parameters."
    )]
    private Task<LegacyReportResultDto> ExecuteFineReportQueryAsync(
        string title,
        string legacyTarget,
        string sql,
        IReadOnlyList<ReportParameter> parameters,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) =>
        ExecutePagedRawReportQueryAsync(
            "fines",
            title,
            legacyTarget,
            sql,
            parameters,
            filters,
            cancellationToken
        );

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Each query is composed from a fixed legacy report template and allowlisted compatibility projections; values, count, and page controls are parameters."
    )]
    private async Task<LegacyReportResultDto> ExecutePagedRawReportQueryAsync(
        string reportKey,
        string title,
        string legacyTarget,
        string sql,
        IReadOnlyList<ReportParameter> parameters,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var pagination = GetFallbackPagination(filters);
        var (selectSql, orderBySql) = SplitRawReportOrderBy(sql);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var totalCount = 0;
            LegacyReportPageWindow pageWindow;
            if (pagination is not null && !pagination.IncludeAll)
            {
                await using var countCommand = connection.CreateCommand();
                countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                countCommand.CommandText =
                    $"SELECT COUNT(1) FROM ({selectSql}) AS [legacy_report_count]";
                AddReportParameters(countCommand, parameters);
                totalCount = Convert.ToInt32(
                    await countCommand.ExecuteScalarAsync(cancellationToken),
                    CultureInfo.InvariantCulture
                );
                pageWindow = GetPageWindow(pagination, totalCount);
            }
            else
            {
                pageWindow = new LegacyReportPageWindow(1, 1, 0, false);
            }

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = pageWindow.IsPaged
                ? $"{selectSql}\n{orderBySql}\nOFFSET @pageSkip ROWS FETCH NEXT @pageSize ROWS ONLY"
                : sql;
            AddReportParameters(dataCommand, parameters);
            if (pageWindow.IsPaged)
            {
                AddReportParameter(dataCommand, "@pageSkip", DbType.Int64, pageWindow.Skip);
                AddReportParameter(dataCommand, "@pageSize", DbType.Int32, pageWindow.PageSize);
            }

            var (columns, rows) = await ReadRawReportRowsAsync(dataCommand, cancellationToken);
            if (!pageWindow.IsPaged)
            {
                totalCount = rows.Count;
                pageWindow = new LegacyReportPageWindow(1, Math.Max(1, totalCount), 0, false);
            }

            return new LegacyReportResultDto
            {
                ReportKey = reportKey,
                Title = title,
                LegacyTarget = legacyTarget,
                IsApproximate = false,
                Columns = columns,
                Rows = rows,
                TotalCount = totalCount,
                Page = pageWindow.Page,
                PageSize = pageWindow.PageSize,
                IsDatabasePaged = pageWindow.IsPaged,
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static (string SelectSql, string OrderBySql) SplitRawReportOrderBy(string sql)
    {
        var normalizedSql = sql.Trim().TrimEnd(';');
        var orderByIndex = normalizedSql.LastIndexOf(
            "ORDER BY",
            StringComparison.OrdinalIgnoreCase
        );
        if (orderByIndex < 0)
        {
            throw new InvalidOperationException(
                "A paged legacy report query must provide a deterministic ORDER BY clause."
            );
        }

        return (normalizedSql[..orderByIndex].TrimEnd(), normalizedSql[orderByIndex..].TrimEnd());
    }

    private static void AddReportParameters(
        DbCommand command,
        IReadOnlyList<ReportParameter> parameters
    )
    {
        foreach (var parameter in parameters)
        {
            AddReportParameter(command, parameter.Name, parameter.Type, parameter.Value);
        }
    }

    private static async Task<(
        List<LegacyReportColumnDto> Columns,
        List<Dictionary<string, string?>> Rows
    )> ReadRawReportRowsAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var columns = new List<LegacyReportColumnDto>();
        var rows = new List<Dictionary<string, string?>>(capacity: 128);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rowKeyOrdinal = -1;
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var header = reader.GetName(index);
            if (string.Equals(header, "__RowKey", StringComparison.Ordinal))
            {
                rowKeyOrdinal = index;
            }
            else
            {
                columns.Add(new LegacyReportColumnDto { Key = header, Header = header });
            }
        }

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (rowKeyOrdinal >= 0)
            {
                row["__rowKey"] = FormatValue(reader[rowKeyOrdinal]);
            }
            foreach (var column in columns)
            {
                row[column.Key] = FormatValue(reader[column.Key]);
            }
            rows.Add(row);
        }

        return (columns, rows);
    }

    private async Task<HashSet<string>> GetReportTableColumnsAsync(
        string tableName,
        CancellationToken cancellationToken,
        string schemaName = "dbo"
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
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
            AddReportParameter(command, "@schema", DbType.String, schemaName);
            AddReportParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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

    private async Task<string> ResolveReportRelationAsync(
        string preferredObject,
        string fallbackTable,
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COALESCE(OBJECT_ID(@preferredObject), OBJECT_ID(@fallbackTable));
                """;
            AddReportParameter(command, "@preferredObject", DbType.String, $"dbo.{preferredObject}");
            AddReportParameter(command, "@fallbackTable", DbType.String, $"dbo.{fallbackTable}");
            var objectId = await command.ExecuteScalarAsync(cancellationToken);
            if (objectId is null or DBNull)
            {
                throw new LegacyReportRelationUnavailableException(preferredObject, fallbackTable);
            }

            await using var typeCommand = connection.CreateCommand();
            typeCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            typeCommand.CommandText = """
                SELECT [type]
                FROM [sys].[objects]
                WHERE [object_id] = COALESCE(OBJECT_ID(@preferredObject), OBJECT_ID(@fallbackTable));
                """;
            AddReportParameter(typeCommand, "@preferredObject", DbType.String, $"dbo.{preferredObject}");
            AddReportParameter(typeCommand, "@fallbackTable", DbType.String, $"dbo.{fallbackTable}");
            var objectType = Convert.ToString(
                await typeCommand.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture
            );
            var relation = string.Equals(objectType, "V", StringComparison.OrdinalIgnoreCase)
                ? preferredObject
                : fallbackTable;
            return $"[dbo].[{relation}]";
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string OptionalReportColumn(
        IReadOnlySet<string> availableColumns,
        string tableAlias,
        string column,
        string alias,
        string sqlType = "nvarchar(255)"
    )
    {
        var qualifiedColumn = string.IsNullOrWhiteSpace(tableAlias)
            ? $"[{column}]"
            : $"{tableAlias}.[{column}]";
        return availableColumns.Contains(column)
            ? $"{qualifiedColumn} AS [{alias}]"
            : $"CAST(NULL AS {sqlType}) AS [{alias}]";
    }

    private static void AddReportParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private Task<LegacyReportResultDto> BuildLossesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var mode = GetString(filters, "mode")?.Trim().ToLowerInvariant();
        return mode switch
        {
            "one-vehicle" => BuildLossesOneVehicleAsync(filters, cancellationToken),
            "all" => BuildLossesAllAsync(filters, cancellationToken),
            "no-report" => BuildLossesReportStatusAsync(
                filters,
                outstanding: true,
                cancellationToken
            ),
            "with-report" => BuildLossesReportStatusAsync(
                filters,
                outstanding: false,
                cancellationToken
            ),
            "dept-period" => BuildLossesDepartmentPeriodAsync(filters, cancellationToken),
            _ => BuildLossesMenu(),
        };
    }

    private static Task<LegacyReportResultDto> BuildLossesMenu()
    {
        var rows = new[]
        {
            new
            {
                Section = "One Vehicle Losses Reports",
                Sequence = "1",
                Report = "Losses for one vehicle",
                Target = "losses/RPT_loss_per_vehicle.htm",
            },
            new
            {
                Section = "All Vehicle Losses Reports",
                Sequence = "2",
                Report = "All Losses Sorted By Loss type, Department number GG number or Loss Date",
                Target = "losses/RPT_All_Losses_menu.aspx",
            },
            new
            {
                Section = "All Vehicle Losses Reports",
                Sequence = "3",
                Report = "Losses where Report from Department is outstanding",
                Target = "losses/RPT_NoReport.aspx",
            },
            new
            {
                Section = "All Vehicle Losses Reports",
                Sequence = "4",
                Report = "Losses where Reports from Departments were supplied",
                Target = "losses/RPT_WithReport.aspx",
            },
            new
            {
                Section = "All Vehicle Losses Reports",
                Sequence = "5",
                Report = "Losses Report for a Site, for a Period, for VIP/GG, for Hire Type",
                Target = "losses/RPT_dept_periodVIP_main_losses.aspx",
            },
            new
            {
                Section = "Navigation",
                Sequence = "R",
                Report = "Return To Main Page",
                Target = "FISReports/FIS_Report.aspx",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Losses Reports",
                "Losses/losses.aspx",
                false,
                null,
                rows,
                Column("Section", row => row.Section),
                Column("Sequence", row => row.Sequence),
                Column("Report", row => row.Report),
                Column("Target", row => row.Target)
            )
        );
    }

    private async Task<LegacyReportResultDto> BuildLossesOneVehicleAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var vmfCode = GetInt(filters, "vmf");
        if (!vmfCode.HasValue)
        {
            var vehicleNumber =
                GetString(filters, "vehicle_number") ?? GetString(filters, "search");
            vmfCode = await ResolveVehicleVmfCodeAsync(
                vehicleNumber,
                GetString(filters, "search_mode"),
                cancellationToken
            );
        }

        var lossColumns = await GetReportTableColumnsAsync("losses", cancellationToken);
        var activePredicate = GetLossesActivePredicate(lossColumns);
        var predicates = new List<string> { activePredicate };
        var parameters = new List<ReportParameter>();
        if (vmfCode.HasValue)
        {
            predicates.Add("l.[vmf_code] = @vmfCode");
            parameters.Add(new ReportParameter("@vmfCode", DbType.Int32, vmfCode.Value));
        }
        else
        {
            predicates.Add("1 = 0");
        }

        var sql = $"""
            SELECT
                l.[loss_date] AS [Loss Date],
                loc.[description] AS [Garage],
                l.[loss_reference] AS [Loss Reference],
                l.[case_number] AS [Case Number],
                s.[description] AS [Site],
                l.[sapd] AS [SAPD],
                lt.[loss_description] AS [Loss Type],
                l.[loss_amount] AS [Loss Amount],
                l.[remarks] AS [Remarks]
            FROM [dbo].[losses] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[location] AS loc ON loc.[location_code] = v.[location_code]
            LEFT JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
            LEFT JOIN [dbo].[Loss_type] AS lt ON lt.[loss_type_code] = l.[loss_type_code]
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY l.[loss_date], l.[loss_code]
            """;

        return await ExecuteLossReportQueryAsync(
            "losses-one-vehicle",
            "Losses for One Vehicle",
            "losses/RPT_loss_per_vehicle.htm",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLossesAllAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var lossColumns = await GetReportTableColumnsAsync("losses", cancellationToken);
        var predicates = new List<string> { GetLossesActivePredicate(lossColumns) };
        var parameters = new List<ReportParameter>();
        AddLossesCommonFilters(filters, predicates, parameters);

        var statusExpression = lossColumns.Contains("loss_status")
            ? "COALESCE(NULLIF(l.[loss_status], ''), CASE WHEN l.[report_from_dept] = 1 THEN 'Outstanding' ELSE 'Reported' END)"
            : "CASE WHEN l.[report_from_dept] = 1 THEN 'Outstanding' ELSE 'Reported' END";
        var sql = $"""
            SELECT
                l.[loss_reference] AS [Reference],
                l.[loss_date] AS [Loss Date],
                v.[fleet_number] AS [GG Number],
                vt.[type_description] AS [Hire Type],
                lt.[loss_description] AS [Loss Type],
                COALESCE(d.[description], s.[description]) AS [Department],
                {statusExpression} AS [Status]
            FROM [dbo].[losses] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[type] AS vt ON vt.[type_code] = v.[type_code]
            LEFT JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
            LEFT JOIN [dbo].[department] AS d ON d.[department_code] = s.[Depatrment_code]
            LEFT JOIN [dbo].[Loss_type] AS lt ON lt.[loss_type_code] = l.[loss_type_code]
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY lt.[loss_description], l.[loss_date], v.[fleet_number], l.[loss_code]
            """;

        return await ExecuteLossReportQueryAsync(
            "losses",
            "All Losses Report",
            "losses/RPT_All_Losses_menu.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLossesReportStatusAsync(
        IDictionary<string, string?> filters,
        bool outstanding,
        CancellationToken cancellationToken
    )
    {
        var lossColumns = await GetReportTableColumnsAsync("losses", cancellationToken);
        var predicates = new List<string>
        {
            GetLossesActivePredicate(lossColumns),
            outstanding ? "l.[report_from_dept] = 1" : "l.[report_from_dept] <> 1",
        };
        var parameters = new List<ReportParameter>();
        AddLossesCommonFilters(filters, predicates, parameters);

        var sql = $"""
            SELECT
                v.[fleet_number] AS [GG Number],
                l.[loss_amount] AS [Loss Amount],
                loc.[description] AS [Garage],
                l.[loss_date] AS [Loss Date],
                l.[loss_reference] AS [Reference],
                l.[case_number] AS [Case Number],
                s.[description] AS [Site],
                lt.[loss_description] AS [Loss Type],
                s.[Department_number] AS [Department],
                l.[Call_Refer] AS [Called Refer]
            FROM [dbo].[losses] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[location] AS loc ON loc.[location_code] = v.[location_code]
            LEFT JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
            LEFT JOIN [dbo].[Loss_type] AS lt ON lt.[loss_type_code] = l.[loss_type_code]
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY v.[fleet_number], l.[loss_date], l.[loss_code]
            """;

        return await ExecuteLossReportQueryAsync(
            outstanding ? "losses-outstanding-report" : "losses-with-report",
            outstanding ? "Losses Without Department Reports" : "Losses With Department Reports",
            outstanding ? "losses/RPT_NoReport.aspx" : "losses/RPT_WithReport.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLossesDepartmentPeriodAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var lossColumns = await GetReportTableColumnsAsync("losses", cancellationToken);
        var predicates = new List<string> { GetLossesActivePredicate(lossColumns) };
        var parameters = new List<ReportParameter>();
        AddLossesCommonFilters(filters, predicates, parameters, includeDates: false);

        var from = GetDate(filters, "begin_date") ?? GetDate(filters, "from");
        var to = GetDate(filters, "end_date") ?? GetDate(filters, "to");
        if (from.HasValue)
        {
            predicates.Add("l.[loss_date] >= @lossFrom");
            parameters.Add(new ReportParameter("@lossFrom", DbType.DateTime, from.Value.Date));
        }
        if (to.HasValue)
        {
            predicates.Add("l.[loss_date] < @lossToExclusive");
            parameters.Add(
                new ReportParameter("@lossToExclusive", DbType.DateTime, to.Value.Date.AddDays(1))
            );
        }

        var hireType = GetString(filters, "hire_type")?.Trim();
        if (!string.IsNullOrWhiteSpace(hireType))
        {
            var typeCode = hireType.ToUpperInvariant() switch
            {
                "VIP" => 1,
                "GG" => 2,
                "PERMANENT" => 3,
                _ => 0,
            };
            if (typeCode > 0)
            {
                predicates.Add("v.[type_code] = @hireTypeCode");
                parameters.Add(new ReportParameter("@hireTypeCode", DbType.Int16, typeCode));
            }
        }

        var sql = $"""
            SELECT
                v.[registration_number] AS [Prov Reg Number],
                v.[fleet_number] AS [GG Number],
                l.[loss_date] AS [Loss Date],
                s.[Department_number] AS [Dept Code],
                s.[description] AS [Site],
                vt.[type_description] AS [Hire Type],
                lt.[loss_description] AS [Loss Type],
                l.[dept_contact] AS [Department Contact],
                l.[loss_amount] AS [Loss Amount]
            FROM [dbo].[losses] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
            LEFT JOIN [dbo].[type] AS vt ON vt.[type_code] = v.[type_code]
            LEFT JOIN [dbo].[Loss_type] AS lt ON lt.[loss_type_code] = l.[loss_type_code]
            WHERE {string.Join(" AND ", predicates)}
            ORDER BY s.[Department_number], v.[fleet_number], l.[loss_date], l.[loss_code]
            """;

        return await ExecuteLossReportQueryAsync(
            "losses-site-period-vip-gg-hire",
            "Losses Report by Department Period",
            "losses/RPT_dept_periodVIP_main_losses.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private static string GetLossesActivePredicate(IReadOnlySet<string> lossColumns) =>
        lossColumns.Contains("is_deleted") ? "l.[is_deleted] = 0" : "1 = 1";

    private static void AddLossesCommonFilters(
        IDictionary<string, string?> filters,
        ICollection<string> predicates,
        ICollection<ReportParameter> parameters,
        bool includeDates = true
    )
    {
        var lossTypeCode = GetShort(filters, "loss_type_code");
        if (lossTypeCode.HasValue)
        {
            predicates.Add("l.[loss_type_code] = @lossTypeCode");
            parameters.Add(new ReportParameter("@lossTypeCode", DbType.Int16, lossTypeCode.Value));
        }

        var department = GetString(filters, "department");
        if (!string.IsNullOrWhiteSpace(department))
        {
            predicates.Add(
                "(s.[Department_number] LIKE @department OR CONVERT(varchar(20), s.[Depatrment_code]) = @department)"
            );
            parameters.Add(
                new ReportParameter("@department", DbType.String, $"%{department.Trim()}%")
            );
        }

        if (!includeDates)
        {
            return;
        }

        var from = GetDate(filters, "begin_date") ?? GetDate(filters, "from");
        var to = GetDate(filters, "end_date") ?? GetDate(filters, "to");
        if (from.HasValue)
        {
            predicates.Add("l.[loss_date] >= @commonLossFrom");
            parameters.Add(
                new ReportParameter("@commonLossFrom", DbType.DateTime, from.Value.Date)
            );
        }
        if (to.HasValue)
        {
            predicates.Add("l.[loss_date] < @commonLossToExclusive");
            parameters.Add(
                new ReportParameter(
                    "@commonLossToExclusive",
                    DbType.DateTime,
                    to.Value.Date.AddDays(1)
                )
            );
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is assembled only from fixed loss report templates; all filter values are parameters."
    )]
    private Task<LegacyReportResultDto> ExecuteLossReportQueryAsync(
        string reportKey,
        string title,
        string legacyTarget,
        string sql,
        IReadOnlyList<ReportParameter> parameters,
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) =>
        ExecutePagedRawReportQueryAsync(
            reportKey,
            title,
            legacyTarget,
            sql,
            parameters,
            filters,
            cancellationToken
        );

    private async Task<LegacyReportResultDto> BuildHighDistanceAllAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        return await BuildHighDistanceAsync(
            filters,
            null,
            "All vehicles with high distances (All Departments)",
            "ShowReport.aspx?Item=KiloAudit",
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildHighDistanceDeptAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var departmentCode = GetShort(filters, "dept") ?? GetShort(filters, "DepartmentID");
        return await BuildHighDistanceAsync(
            filters,
            departmentCode,
            "All vehicles with high distances (Department)",
            "ShowReport.aspx?Item=KiloAudit&DepartmentID=...",
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildHighDistanceAsync(
        IDictionary<string, string?> filters,
        short? departmentCode,
        string title,
        string legacyTarget,
        CancellationToken cancellationToken
    )
    {
        var threshold = GetInt(filters, "threshold") ?? 5000;

        var activeContracts = _context
            .Contracts.AsNoTracking()
            .Where(contract => contract.still_current == "Y");
        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into vehicleStatuses
            from status in vehicleStatuses.DefaultIfEmpty()
            join contract in activeContracts
                on vehicle.vmf_code equals contract.vmf_code
                into vehicleContracts
            from contract in vehicleContracts.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on contract.site_code equals site.Site_code
                into contractSites
            from site in contractSites.DefaultIfEmpty()
            where
                ((vehicle.highest_km ?? 0) >= threshold || vehicle.current_odo >= threshold)
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
                contract.site_code,
            };

        if (departmentCode.HasValue)
        {
            query = query.Where(row => row.DepartmentCode == departmentCode.Value);
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderByDescending(row => row.HighestKm ?? row.CurrentOdo)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            title,
            legacyTarget,
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Current ODO", row => row.CurrentOdo),
            Column("Highest KM", row => row.HighestKm),
            Column("Status", row => row.Status),
            Column("Department Code", row => row.DepartmentCode),
            Column("Site Code", row => row.site_code),
            Column("Site", row => row.Site)
        );
    }

    private async Task<LegacyReportResultDto> BuildIncorrectQuantitiesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var query = _context
            .Vehicles.AsNoTracking()
            .Where(vehicle =>
                (
                    vehicle.take_on_odo < 0
                    || vehicle.current_odo < 0
                    || vehicle.current_odo < vehicle.take_on_odo
                    || (
                        vehicle.average_consumption.HasValue
                        && vehicle.average_consumption.Value < 0
                    )
                    || (vehicle.highest_km.HasValue && vehicle.highest_km.Value < 0)
                    || (vehicle.km_3month_average.HasValue && vehicle.km_3month_average.Value < 0)
                )
            )
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
                Reason = vehicle.current_odo < vehicle.take_on_odo
                    ? "Current odometer is less than take-on odometer."
                : vehicle.take_on_odo < 0 ? "Take-on odometer is negative."
                : vehicle.current_odo < 0 ? "Current odometer is negative."
                : (vehicle.average_consumption.HasValue && vehicle.average_consumption.Value < 0)
                    ? "Average consumption is negative."
                : (vehicle.highest_km.HasValue && vehicle.highest_km.Value < 0)
                    ? "Highest KM is negative."
                : (vehicle.km_3month_average.HasValue && vehicle.km_3month_average.Value < 0)
                    ? "3-month KM average is negative."
                : "Quantity anomaly detected.",
            });

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.fleet_number)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Report to show incorrect calculated quantities",
            "Finance/GeneratedReports.aspx?key=9.3%20Report%20to%20show%20incorrect%20calculated%20quantities",
            true,
            "Legacy generated report output is approximated from vehicle odometer and quantity-related fields in vehicle_master.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Take On ODO", row => row.take_on_odo),
            Column("Current ODO", row => row.current_odo),
            Column("Highest KM", row => row.highest_km),
            Column("Average Consumption", row => row.average_consumption),
            Column("3-Month KM Average", row => row.km_3month_average),
            Column("Reason", row => row.Reason)
        );
    }

    private async Task<LegacyReportResultDto> BuildLicencesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search") ?? GetString(filters, "identifier");
        var mode = (GetString(filters, "mode") ?? "GG").Trim().ToUpperInvariant();
        var statusFilter = (GetString(filters, "status") ?? "all").Trim().ToLowerInvariant();
        var locationFilter = (GetString(filters, "location") ?? "all").Trim().ToLowerInvariant();
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var department = GetString(filters, "department_code") ?? GetString(filters, "department");
        var month = GetInt(filters, "month");
        var year = GetInt(filters, "year");

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into statuses
            from status in statuses.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking()
                on vehicle.model_code equals model.model_code
                into models
            from model in models.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking()
                on vehicle.type_code equals type.type_code
                into types
            from type in types.DefaultIfEmpty()
            join garageSite in _context.Sites.AsNoTracking()
                on vehicle.location_code equals garageSite.Site_code
                into garageSites
            from garageSite in garageSites.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on vehicle.Licence_receiver_site equals site.Site_code
                into sites
            from site in sites.DefaultIfEmpty()
            join fee in _context.LicenseFees.AsNoTracking()
                on model.licence_fee_code equals fee.licence_fee_code
                into fees
            from fee in fees.DefaultIfEmpty()
            where true
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
                LicenceFee = fee != null ? fee.licence_fee : null,
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

        if (!string.IsNullOrWhiteSpace(department))
        {
            var departmentValue = department.Trim();
            query = query.Where(row => row.Department_number == departmentValue);
        }

        if (from.HasValue)
        {
            query = query.Where(row =>
                row.licence_due_date.HasValue && row.licence_due_date.Value.Date >= from.Value
            );
        }

        if (to.HasValue)
        {
            query = query.Where(row =>
                row.licence_due_date.HasValue && row.licence_due_date.Value.Date <= to.Value
            );
        }

        if (month is >= 1 and <= 12)
        {
            query = query.Where(row =>
                row.licence_due_date.HasValue && row.licence_due_date.Value.Month == month.Value
            );
        }

        if (year is > 0)
        {
            query = query.Where(row =>
                row.licence_due_date.HasValue && row.licence_due_date.Value.Year == year.Value
            );
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = mode switch
            {
                "GP" => query.Where(row =>
                    row.registration_number != null && row.registration_number.Contains(term)
                ),
                "REGISTER" => query.Where(row =>
                    row.lic_register_number != null && row.lic_register_number.Contains(term)
                ),
                "ENGINE" => query.Where(row =>
                    row.engine_number_1 != null && row.engine_number_1.Contains(term)
                ),
                "CHASSIS" => query.Where(row =>
                    row.chassis_number != null && row.chassis_number.Contains(term)
                ),
                "VIN" => query.Where(row =>
                    row.chassis_number != null && row.chassis_number.Contains(term)
                ),
                _ => query.Where(row =>
                    row.fleet_number != null && row.fleet_number.Contains(term)
                ),
            };
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.fleet_number)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Licence Reports",
            "License/RPTLicence.aspx",
            true,
            "Legacy licence module includes many report branches (GG/GP/register/engine/chassis/site/date). This approximation keeps the core licence record fields and lookup modes in one modern dynamic grid.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("VMF Code", row => row.vmf_code)
        );
    }

    private Task<LegacyReportResultDto> BuildManagementAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Sequence = "1",
                Report = "GGMT Management Reports",
                Target = "Management_Reports/Management2.aspx",
                Availability = "Available",
            },
            new
            {
                Sequence = "2",
                Report = "Report On Incorrect Captured Data",
                Target = "Management_Reports/incorrect.aspx",
                Availability = "Available",
            },
            new
            {
                Sequence = "3",
                Report = "FIS Site Management Information",
                Target = "Management_Reports/System_Info/SysInfo_menu.aspx",
                Availability = "Available",
            },
            new
            {
                Sequence = "6",
                Report = "Return To Main Page",
                Target = "FISReports/FIS_Report.aspx",
                Availability = "Available",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Management Reports",
                "Management_Reports/Management.aspx",
                false,
                null,
                rows,
                Column("Sequence", row => row.Sequence),
                Column("Report", row => row.Report),
                Column("Target", row => row.Target),
                Column("Availability", row => row.Availability)
            )
        );
    }

    private Task<LegacyReportResultDto> BuildManualsAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Sequence = "1",
                Manual = "General Information",
                Target = "Manuals/RPT_underdev.aspx",
                Availability = "Under Development",
            },
            new
            {
                Sequence = "2",
                Manual = "Accident Manual",
                Target = "Accident/Doc/Doc_Accidents.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "3",
                Manual = "Auction Manual",
                Target = "Auction/Doc/Doc_Auctions.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "4",
                Manual = "Call Centre Manual",
                Target = "CallCentre/Doc/Doc_CallCentre.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "5",
                Manual = "Contract Manual",
                Target = "Contracts/Docs/User Documentation for Contracts Module.html",
                Availability = "Available",
            },
            new
            {
                Sequence = "6",
                Manual = "Electronic Log Sheet and Trip Authority Training Manual",
                Target = "Docs/Electronic Log Sheet and Trip Authority Training Manual.doc",
                Availability = "Available",
            },
            new
            {
                Sequence = "7",
                Manual = "Financial Manual",
                Target = "Manuals/RPT_underdev.aspx",
                Availability = "Under Development",
            },
            new
            {
                Sequence = "8",
                Manual = "Fines Manual",
                Target = "Fines/Doc/Doc_Fines.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "9",
                Manual = "Fuelcard Manual",
                Target = "fuelcard/Doc/Doc_Fuelcards.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "10",
                Manual = "Licence Manual",
                Target = "License/Doc/DOC_LICENCE.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "11",
                Manual = "Logbook Manual",
                Target = "Logbook/Doc/Doc_Logbooks.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "12",
                Manual = "Logsheet Manual",
                Target = "Logs/Doc/Doc_Logsheets.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "13",
                Manual = "Losses Manual",
                Target = "Losses/Doc/Doc_losses.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "14",
                Manual = "Private Hire Manual",
                Target = "Private_Hire/Doc/Doc_PrivateHire.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "15",
                Manual = "Reports Manual",
                Target = "/Doc/Doc_Reports.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "16",
                Manual = "Taxis Manual",
                Target = "Taxis/Doc/Doc_taxis.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "17",
                Manual = "Trip Authority Manual",
                Target = "Manuals/RPT_underdev.aspx",
                Availability = "Under Development",
            },
            new
            {
                Sequence = "18",
                Manual = "Updating Trip Authorities Manual",
                Target = "Docs/doc/Updating Trip Authorities Manual2.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "19",
                Manual = "Troubleshoot Manual",
                Target = "TS_Log/Doc/Doc_Troubleshoot.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "20",
                Manual = "User Admin Manual",
                Target = "/Doc/Doc_UserAdmin.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "21",
                Manual = "Validation Data Manual",
                Target = "Validation/Doc/Doc_ValidationData.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "22",
                Manual = "Vehicle Manual",
                Target = "Manuals/RPT_underdev.aspx",
                Availability = "Under Development",
            },
            new
            {
                Sequence = "23",
                Manual = "Workshop Manual",
                Target = "Workshop/Doc/Doc_Workshop.htm",
                Availability = "Available",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Manuals Menu",
                "Manuals/RPTmanuals.aspx",
                false,
                null,
                rows,
                Column("Sequence", row => row.Sequence),
                Column("Manual", row => row.Manual),
                Column("Target", row => row.Target),
                Column("Availability", row => row.Availability)
            )
        );
    }

    private Task<LegacyReportResultDto> BuildPreviousFinYearMenuAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Sequence = "i",
                ReportName = "Manual Logsheet Kilos captured in current Fin Year",
                ReportKey = "previous-fin-year-manual-logs",
                LegacyItem = "PreviousFinYearManualLogsCapturedInCurrentFinYear",
            },
            new
            {
                Sequence = "ii",
                ReportName = "VIP & Taxi requisitions captured in current Fin Year",
                ReportKey = "previous-fin-year-vip-taxi",
                LegacyItem = "PreviousFinYearKiloLogsCapturedInCurrentFinYear",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Previous Fin Year Reports",
                "Finance/PreviousFinYear.aspx",
                false,
                null,
                rows,
                Column("Sequence", row => row.Sequence),
                Column("Report Name", row => row.ReportName),
                Column("Report Key", row => row.ReportKey),
                Column("Legacy Item", row => row.LegacyItem)
            )
        );
    }

    private async Task<LegacyReportResultDto> BuildPreviousFinYearManualLogsAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var currentFinancialYear = GetFinancialYearKey(DateTime.Today);
        var columns = await GetReportTableColumnsAsync("Logsheets", cancellationToken);
        if (!columns.Contains("date_created"))
        {
            return CreateDatabasePagedResult(
                "Previous Fin Year Manual Logsheet Kilos Captured in Current Fin Year",
                "ShowReport.aspx?Item=PreviousFinYearManualLogsCapturedInCurrentFinYear",
                true,
                "The client-era Logsheets table does not provide a capture timestamp.",
                Array.Empty<object>(),
                0,
                GetPageWindow(GetFallbackPagination(filters), 0),
                Column("Log Code", row => row)
            );
        }

        var activePredicate = columns.Contains("is_deleted")
            ? "COALESCE(l.[is_deleted], 0) = 0"
            : "1 = 1";
        var financialYearSql = "CASE WHEN MONTH({0}) >= 4 THEN YEAR({0}) ELSE YEAR({0}) - 1 END";
        var sql = $"""
            SELECT
                l.[log_code] AS [Log Code],
                l.[vmf_code] AS [VMF Code],
                v.[fleet_number] AS [GG Number],
                v.[registration_number] AS [GP Number],
                l.[month] AS [Logsheet Month],
                l.[date_created] AS [Captured Date],
                l.[rek_num] AS [Requisition Number],
                l.[start_odo] AS [Start ODO],
                l.[end_odo] AS [End ODO],
                l.[site_code] AS [Site Code],
                s.[description] AS [Site]
            FROM [dbo].[Logsheets] AS l
            LEFT JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
            WHERE {activePredicate}
              AND {string.Format(
                CultureInfo.InvariantCulture,
                financialYearSql,
                "l.[date_created]"
            )} = @currentFinancialYear
              AND {string.Format(
                CultureInfo.InvariantCulture,
                financialYearSql,
                "l.[month]"
            )} < @currentFinancialYear
            ORDER BY l.[date_created] DESC, l.[log_code] DESC
            """;
        return await ExecutePagedRawReportQueryAsync(
            "previous-fin-year-manual-logs",
            "Previous Fin Year Manual Logsheet Kilos Captured in Current Fin Year",
            "ShowReport.aspx?Item=PreviousFinYearManualLogsCapturedInCurrentFinYear",
            sql,
            [new ReportParameter("@currentFinancialYear", DbType.Int32, currentFinancialYear)],
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildPreviousFinYearVipTaxiAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _taxiRepository.GetReportPageAsync(
                new TaxiReportPageQuery(
                    TaxiReportKind.PreviousFinYearVipTaxi,
                    pagination.Page,
                    pagination.PageSize,
                    AllowedSiteCodes: GetReportSiteScope(filters)
                )
            );
            var pagedRows = page.Items.Select(taxi => new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.official,
                taxi.rank,
                taxi.vmf_code,
                taxi.date_required,
                CapturedDate = taxi.date_created == default
                    ? taxi.request_date ?? taxi.date_required
                    : taxi.date_created,
                taxi.contractor_id,
                DepartmentCode = taxi.Department?.department_code,
                Department = taxi.Department?.description,
                SiteCode = taxi.Site?.Site_code,
                Site = taxi.Site?.description,
            });
            return CreateDatabasePagedResult(
                "Previous Fin Year VIP & Taxi Requisitions Captured in Current Fin Year",
                "ShowReport.aspx?Item=PreviousFinYearKiloLogsCapturedInCurrentFinYear",
                false,
                null,
                pagedRows,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("Request ID", row => row.request_id),
                Column("Requisition Number", row => row.rek_num),
                Column("Official", row => row.official),
                Column("Rank", row => row.rank),
                Column("Vehicle", row => row.vmf_code),
                Column("Date Required", row => row.date_required),
                Column("Captured Date", row => row.CapturedDate),
                Column("Contractor ID", row => row.contractor_id),
                Column("Department Code", row => row.DepartmentCode),
                Column("Department", row => row.Department),
                Column("Site Code", row => row.SiteCode),
                Column("Site", row => row.Site)
            );
        }

        var currentFinancialYear = GetFinancialYearKey(DateTime.Today);

        var taxis = await _taxiRepository.GetAllAsync(GetReportSiteScope(filters));
        cancellationToken.ThrowIfCancellationRequested();
        var rows = taxis
            .Select(taxi => new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.official,
                taxi.rank,
                taxi.vmf_code,
                taxi.date_required,
                CapturedDate = taxi.date_created == default
                    ? taxi.request_date ?? taxi.date_required
                    : taxi.date_created,
                taxi.contractor_id,
                DepartmentCode = taxi.Department?.department_code,
                Department = taxi.Department?.description,
                SiteCode = taxi.Site?.Site_code,
                Site = taxi.Site?.description,
            })
            .Where(taxi =>
                GetFinancialYearKey(taxi.CapturedDate) == currentFinancialYear
                && GetFinancialYearKey(taxi.date_required) < currentFinancialYear
            )
            .OrderByDescending(taxi => taxi.CapturedDate)
            .ThenByDescending(taxi => taxi.request_id)
            .Take(5000)
            .ToList();

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
            Column("Captured Date", row => row.CapturedDate),
            Column("Contractor ID", row => row.contractor_id),
            Column("Department Code", row => row.DepartmentCode),
            Column("Department", row => row.Department),
            Column("Site Code", row => row.SiteCode),
            Column("Site", row => row.Site)
        );
    }

    private async Task<LegacyReportResultDto> BuildRegistrationCertificatesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";
        var showSingleVehicle = !string.IsNullOrWhiteSpace(search);

        var query =
            from scanDoc in _context.ScanDocs.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking()
                on scanDoc.vmf_code equals vehicle.vmf_code
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                scanDoc.period_begin,
                scanDoc.period_end,
                DateUploaded = scanDoc.period_end ?? scanDoc.period_begin,
                scanDoc.image,
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(
                search,
                mode,
                cancellationToken
            );
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        query = query.Select(row => new
        {
            row.vmf_code,
            row.fleet_number,
            row.registration_number,
            row.period_begin,
            row.period_end,
            row.DateUploaded,
            row.image,
        });

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.fleet_number)
                    .ThenBy(row => row.period_begin)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        if (showSingleVehicle)
        {
            return CreateDatabasePagedResult(
                $"Registration Certificate for {search!.Trim().ToUpperInvariant()}",
                "ScanDocs/ListOne2.aspx",
                false,
                null,
                page.Rows,
                page.TotalCount,
                page.PageWindow,
                Column("From", row => row.period_begin),
                Column("To", row => row.period_end),
                Column("Registration Certificate", row => row.image)
            );
        }

        return CreateDatabasePagedResult(
            "Registration Certificates for All Vehicles",
            "ScanDocs/RPT_ListAll2.aspx",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("Fleet Number", row => row.fleet_number),
            Column("Registration Number", row => row.registration_number),
            Column("From", row => row.period_begin),
            Column("To", row => row.period_end),
            Column("Date Uploaded", row => row.DateUploaded),
            Column("Registration Certificate", row => row.image)
        );
    }

    private async Task<LegacyReportResultDto> BuildTariffsClass2007Async(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var query = _context
            .Tariffs.AsNoTracking()
            .Where(tariff => (tariff.year_manufactured ?? 0) <= 2007)
            .AsQueryable();
        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.class_code)
                    .ThenBy(row => row.year_manufactured)
                    .ThenBy(row => row.tariff_code),
            filters,
            cancellationToken,
            Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000)
        );

        return CreateDatabasePagedResult(
            "Published Tariffs (2007 and Earlier)",
            "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("Tariff Code", row => row.tariff_code),
            Column("Class Code", row => row.class_code),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Monthly Fixed Amount", row => row.monthly_fixed_amount),
            Column("Monthly ODO Amount", row => row.monthly_odo_amount),
            Column("Daily Fixed Amount", row => row.daily_fixed_amount),
            Column("Hourly Fixed Amount", row => row.hourly_fixed_amount),
            Column("Fuel Kilo Tariff", row => row.fuel_kilo_tariff),
            Column("Effective Start Date", row => row.effective_start_date),
            Column("Effective End Date", row => row.effective_end_date)
        );
    }

    private async Task<LegacyReportResultDto> BuildTariffsFinYearAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var from = GetDate(filters, "from")?.Date;
        var to = GetDate(filters, "to")?.Date;
        var query = _context.Tariffs.AsNoTracking();
        if (from.HasValue)
        {
            query = query.Where(tariff => tariff.effective_start_date.Date >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(tariff => tariff.effective_start_date.Date <= to.Value);
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderByDescending(row => row.effective_start_date)
                    .ThenBy(row => row.class_code)
                    .ThenBy(row => row.tariff_code),
            filters,
            cancellationToken,
            Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000)
        );

        return CreateDatabasePagedResult(
            "Published Tariffs by Financial Year",
            "SelectFinancialYear.aspx + ShowReport.aspx?Item=Tariffs",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("Tariff Code", row => row.tariff_code),
            Column("Class Code", row => row.class_code),
            Column("Year Manufactured", row => row.year_manufactured),
            Column("Monthly Fixed Amount", row => row.monthly_fixed_amount),
            Column("Monthly ODO Amount", row => row.monthly_odo_amount),
            Column("Fuel Kilo Tariff", row => row.fuel_kilo_tariff),
            Column("Effective Start Date", row => row.effective_start_date),
            Column("Effective End Date", row => row.effective_end_date),
            Column("Approval Status", row => row.tariff_approval_status)
        );
    }

    private async Task<LegacyReportResultDto> BuildTariffsPerVehicleAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var query =
            from tariff in _context.VehicleTariffs.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking()
                on tariff.vmf_code equals vehicle.vmf_code
                into tariffVehicles
            from vehicle in tariffVehicles.DefaultIfEmpty()
            where vehicle != null && (vehicle.year_manufactured ?? 0) >= 2008
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
                tariff.comment,
            };

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.fleet_number)
                    .ThenBy(row => row.registration_number)
                    .ThenByDescending(row => row.start_date)
                    .ThenBy(row => row.vehicle_tariff_code),
            filters,
            cancellationToken,
            Math.Clamp(GetInt(filters, "max") ?? 300, 1, 5000)
        );

        return CreateDatabasePagedResult(
            "Tariffs per Vehicle",
            "ShowReport.aspx?Item=TariffsPerVehicle",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("Comment", row => row.comment)
        );
    }

    private Task<LegacyReportResultDto> BuildTaxisMenuAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Sequence = "1",
                ReportName = "Future booking made for my department",
                LegacyTarget = "Taxis/RPT_My_reqs.aspx",
                ReportKey = "taxis-future-bookings",
            },
            new
            {
                Sequence = "2",
                ReportName = "History bookings for period",
                LegacyTarget = "Taxis/RPT_My_reqs2.aspx",
                ReportKey = "taxis-history-bookings",
            },
            new
            {
                Sequence = "3",
                ReportName = "List of Requisition Numbers for a period",
                LegacyTarget = "Taxis/RPT_reqno_taxi_main.aspx",
                ReportKey = "taxis-requisition-list",
            },
            new
            {
                Sequence = "4",
                ReportName = "Taxis Per Hire Company",
                LegacyTarget = "Taxis/RPT_taxis_per_company1_c.aspx",
                ReportKey = "taxis-per-hire-company",
            },
            new
            {
                Sequence = "5",
                ReportName = "List Of all Taxis in service in various departments",
                LegacyTarget = "Taxis/RPT_list_of_taxis_inservice_per_department.aspx",
                ReportKey = "taxis-list-inservice-per-department",
            },
            new
            {
                Sequence = "6",
                ReportName = "List Of all Taxis in various departments",
                LegacyTarget = "Taxis/RPT_list_of_taxis_per_department.aspx",
                ReportKey = "taxis-list-per-department",
            },
            new
            {
                Sequence = "7",
                ReportName = "Reprint A Requisition",
                LegacyTarget = "Taxis/Report_Request_GGVIP_reprint_1_2.aspx",
                ReportKey = "taxis-reprint-requisition",
            },
            new
            {
                Sequence = "8",
                ReportName = "Reprint A Taxi Log",
                LegacyTarget = "Taxis/Report_Reprint_Taxi_Log_1.aspx",
                ReportKey = "taxis-reprint-log",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Taxi Reports Menu",
                "Taxis/RPTtaxis.aspx",
                false,
                null,
                rows,
                Column("Sequence", row => row.Sequence),
                Column("Report Name", row => row.ReportName),
                Column("Legacy Target", row => row.LegacyTarget),
                Column("Report Key", row => row.ReportKey)
            )
        );
    }

    private async Task<LegacyReportResultDto> BuildTaxisListPerDepartmentAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _taxiRepository.GetReportPageAsync(
                new TaxiReportPageQuery(
                    TaxiReportKind.ListPerDepartment,
                    pagination.Page,
                    pagination.PageSize,
                    search,
                    AllowedSiteCodes: GetReportSiteScope(filters)
                )
            );
            var pagedRows = page.Items.Select(
                (taxi, index) =>
                    new
                    {
                        Number = ((page.Page - 1) * page.PageSize) + index + 1,
                        taxi.rek_num,
                        taxi.department_code,
                        Department = taxi.Department?.description,
                        taxi.vmf_code,
                        taxi.request_id,
                    }
            );
            return CreateDatabasePagedResult(
                "Report On All Taxis in various Departments",
                "Taxis/RPT_list_of_taxis_per_department.aspx",
                true,
                "Legacy report joins taxis via vehicle/logsheet to derive department text. This approximation uses taxis + department mappings and preserves requisition + department output.",
                pagedRows,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("No.", row => row.Number),
                Column("Requisition Number", row => row.rek_num),
                Column("Department Code", row => row.department_code),
                Column("Department", row => row.Department),
                Column("VMF Code", row => row.vmf_code),
                Column("Request ID", row => row.request_id)
            );
        }

        var taxis = await _taxiRepository.GetAllAsync(GetReportSiteScope(filters));
        cancellationToken.ThrowIfCancellationRequested();
        var query = taxis.Select(taxi => new
        {
            taxi.request_id,
            taxi.rek_num,
            taxi.department_code,
            Department = taxi.Department?.description,
            taxi.vmf_code,
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                (
                    row.rek_num != null
                    && row.rek_num.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.Department != null
                    && row.Department.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.vmf_code != null
                    && row.vmf_code.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || row.request_id.ToString(CultureInfo.InvariantCulture)
                    .Contains(term, StringComparison.OrdinalIgnoreCase)
            );
        }

        var rows = query
            .OrderBy(row => row.Department)
            .ThenBy(row => row.rek_num)
            .Select(row => new
            {
                row.request_id,
                row.rek_num,
                row.department_code,
                row.Department,
                row.vmf_code,
            })
            .Distinct()
            .Take(5000)
            .ToList();

        var numberedRows = rows.Select(
                (row, index) =>
                    new
                    {
                        Number = index + 1,
                        row.rek_num,
                        row.department_code,
                        row.Department,
                        row.vmf_code,
                        row.request_id,
                    }
            )
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
            Column("Request ID", row => row.request_id)
        );
    }

    private async Task<LegacyReportResultDto> BuildTaxisListInServicePerDepartmentAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _taxiRepository.GetReportPageAsync(
                new TaxiReportPageQuery(
                    TaxiReportKind.ListInServicePerDepartment,
                    pagination.Page,
                    pagination.PageSize,
                    search,
                    AllowedSiteCodes: GetReportSiteScope(filters)
                )
            );
            var pagedRows = page.Items.Select(
                (taxi, index) =>
                    new
                    {
                        Number = ((page.Page - 1) * page.PageSize) + index + 1,
                        taxi.rek_num,
                        taxi.department_code,
                        Department = taxi.Department?.description,
                        taxi.vmf_code,
                        taxi.request_id,
                    }
            );
            return CreateDatabasePagedResult(
                "Report On All Taxis in service in various Departments",
                "Taxis/RPT_list_of_taxis_inservice_per_department.aspx",
                true,
                "Legacy report filters on vehicle_status_code = 1. This approximation applies the same in-service status constraint.",
                pagedRows,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("No.", row => row.Number),
                Column("Requisition Number", row => row.rek_num),
                Column("Department Code", row => row.department_code),
                Column("Department", row => row.Department),
                Column("VMF Code", row => row.vmf_code),
                Column("Request ID", row => row.request_id)
            );
        }

        var taxis = await _taxiRepository.GetAllAsync(GetReportSiteScope(filters));
        var vehicles = await _vehicleRepository.GetAllAsync();
        cancellationToken.ThrowIfCancellationRequested();
        var inServiceVmfCodes = vehicles
            .Where(vehicle => vehicle.vehicle_status_code == 1)
            .Select(vehicle => vehicle.vmf_code.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var query = taxis
            .Where(taxi =>
                taxi.vmf_code is not null && inServiceVmfCodes.Contains(taxi.vmf_code.Trim())
            )
            .Select(taxi => new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.department_code,
                Department = taxi.Department?.description,
                taxi.vmf_code,
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                (
                    row.rek_num != null
                    && row.rek_num.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.Department != null
                    && row.Department.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.vmf_code != null
                    && row.vmf_code.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || row.request_id.ToString(CultureInfo.InvariantCulture)
                    .Contains(term, StringComparison.OrdinalIgnoreCase)
            );
        }

        var rows = query
            .OrderBy(row => row.Department)
            .ThenBy(row => row.rek_num)
            .Distinct()
            .Take(5000)
            .ToList();

        var numberedRows = rows.Select(
                (row, index) =>
                    new
                    {
                        Number = index + 1,
                        row.rek_num,
                        row.department_code,
                        row.Department,
                        row.vmf_code,
                        row.request_id,
                    }
            )
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
            Column("Request ID", row => row.request_id)
        );
    }

    private async Task<LegacyReportResultDto> BuildTaxisFinancialAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _taxiRepository.GetReportPageAsync(
                new TaxiReportPageQuery(
                    TaxiReportKind.Financial,
                    pagination.Page,
                    pagination.PageSize,
                    search,
                    AllowedSiteCodes: GetReportSiteScope(filters)
                )
            );
            var pagedRows = page.Items.Select(taxi => new
            {
                taxi.request_id,
                taxi.rek_num,
                taxi.official,
                taxi.rank,
                taxi.vmf_code,
                Company = taxi.address_1,
                taxi.date_required,
                Department = taxi.Department?.description,
                Site = taxi.Site?.description,
                taxi.contractor_id,
                taxi.flight,
                taxi.address_2,
                taxi.address_3,
            });
            return CreateDatabasePagedResult(
                "Financial Reports: Taxis",
                "Taxis/Taxi_Fin_reports.aspx",
                true,
                "Legacy taxi financial report pages are custom. This dynamic approximation uses the underlying legacy taxi request data.",
                pagedRows,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
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
                Column("Address 3", row => row.address_3)
            );
        }

        var taxis = await _taxiRepository.GetAllAsync(GetReportSiteScope(filters));
        cancellationToken.ThrowIfCancellationRequested();
        var query = taxis.Select(taxi => new
        {
            taxi.request_id,
            taxi.rek_num,
            taxi.official,
            taxi.rank,
            taxi.vmf_code,
            Company = taxi.address_1,
            taxi.date_required,
            Department = taxi.Department?.description,
            Site = taxi.Site?.description,
            taxi.contractor_id,
            taxi.flight,
            taxi.address_2,
            taxi.address_3,
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row =>
                row.request_id.ToString(CultureInfo.InvariantCulture)
                    .Contains(term, StringComparison.OrdinalIgnoreCase)
                || (
                    row.rek_num != null
                    && row.rek_num.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.official != null
                    && row.official.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.vmf_code != null
                    && row.vmf_code.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
                || (
                    row.Company != null
                    && row.Company.Contains(term, StringComparison.OrdinalIgnoreCase)
                )
            );
        }

        var rows = query
            .OrderByDescending(row => row.date_required)
            .ThenByDescending(row => row.request_id)
            .Take(5000)
            .ToList();

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
            Column("Address 3", row => row.address_3)
        );
    }

    private Task<LegacyReportResultDto> BuildTripAuthorityAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.1",
                Report = "Registered Vehicles in Site",
                Target = "FISReports/ShowReport.aspx?Item=AllVehiclesPerSite",
            },
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.2",
                Report = "Registered Vehicles in Department",
                Target = "FISReports/ShowReport.aspx?Item=AllVehiclesPerDepartment",
            },
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.3",
                Report = "Vehicles Per Contract Type Per Site",
                Target = "FISReports/ShowReport.aspx?Item=CountVehiclesPerTypePerSite",
            },
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.4",
                Report = "Vehicles Per Contract Type Per Department",
                Target = "FISReports/ShowReport.aspx?Item=CountVehiclesPerTypePerDepartment",
            },
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.5",
                Report = "Vehicles Per Contract Type Per Department Per Site",
                Target = "FISReports/ShowReport.aspx?Item=CountVehiclesPerTypePerDepartmentPerSite",
            },
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.6",
                Report = "Available Vehicles Per Site",
                Target = "FISReports/ShowReport.aspx?Item=VehiclesPerSite",
            },
            new
            {
                Section = "1) Vehicle Reports",
                Sequence = "1.7",
                Report = "Unavailable Vehicles Per Site",
                Target = "FISReports/ShowReport.aspx?Item=AuthVehiclesPerSite",
            },
            new
            {
                Section = "2) Personnel Reports",
                Sequence = "2.1",
                Report = "Registered Users Per Site",
                Target = "FISReports/ShowReport.aspx?Item=UserDetailsPerSite",
            },
            new
            {
                Section = "2) Personnel Reports",
                Sequence = "2.2",
                Report = "Registered Users Per Department",
                Target = "FISReports/ShowReport.aspx?Item=UserDetailsPerDepartment",
            },
            new
            {
                Section = "2) Personnel Reports",
                Sequence = "2.3",
                Report = "Registered Users (All Departments)",
                Target = "FISReports/ShowReport.aspx?Item=UserDetails",
            },
            new
            {
                Section = "2) Personnel Reports",
                Sequence = "2.4",
                Report = "Trips Issued Per User",
                Target = "FISReports/ShowReport.aspx?Item=TripCountPerUser",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.1",
                Report = "Trips Issued for a Vehicle",
                Target = "FISReports/SelectVehicleTrip.aspx?Item=VehicleTrips",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.2",
                Report = "All Drivers For a Vehicle",
                Target = "FISReports/SelectVehicleTrip.aspx?Item=VehicleDrivers",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.3",
                Report = "Vehicle Utilisation by a Driver",
                Target = "FISReports/SelectDriver.aspx?Item=VehicleUtilisationByDriverID",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.4",
                Report = "Vehicle Utilisation",
                Target = "FISReports/SelectVehicleTrip.aspx?Item=VehicleUtilisation",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.10",
                Report = "Trips open for over 31 days",
                Target = "FISReports/ShowReport.aspx?Item=TripsOpenForOver31Days",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.11",
                Report = "Trip Authorities Exceeding 25000",
                Target = "Finance/KiloMaxPerDep_Site_DateRange.aspx?Mode=DSR&Report=AllRoutesOver25000KM",
            },
            new
            {
                Section = "3) Vehicle Trip Reports",
                Sequence = "3.12",
                Report = "Trip Authorities Exceeding 3500 per Day",
                Target = "Finance/KiloMaxPerDep_Site_DateRange.aspx?Mode=DSR&Report=AllDayTripsOver3500KM",
            },
            new
            {
                Section = "4) Vehicle Kilo Reports",
                Sequence = "4.1",
                Report = "Electronic and Manual Logsheet kilo Report",
                Target = "FISReports/GetReportBetweenStartAndEndDate.aspx?Report=ELSLogReport",
            },
            new
            {
                Section = "5) Driver Information Reports",
                Sequence = "5.1",
                Report = "Driver Information over Financial Year",
                Target = "Trips/DriverInfo.aspx?CallPage=/FISReports/TripReports.aspx",
            },
            new
            {
                Section = "Navigation",
                Sequence = "R",
                Report = "Return To Main Report Page",
                Target = "FISReports/Reports.aspx",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Trip Authority Report",
                "FISReports/TripReports.aspx",
                false,
                null,
                rows,
                Column("Section", row => row.Section),
                Column("Sequence", row => row.Sequence),
                Column("Report", row => row.Report),
                Column("Target", row => row.Target)
            )
        );
    }

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildDriverInformationStoredProcedureParameters(
        IDictionary<string, string?> filters
    ) =>
        [
            new LegacyStoredProcedureParameter(
                "@province_code",
                GetInt(filters, "province") ?? 0,
                DbType.Int32
            ),
            new LegacyStoredProcedureParameter(
                "@department_code",
                GetInt(filters, "dept") ?? 0,
                DbType.Int32
            ),
            new LegacyStoredProcedureParameter(
                "@site_code",
                GetInt(filters, "site") ?? 0,
                DbType.Int32
            ),
            new LegacyStoredProcedureParameter(
                "@FinYear",
                GetFinancialYear(filters).ToString(CultureInfo.InvariantCulture),
                DbType.String
            ),
        ];

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The fallback SQL is assembled from fixed table and column names selected through INFORMATION_SCHEMA; all report values are parameters."
    )]
    private async Task<LegacyReportResultDto> BuildDriverInformationAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var financialYear = GetFinancialYear(filters);
        var fromDate = new DateTime(financialYear, 4, 1);
        var toDate = fromDate.AddYears(1);
        var tripColumns = await GetReportTableColumnsAsync("trip_authorities", cancellationToken);
        var contractColumns = await GetReportTableColumnsAsync("contract", cancellationToken);
        var vehicleColumns = await GetReportTableColumnsAsync("vehicle_master", cancellationToken);
        var driverTable = await ResolveTripDriverReportTableAsync(cancellationToken);

        var requiredColumns = new[] { "trip_authority_code", "contract_code", "issue_date" };
        if (
            requiredColumns.Any(column => !tripColumns.Contains(column))
            || !contractColumns.Contains("contract_code")
            || !contractColumns.Contains("vmf_code")
            || !vehicleColumns.Contains("vmf_code")
        )
        {
            return CreateDynamicResult(
                "Driver Information over a Financial Year",
                "ShowReport.aspx?Item=gFleetVehicleUsers",
                true,
                "The compatible base tables are not available in this database.",
                Array.Empty<DriverInformationFallbackRow>(),
                Column("Trip Authority Code", row => row.TripAuthorityCode),
                Column("Driver Name", row => row.DriverName),
                Column("Driver ID", row => row.DriverId),
                Column("Trip Date", row => row.TripDate),
                Column("Contract Code", row => row.ContractCode),
                Column("GG Number", row => row.FleetNumber),
                Column("GP Number", row => row.RegistrationNumber),
                Column("Site Code", row => row.SiteCode)
            );
        }

        var tripDeletedPredicate = tripColumns.Contains("is_deleted")
            ? " AND COALESCE(t.[is_deleted], 0) = 0"
            : string.Empty;
        var contractDeletedPredicate = contractColumns.Contains("is_deleted")
            ? " AND COALESCE(c.[is_deleted], 0) = 0"
            : string.Empty;
        var driverJoin = string.Empty;
        var driverDeletedPredicate = string.Empty;
        var driverSelection =
            "CAST(NULL AS nvarchar(255)) AS [Driver Name], CAST(NULL AS nvarchar(255)) AS [Driver ID], CAST(NULL AS bit) AS [Driver Primary]";
        if (driverTable is not null)
        {
            driverJoin =
                $"LEFT JOIN [dbo].[{driverTable.Name}] d ON d.[trip_authority_code] = t.[trip_authority_code]";
            driverDeletedPredicate = driverTable.Columns.Contains("is_deleted")
                ? " AND COALESCE(d.[is_deleted], 0) = 0"
                : string.Empty;
            driverSelection =
                $"{OptionalReportColumn(driverTable.Columns, "d", "trip_driver_name", "Driver Name")}, {OptionalReportColumn(driverTable.Columns, "d", "trip_driver_id", "Driver ID")}, {OptionalReportColumn(driverTable.Columns, "d", "trip_driver_primary", "Driver Primary", "bit")}";
        }

        var sql = $"""
            SELECT
                t.[trip_authority_code] AS [Trip Authority Code],
                t.[issue_date] AS [Trip Date],
                c.[contract_code] AS [Contract Code],
                c.[site_code] AS [Site Code],
                v.[fleet_number] AS [GG Number],
                v.[registration_number] AS [GP Number],
                {driverSelection}
            FROM [dbo].[trip_authorities] t
            INNER JOIN [dbo].[contract] c ON c.[contract_code] = t.[contract_code]{contractDeletedPredicate}
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = c.[vmf_code]
            {driverJoin}
            WHERE t.[issue_date] >= @fromDate
              AND t.[issue_date] < @toDate{tripDeletedPredicate}{driverDeletedPredicate}
            ORDER BY t.[issue_date], t.[trip_authority_code], [Driver Primary] DESC
            """;

        var result = await ExecutePagedRawReportQueryAsync(
            "driver-information-finyear",
            "Driver Information over a Financial Year",
            "ShowReport.aspx?Item=gFleetVehicleUsers",
            sql,
            [
                new ReportParameter("@fromDate", DbType.DateTime, fromDate),
                new ReportParameter("@toDate", DbType.DateTime, toDate),
            ],
            filters,
            cancellationToken
        );
        result.IsApproximate = true;
        result.ApproximationReason =
            "The legacy report procedure was unavailable; results use the compatible trip, contract, vehicle, and trip-driver tables.";
        return result;
    }

    private async Task<TripDriverReportTable?> ResolveTripDriverReportTableAsync(
        CancellationToken cancellationToken
    )
    {
        foreach (var tableName in new[] { "trip_driver", "trip_drivers" })
        {
            var columns = await GetReportTableColumnsAsync(tableName, cancellationToken);
            if (columns.Contains("trip_authority_code") && columns.Contains("trip_driver_name"))
            {
                return new TripDriverReportTable(tableName, columns);
            }
        }

        return null;
    }

    private async Task<LegacyReportResultDto> BuildTripsOpen31Async(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var days = Math.Max(1, GetInt(filters, "days") ?? 31);
        var cutoffDate = DateTime.Today.AddDays(-days);

        var query =
            from trip in _context.Trips.AsNoTracking()
            join contract in _context.Contracts.AsNoTracking()
                on trip.contract_code equals contract.contract_code
                into tripContracts
            from contract in tripContracts.DefaultIfEmpty()
            join vehicle in _context.Vehicles.AsNoTracking()
                on contract.vmf_code equals vehicle.vmf_code
                into tripVehicles
            from vehicle in tripVehicles.DefaultIfEmpty()
            where
                trip.issue_date.Date <= cutoffDate
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
                vehicle.registration_number,
            };

        var page = await MaterializeDatabasePageAsync(
            query,
            rows => rows.OrderBy(row => row.issue_date).ThenBy(row => row.trip_authority_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Trips Open for Over 31 Days",
            "ShowReport.aspx?Item=TripsOpenForOver31Days",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("GP Number", row => row.registration_number)
        );
    }

    private async Task<LegacyReportResultDto> BuildUnallocatedVehiclesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";
        var siteCode = GetShort(filters, "site");

        var activeVmfCodes = await _context
            .Contracts.AsNoTracking()
            .Where(contract => contract.still_current == "Y")
            .Select(contract => contract.vmf_code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into statuses
            from status in statuses.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on vehicle.location_code equals site.Site_code
                into sites
            from site in sites.DefaultIfEmpty()
            where !activeVmfCodes.Contains(vehicle.vmf_code)
            select new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.current_odo,
                vehicle.take_on_date,
                Status = status != null ? status.status_description : null,
                Site = site != null ? site.description : null,
                vehicle.location_code,
            };

        if (siteCode.HasValue)
        {
            query = query.Where(row => row.location_code == siteCode.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = string.Equals(mode, "GP", StringComparison.OrdinalIgnoreCase)
                ? query.Where(row =>
                    row.registration_number != null && row.registration_number.Contains(term)
                )
                : query.Where(row => row.fleet_number != null && row.fleet_number.Contains(term));
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.fleet_number)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Unallocated Vehicles",
            "Finance/OpenReport.aspx?Report=VehiclesNoCurrentContractAndFuelTransactions",
            true,
            "Approximated from vehicles that currently have no active contract. Fuel transaction parity still requires the original finance report pipeline.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Status", row => row.Status),
            Column("Site", row => row.Site),
            Column("Current ODO", row => row.current_odo),
            Column("Take On Date", row => row.take_on_date)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehicleAdditionsAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var (from, to) = NormalizeDateRange(filters);
        var query = _context
            .Vehicles.AsNoTracking()
            .Where(vehicle =>
                vehicle.purchase_date.HasValue
                && vehicle.purchase_date.Value.Date >= from
                && vehicle.purchase_date.Value.Date <= to
            )
            .Select(vehicle => new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.purchase_date,
                vehicle.purchase_amount,
                vehicle.purchased_from,
                InvoiceNumber = (string?)null,
                vehicle.take_on_date,
                vehicle.current_odo,
            });

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.purchase_date)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Vehicle Additions",
            "SelectStartAndEndDate.aspx?Item=AllVehiclesPurchasedInADateRange",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Purchase Date", row => row.purchase_date),
            Column("Purchase Amount", row => row.purchase_amount),
            Column("Purchased From", row => row.purchased_from),
            Column("Invoice Number", row => row.InvoiceNumber),
            Column("Take On Date", row => row.take_on_date),
            Column("Current ODO", row => row.current_odo)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehicleDisposalsAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var (from, to) = NormalizeDateRange(filters);
        var query = _context
            .Vehicles.AsNoTracking()
            .Where(vehicle =>
                vehicle.sold_date.HasValue
                && vehicle.sold_date.Value.Date >= from
                && vehicle.sold_date.Value.Date <= to
            )
            .Select(vehicle => new
            {
                vehicle.vmf_code,
                vehicle.fleet_number,
                vehicle.registration_number,
                vehicle.sold_date,
                vehicle.sold_amount,
                vehicle.sold_to,
                vehicle.current_odo,
                vehicle.purchase_amount,
            });

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.sold_date)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Vehicle Disposals",
            "SelectStartAndEndDate.aspx?Item=AllVehiclesDisposedInADateRange",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Sold Date", row => row.sold_date),
            Column("Sold Amount", row => row.sold_amount),
            Column("Sold To", row => row.sold_to),
            Column("Current ODO", row => row.current_odo),
            Column("Purchase Amount", row => row.purchase_amount)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehicleInfoAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult<object>(
                "General Vehicle Information Report",
                "Vehicles/RPT_Vehicle_Info.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Sequence = "1",
                        Action = "Open Vehicle Information Search",
                        Target = "Vehicles/RPT_vehicle_details.aspx",
                        Notes = "Enter GG, GP, engine, or chassis number to load detail folders.",
                    },
                },
                Column("Sequence", row => row.Sequence),
                Column("Action", row => row.Action),
                Column("Target", row => row.Target),
                Column("Notes", row => row.Notes)
            );
        }

        var vmfCode = await ResolveVehicleVmfCodeAsync(search, mode, cancellationToken);
        if (!vmfCode.HasValue)
        {
            return CreateDynamicResult<object>(
                "General Vehicle Information Report",
                "Vehicles/RPT_Vehicle_Info.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Sequence = "1",
                        Action = "Open Vehicle Information Search",
                        Target = "Vehicles/RPT_vehicle_details.aspx",
                        Notes = $"No vehicle matched '{search.Trim()}' for {GetVehicleSearchModeLabel(mode)} search.",
                    },
                },
                Column("Sequence", row => row.Sequence),
                Column("Action", row => row.Action),
                Column("Target", row => row.Target),
                Column("Notes", row => row.Notes)
            );
        }

        var rows = new[]
        {
            new
            {
                Sequence = "1",
                Section = "Vehicle Details Folder",
                Action = "Open Vehicle Section",
                Target = $"Vehicles/GV_Report/RPT_veh.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "2",
                Section = "Contract Details Folder",
                Action = "Open Contract Section",
                Target = $"Vehicles/GV_Report/RPT_cont.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "3",
                Section = "Trip Details Folder",
                Action = "Open Trip Section",
                Target = $"Vehicles/GV_Report/RPT_trip.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "4",
                Section = "Fuel Card Details Folder",
                Action = "Open Fuel Section",
                Target = $"Vehicles/GV_Report/RPT_fuel.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "5",
                Section = "Licence Details Folder",
                Action = "Open Licence Section",
                Target = $"Vehicles/GV_Report/RPT_lic.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "6",
                Section = "Call Centre Details Folder",
                Action = "Open Call Section",
                Target = $"Vehicles/GV_Report/RPT_call.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "7",
                Section = "Financial Details Folder",
                Action = "Open Financial Section",
                Target = $"Vehicles/GV_Report/RPT_fin.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "8",
                Section = "Accident Details Folder",
                Action = "Open Accident Section",
                Target = $"Vehicles/GV_Report/RPT_acc.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "9",
                Section = "Losses Details Folder",
                Action = "Open Losses Section",
                Target = $"Vehicles/GV_Report/RPT_los.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "R",
                Section = "Navigation",
                Action = "Return To Main Report Menu",
                Target = "FISReports/FIS_Report.aspx",
            },
        };

        return CreateDynamicResult(
            "General Vehicle Information Report",
            "Vehicles/RPT_vehicle_details.aspx",
            false,
            null,
            rows,
            Column("Sequence", row => row.Sequence),
            Column("Section", row => row.Section),
            Column("Action", row => row.Action),
            Column("Target", row => row.Target)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehicleListDateRangeAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var (startDate, endDate) = NormalizeDateRange(filters);
        var statusFilter = GetString(filters, "status");

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into statuses
            from status in statuses.DefaultIfEmpty()
            where
                (
                    (vehicle.take_on_date.Date >= startDate && vehicle.take_on_date.Date <= endDate)
                    || (
                        vehicle.sold_date.HasValue
                        && vehicle.sold_date.Value.Date >= startDate
                        && vehicle.sold_date.Value.Date <= endDate
                    )
                )
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
                vehicle.year_manufactured,
            };

        if (
            !string.IsNullOrWhiteSpace(statusFilter)
            && !string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase)
            && short.TryParse(statusFilter, out var statusCode)
        )
        {
            query = query.Where(row => row.StatusCode == statusCode);
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.take_on_date)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Vehicle List in Date Range",
            "SelectStartAndEndDate.aspx?Item=FilterAllVehiclesInADateRange",
            false,
            null,
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Take On Date", row => row.take_on_date),
            Column("Sold Date", row => row.sold_date),
            Column("Current ODO", row => row.current_odo),
            Column("Vehicle Status Code", row => row.StatusCode),
            Column("Status", row => row.Status),
            Column("Purchase Amount", row => row.purchase_amount),
            Column("Year Manufactured", row => row.year_manufactured)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehicleStatusAllAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into statuses
            from status in statuses.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on vehicle.location_code equals site.Site_code
                into sites
            from site in sites.DefaultIfEmpty()
            where true
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
                vehicle.sold_date,
            };

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.Status)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "All Vehicle Status",
            "Finance/GeneratedReports.aspx?key=9.2%20All%20Vehicle%20Statuses",
            true,
            "Legacy report is generated from a separate report pipeline. This approximation projects the same core vehicle status fields from legacy tables.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("Sold Date", row => row.sold_date)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehicleLogsReportAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
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
                false,
                null,
                new[]
                {
                    new
                    {
                        Sequence = "1",
                        Action = "Open Vehicle Logs Search",
                        Target = "Logs/RPT_logsheet_per_vehicle.aspx",
                        Notes = "Enter GG, GP, engine, or chassis number to load Contract, ELS, and Logsheet folders.",
                    },
                },
                Column("Sequence", row => row.Sequence),
                Column("Action", row => row.Action),
                Column("Target", row => row.Target),
                Column("Notes", row => row.Notes)
            );
        }

        var rows = new[]
        {
            new
            {
                Sequence = "1",
                Section = "Vehicle Logs Search",
                Action = "Open Vehicle Logs Search",
                Target = "Logs/RPT_logsheet_per_vehicle.aspx",
            },
            new
            {
                Sequence = "2",
                Section = "Contract Details Folder",
                Action = "Open Contract Folder",
                Target = $"Logs/logsheet_report/RPT_cont.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "3",
                Section = "ELS Details Folder",
                Action = "Open Trip/ELS Folder",
                Target = $"Logs/logsheet_report/RPT_trips.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "4",
                Section = "Logsheet Details Folder",
                Action = "Open Logsheet Folder",
                Target = $"Logs/logsheet_report/RPT_log.aspx?v_code={vmfCode.Value}",
            },
            new
            {
                Sequence = "R",
                Section = "Navigation",
                Action = "Return To Main Report Menu",
                Target = "FISReports/FIS_Report.aspx",
            },
        };

        return CreateDynamicResult(
            "Vehicle Logs Report",
            "Logs/RPT_logsheet_per_vehicle.aspx",
            false,
            null,
            rows,
            Column("Sequence", row => row.Sequence),
            Column("Section", row => row.Section),
            Column("Action", row => row.Action),
            Column("Target", row => row.Target)
        );
    }

    private async Task<LegacyReportResultDto> BuildLogbookOneVehicleAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search")
            ?? GetString(filters, "vehicleNumber")
            ?? GetString(filters, "xnumber");
        var mode = (GetString(filters, "mode") ?? GetString(filters, "Radio1") ?? "GG")
            .Trim()
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult(
                "Logbook Report on One Vehicle",
                "Logbook/RPT_one_num_report_Logbk.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Enter a GG or GP number to run the legacy one-vehicle Logbook report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        // The legacy page exposes exactly two branches: Radiogg (fleet/GG)
        // and the registration-number branch. Do not silently reinterpret a
        // third selector as a different identifier.
        var identifierColumn = mode switch
        {
            "GG" or "RADIOGG" => "v.[fleet_number]",
            "GP" or "RADIOGP" or "REG" or "REGISTRATION" => "v.[registration_number]",
            _ => null,
        };
        if (identifierColumn is null)
        {
            return CreateDynamicResult(
                "Logbook Report on One Vehicle",
                "Logbook/RPT_one_num_report_Logbk.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "The legacy report accepts only GG or GP number searches.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var parameters = new List<ReportParameter>
        {
            new("@vehicleNumber", DbType.String, search.Trim()),
        };
        var siteScope = BuildReportSiteScope(parameters, filters, "l.[site_code]");

        var sql = $"""
            SELECT
                v.[registration_number] AS [Prov Reg Number],
                v.[fleet_number] AS [GG Number],
                loc.[description] AS [Garage],
                typ.[type_description] AS [Hire Type],
                s.[description] AS [Site],
                s.[Department_number] AS [Site Code],
                s.[res_person] AS [Site Person],
                s.[telephone] AS [Site Tel],
                s.[fax] AS [Site Fax],
                l.[logbookcode] AS [Logbook Code],
                l.[handout_date] AS [Handout Date],
                l.[begin_num] AS [Logbook Begin Number],
                l.[end_num] AS [Logbook End Number],
                l.[lb_receiver_name] AS [Receiver Name],
                l.[lb_tel_num] AS [Receiver Tel],
                l.[lb_comment] AS [Comment]
            FROM [dbo].[logbook] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON l.[vmf_code] = v.[vmf_code]
            INNER JOIN [dbo].[site] AS s ON l.[site_code] = s.[Site_code]
            INNER JOIN [dbo].[location] AS loc ON v.[location_code] = loc.[location_code]
            INNER JOIN [dbo].[type] AS typ ON v.[type_code] = typ.[type_code]
            WHERE {identifierColumn} = @vehicleNumber
              AND {siteScope}
            ORDER BY v.[registration_number] DESC, l.[logbookcode] DESC
            """;

        return await ExecutePagedRawReportQueryAsync(
            "logbooks-one-vehicle",
            "Logbook Report on One Vehicle",
            "Logbook/RPT_one_num_report_Logbk.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLogbookNumberAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search")
            ?? GetString(filters, "logbookNumber")
            ?? GetString(filters, "xnumber");
        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult(
                "Logbook Report on a Logbook Number",
                "Logbook/RPT_one_lbnum_report_Logbk.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Enter a Logbook begin number to run the legacy Logbook-number report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var parameters = new List<ReportParameter>();
        var siteScope = BuildReportSiteScope(parameters, filters, "l.[site_code]");
        parameters.Add(
            new ReportParameter(
                "@logbookNumber",
                DbType.String,
                ToSqlContainsPattern(search.Trim())
            )
        );
        var sql = $"""
            SELECT
                v.[fleet_number] AS [GG Number],
                v.[registration_number] AS [Reg Number],
                l.[begin_num] AS [Logbook Begin Number],
                l.[end_num] AS [Logbook End Number],
                l.[handout_date] AS [Handout Date],
                l.[lb_receiver_name] AS [Received By],
                s.[Department_number] AS [Dept/Site Code],
                l.[logbookcode] AS [Logbook Code]
            FROM [dbo].[logbook] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON l.[vmf_code] = v.[vmf_code]
            INNER JOIN [dbo].[site] AS s ON l.[site_code] = s.[Site_code]
            INNER JOIN [dbo].[location] AS loc ON v.[location_code] = loc.[location_code]
            INNER JOIN [dbo].[type] AS typ ON v.[type_code] = typ.[type_code]
            WHERE l.[begin_num] LIKE @logbookNumber ESCAPE '~'
              AND {siteScope}
            ORDER BY l.[begin_num], l.[logbookcode]
            """;

        return await ExecutePagedRawReportQueryAsync(
            "logbooks-number",
            "Logbook Report on a Logbook Number",
            "Logbook/RPT_one_lbnum_report_Logbk.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLogbookDepartmentPeriodAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var department = GetString(filters, "department")
            ?? GetString(filters, "dept")
            ?? GetString(filters, "XDEPT");
        var fromRaw = GetString(filters, "from") ?? GetString(filters, "BDAT");
        var toRaw = GetString(filters, "to") ?? GetString(filters, "EDAT");
        var from = GetDate(filters, "from") ?? GetDate(filters, "BDAT");
        var to = GetDate(filters, "to") ?? GetDate(filters, "EDAT");

        if (
            string.IsNullOrWhiteSpace(department)
            || string.IsNullOrWhiteSpace(fromRaw)
            || string.IsNullOrWhiteSpace(toRaw)
            || !from.HasValue
            || !to.HasValue
        )
        {
            return CreateDynamicResult(
                "Logbook Report for a Department/Site and Period",
                "Logbook/RPT_dept_period_report_Logbk.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Select a department/site and both dates to run the legacy period report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var parameters = new List<ReportParameter>
        {
            new(
                "@department",
                DbType.String,
                ToSqlContainsPattern(department.Trim())
            ),
            new("@fromDate", DbType.DateTime, from.Value.Date),
            new("@toDate", DbType.DateTime, to.Value.Date),
        };
        var siteScope = BuildReportSiteScope(parameters, filters, "s.[Site_code]");

        var sql = $"""
            SELECT
                v.[registration_number] AS [Prov Reg Number],
                v.[fleet_number] AS [GG Number],
                l.[handout_date] AS [Handout Date],
                l.[begin_num] AS [Begin Number],
                l.[end_num] AS [End Number],
                l.[lb_receiver_name] AS [Receiver],
                l.[lb_tel_num] AS [Receiver Tel],
                s.[Department_number] AS [Dept/Site],
                l.[logbookcode] AS [Logbook Code]
            FROM [dbo].[logbook] AS l
            INNER JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = l.[vmf_code]
            INNER JOIN [dbo].[model] AS m ON v.[model_code] = m.[model_code]
            INNER JOIN [dbo].[site] AS s ON l.[site_code] = s.[Site_code]
            WHERE s.[Department_number] LIKE @department ESCAPE '~'
              AND l.[handout_date] >= @fromDate
              AND l.[handout_date] <= @toDate
              AND {siteScope}
            ORDER BY s.[Department_number], v.[fleet_number], l.[handout_date] DESC
            """;

        return await ExecutePagedRawReportQueryAsync(
            "logbooks",
            "Logbook Report for a Department/Site and Period",
            "Logbook/RPT_dept_period_report_Logbk.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLogsheetOneVehicleAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search")
            ?? GetString(filters, "vehicleNumber")
            ?? GetString(filters, "GGnum")
            ?? GetString(filters, "txtGGNum");
        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult(
                "Log Report on One Vehicle",
                "Logs/RPT_Logs_per_vehicle.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Enter a GG or GP number to run the legacy one-vehicle log report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var mode = (GetString(filters, "mode") ?? "GG").Trim().ToUpperInvariant();
        var identifierColumn = mode == "GP" ? "v.[registration_number]" : "v.[fleet_number]";
        var logRelation = await ResolveReportRelationAsync(
            "LatestLogsheets",
            "Logsheets",
            cancellationToken
        );
        var parameters = new List<ReportParameter>
        {
            new("@vehicleNumber", DbType.String, search.Trim()),
        };
        var siteScope = BuildReportSiteScope(parameters, filters, "l.[site_code]");
        var sql = $"""
            WITH ordered AS
            (
                SELECT
                    v.[fleet_number] AS [GG Number],
                    l.[start_odo] AS [Start ODO],
                    l.[end_odo] AS [End ODO],
                    l.[rek_num] AS [Requisition],
                    CONVERT(varchar(20), l.[month], 120) AS [Month],
                    l.[bund_num] AS [Batch],
                    l.[trans_date] AS [Captured],
                    s.[Department_number] AS [Department],
                    LAG(l.[start_odo]) OVER (ORDER BY l.[start_odo] DESC, l.[end_odo] DESC) AS [Previous Start ODO]
                FROM {logRelation} AS l
                INNER JOIN [dbo].[vehicle_master] AS v ON l.[vmf_code] = v.[vmf_code]
                LEFT JOIN [dbo].[site] AS s ON s.[Site_code] = l.[site_code]
                WHERE {identifierColumn} = @vehicleNumber
                  AND {siteScope}
            )
            SELECT
                [GG Number],
                [Start ODO],
                [End ODO],
                [Requisition],
                [Month],
                [Batch],
                [Captured],
                [Department],
                CASE
                    WHEN [Previous Start ODO] IS NULL THEN 0
                    ELSE [Previous Start ODO] - [End ODO]
                END AS [Outstanding]
            FROM ordered
            ORDER BY [Start ODO] DESC, [End ODO] DESC
            """;

        return await ExecutePagedRawReportQueryAsync(
            "logsheets-one-vehicle",
            "Log Report on One Vehicle",
            "Logs/RPT_Logs_per_vehicle.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLogsheetOdoBalanceAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search")
            ?? GetString(filters, "vehicleNumber")
            ?? GetString(filters, "GGnum");
        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult(
                "Report per Vehicle - Log ODO Balance",
                "Logs/RPT_logs_per_vehicle_2.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Enter a GG or GP number to run the legacy odometer balance report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var mode = (GetString(filters, "mode") ?? "GG").Trim().ToUpperInvariant();
        var identifierColumn = mode == "GP" ? "v.[registration_number]" : "v.[fleet_number]";
        var logRelation = await ResolveReportRelationAsync(
            "LatestLogsheets",
            "Logsheets",
            cancellationToken
        );
        var fromMonth = GetDate(filters, "from")?.Date ?? new DateTime(2002, 1, 1);
        var parameters = new List<ReportParameter>
        {
            new("@vehicleNumber", DbType.String, search.Trim()),
            new("@fromMonth", DbType.DateTime, fromMonth),
        };
        var siteScope = BuildReportSiteScope(parameters, filters, "l.[site_code]");
        var sql = $"""
            WITH ordered AS
            (
                SELECT
                    l.[end_odo] AS [End ODO],
                    LEAD(l.[start_odo]) OVER (ORDER BY l.[start_odo]) AS [Start ODO],
                    l.[month] AS [Month],
                    l.[rek_num] AS [End Requisition],
                    LEAD(l.[rek_num]) OVER (ORDER BY l.[start_odo]) AS [Start Requisition]
                FROM {logRelation} AS l
                INNER JOIN [dbo].[vehicle_master] AS v ON l.[vmf_code] = v.[vmf_code]
                WHERE {identifierColumn} = @vehicleNumber
                  AND l.[month] >= @fromMonth
                  AND {siteScope}
            )
            SELECT
                [End ODO],
                [Start ODO],
                [Month],
                CASE
                    WHEN [Start ODO] IS NULL OR [Start ODO] = [End ODO] THEN NULL
                    ELSE [Start ODO] - [End ODO]
                END AS [Difference],
                [End Requisition],
                [Start Requisition]
            FROM ordered
            ORDER BY [End ODO], [Start ODO], [Month]
            """;

        return await ExecutePagedRawReportQueryAsync(
            "logsheets-vehicle-odo-balance",
            "Report per Vehicle - Log ODO Balance",
            "Logs/RPT_logs_per_vehicle_2.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLogsheetPerRequisitionAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search")
            ?? GetString(filters, "requisition")
            ?? GetString(filters, "txtNum");
        if (string.IsNullOrWhiteSpace(search))
        {
            return CreateDynamicResult(
                "Vehicle Details per Requisition Number",
                "Logs/RPT_logsheet_per_rek.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Enter a requisition number or part of it to run the legacy report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var parameters = new List<ReportParameter>
        {
            new(
                "@requisition",
                DbType.String,
                ToSqlContainsPattern(search.Trim())
            ),
        };
        var siteScope = BuildReportSiteScope(parameters, filters, "l.[site_code]");
        var sql = $"""
            SELECT
                l.[rek_num] AS [Requisition],
                v.[fleet_number] AS [GG Number],
                v.[registration_number] AS [GP Number],
                l.[start_odo] AS [Start ODO],
                l.[end_odo] AS [End ODO],
                l.[trans_date] AS [Captured],
                l.[bund_num] AS [Batch]
            FROM [dbo].[Logsheets] AS l
            LEFT JOIN [dbo].[vehicle_master] AS v ON l.[vmf_code] = v.[vmf_code]
            WHERE l.[rek_num] LIKE @requisition ESCAPE '~'
              AND {siteScope}
            ORDER BY l.[rek_num], v.[fleet_number]
            """;

        return await ExecutePagedRawReportQueryAsync(
            "logsheets-vehicle-details-per-rek",
            "Vehicle Details per Requisition Number",
            "Logs/RPT_logsheet_per_rek.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private async Task<LegacyReportResultDto> BuildLogsheetDepartmentOrSiteAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var departmentCode = GetShort(filters, "department") ?? GetShort(filters, "dept");
        var siteCode = GetShort(filters, "site") ?? GetShort(filters, "site_code");
        if (!departmentCode.HasValue && !siteCode.HasValue)
        {
            return CreateDynamicResult(
                "Logsheet Report per Department or Site",
                "Logs/RPT_Logs_per_Dept_1.aspx",
                false,
                null,
                new[]
                {
                    new
                    {
                        Message = "Select a department or site to run the legacy logsheet report.",
                    },
                },
                Column("Message", row => row.Message)
            );
        }

        var fromDate = GetDate(filters, "from")?.Date ?? DateTime.Today.AddMonths(-12).Date;
        var toDateExclusive = (GetDate(filters, "to")?.Date ?? DateTime.Today.Date).AddDays(1);
        var predicate = departmentCode.HasValue
            ? "s.[Depatrment_code] = @departmentCode"
            : "s.[Site_code] = @siteCode";
        var logRelation = await ResolveReportRelationAsync(
            "LatestLogsheets",
            "Logsheets",
            cancellationToken
        );
        var parameters = new List<ReportParameter>
        {
            new("@fromDate", DbType.DateTime, fromDate),
            new("@toDateExclusive", DbType.DateTime, toDateExclusive),
        };
        if (departmentCode.HasValue)
        {
            parameters.Add(new ReportParameter("@departmentCode", DbType.Int16, departmentCode.Value));
        }
        else
        {
            parameters.Add(new ReportParameter("@siteCode", DbType.Int16, siteCode!.Value));
        }
        var siteScope = BuildReportSiteScope(parameters, filters, "c.[site_code]");
        var sql = $"""
            WITH ordered AS
            (
                SELECT
                    v.[fleet_number] AS [GG Number],
                    l.[vmf_code] AS [VMF Code],
                    l.[start_odo] AS [Start ODO],
                    l.[end_odo] AS [End ODO],
                    l.[month] AS [Month],
                    l.[rek_num] AS [Logsheet],
                    c.[site_code] AS [Contract Site Code],
                    l.[site_code] AS [Log Site Code],
                    s.[description] AS [Site],
                    l.[trans_date] AS [Captured],
                    LAG(l.[start_odo]) OVER (
                        PARTITION BY l.[vmf_code]
                        ORDER BY l.[start_odo] DESC
                    ) AS [Previous Start ODO]
                FROM [dbo].[vehicle_master] AS v
                INNER JOIN [dbo].[contract] AS c ON v.[vmf_code] = c.[vmf_code]
                INNER JOIN {logRelation} AS l ON v.[vmf_code] = l.[vmf_code]
                INNER JOIN [dbo].[site] AS s ON c.[site_code] = s.[Site_code]
                WHERE c.[still_current] = 'Y'
                  AND v.[type_code] = 3
                  AND {predicate}
                  AND {siteScope}
                  AND l.[trans_date] >= @fromDate
                  AND l.[trans_date] < @toDateExclusive
            )
            SELECT
                [GG Number],
                [VMF Code],
                [Start ODO],
                [End ODO],
                [Month],
                [Logsheet],
                [Contract Site Code],
                [Log Site Code],
                [Site],
                [Captured],
                CASE
                    WHEN [Previous Start ODO] IS NULL OR [Previous Start ODO] = [End ODO] THEN 0
                    ELSE [Previous Start ODO] - [End ODO]
                END AS [Difference]
            FROM ordered
            ORDER BY [Contract Site Code], [GG Number], [Start ODO] DESC
            """;
        return await ExecutePagedRawReportQueryAsync(
            "logsheets",
            "Logsheet Report per Department or Site",
            "Logs/RPT_Logs_per_Dept_1.aspx",
            sql,
            parameters,
            filters,
            cancellationToken
        );
    }

    private Task<LegacyReportResultDto> BuildLogsheetsAllOutstandingAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    ) => throw new LegacyReportProcedureUnavailableException("dbo.DEV_SEL_All_Logs");

    private async Task<LegacyReportResultDto> BuildVehicleStatusRangeAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var statusCode = GetShort(filters, "status");
        var (startDate, endDate) = NormalizeDateRange(filters);

        var query =
            from history in _context.VehicleStatusHistories.AsNoTracking()
            join vehicle in _context.Vehicles.AsNoTracking()
                on history.vmf_code equals vehicle.vmf_code
                into histories
            from vehicle in histories.DefaultIfEmpty()
            where
                history.status_start_date.Date <= endDate
                && history.status_end_date.Date >= startDate
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
            };

        if (statusCode.HasValue)
        {
            query = query.Where(row => row.vehicle_status_code == statusCode.Value);
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderByDescending(row => row.status_start_date)
                    .ThenBy(row => row.fleet_number)
                    .ThenBy(row => row.vehicle_status_history_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Vehicle Status Range Report",
            "Vehicles/VehicleStatus.aspx",
            true,
            "Legacy vehicle status pages are custom. This dynamic approximation uses vehicle_status_history rows.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
            Column("Status History Code", row => row.vehicle_status_history_code),
            Column("VMF Code", row => row.vmf_code),
            Column("GG Number", row => row.fleet_number),
            Column("GP Number", row => row.registration_number),
            Column("Vehicle Status Code", row => row.vehicle_status_code),
            Column("Vehicle Status Description", row => row.vehicle_status_description),
            Column("Status Start Date", row => row.status_start_date),
            Column("Status End Date", row => row.status_end_date)
        );
    }

    private async Task<LegacyReportResultDto> BuildVehiclesAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search = GetString(filters, "search");
        var mode = GetString(filters, "mode") ?? "GG";

        var query =
            from vehicle in _context.Vehicles.AsNoTracking()
            join status in _context.VehicleStatuses.AsNoTracking()
                on vehicle.vehicle_status_code equals status.vehicle_status_code
                into statuses
            from status in statuses.DefaultIfEmpty()
            join model in _context.Models.AsNoTracking()
                on vehicle.model_code equals model.model_code
                into models
            from model in models.DefaultIfEmpty()
            join make in _context.Makes.AsNoTracking()
                on model.make_code equals make.make_code
                into makes
            from make in makes.DefaultIfEmpty()
            join type in _context.VehicleTypes.AsNoTracking()
                on vehicle.type_code equals type.type_code
                into types
            from type in types.DefaultIfEmpty()
            join site in _context.Sites.AsNoTracking()
                on vehicle.location_code equals site.Site_code
                into sites
            from site in sites.DefaultIfEmpty()
            join vehicleClass in _context.Classes.AsNoTracking()
                on model.class_code equals vehicleClass.class_code
                into vehicleClasses
            from vehicleClass in vehicleClasses.DefaultIfEmpty()
            where true
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
                vehicle.sold_date,
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var matchingVmfCodes = await ResolveVehicleVmfCodesAsync(
                search,
                mode,
                cancellationToken
            );
            query = query.Where(row => matchingVmfCodes.Contains(row.vmf_code));
        }

        var page = await MaterializeDatabasePageAsync(
            query,
            rows =>
                rows.OrderBy(row => row.fleet_number)
                    .ThenBy(row => row.registration_number)
                    .ThenBy(row => row.vmf_code),
            filters,
            cancellationToken,
            5000
        );

        return CreateDatabasePagedResult(
            "Vehicle Master List",
            "Vehicles/Vehicles.aspx",
            true,
            "Legacy vehicle list reports are menu-driven and highly parameterized. This approximation now uses human-readable make, model, type, site, and status descriptions instead of raw codes.",
            page.Rows,
            page.TotalCount,
            page.PageWindow,
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
            Column("Sold Date", row => row.sold_date)
        );
    }

    private static Task<LegacyReportResultDto> BuildVehiclesNoTariffAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        // ShowReport.aspx prefixes Item=GetAllVehicleWithNoTariffs to
        // dbo.DEV_REP_GetAllVehicleWithNoTariffs. That object is missing from
        // the archive and is not the GFleet Dev_Rep_Permanentcontractswithouttariffs
        // report. Do not substitute a different procedure or table projection.
        return Task.FromResult(
            CreateDynamicResult(
                "Vehicles with Expired or No Tariffs",
                "ShowReport.aspx?Item=GetAllVehicleWithNoTariffs",
                true,
                "The legacy procedure dbo.DEV_REP_GetAllVehicleWithNoTariffs is unavailable; no different table projection was substituted.",
                Array.Empty<object>(),
                Column("VMF Code", _ => null),
                Column("GG Number", _ => null),
                Column("GP Number", _ => null),
                Column("Year Manufactured", _ => null),
                Column("Current ODO", _ => null),
                Column("Location Code", _ => null),
                Column("Status", _ => null),
                Column("Take On Date", _ => null)
            )
        );
    }

    private static Task<LegacyReportResultDto> BuildPermanentContractsWithoutTariffAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            CreateDynamicResult(
                "Permanent contracts without assigned tariffs",
                "GFleetReports/VehiclesonContractWithoutTariffs.aspx",
                true,
                "The legacy procedure dbo.Dev_Rep_Permanentcontractswithouttariffs is unavailable; no different table projection was substituted.",
                Array.Empty<object>()
            )
        );
    }

    private static Task<LegacyReportResultDto> BuildNomVehiclesWithoutTariffAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        // The archived RPTtariffs.aspx path executes
        // DEV_REP_NOMVehiclesWithoutTariffs. There is no safe table-only
        // substitute for its lease-source, contract, location, and contact
        // joins, so keep the missing-procedure state explicit instead of
        // returning the different permanent-contract tariff report here.
        return Task.FromResult(
            CreateDynamicResult(
                "NOM Vehicles Without Tariffs",
                "ShowReportWithLineNumbers.aspx?Item=NOMVehiclesWithoutTariffs",
                true,
                "The legacy procedure dbo.DEV_REP_NOMVehiclesWithoutTariffs is unavailable; no different table projection was substituted.",
                Array.Empty<object>(),
                Column("Vehicle Counter", _ => null),
                Column("GG Number", _ => null),
                Column("Registration Number", _ => null),
                Column("Hired From", _ => null),
                Column("Vehicle Status", _ => null),
                Column("Location", _ => null),
                Column("Year Model", _ => null),
                Column("Model Description", _ => null),
                Column("Make", _ => null),
                Column("Class Description", _ => null),
                Column("Purchase Amount", _ => null),
                Column("Contract Type", _ => null),
                Column("Contract Start Date", _ => null),
                Column("Contract End Date", _ => null),
                Column("Dispatched Date", _ => null),
                Column("Estimated Return Date", _ => null),
                Column("Department", _ => null),
                Column("Site", _ => null),
                Column("Site Contact Details", _ => null),
                Column("Province", _ => null)
            )
        );
    }

    private static LegacyReportDefinition DistinctArchivedScreen(
        string key,
        string title,
        string legacyTarget,
        string reason
    ) =>
        new(key, title, legacyTarget, null, BuildUnmappedUniversalReportAsync, reason);

    private static LegacyReportDefinition DistinctFineScreen(
        string key,
        string title,
        string legacyTarget
    ) =>
        new(
            key,
            title,
            legacyTarget,
            null,
            BuildUnmappedUniversalReportAsync,
            "Fines/RPTFines.aspx opens a distinct Crystal/HTML fines screen. It is not the generic fines list."
        );

    private static LegacyReportDefinition DistinctVehicleScreen(string key, string title) =>
        new(
            key,
            title,
            string.Empty,
            null,
            BuildUnmappedUniversalReportAsync,
            "This archived vehicle report is a distinct screen. No generic vehicle_master projection was substituted."
        );

    private static LegacyReportDefinition DistinctLicenceScreen(
        string key,
        string title,
        string legacyTarget
    ) =>
        new(
            key,
            title,
            legacyTarget,
            null,
            BuildUnmappedUniversalReportAsync,
            "License/RPTLicence.aspx opens a distinct Crystal/HTML licence screen. It is not the generic licence list."
        );

    private static LegacyReportDefinition DistinctShowReportItem(
        string key,
        string title,
        string storedProcedureItem,
        string reason
    ) =>
        new(
            key,
            title,
            $"ShowReport.aspx?Item={storedProcedureItem}",
            storedProcedureItem,
            BuildUnmappedUniversalReportAsync,
            reason,
            _ => Array.Empty<LegacyStoredProcedureParameter>()
        );

    private static Task<LegacyReportResultDto> BuildUnmappedUniversalReportAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(
            CreateDynamicResult(
                "Universal report",
                string.Empty,
                true,
                "This archived universal report is a custom filter screen. No generic table projection was substituted.",
                Array.Empty<object>()
            )
        );
    }

    private Task<LegacyReportResultDto> BuildWesbankAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var rows = new[]
        {
            new
            {
                Sequence = "1",
                Report = "Wesbank Transaction Report for ONE Vehicle",
                Target = "Transaction/RPT_1reg_main_Wes.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "2",
                Report = "Wesbank Transaction Report for ONE Vehicle, for a Period",
                Target = "Transaction/RPT_1reg_period_main_Wes.htm",
                Availability = "Available",
            },
            new
            {
                Sequence = "3",
                Report = "Wesbank Transaction Report for ONE Dept Site",
                Target = "Transaction/RPT_1site_main_Wes.aspx",
                Availability = "Available",
            },
            new
            {
                Sequence = "4",
                Report = "Exeption Report Overfills",
                Target = "Transaction/RPT_Overfill_Getinfo.aspx",
                Availability = "Available",
            },
            new
            {
                Sequence = "5",
                Report = "Exeption Multiple Daily Fuel Transactions",
                Target = "Transaction/RPT_Mult_Fill_Getinfo.aspx",
                Availability = "Available",
            },
            new
            {
                Sequence = "R",
                Report = "Return To Main Page",
                Target = "FISReports/FIS_Report.aspx",
                Availability = "Available",
            },
        };

        return Task.FromResult(
            CreateDynamicResult(
                "Wesbank First Auto Report",
                "Transaction/RPTTransaction.aspx",
                false,
                null,
                rows,
                Column("Sequence", row => row.Sequence),
                Column("Report", row => row.Report),
                Column("Target", row => row.Target),
                Column("Availability", row => row.Availability)
            )
        );
    }

    private async Task<LegacyReportResultDto> BuildWorkshopPeriodAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var (startDate, endDate) = NormalizeWorkshopDateRange(filters);
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _workshopRepository.GetReportPageAsync(
                new WorkshopReportPageQuery(
                    WorkshopReportKind.Period,
                    startDate,
                    endDate,
                    Garage: GetString(filters, "garage") ?? GetString(filters, "Radio1"),
                    Category: GetString(filters, "category") ?? GetString(filters, "Radio2"),
                    Page: pagination.Page,
                    PageSize: pagination.PageSize
                )
            );
            return CreatePagedWorkshopReportResult(
                "Workshop Report for a Period",
                "WorkShop/RPT_ww_date_report.aspx",
                "The legacy period report filters receive dates by garage and accident/mechanical category. This result applies the same filters through the compatible Workshop repository.",
                page
            );
        }

        var rows = (await LoadWorkshopReportRowsAsync(cancellationToken))
            .Where(row => IsWithinWorkshopDateRange(row, startDate, endDate))
            .Where(row =>
                MatchesWorkshopGarage(
                    row.Garage,
                    GetString(filters, "garage") ?? GetString(filters, "Radio1")
                )
            )
            .Where(row =>
                MatchesWorkshopCategory(
                    row.AccidMech,
                    GetString(filters, "category") ?? GetString(filters, "Radio2")
                )
            )
            .OrderByDescending(row => row.ReceiveDate)
            .ThenByDescending(row => row.WorkshopCode)
            .Take(5000)
            .ToList();

        return CreateWorkshopReportResult(
            "Workshop Report for a Period",
            "WorkShop/RPT_ww_date_report.aspx",
            "The legacy period report filters receive dates by garage and accident/mechanical category. This result applies the same filters through the compatible Workshop repository.",
            rows
        );
    }

    private async Task<LegacyReportResultDto> BuildWorkshopOneVehicleAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search =
            GetString(filters, "search")
            ?? GetString(filters, "vehicleNumber")
            ?? GetString(filters, "xnum");
        var searchMode = GetString(filters, "searchMode") ?? GetString(filters, "Radio1");
        var isGp =
            string.Equals(searchMode, "GP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(searchMode, "Radiogp", StringComparison.OrdinalIgnoreCase);
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return CreateEmptyPagedWorkshopReport(
                    "Workshop Report on One Vehicle",
                    "WorkShop/RPT_ww_one_num_report.aspx",
                    "The legacy report requires a GG or GP number.",
                    pagination
                );
            }

            var page = await _workshopRepository.GetReportPageAsync(
                new WorkshopReportPageQuery(
                    WorkshopReportKind.OneVehicle,
                    VehicleSearch: search,
                    VehicleSearchField: isGp
                        ? WorkshopReportVehicleField.RegistrationNumber
                        : WorkshopReportVehicleField.FleetNumber,
                    Page: pagination.Page,
                    PageSize: pagination.PageSize
                )
            );
            return CreatePagedWorkshopReportResult(
                "Workshop Report on One Vehicle",
                "WorkShop/RPT_ww_one_num_report.aspx",
                "The legacy report uses a wildcard GG or GP number search. This result preserves that search against compatible vehicle and Workshop data.",
                page
            );
        }

        var rows = await LoadWorkshopReportRowsAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(row =>
                    (isGp ? row.RegistrationNumber : row.FleetNumber)?.Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                .OrderBy(row => row.FleetNumber)
                .ThenByDescending(row => row.ReceiveDate)
                .Take(5000)
                .ToList();
        }
        else
        {
            rows.Clear();
        }

        return CreateDynamicResult(
            "Workshop Report on One Vehicle",
            "WorkShop/RPT_ww_one_num_report.aspx",
            true,
            "The legacy report uses a wildcard GG or GP number search. This result preserves that search against compatible vehicle and Workshop data.",
            rows,
            Column("GG Number", row => row.FleetNumber),
            Column("GP Number", row => row.RegistrationNumber),
            Column("Accid/Mech", row => WorkshopCategoryLabel(row.AccidMech)),
            Column("Garage", row => WorkshopGarageLabel(row.Garage)),
            Column("Date Received", row => row.ReceiveDate),
            Column("Time Received", row => row.ReceiveTime),
            Column("Date Completed", row => row.CompleteDate),
            Column("Time Completed", row => row.CompleteTime),
            Column("Contact Name", row => row.ContactName),
            Column("Contact Tel", row => row.ContactTel),
            Column("Days to Complete", row => CompletedDays(row)),
            Column("Time to Complete", row => CompletedHours(row))
        );
    }

    private async Task<LegacyReportResultDto> BuildWorkshopPrintJobCardAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var search =
            GetString(filters, "search")
            ?? GetString(filters, "vehicleNumber")
            ?? GetString(filters, "txtGGNum");
        var searchMode = GetString(filters, "searchMode") ?? GetString(filters, "Radio1");
        var isGp =
            string.Equals(searchMode, "GP", StringComparison.OrdinalIgnoreCase)
            || string.Equals(searchMode, "Radiogp", StringComparison.OrdinalIgnoreCase);
        var workshopCode =
            GetShort(filters, "workshopCode")
            ?? GetShort(filters, "wwCode")
            ?? GetShort(filters, "wwcod");
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            if (!workshopCode.HasValue && string.IsNullOrWhiteSpace(search))
            {
                return CreateEmptyPagedWorkshopPrintResult(pagination);
            }

            var page = await _workshopRepository.GetReportPageAsync(
                new WorkshopReportPageQuery(
                    WorkshopReportKind.PrintJobCard,
                    VehicleSearch: search,
                    VehicleSearchField: isGp
                        ? WorkshopReportVehicleField.RegistrationNumber
                        : WorkshopReportVehicleField.FleetNumber,
                    WorkshopCode: workshopCode,
                    Page: pagination.Page,
                    PageSize: pagination.PageSize
                )
            );
            var pagedMerchants = await Task.WhenAll(
                page.Items.Where(item => item.MerchantCode.HasValue)
                    .Select(item => item.MerchantCode!.Value)
                    .Distinct()
                    .Select(async merchantCode => new KeyValuePair<int, string?>(
                        merchantCode,
                        (await _workshopMerchantRepository.GetByIdAsync(merchantCode))?.wwmerch_name
                    ))
            );
            var merchantNames = pagedMerchants.ToDictionary(pair => pair.Key, pair => pair.Value);
            var pagedRows = page.Items.Select(ToWorkshopReportRow);
            return CreateDatabasePagedResult(
                "Print a Workshop Job Card",
                "WorkShop/RPT_ww_printjob_report.aspx",
                true,
                "The legacy job-card report joins optional vehicle, site, towing, merchant, and Workshop fields. This result preserves the fields available in either supported schema.",
                pagedRows,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                WorkshopPrintColumns(merchantNames)
            );
        }

        var rows = await LoadWorkshopReportRowsAsync(cancellationToken);

        if (workshopCode.HasValue)
        {
            rows = rows.Where(row => row.WorkshopCode == workshopCode.Value).ToList();
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(row =>
                    (isGp ? row.RegistrationNumber : row.FleetNumber)?.Contains(
                        term,
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                .OrderByDescending(row => row.ReceiveDate)
                .Take(5000)
                .ToList();
        }
        else
        {
            rows.Clear();
        }

        var merchants = (await _workshopMerchantRepository.GetAllAsync()).ToDictionary(merchant =>
            merchant.wwmerch_code
        );

        return CreateDynamicResult(
            "Print a Workshop Job Card",
            "WorkShop/RPT_ww_printjob_report.aspx",
            true,
            "The legacy job-card report joins optional vehicle, site, towing, merchant, and Workshop fields. This result preserves the fields available in either supported schema.",
            rows,
            Column("Job Card", row => row.WorkshopCode),
            Column("GG Number", row => row.FleetNumber),
            Column("GP Number", row => row.RegistrationNumber),
            Column("Model", row => row.ModelDescription),
            Column("Garage", row => WorkshopGarageLabel(row.Garage)),
            Column("Date Received", row => row.ReceiveDate),
            Column("Time Received", row => row.ReceiveTime),
            Column("Call Refer", row => row.CallRefer),
            Column("Driver Name", row => row.DriverName),
            Column("KM", row => row.WorkshopKm),
            Column("Contact Name", row => row.ContactName),
            Column("Contact Tel", row => row.ContactTel),
            Column("Contact Fax", row => row.ContactFax),
            Column("Contact Email", row => row.ContactEmail),
            Column("Accid/Mech", row => WorkshopCategoryLabel(row.AccidMech)),
            Column("Remarks", row => row.WorkshopRemarks),
            Column("Reason", row => row.WorkshopReason),
            Column(
                "Merchant",
                row =>
                    row.MerchantCode is int merchantCode
                        ? merchants.GetValueOrDefault(merchantCode)?.wwmerch_name
                        : null
            ),
            Column("Repair Cost", row => row.RepairCost),
            Column("Date Completed", row => row.CompleteDate),
            Column("Time Completed", row => row.CompleteTime)
        );
    }

    private async Task<LegacyReportResultDto> BuildWorkshopInShopAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        var now = DateTime.Now;
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _workshopRepository.GetReportPageAsync(
                new WorkshopReportPageQuery(
                    WorkshopReportKind.InShop,
                    OpenOnly: true,
                    Page: pagination.Page,
                    PageSize: pagination.PageSize
                )
            );
            var pagedRows = page.Items.Select(ToWorkshopReportRow);
            return CreateDatabasePagedResult(
                "List of Vehicles Still in Workshop",
                "WorkShop/RPT_ww_inshop_report.aspx",
                true,
                "The legacy report identifies open job cards with job_close = N. When that legacy flag is absent, the compatibility path uses the incomplete date state.",
                pagedRows,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("GG Number", row => row.FleetNumber),
                Column("GP Number", row => row.RegistrationNumber),
                Column("Accident/Mech", row => WorkshopCategoryLabel(row.AccidMech)),
                Column("Garage", row => WorkshopGarageLabel(row.Garage)),
                Column("Date Received", row => row.ReceiveDate),
                Column("Time Received", row => row.ReceiveTime),
                Column("Contact Name", row => row.ContactName),
                Column("Contact Tel", row => row.ContactTel),
                Column("Days in Workshop", row => ElapsedDays(row, now)),
                Column("Hours in Workshop", row => ElapsedHours(row, now)),
                Column("Date from Workshop", row => row.DateFromWorkshop)
            );
        }

        var rows = (await LoadWorkshopReportRowsAsync(cancellationToken))
            .Where(IsWorkshopOpen)
            .OrderBy(row => row.ReceiveDate)
            .ThenBy(row => row.ReceiveTime)
            .Take(5000)
            .ToList();

        return CreateDynamicResult(
            "List of Vehicles Still in Workshop",
            "WorkShop/RPT_ww_inshop_report.aspx",
            true,
            "The legacy report identifies open job cards with job_close = N. When that legacy flag is absent, the compatibility path uses the incomplete date state.",
            rows,
            Column("GG Number", row => row.FleetNumber),
            Column("GP Number", row => row.RegistrationNumber),
            Column("Accident/Mech", row => WorkshopCategoryLabel(row.AccidMech)),
            Column("Garage", row => WorkshopGarageLabel(row.Garage)),
            Column("Date Received", row => row.ReceiveDate),
            Column("Time Received", row => row.ReceiveTime),
            Column("Contact Name", row => row.ContactName),
            Column("Contact Tel", row => row.ContactTel),
            Column("Days in Workshop", row => ElapsedDays(row, now)),
            Column("Hours in Workshop", row => ElapsedHours(row, now)),
            Column("Date from Workshop", row => row.DateFromWorkshop)
        );
    }

    private async Task<LegacyReportResultDto> BuildWorkshopMerchantsAsync(
        IDictionary<string, string?> filters,
        CancellationToken cancellationToken
    )
    {
        if (GetFallbackPagination(filters) is { IncludeAll: false } pagination)
        {
            var page = await _workshopMerchantRepository.GetReportPageAsync(
                new WorkshopMerchantReportPageQuery(
                    pagination.Page,
                    pagination.PageSize,
                    GetString(filters, "search")
                )
            );
            return CreateDatabasePagedResult(
                "List of All Merchants",
                "WorkShop/RPT_merch_report.aspx",
                true,
                "The result reads the Workshop-specific wwmerchant table through its guarded legacy-schema repository.",
                page.Items,
                page.Total,
                new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
                Column("Merchant Code", merchant => merchant.wwmerch_code),
                Column("Merchant Name", merchant => merchant.wwmerch_name),
                Column("Tel", merchant => merchant.wwmerch_tel),
                Column("Fax", merchant => merchant.wwmerch_fax),
                Column("eMAIL", merchant => merchant.wwmerch_email)
            );
        }

        var merchants = (await _workshopMerchantRepository.GetAllAsync())
            .Where(merchant =>
                string.IsNullOrWhiteSpace(GetString(filters, "search"))
                || merchant.wwmerch_name?.Contains(
                    GetString(filters, "search")!.Trim(),
                    StringComparison.OrdinalIgnoreCase
                ) == true
            )
            .OrderBy(merchant => merchant.wwmerch_name)
            .Take(5000)
            .ToList();

        return CreateDynamicResult(
            "List of All Merchants",
            "WorkShop/RPT_merch_report.aspx",
            true,
            "The result reads the Workshop-specific wwmerchant table through its guarded legacy-schema repository.",
            merchants,
            Column("Merchant Code", merchant => merchant.wwmerch_code),
            Column("Merchant Name", merchant => merchant.wwmerch_name),
            Column("Tel", merchant => merchant.wwmerch_tel),
            Column("Fax", merchant => merchant.wwmerch_fax),
            Column("eMAIL", merchant => merchant.wwmerch_email)
        );
    }

    private async Task<List<WorkshopReportRow>> LoadWorkshopReportRowsAsync(
        CancellationToken cancellationToken
    )
    {
        var workshops = await _workshopRepository.GetAllAsync();
        var vehicles = await _context
            .Vehicles.AsNoTracking()
            .ToDictionaryAsync(vehicle => vehicle.vmf_code, cancellationToken);
        var models = await _context
            .Models.AsNoTracking()
            .ToDictionaryAsync(model => model.model_code, cancellationToken);

        return workshops
            .Select(workshop =>
            {
                vehicles.TryGetValue(workshop.vmf_code ?? 0, out var vehicle);
                models.TryGetValue(vehicle?.model_code ?? 0, out var model);
                return new WorkshopReportRow(
                    workshop.ww_code,
                    workshop.vmf_code,
                    vehicle?.fleet_number,
                    vehicle?.registration_number,
                    model?.model_description,
                    vehicle?.current_odo,
                    workshop.receive_date,
                    workshop.receive_time,
                    workshop.complete_date,
                    workshop.complete_time,
                    workshop.contact_name,
                    workshop.contact_tel,
                    workshop.contact_fax,
                    workshop.contact_email,
                    workshop.accid_mech,
                    workshop.garage,
                    workshop.driver_name,
                    workshop.call_refer,
                    workshop.ww_km,
                    workshop.ww_remarks,
                    workshop.ww_reason,
                    workshop.merch_code,
                    workshop.cost_repair,
                    workshop.date_from_ww,
                    workshop.job_close
                );
            })
            .ToList();
    }

    private static LegacyReportResultDto CreateWorkshopReportResult(
        string title,
        string legacyTarget,
        string approximationReason,
        IEnumerable<WorkshopReportRow> rows
    ) =>
        CreateDynamicResult(
            title,
            legacyTarget,
            true,
            approximationReason,
            rows,
            Column("GG Number", row => row.FleetNumber),
            Column("GP Number", row => row.RegistrationNumber),
            Column("Mech/Accid", row => WorkshopCategoryLabel(row.AccidMech)),
            Column("Garage", row => WorkshopGarageLabel(row.Garage)),
            Column("Date Received", row => row.ReceiveDate),
            Column("Time Received", row => row.ReceiveTime),
            Column("Date Completed", row => row.CompleteDate),
            Column("Time Completed", row => row.CompleteTime),
            Column("Contact Name", row => row.ContactName),
            Column("Contact Tel", row => row.ContactTel),
            Column("Days to Complete", row => CompletedDays(row)),
            Column("Time to Complete", row => CompletedHours(row))
        );

    private static LegacyReportResultDto CreatePagedWorkshopReportResult(
        string title,
        string legacyTarget,
        string approximationReason,
        WorkshopReportPage page
    ) =>
        CreateDatabasePagedResult(
            title,
            legacyTarget,
            true,
            approximationReason,
            page.Items.Select(ToWorkshopReportRow),
            page.Total,
            new LegacyReportPageWindow(page.Page, page.PageSize, 0, true),
            WorkshopSummaryColumns
        );

    private static LegacyReportResultDto CreateEmptyPagedWorkshopReport(
        string title,
        string legacyTarget,
        string approximationReason,
        LegacyReportPagination pagination
    ) =>
        CreateDatabasePagedResult(
            title,
            legacyTarget,
            true,
            approximationReason,
            Array.Empty<WorkshopReportRow>(),
            0,
            GetPageWindow(pagination, 0),
            WorkshopSummaryColumns
        );

    private static LegacyReportResultDto CreateEmptyPagedWorkshopPrintResult(
        LegacyReportPagination pagination
    ) =>
        CreateDatabasePagedResult(
            "Print a Workshop Job Card",
            "WorkShop/RPT_ww_printjob_report.aspx",
            true,
            "Enter a job-card number or a GG/GP number to load a print preview.",
            Array.Empty<WorkshopReportRow>(),
            0,
            GetPageWindow(pagination, 0),
            WorkshopPrintColumns(new Dictionary<int, string?>())
        );

    private static WorkshopReportRow ToWorkshopReportRow(WorkshopReportPageItem row) =>
        new(
            row.WorkshopCode,
            row.VmfCode,
            row.FleetNumber,
            row.RegistrationNumber,
            row.ModelDescription,
            row.CurrentOdo,
            row.ReceiveDate,
            row.ReceiveTime,
            row.CompleteDate,
            row.CompleteTime,
            row.ContactName,
            row.ContactTel,
            row.ContactFax,
            row.ContactEmail,
            row.AccidMech,
            row.Garage,
            row.DriverName,
            row.CallRefer,
            row.WorkshopKm,
            row.WorkshopRemarks,
            row.WorkshopReason,
            row.MerchantCode,
            row.RepairCost,
            row.DateFromWorkshop,
            row.JobClose
        );

    private static readonly LegacyProjectionColumn[] WorkshopSummaryColumns =
    [
        Column("GG Number", row => row.FleetNumber),
        Column("GP Number", row => row.RegistrationNumber),
        Column("Mech/Accid", row => WorkshopCategoryLabel(row.AccidMech)),
        Column("Garage", row => WorkshopGarageLabel(row.Garage)),
        Column("Date Received", row => row.ReceiveDate),
        Column("Time Received", row => row.ReceiveTime),
        Column("Date Completed", row => row.CompleteDate),
        Column("Time Completed", row => row.CompleteTime),
        Column("Contact Name", row => row.ContactName),
        Column("Contact Tel", row => row.ContactTel),
        Column("Days to Complete", row => CompletedDays(row)),
        Column("Time to Complete", row => CompletedHours(row)),
    ];

    private static LegacyProjectionColumn[] WorkshopPrintColumns(
        IReadOnlyDictionary<int, string?> merchantNames
    ) =>
        [
            Column("Job Card", row => row.WorkshopCode),
            Column("GG Number", row => row.FleetNumber),
            Column("GP Number", row => row.RegistrationNumber),
            Column("Model", row => row.ModelDescription),
            Column("Garage", row => WorkshopGarageLabel(row.Garage)),
            Column("Date Received", row => row.ReceiveDate),
            Column("Time Received", row => row.ReceiveTime),
            Column("Call Refer", row => row.CallRefer),
            Column("Driver Name", row => row.DriverName),
            Column("KM", row => row.WorkshopKm),
            Column("Contact Name", row => row.ContactName),
            Column("Contact Tel", row => row.ContactTel),
            Column("Contact Fax", row => row.ContactFax),
            Column("Contact Email", row => row.ContactEmail),
            Column("Accid/Mech", row => WorkshopCategoryLabel(row.AccidMech)),
            Column("Remarks", row => row.WorkshopRemarks),
            Column("Reason", row => row.WorkshopReason),
            Column(
                "Merchant",
                row =>
                    row.MerchantCode is int merchantCode
                        ? merchantNames.GetValueOrDefault(merchantCode)
                        : null
            ),
            Column("Repair Cost", row => row.RepairCost),
            Column("Date Completed", row => row.CompleteDate),
            Column("Time Completed", row => row.CompleteTime),
        ];

    private static bool IsWithinWorkshopDateRange(
        WorkshopReportRow row,
        DateTime startDate,
        DateTime endDate
    ) =>
        row.ReceiveDate.HasValue
        && row.ReceiveDate.Value.Date >= startDate
        && row.ReceiveDate.Value.Date <= endDate;

    private static bool MatchesWorkshopGarage(string? garage, string? filter)
    {
        var normalized = filter?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "radiojhb" or "jhb" or "j" => string.Equals(
                garage,
                "J",
                StringComparison.OrdinalIgnoreCase
            ),
            "radiopta" or "pta" or "p" => string.Equals(
                garage,
                "P",
                StringComparison.OrdinalIgnoreCase
            ),
            _ => true,
        };
    }

    private static bool MatchesWorkshopCategory(string? category, string? filter)
    {
        var normalized = filter?.Trim().ToLowerInvariant();
        if (
            normalized
            is not (
                "radioacc"
                or "accident"
                or "accidents"
                or "radiomec"
                or "mechanical"
                or "mechanic"
            )
        )
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(category))
            return false;
        return normalized is "radioacc" or "accident" or "accidents"
            ? string.Compare(category, "M", StringComparison.OrdinalIgnoreCase) < 0
            : string.Compare(category, "L", StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static bool IsWorkshopOpen(WorkshopReportRow row) =>
        string.Equals(row.JobClose, "N", StringComparison.OrdinalIgnoreCase)
        || (
            string.IsNullOrWhiteSpace(row.JobClose)
            && !row.CompleteDate.HasValue
            && !row.CompleteTime.HasValue
        );

    private static string WorkshopCategoryLabel(string? category) =>
        category?.Trim().ToUpperInvariant() switch
        {
            "M" => "Mechanical",
            "A" => "Accident",
            "V" => "Mech-Loss",
            _ => string.IsNullOrWhiteSpace(category) ? "-" : "Accident-Loss",
        };

    private static string WorkshopGarageLabel(string? garage) =>
        garage?.Trim().ToUpperInvariant() switch
        {
            "J" => "GG JHB",
            "P" => "GG PTA",
            _ => string.IsNullOrWhiteSpace(garage) ? "-" : garage,
        };

    private static (DateTime From, DateTime To) NormalizeWorkshopDateRange(
        IDictionary<string, string?> filters
    )
    {
        var startDate =
            GetDate(filters, "from")?.Date
            ?? GetDate(filters, "BDAT")?.Date
            ?? DateTime.Today.AddMonths(-1).Date;
        var endDate =
            GetDate(filters, "to")?.Date ?? GetDate(filters, "EDAT")?.Date ?? DateTime.Today.Date;
        if (endDate < startDate)
            (startDate, endDate) = (endDate, startDate);
        return (startDate, endDate);
    }

    private static int? CompletedHours(WorkshopReportRow row) =>
        row.ReceiveDate.HasValue
        && row.ReceiveTime.HasValue
        && row.CompleteDate.HasValue
        && row.CompleteTime.HasValue
            ? ElapsedHours(row, row.CompleteDate.Value.Date + row.CompleteTime.Value)
            : null;

    private static decimal? CompletedDays(WorkshopReportRow row)
    {
        var hours = CompletedHours(row);
        return hours.HasValue ? Math.Round(hours.Value / 24m, 2) : null;
    }

    private static int? ElapsedHours(WorkshopReportRow row, DateTime end)
    {
        if (!row.ReceiveDate.HasValue)
            return null;
        var start = row.ReceiveDate.Value.Date + (row.ReceiveTime ?? TimeSpan.Zero);
        var hours = (int)Math.Truncate((end - start).TotalHours);
        return hours >= 0 ? hours : null;
    }

    private static decimal? ElapsedDays(WorkshopReportRow row, DateTime end)
    {
        var hours = ElapsedHours(row, end);
        return hours.HasValue ? Math.Round(hours.Value / 24m, 2) : null;
    }

    private static LegacyReportResultDto CreateDynamicResult<T>(
        string title,
        string legacyTarget,
        bool isApproximate,
        string? approximationReason,
        IEnumerable<T> rows,
        params LegacyProjectionColumn[] columns
    )
    {
        var columnList = columns
            .Select(column => new LegacyReportColumnDto
            {
                Key = column.Header,
                Header = column.Header,
            })
            .ToList();

        var rowList = rows.Select(row =>
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
            TotalCount = rowList.Count,
        };
    }

    private static LegacyProjectionColumn Column(string header, Func<dynamic, object?> selector) =>
        new(header, row => selector(row));

    private static List<LegacyStoredProcedureParameter> BuildOptionalParameterList(
        params (string Name, object? Value, DbType DbType)[] parameters
    ) =>
        parameters
            .Where(parameter => parameter.Value is not null)
            .Select(parameter => new LegacyStoredProcedureParameter(
                parameter.Name,
                parameter.Value,
                parameter.DbType
            ))
            .ToList();

    private static IReadOnlyList<LegacyStoredProcedureParameter> BuildDateRangeParameters(
        IDictionary<string, string?> filters
    ) =>
        BuildOptionalParameterList(
            ("@StartDate", GetDate(filters, "from"), DbType.DateTime),
            ("@EndDate", GetDate(filters, "to"), DbType.DateTime)
        );

    private static (DateTime From, DateTime To) NormalizeDateRange(
        IDictionary<string, string?> filters
    )
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
    {
        if (filters.TryGetValue(key, out var value))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        // Most request dictionaries are ordinal-ignore-case, but callers of
        // the service can provide a regular dictionary.  Legacy parameter
        // names vary in casing (SearchType/searchType), so preserve that
        // compatibility at the service boundary too.
        var matchingPair = filters.FirstOrDefault(pair =>
            string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)
        );
        return string.IsNullOrWhiteSpace(matchingPair.Value) ? null : matchingPair.Value;
    }

    private static AssetListSearchType ResolveAssetListSearchType(
        IDictionary<string, string?> filters
    )
    {
        var explicitSearchType = GetInt(filters, "SearchType") ?? GetInt(filters, "search_type");
        if (explicitSearchType is >= 1 and <= 4)
        {
            return (AssetListSearchType)explicitSearchType.Value;
        }

        return GetString(filters, "rtype")?.Trim().ToLowerInvariant() switch
        {
            "province" or "by-province" or "asset-list-by-province" => AssetListSearchType.Province,
            "department" or "by-department" or "asset-list-by-department" =>
                AssetListSearchType.Department,
            "site" or "by-site" or "asset-list-by-site" => AssetListSearchType.Site,
            _ => AssetListSearchType.All,
        };
    }

    private static int? GetAssetListFilterId(
        IDictionary<string, string?> filters,
        AssetListSearchType searchType
    )
    {
        var explicitId = GetInt(filters, "id") ?? GetInt(filters, "Id");
        if (explicitId.HasValue)
        {
            return explicitId;
        }

        return searchType switch
        {
            AssetListSearchType.Province => GetInt(filters, "province")
                ?? GetInt(filters, "province_code")
                ?? GetInt(filters, "lstProvinceID"),
            AssetListSearchType.Department => GetInt(filters, "department")
                ?? GetInt(filters, "department_code")
                ?? GetInt(filters, "dept")
                ?? GetInt(filters, "DepartmentID")
                ?? GetInt(filters, "lstDepartmentID"),
            AssetListSearchType.Site => GetInt(filters, "site")
                ?? GetInt(filters, "site_code")
                ?? GetInt(filters, "SiteID")
                ?? GetInt(filters, "lstSites"),
            _ => null,
        };
    }

    private static string? JoinLegacyClassName(string? classNumber, string? description)
    {
        if (string.IsNullOrWhiteSpace(classNumber))
        {
            return description;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return classNumber;
        }

        return $"{classNumber} {description}";
    }

    private static bool IsCurrentContract(string? stillCurrent) =>
        string.Equals(stillCurrent, "y", StringComparison.OrdinalIgnoreCase);

    private static int? DifferenceInLegacyMonths(DateTime? start, DateTime? end)
    {
        if (!start.HasValue || !end.HasValue)
        {
            return null;
        }

        return ((end.Value.Year - start.Value.Year) * 12) + end.Value.Month - start.Value.Month + 1;
    }

    private static AssetListFallbackRow CreateAssetListFallbackRow(
        AssetListFallbackVehicle vehicle,
        AssetListFallbackContract? contract,
        IReadOnlyDictionary<string, string?> contractTypeDescriptions
    )
    {
        var contractType = contract?.ContractTypeCode;
        var contractTypeDescription =
            contractType is not null
            && contractTypeDescriptions.TryGetValue(contractType, out var description)
                ? description?.Trim()
                : contractType;
        var contractPeriodMonths = DifferenceInLegacyMonths(
            contract?.StartDate,
            contract?.ExpectedReturnDate
        );
        var monthsUsed = DifferenceInLegacyMonths(contract?.StartDate, contract?.ChargedUntil);

        return new AssetListFallbackRow(
            vehicle.Registration,
            vehicle.GgNumber,
            vehicle.VehicleStatus,
            vehicle.ModelDescription,
            vehicle.Colour,
            vehicle.ClassDescription,
            vehicle.EngineNumber,
            vehicle.ChassisNumber,
            vehicle.BarCode,
            vehicle.YearManufactured,
            vehicle.DatePurchased,
            vehicle.PurchaseAmount,
            vehicle.SourcedVia,
            vehicle.HireType,
            vehicle.VmfCode,
            contract?.StillCurrent,
            contract?.ContractCode,
            contractTypeDescription,
            contract?.StartDate,
            contract?.EndDate,
            contract?.ExpectedReturnDate,
            contract?.ChargedUntil,
            contractPeriodMonths,
            monthsUsed,
            monthsUsed.HasValue && contractPeriodMonths.HasValue
                ? monthsUsed.Value - contractPeriodMonths.Value
                : null,
            contract is null ? null : "empty",
            vehicle.DepartmentName,
            vehicle.SiteNameWithDepartment,
            JoinSiteResponsiblePerson(vehicle),
            vehicle.ProvinceName ?? "un-assigned",
            null,
            null
        );
    }

    private static string? JoinSiteResponsiblePerson(AssetListFallbackVehicle vehicle)
    {
        if (
            vehicle.SiteResponsiblePerson is null
            && vehicle.SiteTelephone is null
            && vehicle.SiteNetAddress is null
        )
        {
            return null;
        }

        return $"{vehicle.SiteResponsiblePerson} | {vehicle.SiteTelephone} | {vehicle.SiteNetAddress}";
    }

    private async Task<int?> ResolveLegacyVehicleVmfCodeAsync(
        string? search,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var term = search.Trim();

        return await _context
            .Vehicles.AsNoTracking()
            .Where(vehicle =>
                (
                    (vehicle.fleet_number != null && vehicle.fleet_number == term)
                    || (vehicle.registration_number != null && vehicle.registration_number == term)
                    || (vehicle.chassis_number != null && vehicle.chassis_number == term)
                    || (vehicle.engine_number_1 != null && vehicle.engine_number_1 == term)
                )
            )
            .OrderBy(vehicle => vehicle.vmf_code)
            .Select(vehicle => (int?)vehicle.vmf_code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> ResolveVehicleVmfCodeAsync(
        string? search,
        string? mode,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var term = search.Trim();
        var query = _context.Vehicles.AsNoTracking();

        query = NormalizeVehicleSearchMode(mode) switch
        {
            "GP" => query.Where(vehicle =>
                vehicle.registration_number != null && vehicle.registration_number == term
            ),
            "ENGINE" => query.Where(vehicle =>
                vehicle.engine_number_1 != null && vehicle.engine_number_1 == term
            ),
            "VIN" => query.Where(vehicle =>
                vehicle.chassis_number != null && vehicle.chassis_number == term
            ),
            "INVOICE" => query.Where(vehicle => false),
            _ => query.Where(vehicle =>
                vehicle.fleet_number != null && vehicle.fleet_number == term
            ),
        };

        return await query
            .OrderBy(vehicle => vehicle.vmf_code)
            .Select(vehicle => (int?)vehicle.vmf_code)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<int>> ResolveVehicleVmfCodesAsync(
        string? search,
        string? mode,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return new List<int>();
        }

        var term = search.Trim();
        var query = _context.Vehicles.AsNoTracking();

        query = NormalizeVehicleSearchMode(mode) switch
        {
            "GP" => query.Where(vehicle =>
                vehicle.registration_number != null && vehicle.registration_number.Contains(term)
            ),
            "ENGINE" => query.Where(vehicle =>
                vehicle.engine_number_1 != null && vehicle.engine_number_1.Contains(term)
            ),
            "VIN" => query.Where(vehicle =>
                vehicle.chassis_number != null && vehicle.chassis_number.Contains(term)
            ),
            "INVOICE" => query.Where(vehicle =>
                false
            ),
            _ => query.Where(vehicle =>
                vehicle.fleet_number != null && vehicle.fleet_number.Contains(term)
            ),
        };

        return await query
            .OrderBy(vehicle => vehicle.vmf_code)
            .Select(vehicle => vehicle.vmf_code)
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeVehicleSearchMode(string? mode) =>
        mode?.Trim().ToUpperInvariant() switch
        {
            "GP" => "GP",
            "ENGINE" => "ENGINE",
            "CHASSIS" => "VIN",
            "VIN" => "VIN",
            "INVOICE" => "INVOICE",
            _ => "GG",
        };

    private static string GetVehicleSearchModeLabel(string? mode) =>
        NormalizeVehicleSearchMode(mode) switch
        {
            "GP" => "GP number",
            "ENGINE" => "engine number",
            "VIN" => "VIN / chassis number",
            "INVOICE" => "invoice number",
            _ => "GG number",
        };

    private static short? GetShort(IDictionary<string, string?> filters, string key) =>
        short.TryParse(GetString(filters, key), out var value) ? value : null;

    private static int? GetInt(IDictionary<string, string?> filters, string key) =>
        int.TryParse(GetString(filters, key), out var value) ? value : null;

    private static DateTime? GetDate(IDictionary<string, string?> filters, string key) =>
        DateTime.TryParse(GetString(filters, key), out var value) ? value : null;

    private static int GetFinancialYear(IDictionary<string, string?> filters)
    {
        var explicitYear =
            GetInt(filters, "FinYear")
            ?? GetInt(filters, "finYear")
            ?? GetInt(filters, "financialYear");
        if (explicitYear.HasValue && explicitYear.Value > 0)
        {
            return explicitYear.Value;
        }

        var startYear = GetDate(filters, "from")?.Year;
        var endYear = GetDate(filters, "to")?.Year;
        return startYear ?? endYear ?? DateTime.Today.Year;
    }

    private static int GetFinancialYearKey(DateTime date) =>
        date.Month >= 4 ? date.Year : date.Year - 1;

    private static bool Contains(string? source, string term) =>
        !string.IsNullOrWhiteSpace(source)
        && source.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static string JoinVehicleLabel(string? ggNumber, string? gpNumber, int vmfCode) =>
        $"{(string.IsNullOrWhiteSpace(ggNumber) ? "-" : ggNumber)} / {(string.IsNullOrWhiteSpace(gpNumber) ? "-" : gpNumber)} ({vmfCode})";

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
            DateTime dateTime when dateTime.TimeOfDay == TimeSpan.Zero => dateTime.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            ),
            DateTime dateTime => dateTime.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            ),
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "Yes" : "No",
            decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
            double number => number.ToString("0.##", CultureInfo.InvariantCulture),
            float number => number.ToString("0.##", CultureInfo.InvariantCulture),
            JsonElement element => element.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }

    private sealed record LegacyReportDefinition(
        string Key,
        string Title,
        string LegacyTarget,
        string? StoredProcedureItem,
        Func<IDictionary<string, string?>, CancellationToken, Task<LegacyReportResultDto>> Fallback,
        string ApproximationReason = "",
        Func<
            IDictionary<string, string?>,
            IReadOnlyList<LegacyStoredProcedureParameter>
        >? BuildStoredProcedureParameters = null,
        string? StoredProcedureName = null
    );

    private sealed record LegacyReportPagination(int Page, int PageSize, bool IncludeAll);

    private sealed record LegacyReportPageWindow(int Page, int PageSize, long Skip, bool IsPaged);

    private sealed record LegacyReportDatabasePage<T>(
        IReadOnlyList<T> Rows,
        int TotalCount,
        LegacyReportPageWindow PageWindow
    );

    private sealed record LegacyStoredProcedureParameter(string Name, object? Value, DbType DbType);

    private sealed record AssetListFallbackVehicle(
        string? Registration,
        string? GgNumber,
        string? VehicleStatus,
        string? ModelDescription,
        string? Colour,
        string? ClassDescription,
        string? EngineNumber,
        string? ChassisNumber,
        string? BarCode,
        short? YearManufactured,
        DateTime? DatePurchased,
        decimal? PurchaseAmount,
        string? SourcedVia,
        string? HireType,
        int VmfCode,
        short? SiteCode,
        string? SiteName,
        string? SiteDepartmentNumber,
        string? SiteResponsiblePerson,
        string? SiteTelephone,
        string? SiteNetAddress,
        string? DepartmentNumber,
        string? DepartmentDescription,
        string? ProvinceName
    )
    {
        public string? DepartmentName =>
            JoinLegacyQuotedName(DepartmentNumber, DepartmentDescription);

        public string? SiteNameWithDepartment =>
            JoinLegacyQuotedName(SiteDepartmentNumber, SiteName);

        private static string? JoinLegacyQuotedName(string? number, string? description)
        {
            if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var value = string.Join(
                " ",
                new[] { number?.Trim(), description?.Trim() }.Where(part =>
                    !string.IsNullOrWhiteSpace(part)
                )
            );
            return $"'{value}'";
        }
    }

    private sealed record AssetListFallbackContract(
        int VmfCode,
        string? StillCurrent,
        int ContractCode,
        string? ContractTypeCode,
        DateTime StartDate,
        DateTime? EndDate,
        DateTime? ExpectedReturnDate,
        DateTime? ChargedUntil,
        short SiteCode,
        short? UserCode
    );

    private sealed record AssetListFallbackRow(
        string? Registration,
        string? GgNumber,
        string? VehicleStatus,
        string? ModelDescription,
        string? Colour,
        string? ClassDescription,
        string? EngineNumber,
        string? ChassisNumber,
        string? BarCode,
        short? YearManufactured,
        DateTime? DatePurchased,
        decimal? PurchaseAmount,
        string? SourcedVia,
        string? HireType,
        int VmfCode,
        string? ContractStillCurrent,
        int? ContractCode,
        string? ContractType,
        DateTime? ContractStartDate,
        DateTime? ContractEndDate,
        DateTime? ExpectedReturnDate,
        DateTime? ContractChargedUntil,
        int? ContractPeriodMonths,
        int? MonthsUsed,
        int? MonthsRemainingOrExceeded,
        string? ContractLastModifiedBy,
        string? DepartmentName,
        string? SiteName,
        string? SiteResponsiblePerson,
        string? Province,
        string? FixedTariff,
        string? KiloTariff
    );

    private sealed record TripDriverReportTable(string Name, IReadOnlySet<string> Columns);

    private sealed record DriverInformationFallbackRow(
        int? TripAuthorityCode,
        string? DriverName,
        string? DriverId,
        DateTime? TripDate,
        int? ContractCode,
        string? FleetNumber,
        string? RegistrationNumber,
        int? SiteCode
    );

    private sealed record ReportParameter(string Name, DbType Type, object? Value);

    private sealed record LegacyProjectionColumn(string Header, Func<object, object?> Selector);

    private sealed record WorkshopReportRow(
        short WorkshopCode,
        int? VmfCode,
        string? FleetNumber,
        string? RegistrationNumber,
        string? ModelDescription,
        int? CurrentOdo,
        DateTime? ReceiveDate,
        TimeSpan? ReceiveTime,
        DateTime? CompleteDate,
        TimeSpan? CompleteTime,
        string? ContactName,
        string? ContactTel,
        string? ContactFax,
        string? ContactEmail,
        string? AccidMech,
        string? Garage,
        string? DriverName,
        decimal? CallRefer,
        decimal? WorkshopKm,
        string? WorkshopRemarks,
        string? WorkshopReason,
        int? MerchantCode,
        decimal? RepairCost,
        DateTime? DateFromWorkshop,
        string? JobClose
    );

    private sealed record AssetVerificationVehicleLookup(
        int VmfCode,
        string? FleetNumber,
        string? RegistrationNumber,
        DateTime? LicenceDueDate
    );

    private sealed record AssetVerificationContractLookup(
        int VmfCode,
        short SiteCode,
        string? SiteName,
        string? DepartmentName
    );

    private sealed record AssetVerificationReportRow(
        int AssetVerificationCode,
        string? VehicleRegNo,
        int? VmfCode,
        string? DepartmentName,
        string? SiteName,
        short? SiteCode,
        string? Province,
        string? VehicleMake,
        string? VehicleModel,
        DateTime? LicenceExpiryDate,
        DateTime? DateLastVerified,
        string? Status,
        string? ResponsibleManager,
        int? CurrentKm,
        string? Barcode
    );

    private sealed record AssetVerificationNotVerifiedRow(
        int VmfCode,
        string? FleetNumber,
        string? RegistrationNumber,
        string? DepartmentName,
        string? SiteName,
        short SiteCode
    );

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
        string? Notes
    );
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
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    [JsonIgnore]
    public bool IsDatabasePaged { get; set; }
}

public sealed class LegacyReportColumnDto
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
}

public sealed class LegacyReportProcedureExecutionException : InvalidOperationException
{
    public LegacyReportProcedureExecutionException(string procedureName, Exception innerException)
        : base(
            $"The legacy report procedure {procedureName} exists but could not be executed. No compatibility fallback was used.",
            innerException
        )
    {
        ProcedureName = procedureName;
    }

    public string ProcedureName { get; }
}

public sealed class LegacyReportProcedureUnavailableException : InvalidOperationException
{
    public LegacyReportProcedureUnavailableException(string procedureName)
        : base(
            $"The required legacy report procedure {procedureName} is not available. No compatibility report fallback was used."
        )
    {
        ProcedureName = procedureName;
    }

    public string ProcedureName { get; }
}

public sealed class LegacyReportRelationUnavailableException : InvalidOperationException
{
    public LegacyReportRelationUnavailableException(string preferredObject, string fallbackTable)
        : base(
            $"Neither the legacy report relation {preferredObject} nor its table fallback {fallbackTable} is available."
        )
    {
        PreferredObject = preferredObject;
        FallbackTable = fallbackTable;
    }

    public string PreferredObject { get; }
    public string FallbackTable { get; }
}
