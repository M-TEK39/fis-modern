using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Reporting API endpoints
/// Provides vehicle, financial, maintenance, and trip reports with export capabilities
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportingService reportingService,
        ILogger<ReportController> logger)
    {
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
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

    #endregion
}

/// <summary>
/// Export request model
/// </summary>
public class ExportRequest
{
    public List<object> Data { get; set; } = new();
    public string Filename { get; set; } = "export.csv";
}

// Note: Report model types referenced above should be defined in IReportingService interface
// VehicleReport, MasterFileReport, UniversalReportRequest, ServiceHistoryReport, etc.
