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
    public async Task<ActionResult<LogsheetEntryResultDto>> CreateEntry([FromBody] LogsheetEntryDto request)
    {
        try
        {
            var logsheet = new Logsheet
            {
                vmf_code = request.VmfCode,
                start_odo = request.StartOdometer,
                end_odo = request.EndOdometer,
                month = request.Month,
                site_code = request.SiteCode,
                rek_num = request.RequisitionNumber,
                days_used = request.DaysUsed,
                bund_num = request.BundleNumber
            };

            var created = await _repository.CreateAsync(logsheet, GetCurrentUserId());

            var result = new LogsheetEntryResultDto
            {
                Success = true,
                LogCode = created.log_code,
                Message = "Logsheet entry created successfully"
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating logsheet entry");
            return StatusCode(500, "Error creating logsheet entry");
        }
    }

    /// <summary>
    /// Edit existing logsheet entry
    /// </summary>
    [HttpPut("edit/{id}")]
    public async Task<ActionResult<LogsheetEntryResultDto>> EditEntry(int id, [FromBody] LogsheetEntryDto request)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Logsheet entry with code {id} not found" });

            existing.vmf_code = request.VmfCode;
            existing.start_odo = request.StartOdometer;
            existing.end_odo = request.EndOdometer;
            existing.month = request.Month;
            existing.site_code = request.SiteCode;
            existing.rek_num = request.RequisitionNumber;
            existing.days_used = request.DaysUsed;
            existing.bund_num = request.BundleNumber;

            var updated = await _repository.UpdateAsync(existing, GetCurrentUserId());

            var result = new LogsheetEntryResultDto
            {
                Success = true,
                LogCode = id,
                Message = "Logsheet entry updated successfully"
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing logsheet entry: {Id}", id);
            return StatusCode(500, "Error editing logsheet entry");
        }
    }

    /// <summary>
    /// Delete logsheet entry
    /// </summary>
    [HttpDelete("entry/{id}")]
    public async Task<ActionResult> DeleteEntry(int id)
    {
        try
        {
            var logsheet = await _repository.GetByIdAsync(id);
            if (logsheet == null)
                return NotFound(new { message = $"Logsheet entry with code {id} not found" });

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return Ok(new { message = "Logsheet entry deleted successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logsheet entry: {Id}", id);
            return StatusCode(500, "Error deleting logsheet entry");
        }
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
    public async Task<ActionResult<LogsheetReportDto>> GetReportOneVehicle([FromBody] LogsheetOneVehicleRequestDto request)
    {
        try
        {
            var vehicleLogsheets = await _repository.GetByVehicleAsync(request.VmfCode);

            var filteredLogsheets = vehicleLogsheets
                .Where(l => !l.is_deleted)
                .Where(l => l.month >= request.StartDate && l.month <= request.EndDate)
                .OrderByDescending(l => l.month)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "OneVehicle",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating logsheet report for vehicle: {VmfCode}", request.VmfCode);
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate logsheet report for one requisition
    /// </summary>
    [HttpPost("reports/one-requisition")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportOneRequisition([FromBody] LogsheetOneRequisitionRequestDto request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RequisitionNumber))
                return BadRequest(new { message = "Requisition number is required" });

            var allLogsheets = await _repository.GetAllAsync();
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
                .Where(l => l.rek_num != null && l.rek_num.Equals(request.RequisitionNumber, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => l.month)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "OneRequisition",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
                GeneratedDate = DateTime.UtcNow
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating logsheet report for requisition: {RequisitionNumber}", request.RequisitionNumber);
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate logsheet report by department and period
    /// </summary>
    [HttpPost("reports/department-period")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportDepartmentPeriod([FromBody] LogsheetDepartmentPeriodRequestDto request)
    {
        try
        {
            var allLogsheets = await _repository.GetAllAsync();

            // Filter by site code (department) and date range
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
                .Where(l => l.site_code == request.DepartmentCode)
                .Where(l => l.month >= request.StartDate && l.month <= request.EndDate)
                .OrderByDescending(l => l.month)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "DepartmentPeriod",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating logsheet report by department period: Department={DepartmentCode}, Start={StartDate}, End={EndDate}",
                request.DepartmentCode, request.StartDate, request.EndDate);
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate captured logsheets report
    /// </summary>
    [HttpPost("reports/captured")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportCaptured([FromBody] LogsheetCapturedRequestDto request)
    {
        try
        {
            var allLogsheets = await _repository.GetAllAsync();

            // Filter by date range (captured in this period)
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
                .Where(l => l.date_created >= request.StartDate && l.date_created <= request.EndDate)
                .OrderByDescending(l => l.date_created)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "Captured",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating captured logsheets report: Start={StartDate}, End={EndDate}",
                request.StartDate, request.EndDate);
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate total kilometers per class code report
    /// </summary>
    [HttpPost("reports/total-km-per-class-code")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportTotalKmPerClass([FromBody] LogsheetKmPerClassRequestDto request)
    {
        try
        {
            var allLogsheets = await _repository.GetAllAsync();

            // Filter by date range and calculate total km per class code
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
                .Where(l => l.month >= request.StartDate && l.month <= request.EndDate)
                .Where(l => l.Vehicle != null) // Ensure vehicle navigation property is loaded
                .ToList();

            // Group by vehicle type code and sum kilometers
            var kmByClassCode = filteredLogsheets
                .GroupBy(l => l.Vehicle?.type_code ?? 0)
                .Select(g => new
                {
                    ClassCode = g.Key,
                    TotalKilometers = g.Sum(l => l.end_odo - l.start_odo),
                    VehicleCount = g.Select(l => l.vmf_code).Distinct().Count(),
                    RecordCount = g.Count()
                })
                .OrderByDescending(x => x.TotalKilometers)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "TotalKmPerClass",
                Data = kmByClassCode.Cast<object>().ToList(),
                RecordCount = kmByClassCode.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating total km per class code report: Start={StartDate}, End={EndDate}",
                request.StartDate, request.EndDate);
            return StatusCode(500, "Error generating report");
        }
    }

    #endregion
}

#region Logsheet DTOs
public class LogsheetMenuDto { public List<string> Options { get; set; } = new(); }
public class LogsheetHelpDto { public string Title { get; set; } = ""; public string Description { get; set; } = ""; }
public class LogsheetEntryDto
{
    public int VmfCode { get; set; }
    public double StartOdometer { get; set; }
    public double EndOdometer { get; set; }
    public DateTime Month { get; set; }
    public short SiteCode { get; set; }
    public string? RequisitionNumber { get; set; }
    public int? DaysUsed { get; set; }
    public int? BundleNumber { get; set; }
}
public class LogsheetEntryResultDto { public bool Success { get; set; } public int LogCode { get; set; } public string Message { get; set; } = ""; }
public class LogsheetReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class LogsheetOneVehicleRequestDto { public int VmfCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetOneRequisitionRequestDto { public string RequisitionNumber { get; set; } = ""; }
public class LogsheetDepartmentPeriodRequestDto { public int DepartmentCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetCapturedRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetKmPerClassRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class LogsheetReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public int RecordCount { get; set; }
    public DateTime GeneratedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
#endregion
