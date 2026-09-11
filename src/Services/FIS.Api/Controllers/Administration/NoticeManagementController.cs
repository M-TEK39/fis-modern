using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/notice-management")]
[Authorize]
public class NoticeManagementController : BaseApiController
{
    private readonly INoticeRepository _noticeRepository;
    private readonly INoticeScheduleRepository _noticeScheduleRepository;
    private readonly ILogger<NoticeManagementController> _logger;

    public NoticeManagementController(
        INoticeRepository noticeRepository,
        INoticeScheduleRepository noticeScheduleRepository,
        ILogger<NoticeManagementController> logger
    )
    {
        _noticeRepository = noticeRepository;
        _noticeScheduleRepository = noticeScheduleRepository;
        _logger = logger;
    }

    #region Menu

    /// <summary>
    /// Get notice management menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<NoticeManagementMenuDto> GetMenu()
    {
        var menu = new NoticeManagementMenuDto
        {
            Options = new List<string>
            {
                "Notice Schedules",
                "Notice Details",
                "Create Notice",
                "View Active Notices",
            },
        };
        return Ok(menu);
    }

    #endregion

    #region Notice Schedules

    /// <summary>
    /// Get all notice schedules
    /// </summary>
    [HttpGet("notice-schedules")]
    public async Task<ActionResult<IEnumerable<NoticeScheduleDto>>> GetAllNoticeSchedules()
    {
        try
        {
            _logger.LogInformation("Getting all notice schedules");
            var schedules = await _noticeScheduleRepository.GetAllAsync();

            var scheduleDtos = schedules.Select(s => new NoticeScheduleDto
            {
                NoticeScheduleId = s.notice_schedule_id,
                NoticeId = s.notice_id,
                TitleField = s.title_field ?? "",
                StartDate = s.start_date,
                EndDate = s.end_date,
                SortOrder = s.sort_order,
                CreatedBy = s.CreatedByUser?.email,
                CreatedDate = s.date_created,
            });

            return Ok(scheduleDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all notice schedules");
            return StatusCode(500, "Error retrieving notice schedules");
        }
    }

    /// <summary>
    /// Get a page of notice schedules without changing the legacy collection response.
    /// </summary>
    [HttpGet("notice-schedules/page")]
    public async Task<IActionResult> GetNoticeSchedulesPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? today = null
    )
    {
        try
        {
            var result = await _noticeScheduleRepository.GetPageAsync(
                new NoticeSchedulePageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    search?.Trim(),
                    status?.Trim(),
                    today?.Date ?? DateTime.UtcNow.Date
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(ToNoticeScheduleDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged notice schedules");
            return StatusCode(500, "Error retrieving notice schedules");
        }
    }

    /// <summary>
    /// Create new notice schedule
    /// </summary>
    [HttpPost("notice-schedules")]
    public async Task<ActionResult<NoticeScheduleDto>> CreateNoticeSchedule(
        [FromBody] CreateNoticeScheduleDto request
    )
    {
        try
        {
            _logger.LogInformation(
                "Creating notice schedule for NoticeId: {NoticeId}",
                request.NoticeId
            );

            var noticeSchedule = new NoticeSchedule
            {
                notice_id = request.NoticeId,
                title_field = request.TitleField,
                start_date = request.StartDate,
                end_date = request.EndDate,
                sort_order = request.SortOrder,
            };

            var created = await _noticeScheduleRepository.CreateAsync(
                noticeSchedule,
                GetCurrentUserId()
            );

            var createdDto = new NoticeScheduleDto
            {
                NoticeScheduleId = created.notice_schedule_id,
                NoticeId = created.notice_id,
                TitleField = created.title_field ?? "",
                StartDate = created.start_date,
                EndDate = created.end_date,
                CreatedBy = GetCurrentUsername(),
                CreatedDate = created.date_created,
                SortOrder = created.sort_order,
            };

            return Ok(createdDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating notice schedule for NoticeId: {NoticeId}",
                request.NoticeId
            );
            return StatusCode(500, "Error creating notice schedule");
        }
    }

    /// <summary>
    /// Update existing notice schedule
    /// </summary>
    [HttpPut("notice-schedules/{id}")]
    public async Task<ActionResult<NoticeScheduleDto>> UpdateNoticeSchedule(
        int id,
        [FromBody] UpdateNoticeScheduleDto request
    )
    {
        try
        {
            _logger.LogInformation("Updating notice schedule {Id}", id);
            if (id != request.NoticeScheduleId)
                return BadRequest("ID mismatch");

            var existing = await _noticeScheduleRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Notice schedule with ID {id} not found" });

            existing.notice_id = request.NoticeId;
            existing.title_field = request.TitleField;
            existing.start_date = request.StartDate;
            existing.end_date = request.EndDate;
            existing.sort_order = request.SortOrder;

            var updated = await _noticeScheduleRepository.UpdateAsync(existing, GetCurrentUserId());

            var updatedDto = new NoticeScheduleDto
            {
                NoticeScheduleId = updated.notice_schedule_id,
                NoticeId = updated.notice_id,
                TitleField = updated.title_field ?? "",
                StartDate = updated.start_date,
                EndDate = updated.end_date,
                SortOrder = updated.sort_order,
            };

            return Ok(updatedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notice schedule {Id}", id);
            return StatusCode(500, "Error updating notice schedule");
        }
    }

    /// <summary>
    /// Delete notice schedule
    /// </summary>
    [HttpDelete("notice-schedules/{id}")]
    public async Task<ActionResult> DeleteNoticeSchedule(int id)
    {
        try
        {
            _logger.LogInformation("Deleting notice schedule {Id}", id);

            var existing = await _noticeScheduleRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Notice schedule with ID {id} not found" });

            await _noticeScheduleRepository.DeleteAsync(id, GetCurrentUserId());

            return Ok(new { message = "Notice schedule deleted successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notice schedule {Id}", id);
            return StatusCode(500, "Error deleting notice schedule");
        }
    }

    #endregion

    #region Notices

    /// <summary>
    /// Get notice by ID
    /// </summary>
    [HttpGet("notices/{id}")]
    public async Task<ActionResult<NoticeDto>> GetNoticeById(int id)
    {
        try
        {
            _logger.LogInformation("Getting notice {Id}", id);

            var notice = await _noticeRepository.GetByIdAsync(id);
            if (notice == null)
                return NotFound(new { message = $"Notice with ID {id} not found" });

            var noticeDto = new NoticeDto
            {
                NoticeId = notice.notice_id,
                NoticeDate = notice.notice_date,
                NoticeFrom = notice.notice_from,
                NoticeTitle = notice.notice_title,
                NoticeBody = notice.notice_body,
                NoticePerson = notice.notice_person,
                NoticePersonTitle = notice.notice_person_title,
                CreatedDate = notice.date_created,
            };

            return Ok(noticeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notice {Id}", id);
            return StatusCode(500, "Error retrieving notice");
        }
    }

    /// <summary>
    /// Create new notice
    /// </summary>
    [HttpPost("notices")]
    public async Task<ActionResult<NoticeDto>> CreateNotice([FromBody] CreateNoticeDto request)
    {
        try
        {
            _logger.LogInformation("Creating notice: {Title}", request.NoticeTitle);

            var notice = new Notice
            {
                notice_date = request.NoticeDate ?? DateTime.UtcNow,
                notice_from = request.NoticeFrom,
                notice_title = request.NoticeTitle,
                notice_body = request.NoticeBody,
                notice_person = request.NoticePerson,
                notice_person_title = request.NoticePersonTitle,
            };

            var created = await _noticeRepository.CreateAsync(notice, GetCurrentUserId());

            var createdDto = new NoticeDto
            {
                NoticeId = created.notice_id,
                NoticeDate = created.notice_date,
                NoticeFrom = created.notice_from,
                NoticeTitle = created.notice_title,
                NoticeBody = created.notice_body,
                NoticePerson = created.notice_person,
                NoticePersonTitle = created.notice_person_title,
                CreatedDate = created.date_created,
            };

            return Ok(createdDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notice: {Title}", request.NoticeTitle);
            return StatusCode(500, "Error creating notice");
        }
    }

    /// <summary>
    /// Update existing notice
    /// </summary>
    [HttpPut("notices/{id}")]
    public async Task<ActionResult<NoticeDto>> UpdateNotice(
        int id,
        [FromBody] UpdateNoticeDto request
    )
    {
        try
        {
            _logger.LogInformation("Updating notice {Id}", id);
            if (id != request.NoticeId)
                return BadRequest("ID mismatch");

            var existing = await _noticeRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Notice with ID {id} not found" });

            existing.notice_date = request.NoticeDate;
            existing.notice_from = request.NoticeFrom;
            existing.notice_title = request.NoticeTitle;
            existing.notice_body = request.NoticeBody;
            existing.notice_person = request.NoticePerson;
            existing.notice_person_title = request.NoticePersonTitle;

            var updated = await _noticeRepository.UpdateAsync(existing, GetCurrentUserId());

            var updatedDto = new NoticeDto
            {
                NoticeId = updated.notice_id,
                NoticeDate = updated.notice_date,
                NoticeFrom = updated.notice_from,
                NoticeTitle = updated.notice_title,
                NoticeBody = updated.notice_body,
                NoticePerson = updated.notice_person,
                NoticePersonTitle = updated.notice_person_title,
                CreatedDate = updated.date_created,
            };

            return Ok(updatedDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notice {Id}", id);
            return StatusCode(500, "Error updating notice");
        }
    }

    #endregion

    /// <summary>
    /// Helper to get current username from authentication context
    /// </summary>
    private string GetCurrentUsername()
    {
        // Get username from JWT claims or User.Identity
        var username = User?.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
            return username;

        // Fallback to user access code from claims
        var userAccessCodeClaim = User?.FindFirst("user_access_code")?.Value;
        if (!string.IsNullOrEmpty(userAccessCodeClaim))
            return $"User-{userAccessCodeClaim}";

        // Final fallback
        return "system";
    }

    private static NoticeScheduleDto ToNoticeScheduleDto(NoticeSchedule schedule) =>
        new()
        {
            NoticeScheduleId = schedule.notice_schedule_id,
            NoticeId = schedule.notice_id,
            TitleField = schedule.title_field ?? "",
            StartDate = schedule.start_date,
            EndDate = schedule.end_date,
            SortOrder = schedule.sort_order,
            CreatedBy = schedule.CreatedByUser?.email,
            CreatedDate = schedule.date_created,
        };
}

#region Notice Management DTOs

public class NoticeManagementMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class NoticeScheduleDto
{
    public int NoticeScheduleId { get; set; }
    public int NoticeId { get; set; }
    public string TitleField { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? SortOrder { get; set; }
}

public class CreateNoticeScheduleDto
{
    public int NoticeId { get; set; }
    public string TitleField { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateNoticeScheduleDto
{
    public int NoticeScheduleId { get; set; }
    public int NoticeId { get; set; }
    public string TitleField { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SortOrder { get; set; }
}

public class NoticeDto
{
    public int NoticeId { get; set; }
    public DateTime? NoticeDate { get; set; }
    public DateTime? CreatedDate { get; set; }
    public string? NoticeFrom { get; set; }
    public string? NoticeTitle { get; set; }
    public string? NoticeBody { get; set; }
    public string? NoticePerson { get; set; }
    public string? NoticePersonTitle { get; set; }
}

public class CreateNoticeDto
{
    public DateTime? NoticeDate { get; set; }
    public string? NoticeFrom { get; set; }
    public string? NoticeTitle { get; set; }
    public string? NoticeBody { get; set; }
    public string? NoticePerson { get; set; }
    public string? NoticePersonTitle { get; set; }
}

public class UpdateNoticeDto
{
    public int NoticeId { get; set; }
    public DateTime? NoticeDate { get; set; }
    public string? NoticeFrom { get; set; }
    public string? NoticeTitle { get; set; }
    public string? NoticeBody { get; set; }
    public string? NoticePerson { get; set; }
    public string? NoticePersonTitle { get; set; }
}

#endregion
