using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotifyListController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ILogger<NotifyListController> _logger;

    public NotifyListController(FisDbContext context, ILogger<NotifyListController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotifyListDto>>> GetAll([FromQuery] string? search = null)
    {
        try
        {
            var query = _context.NotifyLists
                .AsNoTracking()
                .Where(x => !x.is_deleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x =>
                    (x.Notify_list_desc ?? string.Empty).Contains(term) ||
                    (x.Notify_email1 ?? string.Empty).Contains(term));
            }

            var items = await query
                .OrderBy(x => x.Notify_list_desc)
                .ThenBy(x => x.Notify_list_code)
                .Select(x => new NotifyListDto
                {
                    Notify_list_code = x.Notify_list_code,
                    Notify_list_desc = x.Notify_list_desc,
                    Notify_email1 = x.Notify_email1,
                    date_created = x.date_created,
                    date_updated = x.date_updated
                })
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notify list records");
            return StatusCode(500, "Error retrieving notify list records");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotifyListDto>> GetById(int id)
    {
        try
        {
            var item = await _context.NotifyLists
                .AsNoTracking()
                .Where(x => !x.is_deleted && x.Notify_list_code == id)
                .Select(x => new NotifyListDto
                {
                    Notify_list_code = x.Notify_list_code,
                    Notify_list_desc = x.Notify_list_desc,
                    Notify_email1 = x.Notify_email1,
                    date_created = x.date_created,
                    date_updated = x.date_updated
                })
                .FirstOrDefaultAsync();

            return item is null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notify list record {Id}", id);
            return StatusCode(500, "Error retrieving notify list record");
        }
    }

    [HttpPost]
    public async Task<ActionResult<NotifyListDto>> Create([FromBody] NotifyListCreateUpdateDto request)
    {
        try
        {
            var entity = new NotifyList
            {
                Notify_list_desc = request.Notify_list_desc?.Trim(),
                Notify_email1 = request.Notify_email1?.Trim(),
                date_created = DateTime.UtcNow,
                date_updated = null,
                created_by_user_code = GetCurrentUserId(),
                modified_by_user_code = null,
                is_deleted = false
            };

            _context.NotifyLists.Add(entity);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Notify_list_code }, new NotifyListDto
            {
                Notify_list_code = entity.Notify_list_code,
                Notify_list_desc = entity.Notify_list_desc,
                Notify_email1 = entity.Notify_email1,
                date_created = entity.date_created,
                date_updated = entity.date_updated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notify list record");
            return StatusCode(500, "Error creating notify list record");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NotifyListDto>> Update(int id, [FromBody] NotifyListCreateUpdateDto request)
    {
        try
        {
            var entity = await _context.NotifyLists
                .FirstOrDefaultAsync(x => !x.is_deleted && x.Notify_list_code == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.Notify_list_desc = request.Notify_list_desc?.Trim();
            entity.Notify_email1 = request.Notify_email1?.Trim();
            entity.date_updated = DateTime.UtcNow;
            entity.modified_by_user_code = GetCurrentUserId();

            await _context.SaveChangesAsync();

            return Ok(new NotifyListDto
            {
                Notify_list_code = entity.Notify_list_code,
                Notify_list_desc = entity.Notify_list_desc,
                Notify_email1 = entity.Notify_email1,
                date_created = entity.date_created,
                date_updated = entity.date_updated
            });
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
            var entity = await _context.NotifyLists
                .FirstOrDefaultAsync(x => !x.is_deleted && x.Notify_list_code == id);

            if (entity is null)
            {
                return NotFound();
            }

            entity.is_deleted = true;
            entity.date_updated = DateTime.UtcNow;
            entity.modified_by_user_code = GetCurrentUserId();

            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notify list record {Id}", id);
            return StatusCode(500, "Error deleting notify list record");
        }
    }
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
