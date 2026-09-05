using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrafficDeptController : BaseApiController
{
    private readonly ITrafficDeptRepository _repository;
    private readonly ILogger<TrafficDeptController> _logger;
    public TrafficDeptController(ITrafficDeptRepository repository, ILogger<TrafficDeptController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrafficDept>>> GetAll() { if (!HasReportsRole()) return Forbid(); try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<TrafficDept>> GetById(short id) { if (!HasReportsRole()) return Forbid(); try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<TrafficDept>> GetByName(string name) { if (!HasReportsRole()) return Forbid(); try { var item = await _repository.GetByNameAsync(name); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<TrafficDept>> Create([FromBody] TrafficDept item) { if (!HasReportsRole()) return Forbid(); try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.Traffic_dept_code }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<TrafficDept>> Update(short id, [FromBody] TrafficDept item) { if (!HasReportsRole()) return Forbid(); try { if (id != item.Traffic_dept_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { if (!HasReportsRole()) return Forbid(); try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

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
