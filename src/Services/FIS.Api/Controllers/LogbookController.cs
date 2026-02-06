using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LogbookController : BaseApiController
{
    private readonly ILogbookRepository _repository;
    private readonly ILogger<LogbookController> _logger;

    public LogbookController(ILogbookRepository repository, ILogger<LogbookController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Logbook>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Logbook>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Logbook>> Create([FromBody] Logbook item)
    {
        try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.logbookcode }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Logbook>> Update(short id, [FromBody] Logbook item)
    {
        try { if (id != item.logbookcode) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    #region Specialized Operations

    /// <summary>
    /// Get logbook menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<LogbookMenuDto> GetMenu()
    {
        var menu = new LogbookMenuDto
        {
            Options = new List<string> { "Maintenance", "Collection", "Delete", "Reports", "Help" }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Get logbook help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<LogbookHelpDto> GetHelp()
    {
        var help = new LogbookHelpDto
        {
            Title = "Logbook Management Help",
            Description = "Manage vehicle logbooks, track collection and returns"
        };
        return Ok(help);
    }

    /// <summary>
    /// Search for vehicle by fleet number or registration
    /// </summary>
    [HttpGet("vehicle-search")]
    public ActionResult<VehicleLookupDto> SearchVehicle([FromQuery] string identifier)
    {
        // TODO: Implement vehicle search logic
        var result = new VehicleLookupDto
        {
            Found = false,
            Message = $"Search for: {identifier}"
        };
        return Ok(result);
    }

    /// <summary>
    /// Process logbook collection
    /// </summary>
    [HttpPost("collection")]
    public ActionResult<LogbookCollectionResultDto> ProcessCollection([FromBody] LogbookCollectionDto request)
    {
        // TODO: Implement collection logic
        var result = new LogbookCollectionResultDto
        {
            Success = true,
            Message = "Logbook collection processed"
        };
        return Ok(result);
    }

    /// <summary>
    /// Delete/return a handed out logbook
    /// </summary>
    [HttpDelete("handout/{id}")]
    public ActionResult DeleteHandout(short id)
    {
        // TODO: Implement handout deletion logic
        return Ok(new { message = "Handout deleted", id });
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get logbook reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<ReportMenuDto> GetReportsMenu()
    {
        var menu = new ReportMenuDto
        {
            Reports = new List<string> { "One Number", "Department Period" }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate logbook report by number
    /// </summary>
    [HttpPost("reports/one-number")]
    public ActionResult<LogbookReportDto> GetReportByNumber([FromBody] LogbookOneNumberRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogbookReportDto
        {
            ReportType = "OneNumber",
            Data = new List<object>()
        };
        return Ok(report);
    }

    /// <summary>
    /// Generate logbook report by department and period
    /// </summary>
    [HttpPost("reports/department-period")]
    public ActionResult<LogbookReportDto> GetReportByDepartmentPeriod([FromBody] DepartmentPeriodRequestDto request)
    {
        // TODO: Implement report generation
        var report = new LogbookReportDto
        {
            ReportType = "DepartmentPeriod",
            Data = new List<object>()
        };
        return Ok(report);
    }

    #endregion
}

#region Logbook DTOs
public class LogbookMenuDto { public List<string> Options { get; set; } = new(); }
public class LogbookHelpDto { public string Title { get; set; } = ""; public string Description { get; set; } = ""; }
public class VehicleLookupDto { public bool Found { get; set; } public string Message { get; set; } = ""; public int? VmfCode { get; set; } }
public class LogbookCollectionDto { public short LogbookCode { get; set; } public int VmfCode { get; set; } public DateTime CollectionDate { get; set; } }
public class LogbookCollectionResultDto { public bool Success { get; set; } public string Message { get; set; } = ""; }
public class ReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class LogbookOneNumberRequestDto { public string LogbookNumber { get; set; } = ""; }
public class DepartmentPeriodRequestDto { public int DepartmentCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogbookReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
