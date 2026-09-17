using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrafficDeptController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const int MaximumSearchQueryLength = 50;

    private readonly ITrafficDeptRepository _repository;
    private readonly ILogger<TrafficDeptController> _logger;

    public TrafficDeptController(
        ITrafficDeptRepository repository,
        ILogger<TrafficDeptController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrafficDept>>> GetAll()
    {
        if (!HasReportsRole())
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
        [FromQuery] string? searchQuery = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasReportsRole())
            return Forbid();

        var normalizedSearchQuery = searchQuery?.Trim() ?? string.Empty;
        if (normalizedSearchQuery.Length > MaximumSearchQueryLength)
        {
            return BadRequest(new { error = "Search query cannot exceed 50 characters." });
        }

        try
        {
            var result = await _repository.GetPageAsync(
                new TrafficDeptPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    normalizedSearchQuery
                )
            );

            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged traffic department records");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TrafficDept>> GetById(short id)
    {
        if (!HasReportsRole())
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

    [HttpGet("name/{name}")]
    public async Task<ActionResult<TrafficDept>> GetByName(string name)
    {
        if (!HasReportsRole())
            return Forbid();
        try
        {
            var item = await _repository.GetByNameAsync(name);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<TrafficDept>> Create([FromBody] TrafficDept item)
    {
        if (!HasReportsRole())
            return Forbid();
        try
        {
            var name = item.Traf_name?.Trim() ?? string.Empty;
            if (ValidateName(name) is { } validationError)
                return BadRequest(new { error = validationError });

            var duplicate = await _repository.GetByNameAsync(name);
            if (duplicate is not null)
                return Conflict(new { error = "A traffic department with this name already exists." });

            item.Traf_name = name;
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Traffic_dept_code },
                created
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TrafficDept>> Update(short id, [FromBody] TrafficDept item)
    {
        if (!HasReportsRole())
            return Forbid();
        try
        {
            if (item is null || id != item.Traffic_dept_code)
                return BadRequest();
            var name = item.Traf_name?.Trim() ?? string.Empty;
            if (ValidateName(name) is { } validationError)
                return BadRequest(new { error = validationError });

            var duplicate = await _repository.GetByNameAsync(name);
            if (duplicate is not null && duplicate.Traffic_dept_code != id)
                return Conflict(new { error = "A traffic department with this name already exists." });

            item.Traf_name = name;
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
        if (!HasReportsRole())
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

    private bool HasReportsRole() =>
        HasAnyRole("Fines", "Reports", "SystemAdministrator", "System Administrator");

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Traffic department name is required.";
        if (name.Trim().Length > 50)
            return "Traffic department name must be 50 characters or fewer.";
        return null;
    }

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
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
            expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }
}
