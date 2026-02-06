using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NoticeManagementController : BaseApiController
{
    private readonly ILogger<NoticeManagementController> _logger;

    public NoticeManagementController(ILogger<NoticeManagementController> logger)
    {
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
                "View Active Notices"
            }
        };
        return Ok(menu);
    }

    #endregion

    #region Notice Schedules

    /// <summary>
    /// Get all notice schedules
    /// </summary>
    [HttpGet("notice-schedules")]
    public ActionResult<IEnumerable<NoticeScheduleDto>> GetAllNoticeSchedules()
    {
        // TODO: Implement get all notice schedules from database
        _logger.LogInformation("Getting all notice schedules");
        return Ok(new List<NoticeScheduleDto>());
    }

    /// <summary>
    /// Create new notice schedule
    /// </summary>
    [HttpPost("notice-schedules")]
    public ActionResult<NoticeScheduleDto> CreateNoticeSchedule([FromBody] CreateNoticeScheduleDto request)
    {
        // TODO: Implement notice schedule creation
        _logger.LogInformation("Creating notice schedule for NoticeId: {NoticeId}", request.NoticeId);
        var created = new NoticeScheduleDto
        {
            NoticeScheduleId = 0,
            NoticeId = request.NoticeId,
            TitleField = request.TitleField,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedBy = GetCurrentUsername(),
            CreatedDate = DateTime.Now,
            SortOrder = request.SortOrder
        };
        return Ok(created);
    }

    /// <summary>
    /// Update existing notice schedule
    /// </summary>
    [HttpPut("notice-schedules/{id}")]
    public ActionResult<NoticeScheduleDto> UpdateNoticeSchedule(int id, [FromBody] UpdateNoticeScheduleDto request)
    {
        // TODO: Implement notice schedule update
        _logger.LogInformation("Updating notice schedule {Id}", id);
        if (id != request.NoticeScheduleId)
            return BadRequest("ID mismatch");

        var updated = new NoticeScheduleDto
        {
            NoticeScheduleId = id,
            NoticeId = request.NoticeId,
            TitleField = request.TitleField,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            SortOrder = request.SortOrder
        };
        return Ok(updated);
    }

    /// <summary>
    /// Delete notice schedule
    /// </summary>
    [HttpDelete("notice-schedules/{id}")]
    public ActionResult DeleteNoticeSchedule(int id)
    {
        // TODO: Implement notice schedule deletion
        _logger.LogInformation("Deleting notice schedule {Id}", id);
        return Ok(new { message = "Notice schedule deleted successfully", id });
    }

    #endregion

    #region Notices

    /// <summary>
    /// Get notice by ID
    /// </summary>
    [HttpGet("notices/{id}")]
    public ActionResult<NoticeDto> GetNoticeById(int id)
    {
        // TODO: Implement get notice by ID
        _logger.LogInformation("Getting notice {Id}", id);
        return NotFound();
    }

    /// <summary>
    /// Create new notice
    /// </summary>
    [HttpPost("notices")]
    public ActionResult<NoticeDto> CreateNotice([FromBody] CreateNoticeDto request)
    {
        // TODO: Implement notice creation
        _logger.LogInformation("Creating notice: {Title}", request.NoticeTitle);
        var created = new NoticeDto
        {
            NoticeId = 0,
            NoticeDate = DateTime.Now,
            NoticeFrom = request.NoticeFrom,
            NoticeTitle = request.NoticeTitle,
            NoticeBody = request.NoticeBody,
            NoticePerson = request.NoticePerson,
            NoticePersonTitle = request.NoticePersonTitle
        };
        return Ok(created);
    }

    /// <summary>
    /// Update existing notice
    /// </summary>
    [HttpPut("notices/{id}")]
    public ActionResult<NoticeDto> UpdateNotice(int id, [FromBody] UpdateNoticeDto request)
    {
        // TODO: Implement notice update
        _logger.LogInformation("Updating notice {Id}", id);
        if (id != request.NoticeId)
            return BadRequest("ID mismatch");

        var updated = new NoticeDto
        {
            NoticeId = id,
            NoticeDate = request.NoticeDate,
            NoticeFrom = request.NoticeFrom,
            NoticeTitle = request.NoticeTitle,
            NoticeBody = request.NoticeBody,
            NoticePerson = request.NoticePerson,
            NoticePersonTitle = request.NoticePersonTitle
        };
        return Ok(updated);
    }

    #endregion

    /// <summary>
    /// Helper to get current username (placeholder until user context is available)
    /// </summary>
    private string GetCurrentUsername()
    {
        // TODO: Get from User.Identity.Name or claims
        return "system";
    }
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
    public string? NoticeFrom { get; set; }
    public string? NoticeTitle { get; set; }
    public string? NoticeBody { get; set; }
    public string? NoticePerson { get; set; }
    public string? NoticePersonTitle { get; set; }
}

public class CreateNoticeDto
{
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
