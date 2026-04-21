using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatusController : BaseApiController
{
    private readonly IStatusRepository _repository;
    private readonly ILogger<StatusController> _logger;

    public StatusController(IStatusRepository repository, ILogger<StatusController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StatusDto>>> GetAll()
    {
        try
        {
            var statuses = await _repository.GetAllAsync();
            var dtos = statuses.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statuses");
            return StatusCode(500, "Error retrieving statuses");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StatusDto>> GetById(int id)
    {
        try
        {
            var status = await _repository.GetByIdAsync(id);
            return status == null ? NotFound() : Ok(MapToDto(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status {Id}", id);
            return StatusCode(500, "Error retrieving status");
        }
    }

    [HttpGet("step/{stepId}")]
    public async Task<ActionResult<IEnumerable<StatusDto>>> GetByStep(int stepId)
    {
        try
        {
            var statuses = await _repository.GetByStepIdAsync(stepId);
            var dtos = statuses.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statuses for step {StepId}", stepId);
            return StatusCode(500, "Error retrieving statuses");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<StatusDto>>> GetActive()
    {
        try
        {
            var statuses = await _repository.GetActiveStatusesAsync();
            var dtos = statuses.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active statuses");
            return StatusCode(500, "Error retrieving active statuses");
        }
    }

    [HttpGet("user/{userName}")]
    public async Task<ActionResult<IEnumerable<StatusDto>>> GetByUser(string userName)
    {
        try
        {
            var statuses = await _repository.GetByUserAsync(userName);
            var dtos = statuses.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statuses for user {UserName}", userName);
            return StatusCode(500, "Error retrieving statuses");
        }
    }

    [HttpPost]
    public async Task<ActionResult<StatusDto>> Create([FromBody] CreateStatusDto dto)
    {
        try
        {
            var status = new Status
            {
                StepID = dto.StepID,
                DateStarted = dto.DateStarted,
                DateCompleted = dto.DateCompleted,
                IsBusy = dto.IsBusy,
                StartedByUserName = dto.StartedByUserName
            };

            var created = await _repository.CreateAsync(status, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.StatusID }, MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating status");
            return StatusCode(500, "Error creating status");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateStatusDto dto)
    {
        try
        {
            if (id != dto.StatusID)
                return BadRequest("ID mismatch");

            var status = new Status
            {
                StatusID = dto.StatusID,
                StepID = dto.StepID,
                DateStarted = dto.DateStarted,
                DateCompleted = dto.DateCompleted,
                IsBusy = dto.IsBusy,
                StartedByUserName = dto.StartedByUserName
            };

            await _repository.UpdateAsync(status, GetCurrentUserId());
            return Ok(new { message = "Status updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status {Id}", id);
            return StatusCode(500, "Error updating status");
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
            _logger.LogError(ex, "Error deleting status {Id}", id);
            return StatusCode(500, "Error deleting status");
        }
    }

    private StatusDto MapToDto(Status status)
    {
        return new StatusDto
        {
            StatusID = status.StatusID,
            StepID = status.StepID,
            DateStarted = status.DateStarted,
            DateCompleted = status.DateCompleted,
            IsBusy = status.IsBusy,
            StartedByUserName = status.StartedByUserName,
            DateCreated = status.date_created,
            DateUpdated = status.date_updated
        };
    }
}

#region Status DTOs

public class StatusDto
{
    public int StatusID { get; set; }
    public int StepID { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public bool IsBusy { get; set; }
    public string? StartedByUserName { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateStatusDto
{
    public int StepID { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public bool IsBusy { get; set; }
    public string? StartedByUserName { get; set; }
}

public class UpdateStatusDto
{
    public int StatusID { get; set; }
    public int StepID { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public bool IsBusy { get; set; }
    public string? StartedByUserName { get; set; }
}

#endregion
