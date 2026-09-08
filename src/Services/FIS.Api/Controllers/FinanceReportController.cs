using System.Text;
using System.Text.Json;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
///   postingMonthCode — posting_month_code PK (short) from posting_month table
///   filterBy       — "Department" (default) or "Site"
/// </summary>
[ApiController]
[Authorize]
[Route("api/finance/reports")]
[Produces("application/json")]
public class FinanceReportController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ITaxiRepository _taxiRepository;
    private readonly ITaxiLogRepository _taxiLogRepository;
    private readonly IPrivateHireRepository _privateHireRepository;
    private readonly ILogger<FinanceReportController> _logger;

    public FinanceReportController(
        FisDbContext context,
        ITaxiRepository taxiRepository,
        ITaxiLogRepository taxiLogRepository,
        IPrivateHireRepository privateHireRepository,
        ILogger<FinanceReportController> logger
    )
    {
        _context = context;
        _taxiRepository = taxiRepository;
        _taxiLogRepository = taxiLogRepository;
        _privateHireRepository = privateHireRepository;
        _logger = logger;
    }

    // ─── Posting Month Lookup ─────────────────────────────────────────────────

    /// <summary>
    /// Returns available posting months ordered newest-first.
    /// Frontend uses this to populate the posting-month dropdown before any report action.
    /// Legacy equivalent: DEV_SEL_BatchLookup
    /// </summary>
    [HttpGet("posting-months")]
    public async Task<ActionResult> GetPostingMonths([FromQuery] string filterBy = "Department")
    {
        try
        {
            var months = await _context
                .PostingMonths.Where(pm => !pm.is_deleted)
                .Join(
                    _context.PostingYears.Where(py => !py.is_deleted),
                    pm => pm.posting_year_code,
                    py => py.posting_year_code,
                    (pm, py) =>
                        new
                        {
                            value = pm.posting_month_code,
                            label = pm.month_name + " (" + py.description + ")",
                            month_number = pm.month_number,
                            month_name = pm.month_name,
                            is_closed = pm.is_closed,
                            year_description = py.description,
                            year_start = py.year_start_date,
                            year_end = py.year_end_date,
                            sort_key = py.year_start_date.Year * 100 + (int)pm.month_number,
                        }
                )
                .OrderByDescending(x => x.sort_key)
                .ToListAsync();

            return Ok(new { filter_by = filterBy, months });
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
    public async Task<ActionResult> GetInvoiceSummary(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

            var rows = await _context
                .InvoiceItems.Where(ii => !ii.is_deleted)
                .Join(
                    _context.Invoices.Where(i =>
                        !i.is_deleted && i.posting_month_code == postingMonthCode
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
    public async Task<ActionResult> GetInvoiceByCostType(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

            var jdQuery = _context.JournalDetails.Where(jd =>
                jd.journal_detail_date >= pm.PeriodStart
                && jd.journal_detail_date < pm.PeriodEnd
                && jd.journal_detail_isaccepted
            );

            jdQuery = filterBy.Equals("Site", StringComparison.OrdinalIgnoreCase)
                ? jdQuery.Where(jd => jd.site_code == id)
                : jdQuery.Where(jd => jd.department_code == id);

            var rows = await jdQuery
                .Join(
                    _context.JournalDetailTypes,
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
                .ToListAsync();

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
    public async Task<ActionResult> GetInvoiceDetailed(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

            var rows = await _context
                .InvoiceItems.Where(ii => !ii.is_deleted)
                .Join(
                    _context.Invoices.Where(i =>
                        !i.is_deleted && i.posting_month_code == postingMonthCode
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
                    x => postingMonthCode,
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
    public async Task<ActionResult> GetTaxiVip(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

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
    public async Task<ActionResult> GetFuel(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

            var rows = await BuildDailyTransactionReport(
                id,
                postingMonthCode,
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
    public async Task<ActionResult> GetTollOil(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

            var rows = await BuildDailyTransactionReport(
                id,
                postingMonthCode,
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
    public async Task<ActionResult> GetSurcharge(
        [FromQuery] short id,
        [FromQuery] short postingMonthCode,
        [FromQuery] string filterBy = "Department",
        [FromQuery] string format = "json"
    )
    {
        try
        {
            var pm = await GetPostingMonthInfo(postingMonthCode);
            if (pm == null)
                return NotFound(new { error = $"Posting month {postingMonthCode} not found" });

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating surcharge invoice");
            return StatusCode(500, new { error = "Failed to generate surcharge invoice" });
        }
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<PostingMonthInfo?> GetPostingMonthInfo(short postingMonthCode)
    {
        var result = await _context
            .PostingMonths.Where(pm => pm.posting_month_code == postingMonthCode && !pm.is_deleted)
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

        var start = new DateTime(result.year_start.Year, (int)result.month_number, 1);
        return new PostingMonthInfo
        {
            posting_month_code = result.posting_month_code,
            month_name = result.month_name,
            month_number = (int)result.month_number,
            year_description = result.year_description,
            is_closed = result.is_closed,
            PeriodStart = start,
            PeriodEnd = start.AddMonths(1),
        };
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
        public DateTime PeriodStart { get; init; }
        public DateTime PeriodEnd { get; init; }
    }
}
