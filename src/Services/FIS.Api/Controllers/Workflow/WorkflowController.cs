using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : BaseApiController
{
    private readonly IWorkflowRepository _repository;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(IWorkflowRepository repository, ILogger<WorkflowController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowDto>>> GetAll()
    {
        try
        {
            var workflows = await _repository.GetAllAsync();
            var dtos = workflows.Select(w => MapToDto(w));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflows");
            return StatusCode(500, "Error retrieving workflows");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowDto>> GetById(int id)
    {
        try
        {
            var workflow = await _repository.GetByIdAsync(id);
            return workflow == null ? NotFound() : Ok(MapToDto(workflow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow {Id}", id);
            return StatusCode(500, "Error retrieving workflow");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<WorkflowDto>>> GetActive()
    {
        try
        {
            var workflows = await _repository.GetActiveWorkflowsAsync();
            var dtos = workflows.Select(w => MapToDto(w));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active workflows");
            return StatusCode(500, "Error retrieving active workflows");
        }
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDto>> Create([FromBody] CreateWorkflowDto dto)
    {
        try
        {
            var workflow = new Workflow
            {
                WorkflowName = dto.WorkflowName,
                AlwaysExecute = dto.AlwaysExecute,
            };

            var created = await _repository.CreateAsync(workflow, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.WorkflowID },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow");
            return StatusCode(500, "Error creating workflow");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateWorkflowDto dto)
    {
        try
        {
            if (id != dto.WorkflowID)
                return BadRequest("ID mismatch");

            var workflow = new Workflow
            {
                WorkflowID = dto.WorkflowID,
                WorkflowName = dto.WorkflowName,
                AlwaysExecute = dto.AlwaysExecute,
            };

            await _repository.UpdateAsync(workflow, GetCurrentUserId());
            return Ok(new { message = "Workflow updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow {Id}", id);
            return StatusCode(500, "Error updating workflow");
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
            _logger.LogError(ex, "Error deleting workflow {Id}", id);
            return StatusCode(500, "Error deleting workflow");
        }
    }

    private WorkflowDto MapToDto(Workflow workflow)
    {
        return new WorkflowDto
        {
            WorkflowID = workflow.WorkflowID,
            WorkflowName = workflow.WorkflowName,
            AlwaysExecute = workflow.AlwaysExecute,
            DateCreated = workflow.date_created,
            DateUpdated = workflow.date_updated,
        };
    }
}

#region Workflow DTOs

public class WorkflowDto
{
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public bool AlwaysExecute { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateWorkflowDto
{
    public string? WorkflowName { get; set; }
    public bool AlwaysExecute { get; set; }
}

public class UpdateWorkflowDto
{
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public bool AlwaysExecute { get; set; }
}

#endregion
