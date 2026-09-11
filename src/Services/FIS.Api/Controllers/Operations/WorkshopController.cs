using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WorkshopController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly IWorkshopRepository _repository;
    private readonly ILogger<WorkshopController> _logger;

    public WorkshopController(IWorkshopRepository repository, ILogger<WorkshopController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Workshop>>> GetAll()
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            return Ok(await _repository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? searchField = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasWorkshopRole())
            return Forbid();

        var normalizedStatus = status?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedStatus is not ("" or "all" or "open" or "closed" or "vehicle"))
        {
            return BadRequest(
                new { error = "Workshop status must be all, open, closed, or vehicle." }
            );
        }

        var normalizedSearchField = searchField?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedSearchField is not ("" or "fleet" or "registration"))
        {
            return BadRequest(
                new { error = "Workshop search field must be fleet or registration." }
            );
        }

        try
        {
            var result = await _repository.GetPageAsync(
                new WorkshopPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    search,
                    normalizedStatus,
                    normalizedSearchField
                )
            );

            return Ok(
                new
                {
                    items = result
                        .Items.Select(item => new
                        {
                            ww_code = item.WwCode,
                            vmf_code = item.VmfCode,
                            receive_date = item.ReceiveDate,
                            complete_time = item.CompleteTime,
                            complete_date = item.CompleteDate,
                            fleet_number = item.FleetNumber,
                            registration_number = item.RegistrationNumber,
                            location_code = item.LocationCode,
                            status = item.Status,
                        })
                        .ToList(),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged workshop entries");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Workshop>> GetById(short id)
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Workshop>> Create([FromBody] Workshop item)
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.ww_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Workshop>> Update(short id, [FromBody] Workshop item)
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            if (id != item.ww_code)
                return BadRequest();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    private bool HasWorkshopRole()
    {
        if (User.IsInRole("Workshop"))
        {
            return true;
        }

        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );

        return roleClaims.Any(role =>
            string.Equals(role, "Workshop", StringComparison.OrdinalIgnoreCase)
        );
    }
}
