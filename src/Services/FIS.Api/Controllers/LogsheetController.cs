using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LogsheetController : BaseApiController
{
    private readonly ILogsheetRepository _repository;
    private readonly ILogger<LogsheetController> _logger;

    public LogsheetController(ILogsheetRepository repository, ILogger<LogsheetController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Logsheet>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Logsheet>> GetById(int id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Logsheet>> Create([FromBody] Logsheet item)
    {
        try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.log_code }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Logsheet>> Update(int id, [FromBody] Logsheet item)
    {
        try { if (id != item.log_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    #region Specialized Operations

    /// <summary>
    /// Get logsheet menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<LogsheetMenuDto> GetMenu()
    {
        var menu = new LogsheetMenuDto
        {
            Options = new List<string> { "Enter", "Edit", "Delete", "Reports", "Help" }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Get logsheet help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<LogsheetHelpDto> GetHelp()
    {
        var help = new LogsheetHelpDto
        {
            Title = "Logsheet Management Help",
            Description = "Enter and manage vehicle logsheet entries"
        };
        return Ok(help);
    }

    /// <summary>
    /// Create new logsheet entry
    /// </summary>
    [HttpPost("entry")]
    public ActionResult<LogsheetEntryResultDto> CreateEntry([FromBody] LogsheetEntryDto request)
    {
        // TODO: Implement entry creation
        var result = new LogsheetEntryResultDto
        {
            Success = true,
            LogCode = 0,
            Message = "Logsheet entry created"
        };
        return Ok(result);
    }

    /// <summary>
    /// Edit existing logsheet entry
    /// </summary>
    [HttpPut("edit/{id}")]
    public ActionResult<LogsheetEntryResultDto> EditEntry(int id, [FromBody] LogsheetEntryDto request)
    {
        // TODO: Implement entry edit
        var result = new LogsheetEntryResultDto
        {
            Success = true,
            LogCode = id,
            Message = "Logsheet entry updated"
        };
        return Ok(result);
    }

    /// <summary>
    /// Delete logsheet entry
    /// </summary>
    [HttpDelete("entry/{id}")]
    public ActionResult DeleteEntry(int id)
    {
        // TODO: Implement entry deletion
        return Ok(new { message = "Logsheet entry deleted", id });
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get logsheet reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<LogsheetReportMenuDto> GetReportsMenu()
    {
        var menu = new LogsheetReportMenuDto
        {
            Reports = new List<string> { "One Vehicle", "One Requisition", "Department Period", "Captured", "Total KM per Class" }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate logsheet report for one vehicle
    /// </summary>
    [HttpPost("reports/one-vehicle")]
    public ActionResult<LogsheetReportDto> GetReportOneVehicle([FromBody] LogsheetOneVehicleRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogsheetReportDto { ReportType = "OneVehicle", Data = new List<object>() };
        return Ok(report);
    }

    /// <summary>
    /// Generate logsheet report for one requisition
    /// </summary>
    [HttpPost("reports/one-requisition")]
    public ActionResult<LogsheetReportDto> GetReportOneRequisition([FromBody] LogsheetOneRequisitionRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogsheetReportDto { ReportType = "OneRequisition", Data = new List<object>() };
        return Ok(report);
    }

    /// <summary>
    /// Generate logsheet report by department and period
    /// </summary>
    [HttpPost("reports/department-period")]
    public ActionResult<LogsheetReportDto> GetReportDepartmentPeriod([FromBody] LogsheetDepartmentPeriodRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogsheetReportDto { ReportType = "DepartmentPeriod", Data = new List<object>() };
        return Ok(report);
    }

    /// <summary>
    /// Generate captured logsheets report
    /// </summary>
    [HttpPost("reports/captured")]
    public ActionResult<LogsheetReportDto> GetReportCaptured([FromBody] LogsheetCapturedRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogsheetReportDto { ReportType = "Captured", Data = new List<object>() };
        return Ok(report);
    }

    /// <summary>
    /// Generate total kilometers per class code report
    /// </summary>
    [HttpPost("reports/total-km-per-class-code")]
    public ActionResult<LogsheetReportDto> GetReportTotalKmPerClass([FromBody] LogsheetKmPerClassRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogsheetReportDto { ReportType = "TotalKmPerClass", Data = new List<object>() };
        return Ok(report);
    }

    #endregion
}

#region Logsheet DTOs
public class LogsheetMenuDto { public List<string> Options { get; set; } = new(); }
public class LogsheetHelpDto { public string Title { get; set; } = ""; public string Description { get; set; } = ""; }
public class LogsheetEntryDto { public int VmfCode { get; set; } public DateTime LogDate { get; set; } public int Odometer { get; set; } public string? Notes { get; set; } }
public class LogsheetEntryResultDto { public bool Success { get; set; } public int LogCode { get; set; } public string Message { get; set; } = ""; }
public class LogsheetReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class LogsheetOneVehicleRequestDto { public int VmfCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetOneRequisitionRequestDto { public string RequisitionNumber { get; set; } = ""; }
public class LogsheetDepartmentPeriodRequestDto { public int DepartmentCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetCapturedRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetKmPerClassRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
