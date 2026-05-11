using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CallCentreController : BaseApiController
{
    private readonly ICallCentreRepository _repository;
    private readonly ILogger<CallCentreController> _logger;

    public CallCentreController(ICallCentreRepository repository, ILogger<CallCentreController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CallCentre>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CallCentre>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<CallCentre>>> GetByVehicle(int vmfCode)
    {
        try { return Ok(await _repository.GetByVehicleAsync(vmfCode)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpPost]
    public async Task<ActionResult<CallCentre>> Create([FromBody] CreateCallCentreDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var item = new CallCentre
            {
                vmf_code = dto.VmfCode,
                Call_time = dto.CallTime,
                Call_date = dto.CallDate,
                Incident_type = dto.IncidentType,
                Incident_Desc = dto.IncidentDesc,
                Capture_name = dto.CaptureName,
                User_access_code = dto.UserAccessCode,
                Caller_name = dto.CallerName,
                Driver_name = dto.DriverName,
                Driver_persalno = dto.DriverPersalno,
                Driver_Licno = dto.DriverLicno,
                GG_number = dto.GGNumber,
                Driver_base_station = dto.DriverBaseStation,
                Driver_Site = dto.DriverSite,
                Driver_tel = dto.DriverTel,
                Driver_cell = dto.DriverCell,
                date_created = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Call_centre_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating call centre record");
            return StatusCode(500, new { error = "Failed to create call centre record", message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CallCentre>> Update(short id, [FromBody] UpdateCallCentreDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { error = "Call centre record not found", id });

            // Update only provided fields
            existing.vmf_code = dto.VmfCode;
            existing.Call_time = dto.CallTime;
            existing.Call_date = dto.CallDate;
            existing.Incident_type = dto.IncidentType;
            existing.Incident_Desc = dto.IncidentDesc;
            existing.Capture_name = dto.CaptureName;
            existing.User_access_code = dto.UserAccessCode;
            existing.Caller_name = dto.CallerName;
            existing.Driver_name = dto.DriverName;
            existing.Driver_persalno = dto.DriverPersalno;
            existing.Driver_Licno = dto.DriverLicno;
            existing.GG_number = dto.GGNumber;
            existing.Driver_base_station = dto.DriverBaseStation;
            existing.Driver_Site = dto.DriverSite;
            existing.Driver_tel = dto.DriverTel;
            existing.Driver_cell = dto.DriverCell;

            var updated = await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating call centre record {Id}", id);
            return StatusCode(500, new { error = "Failed to update call centre record", message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    #region Specialized Operations

    /// <summary>
    /// Get call centre menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<CallCentreMenuDto> GetMenu()
    {
        var menu = new CallCentreMenuDto
        {
            Options = new List<string>
            {
                "Capture Incident",
                "Edit Incident",
                "Notifications",
                "Reports",
                "Help"
            }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Get call centre help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<CallCentreHelpDto> GetHelp()
    {
        var help = new CallCentreHelpDto
        {
            Title = "Call Centre Help",
            Description = "Manage call centre incidents and vehicle service requests",
            Sections = new List<HelpSectionDto>
            {
                new HelpSectionDto
                {
                    Title = "Incident Capture",
                    Content = "Record new service requests and incidents reported by departments"
                },
                new HelpSectionDto
                {
                    Title = "Incident Editing",
                    Content = "Update existing incident records and track resolution status"
                },
                new HelpSectionDto
                {
                    Title = "Notifications",
                    Content = "View pending notifications and alerts for open incidents"
                }
            }
        };
        return Ok(help);
    }

    /// <summary>
    /// Get call centre notifications
    /// </summary>
    [HttpGet("notifications")]
    public async Task<ActionResult<CallCentreNotificationsDto>> GetNotifications()
    {
        try
        {
            _logger.LogInformation("Getting call centre notifications");

            // Get recent calls (last 30 days) as notifications
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentCalls = await _repository.GetByDateRangeAsync(thirtyDaysAgo, DateTime.UtcNow);

            var notificationsList = recentCalls
                .Where(c => !c.is_deleted)
                .OrderByDescending(c => c.Call_date ?? c.date_created)
                .Take(50) // Limit to 50 most recent
                .Select(c => new NotificationDto
                {
                    NotificationId = c.Call_centre_code,
                    Message = $"Call from {c.Caller_name ?? "Unknown"} for vehicle {c.GG_number ?? "N/A"}",
                    CreatedDate = c.Call_date ?? c.date_created,
                    IsRead = false // Could be enhanced with a separate read tracking mechanism
                })
                .ToList();

            var notifications = new CallCentreNotificationsDto
            {
                Notifications = notificationsList,
                UnreadCount = notificationsList.Count
            };
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting call centre notifications");
            return StatusCode(500, "Error retrieving notifications");
        }
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get call centre reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<CallCentreReportMenuDto> GetReportsMenu()
    {
        var menu = new CallCentreReportMenuDto
        {
            Reports = new List<string>
            {
                "Department/Site Period",
                "Statistics",
                "CLO Inquiry",
                "Data Access",
                "Open Calls"
            }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate call centre report by department and site for a period
    /// </summary>
    [HttpPost("reports/dept-site-period")]
    public async Task<ActionResult<CallCentreReportDto>> GetReportDeptSitePeriod([FromBody] CallCentreDeptSitePeriodRequestDto request)
    {
        try
        {
            _logger.LogInformation("Generating dept/site period report: Dept={DepartmentCode}, Site={SiteCode}, Start={StartDate}, End={EndDate}",
                request.DepartmentCode, request.SiteCode, request.StartDate, request.EndDate);

            var calls = await _repository.GetByDateRangeAsync(request.StartDate, request.EndDate);

            var filteredCalls = calls
                .Where(c => !c.is_deleted)
                .Where(c => request.SiteCode == null || c.Driver_Site == request.SiteCode)
                .OrderByDescending(c => c.Call_date ?? c.date_created)
                .ToList();

            var report = new CallCentreReportDto
            {
                ReportType = "DeptSitePeriod",
                Data = filteredCalls.Cast<object>().ToList(),
                RecordCount = filteredCalls.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dept/site period report");
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate call centre statistics report
    /// </summary>
    [HttpGet("reports/statistics")]
    public async Task<ActionResult<CallCentreReportDto>> GetReportStatistics([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            _logger.LogInformation("Generating statistics report: Start={StartDate}, End={EndDate}", startDate, endDate);

            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var calls = await _repository.GetByDateRangeAsync(start, end);

            var callsList = calls.Where(c => !c.is_deleted).ToList();

            // Calculate statistics
            var statistics = new
            {
                TotalCalls = callsList.Count,
                CallsBySite = callsList.GroupBy(c => c.Driver_Site ?? 0)
                    .Select(g => new { SiteCode = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                CallsByDate = callsList.GroupBy(c => (c.Call_date ?? c.date_created).Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToList(),
                UniqueVehicles = callsList.Where(c => c.vmf_code.HasValue).Select(c => c.vmf_code).Distinct().Count(),
                UniqueCallers = callsList.Where(c => !string.IsNullOrEmpty(c.Caller_name)).Select(c => c.Caller_name).Distinct().Count()
            };

            var report = new CallCentreReportDto
            {
                ReportType = "Statistics",
                Data = new List<object> { statistics },
                RecordCount = 1,
                GeneratedDate = DateTime.UtcNow,
                StartDate = start,
                EndDate = end
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating statistics report");
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate CLO inquiry report
    /// </summary>
    [HttpGet("reports/clo")]
    public async Task<ActionResult<CallCentreReportDto>> GetReportClo([FromQuery] string? cloNumber)
    {
        try
        {
            _logger.LogInformation("Generating CLO inquiry report: CLO={CloNumber}", cloNumber);

            if (string.IsNullOrWhiteSpace(cloNumber))
                return BadRequest(new { message = "CLO number is required" });

            var allCalls = await _repository.GetAllAsync();

            var filteredCalls = allCalls
                .Where(c => !c.is_deleted)
                .Where(c => c.GG_number != null && c.GG_number.Contains(cloNumber, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(c => c.Call_date ?? c.date_created)
                .ToList();

            var report = new CallCentreReportDto
            {
                ReportType = "CLO",
                Data = filteredCalls.Cast<object>().ToList(),
                RecordCount = filteredCalls.Count,
                GeneratedDate = DateTime.UtcNow
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating CLO inquiry report: {CloNumber}", cloNumber);
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate data access report
    /// </summary>
    [HttpGet("reports/data-access")]
    public async Task<ActionResult<CallCentreReportDto>> GetReportDataAccess([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        try
        {
            _logger.LogInformation("Generating data access report: Start={StartDate}, End={EndDate}", startDate, endDate);

            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var calls = await _repository.GetByDateRangeAsync(start, end);

            var filteredCalls = calls
                .Where(c => !c.is_deleted)
                .OrderByDescending(c => c.Call_date ?? c.date_created)
                .ToList();

            // Group by user/capture name for data access tracking
            var dataAccessSummary = filteredCalls
                .GroupBy(c => c.Capture_name ?? "Unknown")
                .Select(g => new
                {
                    UserName = g.Key,
                    AccessCount = g.Count(),
                    FirstAccess = g.Min(c => c.date_created),
                    LastAccess = g.Max(c => c.date_updated ?? c.date_created)
                })
                .OrderByDescending(x => x.AccessCount)
                .ToList();

            var report = new CallCentreReportDto
            {
                ReportType = "DataAccess",
                Data = dataAccessSummary.Cast<object>().ToList(),
                RecordCount = dataAccessSummary.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = start,
                EndDate = end
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data access report");
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate open calls report
    /// </summary>
    [HttpGet("reports/open-calls")]
    public async Task<ActionResult<CallCentreReportDto>> GetReportOpenCalls()
    {
        try
        {
            _logger.LogInformation("Generating open calls report");

            // Get calls from the last 90 days (assume open calls are recent calls)
            var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
            var recentCalls = await _repository.GetByDateRangeAsync(ninetyDaysAgo, DateTime.UtcNow);

            var openCalls = recentCalls
                .Where(c => !c.is_deleted)
                .OrderByDescending(c => c.Call_date ?? c.date_created)
                .ToList();

            var report = new CallCentreReportDto
            {
                ReportType = "OpenCalls",
                Data = openCalls.Cast<object>().ToList(),
                RecordCount = openCalls.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = ninetyDaysAgo,
                EndDate = DateTime.UtcNow
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating open calls report");
            return StatusCode(500, "Error generating report");
        }
    }

    #endregion
}

#region Call Centre DTOs

public class CallCentreMenuDto { public List<string> Options { get; set; } = new(); }

public class CallCentreHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HelpSectionDto> Sections { get; set; } = new();
}

public class CallCentreNotificationsDto
{
    public List<NotificationDto> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
}

public class NotificationDto
{
    public int NotificationId { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public bool IsRead { get; set; }
}

public class CallCentreReportMenuDto { public List<string> Reports { get; set; } = new(); }

public class CallCentreDeptSitePeriodRequestDto
{
    public int DepartmentCode { get; set; }
    public int? SiteCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class CallCentreReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public int RecordCount { get; set; }
    public DateTime GeneratedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class CreateCallCentreDto
{
    public int? VmfCode { get; set; }
    public DateTime? CallTime { get; set; }
    public DateTime? CallDate { get; set; }
    public string? IncidentType { get; set; }
    public string? IncidentDesc { get; set; }
    public string? CaptureName { get; set; }
    public short? UserAccessCode { get; set; }
    public string? CallerName { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPersalno { get; set; }
    public string? DriverLicno { get; set; }
    public string? GGNumber { get; set; }
    public string? DriverBaseStation { get; set; }
    public short? DriverSite { get; set; }
    public string? DriverTel { get; set; }
    public string? DriverCell { get; set; }
}

public class UpdateCallCentreDto
{
    public int? VmfCode { get; set; }
    public DateTime? CallTime { get; set; }
    public DateTime? CallDate { get; set; }
    public string? IncidentType { get; set; }
    public string? IncidentDesc { get; set; }
    public string? CaptureName { get; set; }
    public short? UserAccessCode { get; set; }
    public string? CallerName { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPersalno { get; set; }
    public string? DriverLicno { get; set; }
    public string? GGNumber { get; set; }
    public string? DriverBaseStation { get; set; }
    public short? DriverSite { get; set; }
    public string? DriverTel { get; set; }
    public string? DriverCell { get; set; }
}

#endregion
