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
    private readonly FisDbContext _context;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportingService reportingService,
        FisDbContext context,
        ILogger<ReportController> logger)
    {
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Vehicle Reports

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
                    v.date_created
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
    /// Get audit trail for report access and generation
    /// </summary>
    [HttpGet("audit-trail")]
    [ProducesResponseType(typeof(ReportAuditTrailDto), StatusCodes.Status200OK)]
    public ActionResult GetAuditTrail([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? userId)
    {
        // TODO: Implement audit trail retrieval
        _logger.LogInformation("Audit trail requested");
        var auditTrail = new ReportAuditTrailDto
        {
            Entries = new List<AuditEntryDto>(),
            TotalCount = 0
        };
        return Ok(auditTrail);
    }

    /// <summary>
    /// Get registration certificates report
    /// </summary>
    [HttpGet("registration-certificates")]
    [ProducesResponseType(typeof(RegistrationCertificatesReportDto), StatusCodes.Status200OK)]
    public ActionResult GetRegistrationCertificates([FromQuery] int? vmfCode, [FromQuery] int? departmentCode)
    {
        // TODO: Implement registration certificates report
        _logger.LogInformation("Registration certificates report requested");
        var report = new RegistrationCertificatesReportDto
        {
            Certificates = new List<CertificateDto>(),
            TotalCount = 0
        };
        return Ok(report);
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
    public string RegistrationNumber { get; set; } = "";
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Status { get; set; } = "";
}

#endregion

// Note: Report model types referenced above should be defined in IReportingService interface
// VehicleReport, MasterFileReport, UniversalReportRequest, ServiceHistoryReport, etc.
