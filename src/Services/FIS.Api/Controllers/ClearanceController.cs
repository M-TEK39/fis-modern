using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClearanceController : BaseApiController
{
    private readonly IClearanceRepository _repository;
    private readonly ILogger<ClearanceController> _logger;

    public ClearanceController(IClearanceRepository repository, ILogger<ClearanceController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Clearance>>> GetAll()
    {
        if (!HasClearanceRole())
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

    [HttpGet("{id}")]
    public async Task<ActionResult<Clearance>> GetById(int id)
    {
        if (!HasClearanceRole())
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

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Clearance>>> GetByVehicle(int vmfCode)
    {
        if (!HasClearanceRole())
            return Forbid();
        try
        {
            return Ok(await _repository.GetByVehicleAsync(vmfCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("lookup/{fleetOrReg}")]
    public async Task<ActionResult<ClearanceLookupResult>> LookupVehicle(string fleetOrReg)
    {
        if (!HasClearanceRole())
            return Forbid();
        try
        {
            var result = await _repository.LookupVehicleAsync(fleetOrReg);
            return result == null ? NotFound() : Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up vehicle");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Clearance>> Create([FromBody] Clearance item)
    {
        if (!HasClearanceRole())
            return Forbid();
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.clearance_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Clearance>> Update(int id, [FromBody] Clearance item)
    {
        if (!HasClearanceRole())
            return Forbid();
        try
        {
            if (id != item.clearance_code)
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
    public async Task<ActionResult> Delete(int id)
    {
        if (!HasClearanceRole())
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

    [HttpPost("reports/universal")]
    public async Task<ActionResult<IEnumerable<ClearanceReportRow>>> GetUniversalReport(
        [FromBody] ClearanceUniversalReportRequest request
    )
    {
        if (!HasReportsRole())
            return Forbid();

        if (
            request.StartDate.HasValue
            && request.EndDate.HasValue
            && request.StartDate > request.EndDate
        )
        {
            return BadRequest(new { error = "The report start date must be before the end date." });
        }

        try
        {
            return Ok(
                await _repository.GetUniversalReportAsync(
                    request.StartDate,
                    request.EndDate,
                    request.MerchantCode
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating the clearance universal report");
            return StatusCode(500);
        }
    }

    private bool HasClearanceRole() => HasAnyRole("Clearance");

    private bool HasReportsRole() => HasAnyRole("Reports");

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

public class ClearanceUniversalReportRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MerchantCode { get; set; }
}
