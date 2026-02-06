using FIS.Core.Application.Interfaces;
using MonitorEntity = FIS.Core.Domain.Entities.Monitor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MonitorController : BaseApiController
{
    private readonly IMonitorRepository _repository;
    private readonly ILogger<MonitorController> _logger;

    public MonitorController(IMonitorRepository repository, ILogger<MonitorController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MonitorEntity>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MonitorEntity>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<MonitorEntity>> Create([FromBody] MonitorEntity item)
    {
        try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.monitor_code }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MonitorEntity>> Update(short id, [FromBody] MonitorEntity item)
    {
        try { if (id != item.monitor_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    #region Specialized Operations

    [HttpGet("menu")]
    public ActionResult<MonitorMenuDto> GetMenu() => Ok(new MonitorMenuDto { Options = new List<string> { "Capture", "Edit", "Reports", "Help" } });

    [HttpPost("capture")]
    public ActionResult<MonitorCaptureResultDto> Capture([FromBody] MonitorCaptureDto request)
    {
        return Ok(new MonitorCaptureResultDto { Success = true, MonitorCode = 0, Message = "Monitor inquiry captured" });
    }

    [HttpPut("edit/{id}")]
    public ActionResult<MonitorEditResultDto> Edit(short id, [FromBody] MonitorEditDto request)
    {
        return Ok(new MonitorEditResultDto { Success = true, MonitorCode = id, Message = "Monitor inquiry updated" });
    }

    #endregion

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<MonitorReportMenuDto> GetReportsMenu() => Ok(new MonitorReportMenuDto { Reports = new List<string> { "One Reference Number", "One Vehicle", "Reprint", "Dept/Site Period", "CLO Inquiry", "Statistics" } });

    [HttpGet("reports/one-reference-number/{id}")]
    public ActionResult<MonitorReportDto> GetReportByReferenceNumber(short id) => Ok(new MonitorReportDto { ReportType = "OneReferenceNumber", Data = new List<object>() });

    [HttpPost("reports/one-vehicle")]
    public ActionResult<MonitorReportDto> GetReportOneVehicle([FromBody] MonitorOneVehicleRequestDto request) => Ok(new MonitorReportDto { ReportType = "OneVehicle", Data = new List<object>() });

    [HttpGet("reports/reprint/{id}")]
    public ActionResult<MonitorReportDto> ReprintReport(short id) => Ok(new MonitorReportDto { ReportType = "Reprint", Data = new List<object>() });

    [HttpPost("reports/dept-site-period")]
    public ActionResult<MonitorReportDto> GetReportDeptSitePeriod([FromBody] MonitorDeptSitePeriodRequestDto request) => Ok(new MonitorReportDto { ReportType = "DeptSitePeriod", Data = new List<object>() });

    [HttpPost("reports/clo-inquiry")]
    public ActionResult<MonitorReportDto> GetReportCloInquiry([FromBody] MonitorCloInquiryRequestDto request) => Ok(new MonitorReportDto { ReportType = "CLOInquiry", Data = new List<object>() });

    [HttpPost("reports/inquiry-statistics")]
    public ActionResult<MonitorReportDto> GetReportInquiryStatistics([FromBody] MonitorStatsRequestDto request) => Ok(new MonitorReportDto { ReportType = "InquiryStatistics", Data = new List<object>() });

    #endregion
}

#region Monitor DTOs
public class MonitorMenuDto { public List<string> Options { get; set; } = new(); }
public class MonitorCaptureDto { public int VmfCode { get; set; } public string InquiryType { get; set; } = ""; public string Details { get; set; } = ""; }
public class MonitorCaptureResultDto { public bool Success { get; set; } public short MonitorCode { get; set; } public string Message { get; set; } = ""; }
public class MonitorEditDto { public string InquiryType { get; set; } = ""; public string Details { get; set; } = ""; }
public class MonitorEditResultDto { public bool Success { get; set; } public short MonitorCode { get; set; } public string Message { get; set; } = ""; }
public class MonitorReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class MonitorOneVehicleRequestDto { public int VmfCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class MonitorDeptSitePeriodRequestDto { public int DepartmentCode { get; set; } public int? SiteCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class MonitorCloInquiryRequestDto { public string CloNumber { get; set; } = ""; }
public class MonitorStatsRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class MonitorReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
