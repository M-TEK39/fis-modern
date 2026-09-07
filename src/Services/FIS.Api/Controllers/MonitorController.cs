using FIS.Core.Application.Interfaces;
using MonitorEntity = FIS.Core.Domain.Entities.Monitor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

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
        try
        {
            if (!Validate(item, out var error)) return BadRequest(error);
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.monitor_code }, created);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MonitorEntity>> Update(short id, [FromBody] MonitorEntity item)
    {
        try
        {
            if (item is null || id != item.monitor_code) return BadRequest();
            if (!Validate(item, out var error)) return BadRequest(error);
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
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
    public async Task<ActionResult<MonitorReportDto>> GetReportByReferenceNumber(short id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null || item.is_deleted)
            {
                return Ok(new MonitorReportDto { ReportType = "OneReferenceNumber", Data = Array.Empty<object>().ToList() });
            }

            return Ok(new MonitorReportDto
            {
                ReportType = "OneReferenceNumber",
                Data = new List<object> { ToReportItem(item) }
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost("reports/one-vehicle")]
    public async Task<ActionResult<MonitorReportDto>> GetReportOneVehicle([FromBody] MonitorOneVehicleRequestDto request)
    {
        try
        {
            var start = request.StartDate.Date;
            var endExclusive = request.EndDate.Date.AddDays(1);
            var data = (await _repository.GetAllAsync())
                .Where(item => !item.is_deleted &&
                               item.vmf_code == request.VmfCode &&
                               item.Capture_dat.HasValue &&
                               item.Capture_dat.Value >= start &&
                               item.Capture_dat.Value < endExclusive)
                .OrderByDescending(item => item.Capture_dat)
                .Select(ToReportItem)
                .Cast<object>()
                .ToList();

            return Ok(new MonitorReportDto { ReportType = "OneVehicle", Data = data });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("reports/reprint/{id}")]
    public async Task<ActionResult<MonitorReportDto>> ReprintReport(short id)
        => await GetReportByReferenceNumber(id);

    [HttpPost("reports/dept-site-period")]
    public async Task<ActionResult<MonitorReportDto>> GetReportDeptSitePeriod([FromBody] MonitorDeptSitePeriodRequestDto request)
    {
        try
        {
            var start = request.StartDate.Date;
            var endExclusive = request.EndDate.Date.AddDays(1);
            var data = (await _repository.GetAllAsync())
                .Where(item => !item.is_deleted &&
                               (!request.SiteCode.HasValue || item.Driver_Site == request.SiteCode) &&
                               item.Capture_dat.HasValue &&
                               item.Capture_dat.Value >= start &&
                               item.Capture_dat.Value < endExclusive)
                .OrderByDescending(item => item.Capture_dat)
                .Select(ToReportItem)
                .Cast<object>()
                .ToList();

            return Ok(new MonitorReportDto { ReportType = "DeptSitePeriod", Data = data });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost("reports/clo-inquiry")]
    public async Task<ActionResult<MonitorReportDto>> GetReportCloInquiry([FromBody] MonitorCloInquiryRequestDto request)
    {
        try
        {
            var clo = request.CloNumber?.Trim();
            var query = await _repository.GetAllAsync();
            if (!string.IsNullOrWhiteSpace(clo))
            {
                query = query.Where(item =>
                    (!string.IsNullOrWhiteSpace(item.Inquiry_Desc) && item.Inquiry_Desc.Contains(clo, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(item.Driver_persalno) && item.Driver_persalno.Contains(clo, StringComparison.OrdinalIgnoreCase)));
            }

            var data = query
                .Where(item => !item.is_deleted)
                .OrderByDescending(item => item.Capture_dat)
                .Select(ToReportItem)
                .Cast<object>()
                .ToList();

            return Ok(new MonitorReportDto { ReportType = "CLOInquiry", Data = data });
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost("reports/inquiry-statistics")]
    public async Task<ActionResult<MonitorReportDto>> GetReportInquiryStatistics([FromBody] MonitorStatsRequestDto request)
    {
        try
        {
            if (request.StartDate == default || request.EndDate == default)
            {
                return BadRequest("StartDate and EndDate are required.");
            }

            if (request.EndDate.Date < request.StartDate.Date)
            {
                return BadRequest("EndDate cannot be before StartDate.");
            }

            var start = request.StartDate.Date;
            var endExclusive = request.EndDate.Date.AddDays(1);

            var allItems = await _repository.GetAllAsync();
            var grouped = allItems
                .Where(item => !item.is_deleted &&
                               item.Capture_dat.HasValue &&
                               item.Capture_dat.Value >= start &&
                               item.Capture_dat.Value < endExclusive)
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Inquiry_type) ? "(Unknown)" : item.Inquiry_type!.Trim())
                .Select(group => new MonitorStatisticItemDto
                {
                    InquiryType = group.Key,
                    Count = group.Count()
                })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.InquiryType, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(new MonitorReportDto
            {
                ReportType = "InquiryStatistics",
                Data = grouped.Cast<object>().ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating monitor inquiry statistics report for period {StartDate} to {EndDate}",
                request.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                request.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            return StatusCode(500);
        }
    }

    private static MonitorReportItemDto ToReportItem(MonitorEntity item) => new()
    {
        MonitorCode = item.monitor_code,
        VmfCode = item.vmf_code,
        CaptureDate = item.Capture_dat,
        InquiryType = item.Inquiry_type ?? string.Empty,
        InquiryDescription = item.Inquiry_Desc ?? string.Empty,
        DriverName = item.Driver_name ?? string.Empty,
        DriverPersalNo = item.Driver_persalno ?? string.Empty,
        DriverSite = item.Driver_Site
    };

    private static bool Validate(MonitorEntity? item, out string error)
    {
        if (item is null)
        {
            error = "Monitor inquiry is required.";
            return false;
        }

        if (!item.vmf_code.HasValue || item.vmf_code <= 0)
        {
            error = "A vehicle is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Inquiry_type) || item.Inquiry_type.Trim().Length > 100)
        {
            error = "Inquiry type is required and must be 100 characters or fewer.";
            return false;
        }

        if (item.Driver_name?.Length > 100 || item.Driver_persalno?.Length > 50)
        {
            error = "Driver details exceed the legacy field limits.";
            return false;
        }

        if (item.Driver_Site is <= 0)
        {
            error = "Driver site must be a positive site code when supplied.";
            return false;
        }

        error = string.Empty;
        return true;
    }

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
public class MonitorStatisticItemDto { public string InquiryType { get; set; } = ""; public int Count { get; set; } }
public class MonitorReportItemDto
{
    public short MonitorCode { get; set; }
    public int? VmfCode { get; set; }
    public DateTime? CaptureDate { get; set; }
    public string InquiryType { get; set; } = string.Empty;
    public string InquiryDescription { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string DriverPersalNo { get; set; } = string.Empty;
    public short? DriverSite { get; set; }
}
#endregion
