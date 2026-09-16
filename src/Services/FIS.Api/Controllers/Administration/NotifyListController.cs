using FIS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User Administration")]
public class NotifyListController : BaseApiController
{
    private readonly NotifyListCompatibilityService _notifyListService;
    private readonly ILogger<NotifyListController> _logger;

    public NotifyListController(
        NotifyListCompatibilityService notifyListService,
        ILogger<NotifyListController> logger
    )
    {
        _notifyListService = notifyListService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotifyListDto>>> GetAll(
        [FromQuery] string? search = null
    )
    {
        try
        {
            var items = await _notifyListService.GetAllAsync(search, HttpContext.RequestAborted);
            return Ok(items.Select(ToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notify list records");
            return StatusCode(500, "Error retrieving notify list records");
        }
    }

    [HttpGet("page")]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        try
        {
            var result = await _notifyListService.GetPageAsync(
                search,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100),
                HttpContext.RequestAborted
            );

            return Ok(
                new
                {
                    items = result.Items.Select(ToDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged notify list records");
            return StatusCode(500, "Error retrieving notify list records");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotifyListDto>> GetById(int id)
    {
        try
        {
            var item = await _notifyListService.GetByIdAsync(id, HttpContext.RequestAborted);

            return item is null ? NotFound() : Ok(ToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notify list record {Id}", id);
            return StatusCode(500, "Error retrieving notify list record");
        }
    }

    [HttpPost]
    public async Task<ActionResult<NotifyListDto>> Create(
        [FromBody] NotifyListCreateUpdateDto request
    )
    {
        try
        {
            var validation = Validate(request);
            if (validation is not null)
            {
                return BadRequest(validation);
            }

            var item = await _notifyListService.CreateAsync(
                request.Notify_list_desc?.Trim(),
                request.Notify_email1?.Trim(),
                GetCurrentUserIdOrNull(),
                HttpContext.RequestAborted
            );

            return item is null
                ? StatusCode(500, "Error creating notify list record")
                : CreatedAtAction(nameof(GetById), new { id = item.Notify_list_code }, ToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notify list record");
            return StatusCode(500, "Error creating notify list record");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NotifyListDto>> Update(
        int id,
        [FromBody] NotifyListCreateUpdateDto request
    )
    {
        try
        {
            var validation = Validate(request);
            if (validation is not null)
            {
                return BadRequest(validation);
            }

            var item = await _notifyListService.UpdateAsync(
                id,
                request.Notify_list_desc?.Trim(),
                request.Notify_email1?.Trim(),
                GetCurrentUserIdOrNull(),
                HttpContext.RequestAborted
            );

            return item is null ? NotFound() : Ok(ToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notify list record {Id}", id);
            return StatusCode(500, "Error updating notify list record");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _notifyListService.DeleteAsync(
                id,
                GetCurrentUserIdOrNull(),
                HttpContext.RequestAborted
            );
            return deleted ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notify list record {Id}", id);
            return StatusCode(500, "Error deleting notify list record");
        }
    }

    private int? GetCurrentUserIdOrNull()
    {
        var claim = User.FindFirst("user_access_code")?.Value;
        return int.TryParse(claim, out var userId) ? userId : null;
    }

    private static string? Validate(NotifyListCreateUpdateDto request)
    {
        var description = request.Notify_list_desc?.Trim();
        var email = request.Notify_email1?.Trim();

        if (string.IsNullOrWhiteSpace(description))
        {
            return "Notification section name is required.";
        }

        if (description.Length > 40)
        {
            return "Notification section name must be 40 characters or fewer.";
        }

        if (email?.Length > 240)
        {
            return "Email address must be 240 characters or fewer.";
        }

        return null;
    }

    private static NotifyListDto ToDto(NotifyListRecord item) =>
        new()
        {
            Notify_list_code = item.Notify_list_code,
            Notify_list_desc = item.Notify_list_desc,
            Notify_email1 = item.Notify_email1,
            date_created = item.date_created,
            date_updated = item.date_updated,
        };
}

public class NotifyListDto
{
    public int Notify_list_code { get; set; }
    public string? Notify_list_desc { get; set; }
    public string? Notify_email1 { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_updated { get; set; }
}

public class NotifyListCreateUpdateDto
{
    public string? Notify_list_desc { get; set; }
    public string? Notify_email1 { get; set; }
}
