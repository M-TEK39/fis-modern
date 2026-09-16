using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
// Vehicle assessments are captured from the legacy Contracts workflow; do
// not expose this shared table endpoint to an authenticated user without a
// Contracts entitlement.
[ContractAccess]
[Route("api/[controller]")]
public class VehicleAssessmentController : BaseApiController
{
    private readonly IVehicleAssessmentRepository _repository;
    private readonly ILogger<VehicleAssessmentController> _logger;

    public VehicleAssessmentController(
        IVehicleAssessmentRepository repository,
        ILogger<VehicleAssessmentController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleAssessment>>> GetAll()
    {
        try
        {
            return Ok(await _repository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VehicleAssessment>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<VehicleAssessment>> GetByVehicle(int vmfCode)
    {
        try
        {
            var item = await _repository.GetByVehicleAsync(vmfCode);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpGet("recent/{days}")]
    public async Task<ActionResult<IEnumerable<VehicleAssessment>>> GetRecent(int days)
    {
        try
        {
            return Ok(await _repository.GetRecentAssessmentsAsync(days));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<VehicleAssessment>> Create([FromBody] VehicleAssessment item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.vehicle_assessment_code },
                created
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is NotSupportedException or LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment procedure is unavailable or incompatible. No partial inspection mutation was written.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VehicleAssessment>> Update(
        int id,
        [FromBody] VehicleAssessment item
    )
    {
        try
        {
            if (id != item.vehicle_assessment_code)
                return BadRequest();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is NotSupportedException or LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment procedure is unavailable or incompatible. No partial inspection mutation was written.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is NotSupportedException or LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "Legacy vehicle assessments cannot be deleted; the inspection record is retained for audit history.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }
}
