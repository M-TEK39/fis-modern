using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Reporting API endpoints
/// Provides vehicle, financial, maintenance, and trip reports with export capabilities
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportController : BaseApiController
{
    private readonly IReportingService _reportingService;
    private readonly ILegacyReportResultService _legacyReportResultService;
    private readonly FisDbContext _context;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportingService reportingService,
        ILegacyReportResultService legacyReportResultService,
        FisDbContext context,
        ILogger<ReportController> logger)
    {
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        _legacyReportResultService = legacyReportResultService ?? throw new ArgumentNullException(nameof(legacyReportResultService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Vehicle Reports


    /// <summary>
    /// Generate a legacy-style dynamic report grid using the requested report key and query-string filters.
    /// </summary>
    [HttpGet("dynamic/{reportKey}")]
    [ProducesResponseType(typeof(LegacyReportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegacyReportResultDto>> GetDynamicLegacyReport(string reportKey, CancellationToken cancellationToken)
    {
        try
        {
            var filters = Request.Query
                .ToDictionary(pair => pair.Key, pair => (string?)pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            if (filters.ContainsKey("view"))
            {
                filters.Remove("view");
            }

            ExpandLegacyParameterPairs(filters);
            NormalizeLegacyAliases(filters);

            var report = await _legacyReportResultService.GetReportAsync(reportKey, filters, cancellationToken);
            return Ok(report);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unknown legacy report key requested: {ReportKey}", reportKey);
            return NotFound(new { error = "Unknown legacy report key", reportKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dynamic legacy report {ReportKey}", reportKey);
            return StatusCode(500, new { error = "Failed to generate legacy report", message = ex.Message });
        }
    }

    private static void ExpandLegacyParameterPairs(IDictionary<string, string?> filters)
    {
        var hasNumberedParameters = filters.Keys.Any(key => key.StartsWith("ParamName", StringComparison.OrdinalIgnoreCase));
        if (!hasNumberedParameters)
        {
            return;
        }

        for (var index = 1; index <= 20; index++)
        {
            var nameKey = $"ParamName{index}";
            var valueKey = $"ParamValue{index}";

            if (!filters.TryGetValue(nameKey, out var parameterName) || string.IsNullOrWhiteSpace(parameterName))
            {
                continue;
            }

            filters.TryGetValue(valueKey, out var parameterValue);
            if (!filters.ContainsKey(parameterName))
            {
                filters[parameterName] = parameterValue;
            }
        }
    }

    private static void NormalizeLegacyAliases(IDictionary<string, string?> filters)
    {
        CopyAliasIfMissing(filters, "from", "StartDate");
        CopyAliasIfMissing(filters, "from", "FromDate");
        CopyAliasIfMissing(filters, "to", "EndDate");
        CopyAliasIfMissing(filters, "to", "ToDate");
        CopyAliasIfMissing(filters, "dept", "DepartmentID");
        CopyAliasIfMissing(filters, "site", "SiteID");
        CopyAliasIfMissing(filters, "search", "txtNum");
        CopyAliasIfMissing(filters, "vmf", "v_code");
    }

    private static void CopyAliasIfMissing(IDictionary<string, string?> filters, string canonicalKey, string aliasKey)
    {
        if (filters.ContainsKey(canonicalKey))
        {
            return;
        }

        if (filters.TryGetValue(aliasKey, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            filters[canonicalKey] = value;
        }
    }

    /// <summary>
    /// Generate detailed vehicle report
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    [ProducesResponseType(typeof(VehicleReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetVehicleReport(int vmfCode)
    {
        try
        {
            var report = await _reportingService.GenerateVehicleReportAsync(vmfCode);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating vehicle report for VMF {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to generate vehicle report", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate vehicle report as PDF
    /// </summary>
    [HttpGet("vehicle/{vmfCode}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetVehicleReportPdf(int vmfCode)
    {
        try
        {
            var pdfBytes = await _reportingService.GenerateVehicleReportPdfAsync(vmfCode);
            return File(pdfBytes, "application/pdf", $"VehicleReport_{vmfCode}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating vehicle PDF for VMF {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to generate PDF", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate master file report for all vehicles
    /// </summary>
    [HttpGet("masterfile")]
    [ProducesResponseType(typeof(MasterFileReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMasterFileReport([FromQuery] int? vmfCode = null)
    {
        try
        {
            var report = await _reportingService.GenerateMasterFileReportAsync(vmfCode);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating master file report");
            return StatusCode(500, new { error = "Failed to generate master file report", message = ex.Message });
        }
    }

    /// <summary>
    /// New and In-Service vehicles report.
    /// Returns all vehicles with status In-Service (1) or Out-of-Service (2),
    /// optionally filtered by vehicle source (vs_code), hire type (type_code), or location.
    /// Includes ALL sources — including TSS (vs_code = 6) — which were previously
    /// missing from the legacy report due to a gap in Contract_Type_Group_Mapping.
    /// ⚠️ ASSUMPTION: status_code 1 = In Service, 2 = Out of Service / New-awaiting-assignment.
    ///    Confirm with business unit — see QUESTIONS.md R-3.
    /// </summary>
    [HttpGet("new-in-service")]
    public async Task<ActionResult> GetNewAndInServiceReport(
        [FromQuery] byte? vs_code = null,
        [FromQuery] short? type_code = null,
        [FromQuery] short? location_code = null,
        [FromQuery] short? make_code = null,
        [FromQuery] short? model_code = null,
        [FromQuery] short? vehicle_status_code = null,
        [FromQuery] string? search = null)
    {
        try
        {
            // Base: new (status 2) and in-service (status 1) vehicles,
            // unless a specific status is requested
            var query = _context.Vehicles
                .Where(v => !v.is_deleted &&
                    (vehicle_status_code.HasValue
                        ? v.vehicle_status_code == vehicle_status_code.Value
                        : (v.vehicle_status_code == 1 || v.vehicle_status_code == 2)))
                .AsQueryable();

            if (vs_code.HasValue)
                query = query.Where(v => v.vs_code == vs_code.Value);

            if (type_code.HasValue)
                query = query.Where(v => v.type_code == type_code.Value);

            if (location_code.HasValue)
                query = query.Where(v => v.location_code == location_code.Value);

            if (make_code.HasValue)
                query = query.Include(v => v.Model)
                             .Where(v => v.Model != null && v.Model.make_code == make_code.Value);

            if (model_code.HasValue)
                query = query.Where(v => v.model_code == model_code.Value);

            // Free-text search: fleet number, registration, chassis, engine, invoice number
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(v =>
                    (v.fleet_number        != null && v.fleet_number.ToLower().Contains(term)) ||
                    (v.registration_number != null && v.registration_number.ToLower().Contains(term)) ||
                    (v.chassis_number      != null && v.chassis_number.ToLower().Contains(term)) ||
                    (v.engine_number_1     != null && v.engine_number_1.ToLower().Contains(term)) ||
                    (v.invoice_number      != null && v.invoice_number.ToLower().Contains(term)));
            }

            var vehicles = await query
                .OrderBy(v => v.fleet_number)
                .Select(v => new
                {
                    v.vmf_code,
                    v.fleet_number,
                    v.registration_number,
                    v.vehicle_status_code,
                    v.type_code,
                    v.vs_code,
                    v.model_code,
                    v.location_code,
                    v.chassis_number,
                    v.engine_number_1,
                    v.year_manufactured,
                    v.take_on_date,
                    v.invoice_number,
                    v.date_created,
                    v.current_odo
                })
                .ToListAsync();

            // ── Load all reference lookups in parallel ───────────────────────
            var sourcesTask = _context.VehicleSources
                .Where(s => !s.is_deleted)
                .ToDictionaryAsync(s => s.vs_code, s => s.name);

            var sitesTask = _context.Sites
                .Where(s => !s.is_deleted)
                .ToDictionaryAsync(s => s.Site_code, s => s.description ?? string.Empty);

            var statusesTask = _context.VehicleStatuses
                .ToDictionaryAsync(s => s.vehicle_status_code, s => s.status_description ?? string.Empty);

            var typesTask = _context.VehicleTypes
                .Where(t => !t.is_deleted)
                .ToDictionaryAsync(t => t.type_code, t => t.type_description);

            var modelsTask = _context.Models
                .Include(m => m.Make)
                .Where(m => !m.is_deleted)
                .ToDictionaryAsync(m => m.model_code,
                    m => new { make = m.Make != null ? m.Make.make_description : string.Empty, model = m.model_description });

            await Task.WhenAll(sourcesTask, sitesTask, statusesTask, typesTask, modelsTask);

            var allSources  = await sourcesTask;
            var allSites    = await sitesTask;
            var allStatuses = await statusesTask;
            var allTypes    = await typesTask;
            var allModels   = await modelsTask;

            // Fetch latest active remark per vehicle for the report
            var vmfCodes = vehicles.Select(v => v.vmf_code).ToList();
            var activeRemarks = await _context.VehicleRemarks
                .Where(r => !r.is_deleted && !r.is_resolved && vmfCodes.Contains(r.vmf_code))
                .OrderByDescending(r => r.date_created)
                .Select(r => new { r.vmf_code, r.remark_id, r.remark_category, r.remark_text, r.date_created })
                .ToListAsync();

            // Keep only the most recent active remark per vehicle
            var latestRemark = activeRemarks
                .GroupBy(r => r.vmf_code)
                .ToDictionary(g => g.Key, g => g.First());

            var result = vehicles.Select(v =>
            {
                string? hiredFrom = v.vs_code.HasValue && allSources.TryGetValue(v.vs_code.Value, out var srcName) ? srcName : null;
                allSites.TryGetValue(v.location_code, out var siteName);
                allStatuses.TryGetValue(v.vehicle_status_code, out var statusText);
                allTypes.TryGetValue(v.type_code, out var typeDesc);
                allModels.TryGetValue(v.model_code, out var modelInfo);
                latestRemark.TryGetValue(v.vmf_code, out var remark);

                return new
                {
                    v.vmf_code,
                    v.fleet_number,
                    v.registration_number,
                    v.vehicle_status_code,
                    status_text = statusText ?? (v.vehicle_status_code == 1 ? "In Service" : "Out of Service"),
                    v.type_code,
                    type_description = typeDesc ?? string.Empty,
                    v.vs_code,
                    hired_from = hiredFrom,
                    v.model_code,
                    make_description = modelInfo?.make ?? string.Empty,
                    model_description = modelInfo?.model ?? string.Empty,
                    v.location_code,
                    site_name = siteName ?? string.Empty,
                    v.chassis_number,
                    v.engine_number_1,
                    v.year_manufactured,
                    v.take_on_date,
                    v.invoice_number,
                    v.date_created,
                    v.current_odo,
                    // Active remark (null if none)
                    active_remark = remark == null ? null : (object)new
                    {
                        remark.remark_id,
                        remark.remark_category,
                        remark.remark_text,
                        remark.date_created
                    }
                };
            }).ToList();

            _logger.LogInformation(
                "New/In-Service report: {Count} vehicles (search={Search}, vs_code={VsCode}, type={TypeCode}, location={Loc}, make={Make}, model={Model}, status={Status})",
                result.Count, search, vs_code, type_code, location_code, make_code, model_code, vehicle_status_code);

            return Ok(new
            {
                total_count = result.Count,
                filters_applied = new { search, vs_code, type_code, location_code, make_code, model_code, vehicle_status_code },
                assumption_note = "Status 1=InService, 2=OutOfService treated as New/Available. Confirm with business unit.",
                vehicles = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating New/In-Service report");
            return StatusCode(500, new { error = "Failed to generate New/In-Service report", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate universal vehicle report with custom filters
    /// </summary>
    [HttpPost("universal")]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUniversalReport([FromBody] UniversalReportRequest request)
    {
        try
        {
            var report = await _reportingService.GenerateUniversalReportAsync(request);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating universal report");
            return StatusCode(500, new { error = "Failed to generate universal report", message = ex.Message });
        }
    }

    #endregion

    #region Maintenance Reports

    /// <summary>
    /// Generate service history report for a vehicle
    /// </summary>
    [HttpGet("maintenance/history/{vmfCode}")]
    [ProducesResponseType(typeof(ServiceHistoryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetServiceHistory(int vmfCode)
    {
        try
        {
            var report = await _reportingService.GenerateServiceHistoryReportAsync(vmfCode);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating service history for VMF {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to generate service history", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate maintenance cost report
    /// </summary>
    [HttpGet("maintenance/cost")]
    [ProducesResponseType(typeof(MaintenanceCostReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetMaintenanceCostReport(
        [FromQuery] int? vmfCode,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var report = await _reportingService.GenerateMaintenanceCostReportAsync(vmfCode, startDate, endDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maintenance cost report");
            return StatusCode(500, new { error = "Failed to generate maintenance cost report", message = ex.Message });
        }
    }

    #endregion

    #region Financial Reports

    /// <summary>
    /// Generate vehicle billing history
    /// </summary>
    [HttpGet("billing/history/{vmfCode}")]
    [ProducesResponseType(typeof(VehicleBillingHistoryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetBillingHistory(int vmfCode, [FromQuery] int financialYear)
    {
        try
        {
            var report = await _reportingService.GenerateVehicleBillingHistoryAsync(vmfCode, financialYear);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating billing history for VMF {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to generate billing history", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate contract billing report
    /// </summary>
    [HttpGet("contract/billing/{contractId}")]
    [ProducesResponseType(typeof(ContractBillingReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetContractBilling(
        int contractId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var report = await _reportingService.GenerateContractBillingReportAsync(contractId, startDate, endDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contract billing for contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to generate contract billing", message = ex.Message });
        }
    }

    #endregion

    #region Trip Reports

    /// <summary>
    /// Generate trip summary report
    /// </summary>
    [HttpGet("trip/summary")]
    [ProducesResponseType(typeof(TripSummaryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTripSummary(
        [FromQuery] int? vmfCode,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var report = await _reportingService.GenerateTripSummaryReportAsync(vmfCode, startDate, endDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trip summary");
            return StatusCode(500, new { error = "Failed to generate trip summary", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate trip detail report
    /// </summary>
    [HttpGet("trip/detail/{tripId}")]
    [ProducesResponseType(typeof(TripDetailReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTripDetail(int tripId)
    {
        try
        {
            var report = await _reportingService.GenerateTripDetailReportAsync(tripId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trip detail for trip {TripId}", tripId);
            return StatusCode(500, new { error = "Failed to generate trip detail", message = ex.Message });
        }
    }

    #endregion

    #region Contract Reports

    /// <summary>
    /// Generate contract summary report
    /// </summary>
    [HttpGet("contract/summary")]
    [ProducesResponseType(typeof(ContractSummaryReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetContractSummary([FromQuery] int? contractId = null)
    {
        try
        {
            var report = await _reportingService.GenerateContractSummaryReportAsync(contractId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating contract summary");
            return StatusCode(500, new { error = "Failed to generate contract summary", message = ex.Message });
        }
    }

    /// <summary>
    /// Generate authority report for contract
    /// </summary>
    [HttpGet("contract/authority/{contractId}")]
    [ProducesResponseType(typeof(AuthorityReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuthorityReport(int contractId)
    {
        try
        {
            var report = await _reportingService.GenerateAuthorityReportAsync(contractId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating authority report for contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to generate authority report", message = ex.Message });
        }
    }

    #endregion

    #region Export Functions

    /// <summary>
    /// Export data to CSV
    /// </summary>
    [HttpPost("export/csv")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportToCsv([FromBody] ExportRequest request)
    {
        try
        {
            // This would need to handle generic data export
            // For now, return placeholder
            _logger.LogInformation("CSV export requested for {Filename}", request.Filename);

            var csvBytes = await _reportingService.ExportToCsvAsync(request.Data, request.Filename);
            return File(csvBytes, "text/csv", request.Filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to CSV");
            return StatusCode(500, new { error = "Failed to export to CSV", message = ex.Message });
        }
    }

    /// <summary>
    /// Export data to Excel
    /// </summary>
    [HttpPost("export/excel")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportToExcel([FromBody] ExportRequest request)
    {
        try
        {
            _logger.LogInformation("Excel export requested for {Filename}", request.Filename);

            var excelBytes = await _reportingService.ExportToExcelAsync(request.Data, request.Filename);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", request.Filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting to Excel");
            return StatusCode(500, new { error = "Failed to export to Excel", message = ex.Message });
        }
    }

    #endregion

    #region Report Metadata

    /// <summary>
    /// Get list of available report definitions
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(List<ReportDefinition>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAvailableReports()
    {
        try
        {
            var reports = await _reportingService.GetAvailableReportsAsync();
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available reports");
            return StatusCode(500, new { error = "Failed to retrieve available reports", message = ex.Message });
        }
    }

    /// <summary>
    /// Get report help information
    /// </summary>
    [HttpGet("help")]
    [ProducesResponseType(typeof(ReportHelpDto), StatusCodes.Status200OK)]
    public ActionResult GetHelp()
    {
        var help = new ReportHelpDto
        {
            Title = "Reporting System Help",
            Description = "Generate comprehensive reports for vehicles, contracts, maintenance, and financial data",
            Sections = new List<HelpSectionDto>
            {
                new HelpSectionDto
                {
                    Title = "Vehicle Reports",
                    Content = "Access detailed vehicle information including history, maintenance, and utilization"
                },
                new HelpSectionDto
                {
                    Title = "Financial Reports",
                    Content = "Review billing, contract costs, and financial summaries"
                },
                new HelpSectionDto
                {
                    Title = "Export Options",
                    Content = "Export report data to CSV or Excel formats"
                }
            }
        };
        return Ok(help);
    }

    /// <summary>
    /// Request additional report data or custom report generation
    /// </summary>
    [HttpPost("request-additional")]
    [ProducesResponseType(typeof(ReportRequestResultDto), StatusCodes.Status200OK)]
    public ActionResult RequestAdditional([FromBody] AdditionalReportRequestDto request)
    {
        // TODO: Implement custom report request handling
        _logger.LogInformation("Additional report requested: {ReportType}", request.ReportType);
        var result = new ReportRequestResultDto
        {
            Success = true,
            RequestId = Guid.NewGuid().ToString(),
            Message = "Report request submitted successfully",
            EstimatedCompletionTime = DateTime.Now.AddMinutes(5)
        };
        return Ok(result);
    }

    /// <summary>
    /// Get audit trail sourced from contract audit log entries.
    /// Filters: startDate, endDate (inclusive), userId (performed_by_user_code).
    /// </summary>
    [HttpGet("audit-trail")]
    [ProducesResponseType(typeof(ReportAuditTrailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuditTrail(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? userId)
    {
        try
        {
            var query = _context.ContractAuditLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(a => a.performed_at >= startDate.Value.Date);

            if (endDate.HasValue)
                query = query.Where(a => a.performed_at <= endDate.Value.Date.AddDays(1).AddSeconds(-1));

            if (!string.IsNullOrWhiteSpace(userId) && int.TryParse(userId, out var userCode))
                query = query.Where(a => a.performed_by_user_code == userCode);

            var entries = await query
                .OrderByDescending(a => a.performed_at)
                .Take(500)
                .Select(a => new AuditEntryDto
                {
                    AuditId = a.id,
                    ReportType = "Contract",
                    UserId = a.performed_by_user_code.ToString(),
                    AccessedDate = a.performed_at,
                    Action = a.action
                })
                .ToListAsync();

            _logger.LogInformation("Audit trail requested: {Count} entries", entries.Count);
            return Ok(new ReportAuditTrailDto { Entries = entries, TotalCount = entries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audit trail report");
            return StatusCode(500, new { error = "Failed to generate audit trail report", message = ex.Message });
        }
    }

    /// <summary>
    /// Registration certificates report — legacy scan_docs-backed certificate listing.
    /// Filters: vmfCode (optional), search/mode for GG, GP, engine, VIN/chassis, or invoice lookup, departmentCode reserved.
    /// </summary>
    [HttpGet("registration-certificates")]
    [ProducesResponseType(typeof(RegistrationCertificatesReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetRegistrationCertificates(
        [FromQuery] int? vmfCode,
        [FromQuery] int? departmentCode,
        [FromQuery] string? mode,
        [FromQuery] string? search)
    {
        try
        {
            var normalizedMode = mode?.Trim().ToUpperInvariant() switch
            {
                "GP" => "GP",
                "ENGINE" => "ENGINE",
                "CHASSIS" => "VIN",
                "VIN" => "VIN",
                "INVOICE" => "INVOICE",
                _ => "GG"
            };
            var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var query =
                from scanDoc in _context.ScanDocs.AsNoTracking()
                join vehicle in _context.Vehicles.AsNoTracking() on scanDoc.vmf_code equals vehicle.vmf_code
                where !scanDoc.is_deleted && !vehicle.is_deleted
                select new
                {
                    vehicle.vmf_code,
                    vehicle.fleet_number,
                    vehicle.registration_number,
                    vehicle.chassis_number,
                    vehicle.engine_number_1,
                    vehicle.invoice_number,
                    scanDoc.period_begin,
                    scanDoc.period_end,
                    scanDoc.image,
                    DateUploaded = scanDoc.date_updated ?? scanDoc.date_created
                };

            if (vmfCode.HasValue)
            {
                query = query.Where(row => row.vmf_code == vmfCode.Value);
            }
            else if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = normalizedMode switch
                {
                    "GP" => query.Where(row => row.registration_number != null && row.registration_number.Contains(normalizedSearch)),
                    "ENGINE" => query.Where(row => row.engine_number_1 != null && row.engine_number_1.Contains(normalizedSearch)),
                    "VIN" => query.Where(row => row.chassis_number != null && row.chassis_number.Contains(normalizedSearch)),
                    "INVOICE" => query.Where(row => row.invoice_number != null && row.invoice_number.Contains(normalizedSearch)),
                    _ => query.Where(row => row.fleet_number != null && row.fleet_number.Contains(normalizedSearch))
                };
            }

            var certificates = await query
                .OrderBy(row => row.fleet_number)
                .ThenBy(row => row.period_begin)
                .ThenBy(row => row.registration_number)
                .Select(row => new CertificateDto
                {
                    VmfCode = row.vmf_code,
                    FleetNumber = row.fleet_number ?? string.Empty,
                    RegistrationNumber = row.registration_number ?? string.Empty,
                    PeriodFrom = row.period_begin,
                    PeriodTo = row.period_end,
                    DateUploaded = row.DateUploaded,
                    RegistrationCertificate = row.image ?? string.Empty
                })
                .ToListAsync();

            _logger.LogInformation("Registration certificates report: {Count} records", certificates.Count);
            return Ok(new RegistrationCertificatesReportDto { Certificates = certificates, TotalCount = certificates.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating registration certificates report");
            return StatusCode(500, new { error = "Failed to generate registration certificates report", message = ex.Message });
        }
    }

    /// <summary>
    /// Capture activity report — shows all records captured within a date range,
    /// broken down per module with summary counts and individual record details.
    /// Filters: date_from (required), date_to (default today), module, site_code, vmf_code, captured_by.
    /// Modules: All | Vehicles | Contracts | Accidents | Fines | JobCards | Logbooks | Documents | Remarks
    /// </summary>
    [HttpGet("capture-activity")]
    public async Task<ActionResult> GetCaptureActivityReport(
        [FromQuery] DateTime date_from,
        [FromQuery] DateTime? date_to = null,
        [FromQuery] string? module = null,
        [FromQuery] short? site_code = null,
        [FromQuery] int? vmf_code = null,
        [FromQuery] int? captured_by = null)
    {
        try
        {
            var toDate = (date_to ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1); // end of day
            var fromDate = date_from.Date;
            var moduleFilter = string.IsNullOrWhiteSpace(module) ? "All" : module.Trim();

            var filtersApplied = new Dictionary<string, object?>();
            filtersApplied["date_from"] = fromDate.ToString("yyyy-MM-dd");
            filtersApplied["date_to"] = toDate.Date.ToString("yyyy-MM-dd");
            if (moduleFilter != "All") filtersApplied["module"] = moduleFilter;
            if (site_code.HasValue) filtersApplied["site_code"] = site_code;
            if (vmf_code.HasValue) filtersApplied["vmf_code"] = vmf_code;
            if (captured_by.HasValue) filtersApplied["captured_by"] = captured_by;

            // Build a lookup of vmf_code → (fleet_number, registration_number, veh_site_code) to enrich results
            // Only load vehicles that match site/vmf filters to keep query light
            var vehicleBase = _context.Vehicles.Where(v => !v.is_deleted);
            if (vmf_code.HasValue) vehicleBase = vehicleBase.Where(v => v.vmf_code == vmf_code.Value);
            if (site_code.HasValue) vehicleBase = vehicleBase.Where(v => v.veh_site_code == site_code.Value);
            var vehicleMap = await vehicleBase
                .Select(v => new { v.vmf_code, v.fleet_number, v.registration_number, v.veh_site_code })
                .ToDictionaryAsync(v => v.vmf_code, v => v);

            // Helper: resolve vehicle info
            string? FleetNum(int? code) => (code.HasValue && vehicleMap.TryGetValue(code.Value, out var fv)) ? fv.fleet_number : null;
            string? RegNum(int? code) => (code.HasValue && vehicleMap.TryGetValue(code.Value, out var rv)) ? rv.registration_number : null;

            // Helper: check if a vmf_code passes site/vmf filter
            bool VehicleInScope(int? code)
            {
                if (!vmf_code.HasValue && !site_code.HasValue) return true;
                if (code == null) return false;
                return vehicleMap.ContainsKey(code.Value);
            }

            var summary = new Dictionary<string, int>();
            var details = new Dictionary<string, List<object>>();

            // ── Vehicles ─────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Vehicles")
            {
                var q = _context.Vehicles
                    .Where(v => !v.is_deleted
                        && v.date_created >= fromDate && v.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(v => v.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(v => v.vmf_code == vmf_code.Value);
                if (site_code.HasValue) q = q.Where(v => v.veh_site_code == site_code.Value);
                var rows = await q.OrderByDescending(v => v.date_created)
                    .Select(v => new CaptureActivityEntry
                    {
                        record_id = v.vmf_code,
                        vmf_code = v.vmf_code,
                        fleet_number = v.fleet_number,
                        registration_number = v.registration_number,
                        description = $"Vehicle {v.fleet_number ?? v.registration_number ?? v.vmf_code.ToString()} added",
                        date_captured = v.date_created,
                        captured_by_user_code = v.created_by_user_code,
                        module = "Vehicles"
                    }).ToListAsync();
                summary["Vehicles"] = rows.Count;
                details["Vehicles"] = rows.Cast<object>().ToList();
            }

            // ── Contracts ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Contracts")
            {
                var q = _context.Contracts
                    .Where(c => !c.is_deleted
                        && c.date_created >= fromDate && c.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(c => c.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(c => c.vmf_code == vmf_code.Value);
                if (site_code.HasValue) q = q.Where(c => c.site_code == site_code.Value);
                var rows = await q.OrderByDescending(c => c.date_created)
                    .Select(c => new { c.contract_code, c.vmf_code, c.site_code, c.date_created, c.created_by_user_code, c.still_current })
                    .ToListAsync();
                var mapped = rows.Where(c => VehicleInScope(c.vmf_code) || site_code == null)
                    .Select(c => (object)new CaptureActivityEntry
                    {
                        record_id = c.contract_code,
                        vmf_code = c.vmf_code,
                        fleet_number = FleetNum(c.vmf_code),
                        registration_number = RegNum(c.vmf_code),
                        description = $"Contract captured (status: {(c.still_current == "Y" ? "Active" : "Inactive")})",
                        date_captured = c.date_created,
                        captured_by_user_code = c.created_by_user_code,
                        module = "Contracts"
                    }).ToList();
                summary["Contracts"] = mapped.Count;
                details["Contracts"] = mapped;
            }

            // ── Accidents ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Accidents")
            {
                var q = _context.Accidents
                    .Where(a => !a.is_deleted
                        && a.date_created >= fromDate && a.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(a => a.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(a => a.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(a => a.date_created)
                    .Select(a => new { a.accident_code, a.vmf_code, a.description, a.date_created, a.created_by_user_code })
                    .ToListAsync();
                var mapped = rows.Where(a => VehicleInScope(a.vmf_code))
                    .Select(a => (object)new CaptureActivityEntry
                    {
                        record_id = a.accident_code,
                        vmf_code = a.vmf_code,
                        fleet_number = FleetNum(a.vmf_code),
                        registration_number = RegNum(a.vmf_code),
                        description = a.description ?? "Accident recorded",
                        date_captured = a.date_created,
                        captured_by_user_code = a.created_by_user_code,
                        module = "Accidents"
                    }).ToList();
                summary["Accidents"] = mapped.Count;
                details["Accidents"] = mapped;
            }

            // ── Fines ────────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Fines")
            {
                var q = _context.Fines
                    .Where(f => !f.is_deleted
                        && f.date_created >= fromDate && f.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(f => f.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(f => f.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(f => f.date_created)
                    .Select(f => new { f.Fine_code, f.vmf_code, f.Offence_reference, f.date_created, f.created_by_user_code })
                    .ToListAsync();
                var mapped = rows.Where(f => VehicleInScope(f.vmf_code))
                    .Select(f => (object)new CaptureActivityEntry
                    {
                        record_id = f.Fine_code,
                        vmf_code = f.vmf_code,
                        fleet_number = FleetNum(f.vmf_code),
                        registration_number = RegNum(f.vmf_code),
                        description = $"Fine {f.Offence_reference ?? f.Fine_code.ToString()} captured",
                        date_captured = f.date_created,
                        captured_by_user_code = f.created_by_user_code,
                        module = "Fines"
                    }).ToList();
                summary["Fines"] = mapped.Count;
                details["Fines"] = mapped;
            }

            // ── Job Cards ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "JobCards")
            {
                var q = _context.JobCards
                    .Where(j => !j.is_deleted
                        && j.date_created >= fromDate && j.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(j => j.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(j => j.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(j => j.date_created)
                    .Select(j => new { j.job_card_id, j.vmf_code, j.jcs_comment, j.status_code, j.date_created, j.created_by_user_code })
                    .ToListAsync();
                var mapped = rows.Where(j => VehicleInScope(j.vmf_code))
                    .Select(j => (object)new CaptureActivityEntry
                    {
                        record_id = j.job_card_id,
                        vmf_code = j.vmf_code,
                        fleet_number = FleetNum(j.vmf_code),
                        registration_number = RegNum(j.vmf_code),
                        description = j.jcs_comment ?? $"Job card #{j.job_card_id} (status {j.status_code})",
                        date_captured = j.date_created,
                        captured_by_user_code = j.created_by_user_code,
                        module = "JobCards"
                    }).ToList();
                summary["JobCards"] = mapped.Count;
                details["JobCards"] = mapped;
            }

            // ── Logbooks ─────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Logbooks")
            {
                var q = _context.Logbooks
                    .Where(l => !l.is_deleted
                        && l.date_created >= fromDate && l.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(l => l.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(l => l.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(l => l.date_created)
                    .Select(l => new { l.logbookcode, l.vmf_code, l.date_created, l.created_by_user_code })
                    .ToListAsync();
                var mapped = rows.Where(l => VehicleInScope(l.vmf_code))
                    .Select(l => (object)new CaptureActivityEntry
                    {
                        record_id = l.logbookcode,
                        vmf_code = l.vmf_code,
                        fleet_number = FleetNum(l.vmf_code),
                        registration_number = RegNum(l.vmf_code),
                        description = $"Logbook entry #{l.logbookcode}",
                        date_captured = l.date_created,
                        captured_by_user_code = l.created_by_user_code,
                        module = "Logbooks"
                    }).ToList();
                summary["Logbooks"] = mapped.Count;
                details["Logbooks"] = mapped;
            }

            // ── Documents ────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Documents")
            {
                var q = _context.VehicleDocuments
                    .Where(d => !d.is_deleted
                        && d.date_created >= fromDate && d.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(d => d.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(d => d.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(d => d.date_created)
                    .Select(d => new { d.document_id, d.vmf_code, d.document_category, d.original_file_name, d.date_created, d.created_by_user_code })
                    .ToListAsync();
                var mapped = rows.Where(d => VehicleInScope(d.vmf_code))
                    .Select(d => (object)new CaptureActivityEntry
                    {
                        record_id = d.document_id,
                        vmf_code = d.vmf_code,
                        fleet_number = FleetNum(d.vmf_code),
                        registration_number = RegNum(d.vmf_code),
                        description = $"{d.document_category} document: {d.original_file_name}",
                        date_captured = d.date_created,
                        captured_by_user_code = d.created_by_user_code,
                        module = "Documents"
                    }).ToList();
                summary["Documents"] = mapped.Count;
                details["Documents"] = mapped;
            }

            // ── Remarks ──────────────────────────────────────────────────────────
            if (moduleFilter == "All" || moduleFilter == "Remarks")
            {
                var q = _context.VehicleRemarks
                    .Where(r => !r.is_deleted
                        && r.date_created >= fromDate && r.date_created <= toDate);
                if (captured_by.HasValue) q = q.Where(r => r.created_by_user_code == captured_by.Value);
                if (vmf_code.HasValue) q = q.Where(r => r.vmf_code == vmf_code.Value);
                var rows = await q.OrderByDescending(r => r.date_created)
                    .Select(r => new { r.remark_id, r.vmf_code, r.remark_text, r.date_created, r.created_by_user_code })
                    .ToListAsync();
                var mapped = rows.Where(r => VehicleInScope(r.vmf_code))
                    .Select(r => (object)new CaptureActivityEntry
                    {
                        record_id = r.remark_id,
                        vmf_code = r.vmf_code,
                        fleet_number = FleetNum(r.vmf_code),
                        registration_number = RegNum(r.vmf_code),
                        description = r.remark_text ?? $"Remark #{r.remark_id}",
                        date_captured = r.date_created,
                        captured_by_user_code = r.created_by_user_code,
                        module = "Remarks"
                    }).ToList();
                summary["Remarks"] = mapped.Count;
                details["Remarks"] = mapped;
            }

            var totalCount = summary.Values.Sum();
            _logger.LogInformation(
                "Capture activity report: {From} – {To}, module={Module}, total={Total}",
                fromDate, toDate.Date, moduleFilter, totalCount);

            return Ok(new
            {
                filters_applied = filtersApplied,
                total_count = totalCount,
                summary,
                details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating capture activity report");
            return StatusCode(500, new { error = "Failed to generate capture activity report" });
        }
    }

    #endregion
}

#region Report DTOs

/// <summary>
/// Export request model
/// </summary>
public class ExportRequest
{
    public List<object> Data { get; set; } = new();
    public string Filename { get; set; } = "export.csv";
}

public class ReportHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HelpSectionDto> Sections { get; set; } = new();
}

public class AdditionalReportRequestDto
{
    public string ReportType { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string RequestedBy { get; set; } = "";
}

public class ReportRequestResultDto
{
    public bool Success { get; set; }
    public string RequestId { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime? EstimatedCompletionTime { get; set; }
}

public class ReportAuditTrailDto
{
    public List<AuditEntryDto> Entries { get; set; } = new();
    public int TotalCount { get; set; }
}

public class AuditEntryDto
{
    public int AuditId { get; set; }
    public string ReportType { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTime AccessedDate { get; set; }
    public string Action { get; set; } = "";
}

public class RegistrationCertificatesReportDto
{
    public List<CertificateDto> Certificates { get; set; } = new();
    public int TotalCount { get; set; }
}

public class CertificateDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public DateTime? DateUploaded { get; set; }
    public string RegistrationCertificate { get; set; } = "";
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = "";
}

#endregion

public class CaptureActivityEntry
{
    public int record_id { get; set; }
    public int? vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public string? description { get; set; }
    public DateTime date_captured { get; set; }
    public int? captured_by_user_code { get; set; }
    public string module { get; set; } = "";
}

// Note: Report model types referenced above should be defined in IReportingService interface
// VehicleReport, MasterFileReport, UniversalReportRequest, ServiceHistoryReport, etc.
