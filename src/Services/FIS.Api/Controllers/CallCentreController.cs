using System.Data;
using System.Data.Common;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Api.Services;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CallCentreController : BaseApiController
{
    private readonly ICallCentreRepository _repository;
    private readonly ITowingRepository _towingRepository;
    private readonly AccidentCompatibilityService _accidentService;
    private readonly LossCompatibilityService _lossService;
    private readonly CallCentreEditCompatibilityService _editCompatibilityService;
    private readonly FisDbContext _context;
    private readonly ILogger<CallCentreController> _logger;

    public CallCentreController(
        ICallCentreRepository repository,
        ITowingRepository towingRepository,
        AccidentCompatibilityService accidentService,
        LossCompatibilityService lossService,
        CallCentreEditCompatibilityService editCompatibilityService,
        FisDbContext context,
        ILogger<CallCentreController> logger)
    {
        _repository = repository;
        _towingRepository = towingRepository;
        _accidentService = accidentService;
        _lossService = lossService;
        _editCompatibilityService = editCompatibilityService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("hijack")]
    public async Task<ActionResult<HiJackCreateResultDto>> CreateHiJack(
        [FromBody] CreateHiJackDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.VmfCode is not > 0)
        {
            return BadRequest(new { error = "A valid vehicle is required." });
        }

        if (!string.Equals(dto.IncidentType, "Hi-Jack", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "The incident type must be Hi-Jack." });
        }

        if (dto.IncidentDate is null)
        {
            return BadRequest(new { error = "A valid Hi-Jack date is required." });
        }

        if (!IsIncidentChoice(dto.InformCro) || !IsIncidentChoice(dto.CallClosed))
        {
            return BadRequest(new { error = "The incident notification and closure choices are invalid." });
        }

        var currentUserId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var call = new CallCentre
        {
            Call_time = now,
            Call_date = now.Date,
            Incident_date = dto.IncidentDate.Value.Date,
            Incident_time = dto.IncidentTime,
            Counter = 1,
            User_access_code = GetLegacyUserAccessCode(),
            Capture_name = User.Identity?.Name
        };
        ApplyFields(call, dto);
        call.Incident_type = "Hi-Jack";
        call.User_access_code = GetLegacyUserAccessCode();
        call.Capture_name = User.Identity?.Name;

        // The legacy form uses the transport officer as the caller and driver
        // when those optional fields are left blank.
        var callerProvided = !string.IsNullOrWhiteSpace(dto.CallerName);
        call.Caller_name = callerProvided ? dto.CallerName : dto.TransportOfficerName;
        call.Caller_tel = callerProvided ? dto.CallerTel : dto.TransportOfficerTel;
        call.Caller_fax = callerProvided ? dto.CallerFax : dto.TransportOfficerFax;
        call.Caller_email = callerProvided ? dto.CallerEmail : dto.TransportOfficerEmail;

        var driverProvided = !string.IsNullOrWhiteSpace(dto.DriverName);
        call.Driver_name = driverProvided ? dto.DriverName : dto.TransportOfficerName;
        call.Driver_tel = driverProvided ? dto.DriverTel : dto.TransportOfficerTel;

        try
        {
            var created = await _repository.CreateAsync(call, currentUserId);
            return Ok(new HiJackCreateResultDto
            {
                CallCentreCode = created.Call_centre_code
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating Hi-Jack record for vehicle {VmfCode}",
                dto.VmfCode);
            return StatusCode(500, new { error = "Failed to create Hi-Jack record." });
        }
    }

    [HttpPost("loss")]
    public async Task<ActionResult<LossCreateResultDto>> CreateLoss(
        [FromBody] CreateLossDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.VmfCode is not > 0)
        {
            return BadRequest(new { error = "A valid vehicle is required." });
        }

        if (!string.Equals(dto.IncidentType, "Loss_Theft", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "The incident type must be Loss_Theft." });
        }

        if (dto.IncidentDate is null)
        {
            return BadRequest(new { error = "A valid loss date is required." });
        }

        if (dto.LossTypeCode is not > 0)
        {
            return BadRequest(new { error = "A loss type is required." });
        }

        if (!IsIncidentChoice(dto.InformCro) || !IsIncidentChoice(dto.CallClosed) ||
            !IsIncidentChoice(dto.TowNeed))
        {
            return BadRequest(new { error = "The incident notification, closure, or towing choices are invalid." });
        }

        var currentUserId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var rawDriverName = dto.DriverName;
        var call = new CallCentre
        {
            Call_time = now,
            Call_date = now.Date,
            Incident_date = dto.IncidentDate.Value.Date,
            Incident_time = null,
            Counter = 1,
            User_access_code = GetLegacyUserAccessCode(),
            Capture_name = User.Identity?.Name
        };
        ApplyFields(call, dto);
        call.Incident_type = "Loss_Theft";
        call.User_access_code = GetLegacyUserAccessCode();
        call.Capture_name = User.Identity?.Name;

        var callerProvided = !string.IsNullOrWhiteSpace(dto.CallerName);
        call.Caller_name = callerProvided ? dto.CallerName : dto.TransportOfficerName;
        call.Caller_tel = callerProvided ? dto.CallerTel : dto.TransportOfficerTel;
        call.Caller_fax = callerProvided ? dto.CallerFax : dto.TransportOfficerFax;
        call.Caller_email = callerProvided ? dto.CallerEmail : dto.TransportOfficerEmail;

        var driverProvided = !string.IsNullOrWhiteSpace(dto.DriverName);
        call.Driver_name = driverProvided ? dto.DriverName : dto.TransportOfficerName;
        call.Driver_tel = driverProvided ? dto.DriverTel : dto.TransportOfficerTel;

        await using var transaction = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            var createdCall = await _repository.CreateAsync(call, currentUserId);
            var createdLoss = await _lossService.CreateAsync(
                new LossCaptureValues(
                    dto.VmfCode.Value,
                    dto.IncidentDate.Value.Date,
                    dto.LossTypeCode,
                    dto.TransportOfficerSite,
                    dto.TransportOfficerName,
                    dto.IncidentTown,
                    rawDriverName,
                    dto.IncidentRemarks,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    createdCall.Call_centre_code,
                    dto.TowNeed),
                currentUserId,
                HttpContext.RequestAborted);

            await transaction.CommitAsync(HttpContext.RequestAborted);
            return Ok(new LossCreateResultDto
            {
                CallCentreCode = createdCall.Call_centre_code,
                LossCode = createdLoss,
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(
                ex,
                "Error creating Loss/Theft record for vehicle {VmfCode}",
                dto.VmfCode);
            return StatusCode(500, new { error = "Failed to create Loss/Theft record." });
        }
    }

    [HttpPost("accident")]
    public async Task<ActionResult<AccidentCreateResultDto>> CreateAccident(
        [FromBody] CreateAccidentDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.VmfCode is not > 0)
        {
            return BadRequest(new { error = "A valid vehicle is required." });
        }

        if (!string.Equals(dto.IncidentType, "Accident", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "The incident type must be Accident." });
        }

        if (!IsIncidentChoice(dto.InformCro) || !IsIncidentChoice(dto.CallClosed))
        {
            return BadRequest(new { error = "The incident notification and closure choices are invalid." });
        }

        if (!IsQuestionChoice(dto.Death) || !IsQuestionChoice(dto.Injured))
        {
            return BadRequest(new { error = "The death and injury choices are invalid." });
        }

        if (!IsIncidentChoice(dto.TowNeed))
        {
            return BadRequest(new { error = "A tow requirement choice is required." });
        }

        var currentUserId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var call = new CallCentre
        {
            Call_time = now,
            Call_date = now.Date,
            Incident_date = dto.IncidentDate ?? now.Date,
            Incident_time = dto.IncidentTime,
            Counter = 1,
            User_access_code = GetLegacyUserAccessCode(),
            Capture_name = User.Identity?.Name
        };
        ApplyFields(call, dto);
        call.Incident_type = "Accident";
        call.User_access_code = GetLegacyUserAccessCode();
        call.Capture_name = User.Identity?.Name;

        var callerProvided = !string.IsNullOrWhiteSpace(dto.CallerName);
        call.Caller_name = callerProvided ? dto.CallerName : dto.TransportOfficerName;
        call.Caller_tel = callerProvided ? dto.CallerTel : dto.TransportOfficerTel;
        call.Caller_fax = callerProvided ? dto.CallerFax : dto.TransportOfficerFax;
        call.Caller_email = callerProvided ? dto.CallerEmail : dto.TransportOfficerEmail;

        var driverProvided = !string.IsNullOrWhiteSpace(dto.DriverName);
        call.Driver_name = driverProvided ? dto.DriverName : dto.TransportOfficerName;
        call.Driver_tel = driverProvided ? dto.DriverTel : dto.TransportOfficerTel;

        await using var transaction = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            var createdCall = await _repository.CreateAsync(call, currentUserId);
            var createdAccident = await _accidentService.CreateAsync(
                new AccidentCaptureValues(
                    dto.VmfCode.Value,
                    createdCall.Call_centre_code,
                    "Cal",
                    dto.IncidentDate ?? now.Date,
                    dto.IncidentTime,
                    dto.AccidentDescription,
                    dto.AccidentDriverName ?? dto.DriverName,
                    dto.AccidentDriverEmployNumber ?? dto.DriverPersalno,
                    dto.AccidentDriverTel ?? dto.DriverTel,
                    dto.TransportOfficerSite,
                    dto.TransportOfficerName,
                    dto.TransportOfficerTel,
                    dto.Death,
                    dto.Injured,
                    dto.ThirdPartyRegistration,
                    dto.ThirdPartyOwner,
                    dto.ThirdPartyTelephone,
                    dto.DamageDescription,
                    now,
                    dto.AccidentNotes,
                    1,
                    "C",
                    now,
                    dto.OccurencePlace,
                    dto.TowNeed),
                currentUserId,
                HttpContext.RequestAborted);

            await transaction.CommitAsync(HttpContext.RequestAborted);
            return Ok(new AccidentCreateResultDto
            {
                CallCentreCode = createdCall.Call_centre_code,
                AccidentCode = createdAccident
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(
                ex,
                "Error creating accident record for vehicle {VmfCode}",
                dto.VmfCode);
            return StatusCode(500, new { error = "Failed to create accident record." });
        }
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

            var now = DateTime.UtcNow;
            var item = new CallCentre
            {
                Call_time = dto.CallTime ?? now,
                Call_date = dto.CallDate ?? now.Date,
                Incident_date = dto.IncidentDate ?? now.Date,
                Incident_time = dto.IncidentTime ?? now,
                Counter = dto.Counter ?? 1,
                User_access_code = dto.UserAccessCode ?? GetLegacyUserAccessCode()
            };
            ApplyFields(item, dto);

            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Call_centre_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating call centre record");
            return StatusCode(500, new { error = "Failed to create call centre record", message = ex.Message });
        }
    }

    [HttpPost("road-assistance")]
    public async Task<ActionResult<RoadAssistanceCreateResultDto>> CreateRoadAssistance(
        [FromBody] CreateRoadAssistanceDto dto)
    {
        if (dto.VmfCode is not > 0)
        {
            return BadRequest(new { error = "A valid vehicle is required." });
        }

        if (!string.Equals(dto.IncidentType, "Road_Assistance", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "The incident type must be Road_Assistance." });
        }

        if (!string.Equals(dto.InformCro, "Y", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dto.InformCro, "N", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Inform_CRO must be Y or N." });
        }

        if (!string.Equals(dto.CallClosed, "Y", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(dto.CallClosed, "N", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "call_closed must be Y or N." });
        }

        var currentUserId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var call = new CallCentre
        {
            Call_time = now,
            Call_date = now.Date,
            Incident_date = dto.IncidentDate ?? now.Date,
            Incident_time = dto.IncidentTime,
            Counter = 1,
            User_access_code = GetLegacyUserAccessCode(),
            Capture_name = User.Identity?.Name
        };
        ApplyFields(call, dto);
        call.User_access_code = GetLegacyUserAccessCode();
        call.Capture_name = User.Identity?.Name;

        await using var transaction = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            var createdCall = await _repository.CreateAsync(call, currentUserId);
            var towing = new Towing
            {
                vmf_code = dto.VmfCode.Value,
                Call_refer = createdCall.Call_centre_code,
                Tow_request_date = now.Date,
                Tow_request_time = now,
                Tow_location_start = dto.TowingLocationStart,
                Vehicle_problem = dto.VehicleProblem,
                Site_code = dto.TransportOfficerSite,
                Tow_Truck_code = dto.TowTruckCode,
                Contact_person_name = dto.TransportOfficerName,
                Contact_person_tel = dto.TransportOfficerTel,
                Remaks = dto.TowingRemarks
            };
            var createdTowing = await _towingRepository.CreateAsync(towing, currentUserId);

            await transaction.CommitAsync(HttpContext.RequestAborted);
            return Ok(new RoadAssistanceCreateResultDto
            {
                CallCentreCode = createdCall.Call_centre_code,
                TowingCode = createdTowing.Towing_code
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(
                ex,
                "Error creating road assistance record for vehicle {VmfCode}",
                dto.VmfCode);
            return StatusCode(500, new { error = "Failed to create road assistance record." });
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

            ApplyFields(existing, dto);

            var updated = await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating call centre record {Id}", id);
            return StatusCode(500, new { error = "Failed to update call centre record", message = ex.Message });
        }
    }

    [HttpGet("{id}/edit-details")]
    public async Task<ActionResult<CallCentreEditDetails>> GetEditDetails(short id)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { error = "Call centre record not found", id });
            }

            return Ok(await _editCompatibilityService.GetAsync(id, HttpContext.RequestAborted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading child edit details for call centre record {Id}", id);
            return StatusCode(500, new { error = "Failed to load call centre edit details." });
        }
    }

    [HttpPut("{id}/edit-details")]
    public async Task<ActionResult<CallCentre>> UpdateEditDetails(
        short id,
        [FromBody] UpdateCallCentreEditDetailsDto dto)
    {
        if (!ModelState.IsValid || dto.CallCentre is null)
        {
            return BadRequest(ModelState);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { error = "Call centre record not found", id });
            }

            ApplyFields(existing, dto.CallCentre);
            var updated = await _repository.UpdateAsync(existing, GetCurrentUserId());
            await _editCompatibilityService.UpdateAsync(
                id,
                dto.ChildUpdates ?? new CallCentreEditUpdate(),
                GetCurrentUserId(),
                HttpContext.RequestAborted);

            await transaction.CommitAsync(HttpContext.RequestAborted);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(HttpContext.RequestAborted);
            _logger.LogError(ex, "Error updating complete call centre edit workflow for record {Id}", id);
            return StatusCode(500, new { error = "Failed to update call centre edit details." });
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
    /// Get the legacy per-access audit rows for one Call_centre record.
    /// Call_Centre_Counter is present in the original database but its audit
    /// columns are only present in the expanded schema, so the projection is
    /// resolved at runtime in the same way as the Call_centre repository.
    /// </summary>
    [HttpGet("reports/data-access/{callCentreCode:int}")]
    public async Task<ActionResult<CallCentreDataAccessReportDto>> GetReportDataAccessDetail(int callCentreCode)
    {
        if (callCentreCode <= 0 || callCentreCode > short.MaxValue)
        {
            return BadRequest(new { error = "A valid Call Centre reference is required." });
        }

        try
        {
            var call = await _repository.GetByIdAsync((short)callCentreCode);
            if (call == null)
            {
                return NotFound(new { error = "Call centre record not found." });
            }

            var access = await ReadLegacyAccessRowsAsync((short)callCentreCode);
            return Ok(new CallCentreDataAccessReportDto
            {
                CallCentreCode = (short)callCentreCode,
                AccessTableAvailable = access.Available,
                Entries = access.Entries
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading data access detail for call centre record {CallCentreCode}", callCentreCode);
            return StatusCode(500, new { error = "Failed to load data access detail." });
        }
    }

    private async Task<(bool Available, List<CallCentreDataAccessEntryDto> Entries)> ReadLegacyAccessRowsAsync(short callCentreCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(HttpContext.RequestAborted);
        }

        try
        {
            await using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddDbParameter(columnsCommand, "@schema", DbType.String, "dbo");
            AddDbParameter(columnsCommand, "@table", DbType.String, "Call_Centre_Counter");

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await columnsCommand.ExecuteReaderAsync(HttpContext.RequestAborted))
            {
                while (await reader.ReadAsync(HttpContext.RequestAborted))
                {
                    columns.Add(reader.GetString(0));
                }
            }

            if (!columns.Contains("Call_Center_code"))
            {
                return (false, new List<CallCentreDataAccessEntryDto>());
            }

            var counterProjection = columns.Contains("CounterCC")
                ? "[CounterCC] AS [CounterCC]"
                : "CAST(NULL AS smallint) AS [CounterCC]";
            var dataCaptureProjection = columns.Contains("DataCapture_id")
                ? "[DataCapture_id] AS [DataCapture_id]"
                : "CAST(NULL AS smallint) AS [DataCapture_id]";
            var dateProjection = columns.Contains("DataCapture_date")
                ? "[DataCapture_date] AS [DataCapture_date]"
                : "CAST(NULL AS datetime2) AS [DataCapture_date]";
            var timeProjection = columns.Contains("DataCapture_time")
                ? "[DataCapture_time] AS [DataCapture_time]"
                : "CAST(NULL AS datetime2) AS [DataCapture_time]";
            var orderBy = columns.Contains("Call_Centre_Counter_code")
                ? "ORDER BY [Call_Centre_Counter_code]"
                : columns.Contains("DataCapture_date")
                    ? $"ORDER BY [DataCapture_date]{(columns.Contains("DataCapture_time") ? ", [DataCapture_time]" : string.Empty)}"
                    : "ORDER BY (SELECT 1)";

            await using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT {counterProjection}, {dataCaptureProjection}, {dateProjection}, {timeProjection}
                FROM [dbo].[Call_Centre_Counter]
                WHERE [Call_Center_code] = @callCentreCode
                {orderBy}
                """;
            AddDbParameter(command, "@callCentreCode", DbType.Int16, callCentreCode);

            var entries = new List<CallCentreDataAccessEntryDto>();
            await using var dataReader = await command.ExecuteReaderAsync(HttpContext.RequestAborted);
            while (await dataReader.ReadAsync(HttpContext.RequestAborted))
            {
                entries.Add(new CallCentreDataAccessEntryDto
                {
                    Counter = ReadNullableInt16(dataReader, "CounterCC"),
                    DataCaptureId = ReadNullableInt16(dataReader, "DataCapture_id"),
                    DataCaptureDate = ReadNullableDateTime(dataReader, "DataCapture_date"),
                    DataCaptureTime = ReadNullableDateTime(dataReader, "DataCapture_time")
                });
            }

            return (true, entries);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddDbParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static short? ReadNullableInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
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

    private short? GetLegacyUserAccessCode()
    {
        var userId = GetCurrentUserId();
        return userId is > 0 and <= short.MaxValue ? (short)userId : null;
    }

    private static void ApplyFields(CallCentre target, CallCentreFieldsDto dto)
    {
        target.vmf_code = dto.VmfCode;
        target.Call_time = dto.CallTime ?? target.Call_time;
        target.Call_date = dto.CallDate ?? target.Call_date;
        target.Incident_type = dto.IncidentType;
        target.Incident_Desc = dto.IncidentDesc;
        target.Capture_name = dto.CaptureName;
        target.User_access_code = dto.UserAccessCode ?? target.User_access_code;
        target.Caller_name = dto.CallerName;
        target.Driver_name = dto.DriverName;
        target.Driver_persalno = dto.DriverPersalno;
        target.Driver_Licno = dto.DriverLicno;
        target.GG_number = dto.GGNumber;
        target.Driver_base_station = dto.DriverBaseStation;
        target.Driver_Site = dto.DriverSite;
        target.Driver_tel = dto.DriverTel;
        target.Driver_cell = dto.DriverCell;
        target.Driver_fax = dto.DriverFax;
        target.Driver_email = dto.DriverEmail;
        target.Incident_date = dto.IncidentDate ?? target.Incident_date;
        target.Incident_time = dto.IncidentTime ?? target.Incident_time;
        target.Caller_tel = dto.CallerTel;
        target.TrOfficer_name = dto.TransportOfficerName;
        target.TrOfficer_tel = dto.TransportOfficerTel;
        target.TrOfficer_Site = dto.TransportOfficerSite;
        target.Incident_town = dto.IncidentTown;
        target.Incident_street = dto.IncidentStreet;
        target.Counter = dto.Counter ?? target.Counter;
        target.Caller_fax = dto.CallerFax;
        target.TrOfficer_fax = dto.TransportOfficerFax;
        target.Caller_email = dto.CallerEmail;
        target.TrOfficer_email = dto.TransportOfficerEmail;
        target.Inform_CRO = dto.InformCro;
        target.CRO_Remarks = dto.CroRemarks;
        target.Incident_Remarks = dto.IncidentRemarks;
        target.Notify_list_code = dto.NotifyListCode;
        target.call_closed = dto.CallClosed;
    }

    private static bool IsIncidentChoice(string? value)
        => string.Equals(value, "Y", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "N", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuestionChoice(string? value)
        => IsIncidentChoice(value) || string.Equals(value, "?", StringComparison.Ordinal);
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

public class CallCentreDataAccessReportDto
{
    public short CallCentreCode { get; set; }
    public bool AccessTableAvailable { get; set; }
    public List<CallCentreDataAccessEntryDto> Entries { get; set; } = new();
}

public class CallCentreDataAccessEntryDto
{
    public short? Counter { get; set; }
    public short? DataCaptureId { get; set; }
    public DateTime? DataCaptureDate { get; set; }
    public DateTime? DataCaptureTime { get; set; }
}

public class CreateCallCentreDto : CallCentreFieldsDto
{
}

public class UpdateCallCentreDto : CallCentreFieldsDto
{
}

public class UpdateCallCentreEditDetailsDto
{
    public UpdateCallCentreDto? CallCentre { get; set; }
    public CallCentreEditUpdate? ChildUpdates { get; set; }
}

public class CreateRoadAssistanceDto : CallCentreFieldsDto
{
    public string? TowingLocationStart { get; set; }
    public string? VehicleProblem { get; set; }
    public string? TowingRemarks { get; set; }
    public short? TowTruckCode { get; set; }
}

public class CreateAccidentDto : CallCentreFieldsDto
{
    public string? AccidentDescription { get; set; }
    public string? DamageDescription { get; set; }
    public string? ThirdPartyRegistration { get; set; }
    public string? ThirdPartyOwner { get; set; }
    public string? ThirdPartyTelephone { get; set; }
    public string? Death { get; set; }
    public string? Injured { get; set; }
    public string? OccurencePlace { get; set; }
    public string? TowNeed { get; set; }
    public string? AccidentNotes { get; set; }
    public string? AccidentDriverName { get; set; }
    public string? AccidentDriverTel { get; set; }
    public string? AccidentDriverEmployNumber { get; set; }
}

public class CreateHiJackDto : CallCentreFieldsDto
{
}

public class CreateLossDto : CallCentreFieldsDto
{
    public short? LossTypeCode { get; set; }
    public string? TowNeed { get; set; }
}

public class RoadAssistanceCreateResultDto
{
    public short CallCentreCode { get; set; }
    public short TowingCode { get; set; }
}

public class AccidentCreateResultDto
{
    public short CallCentreCode { get; set; }
    public int AccidentCode { get; set; }
}

public class HiJackCreateResultDto
{
    public short CallCentreCode { get; set; }
}

public class LossCreateResultDto
{
    public short CallCentreCode { get; set; }
    public short LossCode { get; set; }
}

public abstract class CallCentreFieldsDto
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
    public string? DriverFax { get; set; }
    public string? DriverEmail { get; set; }
    public DateTime? IncidentDate { get; set; }
    public DateTime? IncidentTime { get; set; }
    public string? CallerTel { get; set; }
    public string? TransportOfficerName { get; set; }
    public string? TransportOfficerTel { get; set; }
    public short? TransportOfficerSite { get; set; }
    public string? IncidentTown { get; set; }
    public string? IncidentStreet { get; set; }
    public short? Counter { get; set; }
    public string? CallerFax { get; set; }
    public string? TransportOfficerFax { get; set; }
    public string? CallerEmail { get; set; }
    public string? TransportOfficerEmail { get; set; }
    public string? InformCro { get; set; }
    public string? CroRemarks { get; set; }
    public string? IncidentRemarks { get; set; }
    public int? NotifyListCode { get; set; }
    public string? CallClosed { get; set; }
}

#endregion
