using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FIS.Api.Services.Finance;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Finance Department Report endpoints — one dedicated action per legacy report button.
///
/// Legacy route: Finance/GetFinancialReports.aspx?Mode=Department
///   then Finance/OpenReport.aspx?Report={name}&amp;OutputFormat={PDF|HTML}
///   or Taxis/RPT_dept_invoices_1.aspx (VIP/Taxi dedicated page)
///
/// Each endpoint maps to a specific DEV_REP_* stored procedure.
/// Posting-month filtering is applied server-side (not UI post-filtering).
///
/// Common query parameters for all report endpoints:
///   id             — department_code OR site_code (short)
///   batchDate      — legacy batch_date selected by DEV_SEL_BatchLookup
///   filterBy       — "Department" (default) or "Site"
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/reports")]
[Produces("application/json")]
[ServiceFilter(typeof(LegacyFinanceAuthorizationFilter))]
public class FinanceReportController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly IJournalDetailService _journalService;
    private readonly ITaxiRepository _taxiRepository;
    private readonly ITaxiLogRepository _taxiLogRepository;
    private readonly IPrivateHireRepository _privateHireRepository;
    private readonly LegacyFinanceAccessService _financeAccess;
    private readonly ILogger<FinanceReportController> _logger;

    public FinanceReportController(
        FisDbContext context,
        IJournalDetailService journalService,
        ITaxiRepository taxiRepository,
        ITaxiLogRepository taxiLogRepository,
        IPrivateHireRepository privateHireRepository,
        LegacyFinanceAccessService financeAccess,
        ILogger<FinanceReportController> logger
    )
    {
        _context = context;
        _journalService = journalService;
        _taxiRepository = taxiRepository;
        _taxiLogRepository = taxiLogRepository;
        _privateHireRepository = privateHireRepository;
        _financeAccess = financeAccess;
        _logger = logger;
    }

    // ─── Posting Month Lookup ─────────────────────────────────────────────────

    /// <summary>
    /// Returns legacy batch dates ordered newest-first.
    /// Frontend uses this to populate the posting-date dropdown before any report action.
    /// Legacy equivalent: DEV_SEL_BatchLookup
    /// </summary>
    [HttpGet("posting-months")]
    public async Task<ActionResult> GetPostingMonths(
        [FromQuery] string filterBy = "Department",
        [FromQuery] short id = 0
    )
    {
        try
        {
            var normalizedFilterBy = filterBy.Trim().ToLowerInvariant() switch
            {
                "site" => "Site",
                "province" => "Province",
                _ => "Department",
            };
            var isSite = normalizedFilterBy == "Site";
            var access = await _financeAccess.ResolveAsync(User, HttpContext.RequestAborted);
            var selectedId = id > 0
                ? id
                : isSite
                    ? access.Profile?.SiteCode ?? 0
                    : access.Profile?.DepartmentCode ?? 0;

            if (selectedId <= 0)
            {
                return BadRequest(new { error = "A Finance profile location is required." });
            }

            var allowed = isSite
                ? await _financeAccess.CanAccessSiteAsync(access, selectedId, HttpContext.RequestAborted)
                : await _financeAccess.CanAccessDepartmentAsync(
                    access,
                    selectedId,
                    HttpContext.RequestAborted
                );
            if (!allowed)
            {
                return Forbid();
            }

            var batchDates = await GetLegacyBatchDateOptionsAsync(
                selectedId,
                normalizedFilterBy,
                HttpContext.RequestAborted
            );
            return Ok(new { filter_by = normalizedFilterBy, filter_id = selectedId, months = batchDates });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching posting months");
            return StatusCode(500, new { error = "Failed to fetch posting months" });
        }
    }

    // ─── Invoice Summary ──────────────────────────────────────────────────────

    /// <summary>
    /// Summarised invoice per vehicle for the selected department/site and posting month.
    /// Covers: "Show Summarised Invoice" (PDF) and "Show HTML invoice" buttons.
    /// Legacy equivalent: DEV_REP_DetailedInvoicedReport (summary grouping)
    /// </summary>
    [HttpGet("invoice-summary")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetInvoiceSummary(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "SummaryInvoicedReport",
                "Summarised Invoice",
                id,
                batchDate,
                filterBy,
                cancellationToken: HttpContext.RequestAborted
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var rows = await _context
                .InvoiceItems.Where(ii => !ii.is_deleted)
                .Join(
                    _context.Invoices.Where(i =>
                        !i.is_deleted && i.posting_month_code == pm.posting_month_code
                    ),
                    ii => ii.invoice_code,
                    i => i.invoice_code,
                    (ii, i) => new { ii, dept_code = i.department_code }
                )
                .Where(x =>
                    filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase)
                        ? x.ii.site_code == id
                        : x.dept_code == id
                )
                .Join(
                    _context.Vehicles.Where(v => !v.is_deleted),
                    x => x.ii.vmf_code,
                    v => v.vmf_code,
                    (x, v) =>
                        new
                        {
                            x.ii.vmf_code,
                            v.fleet_number,
                            v.registration_number,
                            x.ii.contract_type,
                            x.ii.site_code,
                            x.ii.fixed_tariff_amount,
                            x.ii.odo_tariff_amount,
                            x.ii.start_odometer,
                            x.ii.end_odometer,
                        }
                )
                .GroupBy(x => new
                {
                    x.vmf_code,
                    x.fleet_number,
                    x.registration_number,
                    x.contract_type,
                    x.site_code,
                })
                .Select(g => new
                {
                    vmf_code = g.Key.vmf_code,
                    fleet_number = g.Key.fleet_number,
                    registration_number = g.Key.registration_number,
                    contract_type = g.Key.contract_type,
                    site_code = g.Key.site_code,
                    fixed_tariff_total = g.Sum(x => x.fixed_tariff_amount),
                    odo_tariff_total = g.Sum(x => x.odo_tariff_amount),
                    total_billed = g.Sum(x => x.fixed_tariff_amount + x.odo_tariff_amount),
                    start_odometer = g.Min(x => x.start_odometer),
                    end_odometer = g.Max(x => x.end_odometer),
                })
                .OrderBy(x => x.fleet_number)
                .ToListAsync();

            var siteMap = await BuildSiteMap(rows.Select(r => r.site_code).ToList());

            var enriched = rows.Select(r =>
                {
                    var found = siteMap.TryGetValue(r.site_code, out var site);
                    return new
                    {
                        r.vmf_code,
                        r.fleet_number,
                        r.registration_number,
                        r.contract_type,
                        site_name = found ? site.site_name : "",
                        department_name = found ? site.dept_name : "",
                        r.fixed_tariff_total,
                        r.odo_tariff_total,
                        r.total_billed,
                        r.start_odometer,
                        r.end_odometer,
                    };
                })
                .ToList();

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                total_vehicles = enriched.Count,
                grand_total = enriched.Sum(r => r.total_billed),
                rows = enriched,
            };
            return FormatResult(
                format,
                $"Invoice_Summary_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice summary");
            return StatusCode(500, new { error = "Failed to generate invoice summary" });
        }
    }

    // ─── Invoice by Cost Type ─────────────────────────────────────────────────

    /// <summary>
    /// Invoice totals grouped by journal_detail_type (cost category).
    /// Covers: "Show Invoice by Cost Type, Site Name &amp; Journal" button.
    /// Legacy equivalent: DEV_REP_SummaryInvoiceByJournalDetailType
    /// </summary>
    [HttpGet("invoice-by-cost-type")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetInvoiceByCostType(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "SummaryInvoiceByCostTypeAndJournal",
                "Invoice by Cost Type, Site and Journal",
                id,
                batchDate,
                filterBy,
                cancellationToken: HttpContext.RequestAborted
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var jdQuery = (await _journalService.GetAllJournalDetailsAsync()).Where(jd =>
                jd.journal_detail_date >= pm.PeriodStart
                && jd.journal_detail_date < pm.PeriodEnd
                && jd.journal_detail_isaccepted
            );

            jdQuery = filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase)
                ? jdQuery.Where(jd => jd.site_code == id)
                : jdQuery.Where(jd => jd.department_code == id);

            var journalDetailTypes = await _context.JournalDetailTypes.AsNoTracking().ToListAsync();
            var rows = jdQuery
                .Join(
                    journalDetailTypes,
                    jd => jd.journal_detail_type_code,
                    jdt => jdt.journal_detail_type_code,
                    (jd, jdt) => new { jd, jdt }
                )
                .GroupBy(x => new
                {
                    x.jdt.journal_detail_type_code,
                    x.jdt.journal_detail_type_name,
                    x.jdt.journal_detail_type_description,
                })
                .Select(g => new
                {
                    type_code = g.Key.journal_detail_type_code,
                    type_name = g.Key.journal_detail_type_name,
                    type_description = g.Key.journal_detail_type_description,
                    line_count = g.Count(),
                    total_amount = g.Sum(x => x.jd.journal_detail_amount),
                    total_quantity = g.Sum(x => x.jd.journal_detail_quantity),
                })
                .OrderBy(x => x.type_name)
                .ToList();

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                grand_total = rows.Sum(r => r.total_amount),
                rows,
            };
            return FormatResult(
                format,
                $"Invoice_By_CostType_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice by cost type");
            return StatusCode(500, new { error = "Failed to generate invoice by cost type" });
        }
    }

    // ─── Invoice Detailed (Table / Excel) ────────────────────────────────────

    /// <summary>
    /// Full line-item invoice detail per vehicle. Used for tabular display and Excel export.
    /// Covers: "Show Invoice in a Table" and "Download Excel invoice" buttons.
    /// Legacy equivalent: DEV_REP_DetailedInvoicedReport (full detail)
    /// </summary>
    [HttpGet("invoice-detailed")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetInvoiceDetailed(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] short? departmentCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "DetailedInvoicedReport",
                "Detailed Invoice",
                id,
                batchDate,
                filterBy,
                HttpContext.RequestAborted,
                departmentCode
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var rows = await _context
                .InvoiceItems.Where(ii => !ii.is_deleted)
                .Join(
                    _context.Invoices.Where(i =>
                        !i.is_deleted && i.posting_month_code == pm.posting_month_code
                    ),
                    ii => ii.invoice_code,
                    i => i.invoice_code,
                    (ii, i) => new { ii, dept_code = i.department_code }
                )
                .Where(x =>
                    filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase)
                        ? x.ii.site_code == id
                        : x.dept_code == id
                )
                .Join(
                    _context.PostingMonths.Where(p => !p.is_deleted),
                    x => pm.posting_month_code,
                    p => p.posting_month_code,
                    (x, p) =>
                        new
                        {
                            x.ii,
                            x.dept_code,
                            month_name = p.month_name,
                        }
                )
                .Join(
                    _context.Vehicles.Where(v => !v.is_deleted),
                    x => x.ii.vmf_code,
                    v => v.vmf_code,
                    (x, v) =>
                        new
                        {
                            x.ii.vmf_code,
                            v.fleet_number,
                            v.registration_number,
                            x.ii.contract_type,
                            x.ii.site_code,
                            x.ii.fixed_tariff_amount,
                            x.ii.odo_tariff_amount,
                            total = x.ii.fixed_tariff_amount + x.ii.odo_tariff_amount,
                            x.ii.start_odometer,
                            x.ii.end_odometer,
                            x.ii.start_odo_date,
                            x.ii.end_odo_date,
                            month = x.month_name,
                            department_code = x.dept_code,
                        }
                )
                .OrderBy(x => x.fleet_number)
                .ThenBy(x => x.start_odo_date)
                .ToListAsync();

            var siteMap = await BuildSiteMap(rows.Select(r => r.site_code).ToList());

            var enriched = rows.Select(r =>
                {
                    var found = siteMap.TryGetValue(r.site_code, out var site);
                    return new
                    {
                        r.vmf_code,
                        r.fleet_number,
                        r.registration_number,
                        r.contract_type,
                        r.fixed_tariff_amount,
                        r.odo_tariff_amount,
                        r.total,
                        r.start_odometer,
                        r.end_odometer,
                        r.start_odo_date,
                        r.end_odo_date,
                        r.month,
                        site_name = found ? site.site_name : "",
                        department_name = found ? site.dept_name : "",
                    };
                })
                .ToList();

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                total_lines = enriched.Count,
                grand_total = enriched.Sum(r => r.total),
                rows = enriched,
            };
            return FormatResult(
                format,
                $"Invoice_Detailed_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating detailed invoice");
            return StatusCode(500, new { error = "Failed to generate detailed invoice" });
        }
    }

    // ─── TAXI and VIP Transactions ────────────────────────────────────────────

    /// <summary>
    /// Taxi and VIP transaction report for the selected department/site and posting month.
    /// Covers: "Show TAXI and VIP transactions" button (HTML) and the PDF variant.
    /// Legacy equivalent: DEV_REP_DetailedInvoicedVIPandTaxiReport
    ///   + Taxis/RPT_dept_invoices_1.aspx (dedicated HTML page)
    /// contractor_id = 2 means VIP; all others are Taxi.
    /// </summary>
    [HttpGet("taxi-vip")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetTaxiVip(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "DetailedInvoicedVIPAndTAXIReport",
                "Detailed VIP and Taxi Invoice",
                id,
                batchDate,
                filterBy,
                cancellationToken: HttpContext.RequestAborted
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var taxis = await _taxiRepository.GetAllAsync();
            var taxisInPeriod = taxis
                .Where(t =>
                    (
                        filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase)
                            ? t.site_code == id
                            : t.department_code == id
                    )
                    && t.date_required >= pm.PeriodStart
                    && t.date_required < pm.PeriodEnd
                )
                .Select(t => new
                {
                    t.rek_num,
                    t.contractor_id,
                    t.official,
                    t.rank,
                    t.date_required,
                    t.site_code,
                    t.department_code,
                })
                .ToList();

            var rekNums = taxisInPeriod.Select(t => t.rek_num).ToList();

            var logs = (await _taxiLogRepository.GetAllAsync())
                .Where(tl => tl.rek_num is not null && rekNums.Contains(tl.rek_num))
                .Select(tl => new
                {
                    tl.rek_num,
                    tl.driver_start_date,
                    tl.distance,
                    tl.days,
                    hours = tl.hours,
                })
                .ToList();

            var logMap = logs.GroupBy(l => l.rek_num).ToDictionary(g => g.Key!, g => g.First());

            var contractorIds = taxisInPeriod
                .Where(t => t.contractor_id.HasValue)
                .Select(t => t.contractor_id!.Value)
                .Distinct()
                .ToList();

            var contractorMap = (await _privateHireRepository.GetContractorsAsync())
                .Where(contractor => contractorIds.Contains(contractor.contractor_id))
                .ToDictionary(contractor => contractor.contractor_id);

            var rows = taxisInPeriod
                .Select(t =>
                {
                    logMap.TryGetValue(t.rek_num ?? "", out var log);
                    var contractor = t.contractor_id.HasValue
                        ? contractorMap.GetValueOrDefault(t.contractor_id.Value)
                        : null;
                    return new
                    {
                        rek_num = t.rek_num,
                        contractor_name = contractor?.contractor_name ?? "",
                        is_vip = t.contractor_id == 2,
                        official = t.official ?? "",
                        rank = t.rank ?? "",
                        date_required = t.date_required,
                        driver_start_date = log?.driver_start_date,
                        distance_km = log?.distance,
                        days = log?.days,
                        hours = log?.hours,
                    };
                })
                .OrderBy(r => r.driver_start_date)
                .ToList();

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                total_trips = rows.Count,
                vip_count = rows.Count(r => r.is_vip),
                taxi_count = rows.Count(r => !r.is_vip),
                rows,
            };
            return FormatResult(
                format,
                $"Taxi_VIP_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating taxi/vip report");
            return StatusCode(500, new { error = "Failed to generate taxi/vip report" });
        }
    }

    // ─── Fuel Invoice ─────────────────────────────────────────────────────────

    /// <summary>
    /// Detailed fuel invoice for the selected department/site and posting month.
    /// Covers: "Show detailed FUEL Invoice" (PDF) and Excel download buttons.
    /// Legacy equivalent: DEV_REP_FuelDetailedInvoicedReport
    /// Fuel transactions identified via CostCategory.description containing "fuel".
    /// </summary>
    [HttpGet("fuel")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetFuel(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "FuelDetailedInvoicedReport",
                "Detailed Fuel Invoice",
                id,
                batchDate,
                filterBy,
                cancellationToken: HttpContext.RequestAborted
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var rows = await BuildDailyTransactionReport(
                id,
                pm.posting_month_code,
                filterBy,
                new[] { "fuel" }
            );

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                total_lines = rows.Count,
                rows,
            };
            return FormatResult(
                format,
                $"Fuel_Invoice_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fuel invoice");
            return StatusCode(500, new { error = "Failed to generate fuel invoice" });
        }
    }

    // ─── Toll and Oil Invoice ─────────────────────────────────────────────────

    /// <summary>
    /// Detailed Toll and Oil invoice for the selected department/site and posting month.
    /// Covers: "Show detailed TOLL and OIL Invoice" (PDF) and Excel download.
    /// Legacy equivalent: DEV_REP_DetailedInvoicedTollAndOil
    /// Toll/Oil identified via CostCategory.description containing "toll" or "oil".
    /// </summary>
    [HttpGet("toll-oil")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetTollOil(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "TollAndOilDetailedInvoicedReport",
                "Detailed Toll and Oil Invoice",
                id,
                batchDate,
                filterBy,
                cancellationToken: HttpContext.RequestAborted
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var rows = await BuildDailyTransactionReport(
                id,
                pm.posting_month_code,
                filterBy,
                new[] { "toll", "oil" }
            );

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                total_lines = rows.Count,
                rows,
            };
            return FormatResult(
                format,
                $"TollOil_Invoice_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating toll/oil invoice");
            return StatusCode(500, new { error = "Failed to generate toll/oil invoice" });
        }
    }

    // ─── Surcharge Invoice ────────────────────────────────────────────────────

    /// <summary>
    /// Surcharge invoice for the selected department/site and posting month.
    /// Covers: "Show Surcharge Invoice" (PDF) and Excel download buttons.
    /// Legacy equivalent: dev_rep_surchargedetailedinvoicedreport
    /// Uses the Surcharge table directly (not journal_detail).
    /// </summary>
    [HttpGet("surcharge")]
    [ServiceFilter(typeof(LegacyFinanceReportScopeFilter))]
    public async Task<ActionResult> GetSurcharge(
        [FromQuery] short id,
        [FromQuery] DateTime? batchDate,
        [FromQuery] short? postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var legacyResult = await TryExecuteLegacyProcedureAsync(
                "SurchargeDetailedInvoicedReport",
                "Detailed Surcharge Invoice",
                id,
                batchDate,
                filterBy,
                cancellationToken: HttpContext.RequestAborted
            );
            if (legacyResult is not null)
                return FormatResult(format, legacyResult.ReportName, legacyResult.Data);

            var pm = await GetPostingMonthInfo(batchDate, postingMonthCode);
            if (pm == null)
                return NotFound(new { error = "The selected legacy batch date was not found." });

            var query = _context.Surcharges.Where(s =>
                !s.is_deleted && s.TrxDate >= pm.PeriodStart && s.TrxDate < pm.PeriodEnd
            );

            query = filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase)
                ? query.Where(s => s.Site_code == id)
                : query.Where(s => s.Department == id.ToString());

            var rows = await query
                .OrderBy(s => s.TrxDate)
                .Select(s => new
                {
                    s.surcharge_code,
                    s.vmf_code,
                    s.RegNo1,
                    s.RegNo2,
                    s.Department,
                    s.Site_code,
                    s.Merchant,
                    s.TrxDate,
                    s.AuthorityNo,
                    s.ServiceType,
                    s.Same,
                })
                .ToListAsync();

            var vmfCodes = rows.Where(r => r.vmf_code.HasValue)
                .Select(r => r.vmf_code!.Value)
                .Distinct()
                .ToList();
            var vehicleMap = await _context
                .Vehicles.Where(v => vmfCodes.Contains(v.vmf_code) && !v.is_deleted)
                .Select(v => new
                {
                    v.vmf_code,
                    v.fleet_number,
                    v.registration_number,
                })
                .ToDictionaryAsync(v => v.vmf_code);

            var enriched = rows.Select(r =>
                {
                    var veh = r.vmf_code.HasValue
                        ? vehicleMap.GetValueOrDefault(r.vmf_code.Value)
                        : null;
                    return new
                    {
                        r.surcharge_code,
                        r.vmf_code,
                        fleet_number = veh?.fleet_number ?? "",
                        registration_number = veh?.registration_number ?? r.RegNo1 ?? "",
                        r.Department,
                        r.Site_code,
                        r.Merchant,
                        r.TrxDate,
                        r.AuthorityNo,
                        r.ServiceType,
                        r.Same,
                    };
                })
                .ToList();

            var result = new
            {
                filter_by = filterBy,
                filter_id = id,
                posting_month = pm,
                total_lines = enriched.Count,
                rows = enriched,
            };
            return FormatResult(
                format,
                $"Surcharge_{pm?.month_name}_{pm?.year_description}",
                result
            );
        }
        catch (LegacyFinanceProcedureContractException)
        {
            return LegacyProcedureContractMismatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating surcharge invoice");
            return StatusCode(500, new { error = "Failed to generate surcharge invoice" });
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<LegacyBatchDateOption>> GetLegacyBatchDateOptionsAsync(
        short filterId,
        string filterBy,
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
            if (await HasLegacyProcedureAsync(connection, "DEV_SEL_BatchLookup", cancellationToken))
            {
                await using var lookupCommand = connection.CreateCommand();
                lookupCommand.CommandText = "[dbo].[DEV_SEL_BatchLookup]";
                lookupCommand.CommandType = CommandType.StoredProcedure;
                AddParameter(lookupCommand, "@filterid", DbType.Int32, (int)filterId);
                AddParameter(lookupCommand, "@filterby", DbType.String, filterBy);

                var legacyOptions = new List<LegacyBatchDateOption>();
                await using var lookupReader = await lookupCommand.ExecuteReaderAsync(cancellationToken);
                var batchDateOrdinal = FindColumnOrdinal(lookupReader, "batch_date");
                if (batchDateOrdinal < 0)
                {
                    _logger.LogWarning(
                        "Legacy batch lookup did not return batch_date; using the compatibility selector."
                    );
                }
                else
                {
                    while (await lookupReader.ReadAsync(cancellationToken))
                    {
                        if (lookupReader.IsDBNull(batchDateOrdinal))
                        {
                            continue;
                        }

                        var batchDate = lookupReader.GetDateTime(batchDateOrdinal).Date;
                        legacyOptions.Add(
                            new LegacyBatchDateOption(
                                batchDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                                batchDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture)
                            )
                        );
                    }

                    return legacyOptions;
                }
            }

            await using var command = connection.CreateCommand();
            // This is the DEV_SEL_BatchLookup relationship expressed as a
            // parameterized compatibility query when the client procedure is
            // absent. It intentionally avoids expanded-schema audit fields.
            if (string.Equals(filterBy, "Site", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText = """
                    SELECT DISTINCT b.[batch_date]
                    FROM [dbo].[batch] AS b
                    INNER JOIN [dbo].[journal] AS j ON j.[batch_code] = b.[batch_code]
                    INNER JOIN [dbo].[journal_detail] AS jd
                        ON jd.[journal_code] = j.[journal_code]
                    WHERE jd.[site_code] = @filterId
                      AND b.[batch_date] IS NOT NULL
                    ORDER BY b.[batch_date] DESC
                    """;
            }
            else
            {
                command.CommandText = """
                    SELECT DISTINCT b.[batch_date]
                    FROM [dbo].[batch] AS b
                    INNER JOIN [dbo].[journal] AS j ON j.[batch_code] = b.[batch_code]
                    INNER JOIN [dbo].[journal_detail] AS jd
                        ON jd.[journal_code] = j.[journal_code]
                    WHERE jd.[department_code] = @filterId
                      AND b.[batch_date] IS NOT NULL
                    ORDER BY b.[batch_date] DESC
                    """;
            }
            command.CommandType = CommandType.Text;
            AddParameter(command, "@filterId", DbType.Int16, filterId);

            var options = new List<LegacyBatchDateOption>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var batchDate = reader.GetDateTime(0).Date;
                options.Add(
                    new LegacyBatchDateOption(
                        batchDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        batchDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture)
                    )
                );
            }

            if (options.Count > 0)
            {
                return options;
            }
        }
        catch (SqlException ex) when (ex.Number is 207 or 208 or 2812)
        {
            _logger.LogInformation(
                "Legacy batch lookup table is unavailable; falling back to posting months."
            );
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var fallbackMonths = string.Equals(filterBy, "Site", StringComparison.OrdinalIgnoreCase)
            ? await (
                    from item in _context.InvoiceItems
                    join invoice in _context.Invoices
                        on item.invoice_code equals invoice.invoice_code
                    join month in _context.PostingMonths
                        on invoice.posting_month_code equals month.posting_month_code
                    join year in _context.PostingYears
                        on month.posting_year_code equals year.posting_year_code
                    where item.site_code == filterId
                        && !item.is_deleted
                        && !invoice.is_deleted
                        && !month.is_deleted
                        && !year.is_deleted
                    select new { year.year_start_date, month.month_number }
                )
                .Distinct()
                .OrderByDescending(item => item.year_start_date)
                .ThenByDescending(item => item.month_number)
                .ToListAsync(cancellationToken)
            : await (
                    from invoice in _context.Invoices
                    join month in _context.PostingMonths
                        on invoice.posting_month_code equals month.posting_month_code
                    join year in _context.PostingYears
                        on month.posting_year_code equals year.posting_year_code
                    where invoice.department_code == filterId
                        && !invoice.is_deleted
                        && !month.is_deleted
                        && !year.is_deleted
                    select new { year.year_start_date, month.month_number }
                )
                .Distinct()
                .OrderByDescending(item => item.year_start_date)
                .ThenByDescending(item => item.month_number)
                .ToListAsync(cancellationToken);
        return fallbackMonths
            .Select(
                item =>
                {
                    var fallbackDate = new DateTime(
                        item.year_start_date.Year,
                        item.month_number,
                        1
                    )
                        .AddMonths(1)
                        .AddDays(-1);
                    return new LegacyBatchDateOption(
                        fallbackDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        fallbackDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture)
                    );
                }
            )
            .ToList();
    }

    private async Task<PostingMonthInfo?> GetPostingMonthInfo(
        DateTime? batchDate,
        short? postingMonthCode
    )
    {
        var months = _context.PostingMonths.Where(pm => !pm.is_deleted);
        if (batchDate.HasValue)
        {
            var selectedDate = batchDate.Value.Date;
            months = months.Where(pm => pm.month_number == selectedDate.Month);
            var byDate = await months
                .Join(
                    _context.PostingYears.Where(py => !py.is_deleted),
                    pm => pm.posting_year_code,
                    py => py.posting_year_code,
                    (pm, py) => new
                    {
                        pm.posting_month_code,
                        pm.month_name,
                        pm.month_number,
                        pm.is_closed,
                        year_description = py.description,
                        year_start = py.year_start_date,
                    }
                )
                .FirstOrDefaultAsync(item => item.year_start.Year == selectedDate.Year);

            if (byDate is not null)
            {
                return CreatePostingMonthInfo(
                    byDate.posting_month_code,
                    byDate.month_name,
                    byDate.month_number,
                    byDate.year_description,
                    byDate.is_closed,
                    byDate.year_start,
                    selectedDate
                );
            }
        }

        if (!postingMonthCode.HasValue || postingMonthCode.Value <= 0)
        {
            return null;
        }

        var result = await months
            .Where(pm => pm.posting_month_code == postingMonthCode.Value)
            .Join(
                _context.PostingYears.Where(py => !py.is_deleted),
                pm => pm.posting_year_code,
                py => py.posting_year_code,
                (pm, py) =>
                    new
                    {
                        pm.posting_month_code,
                        pm.month_name,
                        pm.month_number,
                        pm.is_closed,
                        year_description = py.description,
                        year_start = py.year_start_date,
                    }
            )
            .FirstOrDefaultAsync();

        if (result == null)
            return null;

        return CreatePostingMonthInfo(
            result.posting_month_code,
            result.month_name,
            result.month_number,
            result.year_description,
            result.is_closed,
            result.year_start,
            null
        );
    }

    private static PostingMonthInfo CreatePostingMonthInfo(
        short postingMonthCode,
        string? monthName,
        byte monthNumber,
        string? yearDescription,
        bool isClosed,
        DateTime yearStart,
        DateTime? selectedBatchDate
    )
    {
        var start = new DateTime(yearStart.Year, monthNumber, 1);
        return new PostingMonthInfo
        {
            posting_month_code = postingMonthCode,
            month_name = monthName,
            month_number = monthNumber,
            year_description = yearDescription,
            is_closed = isClosed,
            BatchDate = selectedBatchDate ?? start.AddMonths(1).AddDays(-1),
            PeriodStart = start,
            PeriodEnd = start.AddMonths(1),
        };
    }

    private async Task<LegacyProcedureResult?> TryExecuteLegacyProcedureAsync(
        string legacyReport,
        string reportName,
        short id,
        DateTime? batchDate,
        string filterBy,
        CancellationToken cancellationToken,
        short? departmentCode = null
    )
    {
        if (
            !batchDate.HasValue
            || !LegacyProcedureNames.TryGetValue(legacyReport, out var procedureName)
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
            await using (var existsCommand = connection.CreateCommand())
            {
                existsCommand.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
                existsCommand.CommandType = CommandType.Text;
                AddParameter(existsCommand, "@procedureName", DbType.String, $"dbo.{procedureName}");
                var exists = await existsCommand.ExecuteScalarAsync(cancellationToken);
                if (exists is null or DBNull)
                {
                    return null;
                }
            }

            var parameterNames = await GetLegacyProcedureParameterNamesAsync(
                connection,
                procedureName,
                cancellationToken
            );
            if (!parameterNames.Contains("@id") || !parameterNames.Contains("@batchdate"))
            {
                _logger.LogWarning(
                    "Legacy Finance procedure {ProcedureName} does not have the expected ID and BatchDate parameters.",
                    procedureName
                );
                throw new LegacyFinanceProcedureContractException(
                    procedureName,
                    ["@ID", "@BatchDate"]
                );
            }

            await using var command = connection.CreateCommand();
            switch (legacyReport)
            {
                case "SummaryInvoicedReport":
                case "DetailedInvoicedReport":
                    command.CommandText = "[dbo].[DEV_REP_DetailedInvoicedReport]";
                    break;
                case "SummaryInvoiceByCostTypeAndJournal":
                    command.CommandText = "[dbo].[DEV_REP_SummaryInvoiceByJournalDetailType]";
                    break;
                case "DetailedInvoicedVIPAndTAXIReport":
                    command.CommandText = "[dbo].[DEV_REP_DetailedInvoicedVIPandTaxiReport]";
                    break;
                case "FuelDetailedInvoicedReport":
                    command.CommandText = "[dbo].[DEV_REP_FuelDetailedInvoicedReport]";
                    break;
                case "TollAndOilDetailedInvoicedReport":
                    command.CommandText = "[dbo].[DEV_REP_DetailedInvoicedTollAndOil]";
                    break;
                case "SurchargeDetailedInvoicedReport":
                    command.CommandText = "[dbo].[DEV_REP_SurchargeDetailedInvoicedReport]";
                    break;
                default:
                    throw new InvalidOperationException("The requested legacy Finance report is not mapped.");
            }
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 180;
            AddParameter(command, "@ID", DbType.Int32, (int)id);
            AddParameter(command, "@BatchDate", DbType.DateTime, batchDate.Value.Date);

            if (parameterNames.Contains("@filterbysite"))
            {
                AddParameter(
                    command,
                    "@filterbysite",
                    DbType.String,
                    string.Equals(filterBy, "Site", StringComparison.OrdinalIgnoreCase) ? "1" : "0"
                );
            }
            else
            {
                // The legacy detailed province path sends the selected
                // department as the fourth parameter. Department and Site
                // reports send the legacy sentinel value 0 instead.
                if (parameterNames.Contains("@depcode"))
                {
                    AddParameter(command, "@DepCode", DbType.Int32, departmentCode ?? 0);
                }

                if (parameterNames.Contains("@filterby"))
                {
                    AddParameter(command, "@FilterBy", DbType.String, filterBy);
                }
            }

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var columns = GetColumnNames(reader);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var columnIndex = 0; columnIndex < reader.FieldCount; columnIndex++)
                {
                    row[columns[columnIndex]] = reader.IsDBNull(columnIndex)
                        ? null
                        : reader.GetValue(columnIndex);
                }

                rows.Add(row);
            }

            return new LegacyProcedureResult(
                reportName,
                new
                {
                    title = reportName,
                    source = "legacy-procedure",
                    filter_by = filterBy,
                    filter_id = id,
                    batch_date = batchDate.Value.Date,
                    total_lines = rows.Count,
                    rows,
                }
            );
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            _logger.LogInformation(
                "Legacy Finance procedure {ProcedureName} is unavailable; using the explicit compatibility fallback.",
                procedureName
            );
            return null;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, string> LegacyProcedureNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SummaryInvoicedReport"] = "DEV_REP_DetailedInvoicedReport",
            ["SummaryInvoiceByCostTypeAndJournal"] = "DEV_REP_SummaryInvoiceByJournalDetailType",
            ["DetailedInvoicedReport"] = "DEV_REP_DetailedInvoicedReport",
            ["DetailedInvoicedVIPAndTAXIReport"] = "DEV_REP_DetailedInvoicedVIPandTaxiReport",
            ["FuelDetailedInvoicedReport"] = "DEV_REP_FuelDetailedInvoicedReport",
            ["TollAndOilDetailedInvoicedReport"] = "DEV_REP_DetailedInvoicedTollAndOil",
            ["SurchargeDetailedInvoicedReport"] = "DEV_REP_SurchargeDetailedInvoicedReport",
        };

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<bool> HasLegacyProcedureAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OBJECT_ID(@procedureName, 'P');";
        command.CommandType = CommandType.Text;
        AddParameter(command, "@procedureName", DbType.String, $"dbo.{procedureName}");
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null and not DBNull;
    }

    private static int FindColumnOrdinal(DbDataReader reader, string expectedName)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (string.Equals(reader.GetName(index), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static async Task<IReadOnlySet<string>> GetLegacyProcedureParameterNamesAsync(
        DbConnection connection,
        string procedureName,
        CancellationToken cancellationToken
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [name]
            FROM [sys].[parameters]
            WHERE [object_id] = OBJECT_ID(@procedureName, 'P')
            """;
        command.CommandType = CommandType.Text;
        AddParameter(command, "@procedureName", DbType.String, $"dbo.{procedureName}");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                names.Add(reader.GetString(0));
            }
        }

        return names;
    }

    private static string[] GetColumnNames(DbDataReader reader)
    {
        var names = new string[reader.FieldCount];
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            var sourceName = reader.GetName(index);
            var name = string.IsNullOrWhiteSpace(sourceName) ? $"column_{index + 1}" : sourceName;
            var uniqueName = name;
            var duplicateIndex = 2;
            while (!used.Add(uniqueName))
            {
                uniqueName = $"{name}_{duplicateIndex++}";
            }

            names[index] = uniqueName;
        }

        return names;
    }

    private async Task<Dictionary<short, (string site_name, string dept_name)>> BuildSiteMap(
        IEnumerable<short> siteIds
    )
    {
        var ids = siteIds.Distinct().ToList();
        var raw = await _context
            .Sites.Where(s => ids.Contains(s.Site_code) && !s.is_deleted)
            .Join(
                _context.Departments.Where(d => !d.is_deleted),
                s => s.Depatrment_code,
                d => d.department_code,
                (s, d) =>
                    new
                    {
                        s.Site_code,
                        site_name = s.description ?? "",
                        dept_name = d.description ?? "",
                    }
            )
            .ToListAsync();

        return raw.ToDictionary(x => x.Site_code, x => (x.site_name, x.dept_name));
    }

    /// <summary>
    /// Shared helper for fuel / toll-oil daily transaction reports.
    /// Filters DailyTransaction by posting_month_code and by vehicles belonging to the
    /// dept/site, then further filters by CostCategory.description matching any of the
    /// supplied keyword terms (case-insensitive).
    /// </summary>
    private async Task<List<object>> BuildDailyTransactionReport(
        short id,
        short postingMonthCode,
        string filterBy,
        string[] descTerms
    )
    {
        // Determine which CostCategory codes to include (small lookup table — loaded in memory)
        var allCategories = await _context.CostCategories.Where(cc => !cc.is_deleted).ToListAsync();
        var matchCodes = allCategories
            .Where(cc =>
                descTerms.Any(t =>
                    cc.description != null
                    && cc.description.Contains(t, StringComparison.OrdinalIgnoreCase)
                )
            )
            .Select(cc => cc.cost_category_code)
            .ToHashSet();

        if (!matchCodes.Any())
            return new List<object>();

        // Resolve which vmf_codes belong to the dept or site
        List<int> vmfCodes;
        if (filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase))
        {
            vmfCodes = await _context
                .Contracts.Where(c => !c.is_deleted && c.still_current == "Y" && c.site_code == id)
                .Select(c => c.vmf_code)
                .Distinct()
                .ToListAsync();
        }
        else
        {
            var siteCodesForDept = await _context
                .Sites.Where(s => !s.is_deleted && s.Depatrment_code == id)
                .Select(s => s.Site_code)
                .ToListAsync();

            vmfCodes = await _context
                .Contracts.Where(c =>
                    !c.is_deleted
                    && c.still_current == "Y"
                    && siteCodesForDept.Contains(c.site_code)
                )
                .Select(c => c.vmf_code)
                .Distinct()
                .ToListAsync();
        }

        if (!vmfCodes.Any())
            return new List<object>();

        var txRows = await _context
            .DailyTransactions.Where(dt =>
                !dt.is_deleted
                && dt.posting_month_code == postingMonthCode
                && vmfCodes.Contains(dt.vmf_code)
                && matchCodes.Contains(dt.cost_category_code)
            )
            .OrderBy(dt => dt.vmf_code)
            .ThenBy(dt => dt.transaction_date)
            .Select(dt => new
            {
                dt.daily_transaction_code,
                dt.vmf_code,
                dt.cost_category_code,
                dt.transaction_date,
            })
            .ToListAsync();

        if (!txRows.Any())
            return new List<object>();

        var txVmfCodes = txRows.Select(r => r.vmf_code).Distinct().ToList();
        var vehicleMap = await _context
            .Vehicles.Where(v => txVmfCodes.Contains(v.vmf_code) && !v.is_deleted)
            .Select(v => new
            {
                v.vmf_code,
                v.fleet_number,
                v.registration_number,
            })
            .ToDictionaryAsync(v => v.vmf_code);

        var categoryMap = allCategories.ToDictionary(cc => cc.cost_category_code);

        return txRows
            .Select(r =>
            {
                vehicleMap.TryGetValue(r.vmf_code, out var veh);
                categoryMap.TryGetValue(r.cost_category_code, out var cat);
                return (object)
                    new
                    {
                        r.daily_transaction_code,
                        r.vmf_code,
                        fleet_number = veh?.fleet_number ?? "",
                        registration_number = veh?.registration_number ?? "",
                        cost_category = cat?.description ?? "",
                        r.transaction_date,
                    };
            })
            .ToList();
    }

    /// <summary>
    /// Serialises the response object in the requested format.
    /// json (default) → Ok(data) as normal JSON.
    /// html           → text/html page with a printable table (browser print = PDF).
    /// csv            → text/csv file download (opens in Excel).
    /// The "rows" property of the response object is used for html/csv column extraction.
    /// </summary>
    private ActionResult FormatResult(string format, string reportName, object data)
    {
        if (
            !format.Equals("html", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("csv", StringComparison.OrdinalIgnoreCase)
        )
            return Ok(data);

        // Serialize to JSON so we can extract rows generically
        var json = JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions { PropertyNamingPolicy = null }
        );
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
            return Ok(data); // fall back to JSON if no rows property

        var rows = rowsEl.EnumerateArray().ToList();

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var sb = new StringBuilder();
            if (rows.Count > 0)
            {
                var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();
                sb.AppendLine(string.Join(",", headers.Select(CsvQuote)));
                foreach (var row in rows)
                {
                    var values = row.EnumerateObject()
                        .Select(p =>
                            CsvQuote(
                                p.Value.ValueKind == JsonValueKind.Null ? "" : p.Value.ToString()
                            )
                        );
                    sb.AppendLine(string.Join(",", values));
                }
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"{reportName.Replace(" ", "_")}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        // HTML format — printable table suitable for browser print-to-PDF
        var htmlSb = new StringBuilder();
        htmlSb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        htmlSb.AppendLine($"<title>{System.Web.HttpUtility.HtmlEncode(reportName)}</title>");
        htmlSb.AppendLine("<style>body{font-family:Arial,sans-serif;font-size:11pt}");
        htmlSb.AppendLine("table{border-collapse:collapse;width:100%}");
        htmlSb.AppendLine("th,td{border:1px solid #ccc;padding:4px 8px;text-align:left}");
        htmlSb.AppendLine("th{background:#003366;color:#fff}");
        htmlSb.AppendLine("tr:nth-child(even){background:#f2f2f2}");
        htmlSb.AppendLine("h2{color:#003366}");
        htmlSb.AppendLine("@media print{@page{margin:1cm}}</style></head><body>");
        htmlSb.AppendLine(
            $"<h2>{System.Web.HttpUtility.HtmlEncode(reportName.Replace("_", " "))}</h2>"
        );

        if (rows.Count > 0)
        {
            htmlSb.AppendLine("<table><thead><tr>");
            var headers = rows[0].EnumerateObject().Select(p => p.Name).ToList();
            foreach (var h in headers)
                htmlSb.AppendLine(
                    $"<th>{System.Web.HttpUtility.HtmlEncode(h.Replace("_", " "))}</th>"
                );
            htmlSb.AppendLine("</tr></thead><tbody>");
            foreach (var row in rows)
            {
                htmlSb.AppendLine("<tr>");
                foreach (var prop in row.EnumerateObject())
                {
                    var val =
                        prop.Value.ValueKind == JsonValueKind.Null ? "" : prop.Value.ToString();
                    htmlSb.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(val)}</td>");
                }
                htmlSb.AppendLine("</tr>");
            }
            htmlSb.AppendLine("</tbody></table>");
        }
        else
        {
            htmlSb.AppendLine("<p>No data found for the selected period.</p>");
        }
        htmlSb.AppendLine("</body></html>");

        return Content(htmlSb.ToString(), "text/html; charset=utf-8");
    }

    private ObjectResult LegacyProcedureContractMismatch() =>
        StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new { error = "The legacy Finance report procedure has an incompatible parameter contract." }
        );

    private static string CsvQuote(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private sealed class PostingMonthInfo
    {
        public short posting_month_code { get; init; }
        public string? month_name { get; init; }
        public int month_number { get; init; }
        public string? year_description { get; init; }
        public bool is_closed { get; init; }
        public DateTime BatchDate { get; init; }
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }
    }

    private sealed record LegacyBatchDateOption(string value, string label);

    private sealed record LegacyProcedureResult(string ReportName, object Data);
}
