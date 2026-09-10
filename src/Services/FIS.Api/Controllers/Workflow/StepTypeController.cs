using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StepTypeController : BaseApiController
{
    private readonly IStepTypeRepository _repository;
    private readonly ILogger<StepTypeController> _logger;

    public StepTypeController(IStepTypeRepository repository, ILogger<StepTypeController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StepTypeDto>>> GetAll()
    {
        try
        {
            var stepTypes = await _repository.GetAllAsync();
            var dtos = stepTypes.Select(st => MapToDto(st));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving step types");
            return StatusCode(500, "Error retrieving step types");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StepTypeDto>> GetById(int id)
    {
        try
        {
            var stepType = await _repository.GetByIdAsync(id);
            return stepType == null ? NotFound() : Ok(MapToDto(stepType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving step type {Id}", id);
            return StatusCode(500, "Error retrieving step type");
        }
    }

    [HttpPost]
    public async Task<ActionResult<StepTypeDto>> Create([FromBody] CreateStepTypeDto dto)
    {
        try
        {
            var stepType = new StepType
            {
                StepTypeName = dto.StepTypeName,
                StepTypeData = dto.StepTypeData,
            };

            var created = await _repository.CreateAsync(stepType, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.StepTypeID },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating step type");
            return StatusCode(500, "Error creating step type");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateStepTypeDto dto)
    {
        try
        {
            if (id != dto.StepTypeID)
                return BadRequest("ID mismatch");

            var stepType = new StepType
            {
                StepTypeID = dto.StepTypeID,
                StepTypeName = dto.StepTypeName,
                StepTypeData = dto.StepTypeData,
            };

            await _repository.UpdateAsync(stepType, GetCurrentUserId());
            return Ok(new { message = "Step type updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating step type {Id}", id);
            return StatusCode(500, "Error updating step type");
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
            _logger.LogError(ex, "Error deleting step type {Id}", id);
            return StatusCode(500, "Error deleting step type");
        }
    }

    private StepTypeDto MapToDto(StepType stepType)
    {
        return new StepTypeDto
        {
            StepTypeID = stepType.StepTypeID,
            StepTypeName = stepType.StepTypeName,
            StepTypeData = stepType.StepTypeData,
            DateCreated = stepType.date_created,
            DateUpdated = stepType.date_updated,
        };
    }
}

#region StepType DTOs

public class StepTypeDto
{
    public int StepTypeID { get; set; }
    public string? StepTypeName { get; set; }
    public string? StepTypeData { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateStepTypeDto
{
    public string? StepTypeName { get; set; }
    public string? StepTypeData { get; set; }
}

public class UpdateStepTypeDto
{
    public int StepTypeID { get; set; }
    public string? StepTypeName { get; set; }
    public string? StepTypeData { get; set; }
}

#endregion
