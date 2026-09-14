using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lease-tariffs")]
public sealed class LeaseTariffsController : BaseApiController
{
    private readonly ILeaseTariffRepository _repository;
    private readonly ILogger<LeaseTariffsController> _logger;

    public LeaseTariffsController(
        ILeaseTariffRepository repository,
        ILogger<LeaseTariffsController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<LeaseTariff>>> GetAll() =>
        await ExecuteAsync(() => _repository.GetAllAsync(), "lease tariffs");

    [HttpGet("vehicle/{vmfCode:int}")]
    public async Task<ActionResult<List<LeaseTariff>>> GetByVehicle(int vmfCode) =>
        await ExecuteAsync(() => _repository.GetByVehicleAsync(vmfCode), "vehicle lease tariffs");

    [HttpGet("vehicle/{vmfCode:int}/latest")]
    public async Task<ActionResult<LeaseTariff>> GetLatestByVehicle(int vmfCode)
    {
        try
        {
            var tariffs = await _repository.GetByVehicleAsync(vmfCode);
            var latest = tariffs
                .OrderByDescending(t => t.end_date)
                .ThenByDescending(t => t.lease_tariff_code)
                .FirstOrDefault();
            return latest is null ? NotFound() : Ok(latest);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving the latest lease tariff for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to retrieve the latest lease tariff" });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeaseTariff>> GetById(int id)
    {
        try
        {
            var tariff = await _repository.GetByIdAsync(id);
            return tariff is null ? NotFound() : Ok(tariff);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lease tariff {LeaseTariffCode}", id);
            return StatusCode(500, new { error = "Failed to retrieve lease tariff" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<LeaseTariff>> Create([FromBody] LeaseTariff tariff)
    {
        if (!HasVehicleMasterRole())
            return Forbid();

        try
        {
            var created = await _repository.CreateAsync(tariff, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.lease_tariff_code },
                created
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lease tariff");
            return StatusCode(500, new { error = "Failed to create lease tariff" });
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult<LeaseTariffImportResult>> Import(
        [FromBody] IReadOnlyList<LeaseTariffImportRow> rows
    )
    {
        // The legacy FML menu exposes the import to Lease Vehicle Pending users. The
        // add and extension pages apply the separate Vehicle Master restriction.
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            return Ok(await _repository.ImportAsync(rows, GetCurrentUserId()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing lease tariffs");
            return StatusCode(500, new { error = "Failed to import lease tariffs" });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LeaseTariff>> Update(int id, [FromBody] LeaseTariff tariff)
    {
        if (!HasVehicleMasterRole())
            return Forbid();

        if (id != tariff.lease_tariff_code)
        {
            return BadRequest(new { error = "The lease tariff ID does not match the route." });
        }

        try
        {
            return Ok(await _repository.UpdateAsync(tariff, GetCurrentUserId()));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lease tariff {LeaseTariffCode}", id);
            return StatusCode(500, new { error = "Failed to update lease tariff" });
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        if (!HasVehicleMasterRole())
            return Forbid();

        // The legacy FML tariff pages provide add and extension flows only. Until a
        // corresponding legacy delete path is evidenced, do not expose a modern-only
        // destructive action that bypasses its database behavior.
        return Conflict(
            new { error = "Deleting a lease tariff is not available in the legacy FML workflow." }
        );
    }

    private async Task<ActionResult<T>> ExecuteAsync<T>(Func<Task<T>> operation, string resource)
    {
        try
        {
            return Ok(await operation());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {Resource}", resource);
            return StatusCode(500, new { error = $"Failed to retrieve {resource}" });
        }
    }

    private bool HasVehicleMasterRole() => HasRole("Vehicle Master");

    private bool HasLeaseVehiclePendingRole() => HasRole("Lease Vehicle Pending");

    private bool HasRole(string expectedRole)
    {
        if (User.IsInRole(expectedRole))
            return true;

        return User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            )
            .Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));
    }
}
