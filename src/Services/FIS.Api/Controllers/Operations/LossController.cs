using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Losses")]
[Route("api/[controller]")]
public class LossController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly ILossRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<LossController> _logger;

    public LossController(
        ILossRepository repository,
        IVehicleRepository vehicleRepository,
        ILogger<LossController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Loss>>> GetAll()
    {
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
        [FromQuery] int? vmfCode = null,
        [FromQuery] string? identifier = null,
        [FromQuery] string? mode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasLossRole())
        {
            return Forbid();
        }

        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var hasIdentifier = !string.IsNullOrWhiteSpace(identifier);

        try
        {
            var selectedVmfCode = vmfCode is > 0 ? vmfCode : null;
            var normalizedIdentifier = identifier?.Trim();
            if (selectedVmfCode is null && !string.IsNullOrWhiteSpace(normalizedIdentifier))
            {
                var normalizedMode = mode?.Trim().ToUpperInvariant();
                var vehicle =
                    normalizedMode == "GP"
                        ? await _vehicleRepository.GetByRegistrationNumberAsync(
                            normalizedIdentifier
                        )
                        : await _vehicleRepository.GetByFleetNumberAsync(normalizedIdentifier);
                selectedVmfCode = vehicle?.vmf_code;
            }

            if (hasIdentifier && selectedVmfCode is null)
            {
                return Ok(
                    new
                    {
                        items = Array.Empty<Loss>(),
                        page = 1,
                        pageSize = normalizedPageSize,
                        total = 0,
                        totalPages = 1,
                        vmfCode = (int?)null,
                    }
                );
            }

            var result = await _repository.GetPageAsync(
                new LossPageQuery(normalizedPage, normalizedPageSize, selectedVmfCode)
            );

            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                    vmfCode = selectedVmfCode,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged loss records");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Loss>> GetById(short id)
    {
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

    [HttpGet("vehicle/{identifier}")]
    public async Task<ActionResult<IEnumerable<Loss>>> GetByVehicleIdentifier(
        string identifier,
        [FromQuery] string? mode
    )
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Ok(Array.Empty<Loss>());
        }

        try
        {
            var normalizedIdentifier = identifier.Trim();
            var normalizedMode = mode?.Trim().ToUpperInvariant();
            var vehicle =
                normalizedMode == "GP"
                    ? await _vehicleRepository.GetByRegistrationNumberAsync(normalizedIdentifier)
                    : await _vehicleRepository.GetByFleetNumberAsync(normalizedIdentifier);
            return vehicle is null
                ? Ok(Array.Empty<Loss>())
                : Ok(await _repository.GetByVehicleAsync(vehicle.vmf_code));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving losses for vehicle identifier {Identifier}",
                identifier
            );
            return StatusCode(500);
        }
    }

    [HttpGet("vmf/{vmfCode:int}")]
    public async Task<ActionResult<IEnumerable<Loss>>> GetByVehicleCode(int vmfCode)
    {
        if (vmfCode <= 0)
        {
            return BadRequest(new { message = "A valid vehicle code is required." });
        }

        try
        {
            return Ok(await _repository.GetByVehicleAsync(vmfCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving losses for vehicle {VmfCode}", vmfCode);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Loss>> Create([FromBody] Loss item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.loss_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Loss>> Update(short id, [FromBody] Loss item)
    {
        try
        {
            if (id != item.loss_code)
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

    private bool HasLossRole()
    {
        if (User.IsInRole("Losses"))
        {
            return true;
        }

        return User.Claims.Any(claim =>
            (
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            && claim
                .Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Any(role => string.Equals(role, "Losses", StringComparison.OrdinalIgnoreCase))
        );
    }
}
