using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FineController : BaseApiController
{
    private readonly IFineRepository _repository;
    private readonly ILogger<FineController> _logger;

    public FineController(IFineRepository repository, ILogger<FineController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Fine>>> GetAll()
    {
        if (!HasReportsRole()) return Forbid();
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Fine>> GetById(int id)
    {
        if (!HasReportsRole()) return Forbid();
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("unpaid")]
    public async Task<ActionResult<IEnumerable<Fine>>> GetUnpaid()
    {
        if (!HasReportsRole()) return Forbid();
        try { return Ok(await _repository.GetUnpaidFinesAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Fine>> Create([FromBody] Fine item)
    {
        if (!HasReportsRole()) return Forbid();
        try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.Fine_code }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Fine>> Update(int id, [FromBody] Fine item)
    {
        if (!HasReportsRole()) return Forbid();
        try { if (id != item.Fine_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!HasReportsRole()) return Forbid();
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    private bool HasReportsRole() => HasAnyRole("Reports");

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

        var roleClaims = User.Claims
            .Where(claim => claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

        return roleClaims.Any(role => expectedRoles.Any(expected => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)));
    }
}
