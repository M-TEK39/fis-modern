using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StepController : BaseApiController
{
    private readonly IStepRepository _repository;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly ILogger<StepController> _logger;

    public StepController(
        IStepRepository repository, 
        IConditionEvaluator conditionEvaluator,
        ILogger<StepController> logger)
    {
        _repository = repository;
        _conditionEvaluator = conditionEvaluator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<StepDto>>> GetAll()
    {
        try
        {
            var steps = await _repository.GetAllAsync();
            var dtos = steps.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving steps");
            return StatusCode(500, "Error retrieving steps");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StepDto>> GetById(int id)
    {
        try
        {
            var step = await _repository.GetByIdAsync(id);
            return step == null ? NotFound() : Ok(MapToDto(step));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving step {Id}", id);
            return StatusCode(500, "Error retrieving step");
        }
    }

    [HttpGet("workflow/{workflowId}")]
    public async Task<ActionResult<IEnumerable<StepDto>>> GetByWorkflow(int workflowId)
    {
        try
        {
            var steps = await _repository.GetByWorkflowIdAsync(workflowId);
            var dtos = steps.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving steps for workflow {WorkflowId}", workflowId);
            return StatusCode(500, "Error retrieving steps");
        }
    }

    [HttpGet("parent/{parentStepId}")]
    public async Task<ActionResult<IEnumerable<StepDto>>> GetChildSteps(int parentStepId)
    {
        try
        {
            var steps = await _repository.GetChildStepsAsync(parentStepId);
            var dtos = steps.Select(s => MapToDto(s));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child steps for {ParentStepId}", parentStepId);
            return StatusCode(500, "Error retrieving child steps");
        }
    }

    [HttpPost]
    public async Task<ActionResult<StepDto>> Create([FromBody] CreateStepDto dto)
    {
        try
        {
            var step = new Step
            {
                StepName = dto.StepName,
                StepOrder = dto.StepOrder,
                StepTypeID = dto.StepTypeID,
                WorkflowID = dto.WorkflowID,
                ParentStepID = dto.ParentStepID,
                StepParameters = dto.StepParameters,
                HandlerType = dto.HandlerType,
                IsConditional = dto.IsConditional,
                ConditionExpression = dto.ConditionExpression,
                TrueStepID = dto.TrueStepID,
                FalseStepID = dto.FalseStepID
            };

            var created = await _repository.CreateAsync(step, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.StepID }, MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating step");
            return StatusCode(500, "Error creating step");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateStepDto dto)
    {
        try
        {
            if (id != dto.StepID)
                return BadRequest("ID mismatch");

            var step = new Step
            {
                StepID = dto.StepID,
                StepName = dto.StepName,
                StepOrder = dto.StepOrder,
                StepTypeID = dto.StepTypeID,
                WorkflowID = dto.WorkflowID,
                ParentStepID = dto.ParentStepID,
                StepParameters = dto.StepParameters,
                HandlerType = dto.HandlerType,
                IsConditional = dto.IsConditional,
                ConditionExpression = dto.ConditionExpression,
                TrueStepID = dto.TrueStepID,
                FalseStepID = dto.FalseStepID
            };

            await _repository.UpdateAsync(step, GetCurrentUserId());
            return Ok(new { message = "Step updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating step {Id}", id);
            return StatusCode(500, "Error updating step");
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
            _logger.LogError(ex, "Error deleting step {Id}", id);
            return StatusCode(500, "Error deleting step");
        }
    }

    /// <summary>
    /// Configure conditional branching for a step
    /// </summary>
    [HttpPut("{id}/condition")]
    public async Task<ActionResult> SetCondition(int id, [FromBody] SetStepConditionDto dto)
    {
        try
        {
            var step = await _repository.GetByIdAsync(id);
            if (step == null)
                return NotFound("Step not found");

            // Validate condition expression before saving
            if (!string.IsNullOrWhiteSpace(dto.ConditionExpression))
            {
                var isValid = await _conditionEvaluator.ValidateExpressionAsync(dto.ConditionExpression);
                if (!isValid)
                {
                    return BadRequest("Invalid condition expression syntax");
                }
            }

            step.IsConditional = dto.IsConditional;
            step.ConditionExpression = dto.ConditionExpression;
            step.TrueStepID = dto.TrueStepID;
            step.FalseStepID = dto.FalseStepID;

            await _repository.UpdateAsync(step, GetCurrentUserId());

            return Ok(new { 
                message = "Step condition configured successfully", 
                id,
                isConditional = step.IsConditional,
                conditionExpression = step.ConditionExpression
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting condition for step {Id}", id);
            return StatusCode(500, "Error setting step condition");
        }
    }

    /// <summary>
    /// Test a condition expression with sample data
    /// </summary>
    [HttpPost("condition/test")]
    public async Task<ActionResult<ConditionTestResultDto>> TestCondition([FromBody] TestConditionDto dto)
    {
        try
        {
            // Validate expression
            var isValid = await _conditionEvaluator.ValidateExpressionAsync(dto.ConditionExpression);
            if (!isValid)
            {
                return BadRequest("Invalid condition expression syntax");
            }

            // Evaluate with test data
            var result = await _conditionEvaluator.EvaluateAsync(dto.ConditionExpression, dto.TestData);

            return Ok(new ConditionTestResultDto
            {
                IsValid = true,
                EvaluationResult = result,
                Expression = dto.ConditionExpression,
                TestData = dto.TestData,
                Message = $"Condition evaluated to: {result}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing condition expression");
            return Ok(new ConditionTestResultDto
            {
                IsValid = false,
                EvaluationResult = false,
                Expression = dto.ConditionExpression,
                TestData = dto.TestData,
                Message = $"Error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get condition configuration for a step
    /// </summary>
    [HttpGet("{id}/condition")]
    public async Task<ActionResult<StepConditionDto>> GetCondition(int id)
    {
        try
        {
            var step = await _repository.GetByIdAsync(id);
            if (step == null)
                return NotFound("Step not found");

            return Ok(new StepConditionDto
            {
                StepID = step.StepID,
                StepName = step.StepName,
                IsConditional = step.IsConditional,
                ConditionExpression = step.ConditionExpression,
                TrueStepID = step.TrueStepID,
                FalseStepID = step.FalseStepID
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving condition for step {Id}", id);
            return StatusCode(500, "Error retrieving step condition");
        }
    }

    private StepDto MapToDto(Step step)
    {
        return new StepDto
        {
            StepID = step.StepID,
            StepName = step.StepName,
            StepOrder = step.StepOrder,
            StepTypeID = step.StepTypeID,
            WorkflowID = step.WorkflowID,
            ParentStepID = step.ParentStepID,
            StepParameters = step.StepParameters,
            HandlerType = step.HandlerType,
            IsConditional = step.IsConditional,
            ConditionExpression = step.ConditionExpression,
            TrueStepID = step.TrueStepID,
            FalseStepID = step.FalseStepID,
            DateCreated = step.date_created,
            DateUpdated = step.date_updated
        };
    }
}

#region Step DTOs

public class StepDto
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public int StepOrder { get; set; }
    public int StepTypeID { get; set; }
    public int WorkflowID { get; set; }
    public int? ParentStepID { get; set; }
    public string? StepParameters { get; set; }
    public string? HandlerType { get; set; }
    public bool IsConditional { get; set; }
    public string? ConditionExpression { get; set; }
    public int? TrueStepID { get; set; }
    public int? FalseStepID { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateStepDto
{
    public string? StepName { get; set; }
    public int StepOrder { get; set; }
    public int StepTypeID { get; set; }
    public int WorkflowID { get; set; }
    public int? ParentStepID { get; set; }
    public string? StepParameters { get; set; }
    public string? HandlerType { get; set; }
    public bool IsConditional { get; set; }
    public string? ConditionExpression { get; set; }
    public int? TrueStepID { get; set; }
    public int? FalseStepID { get; set; }
}

public class UpdateStepDto
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public int StepOrder { get; set; }
    public int StepTypeID { get; set; }
    public int WorkflowID { get; set; }
    public int? ParentStepID { get; set; }
    public string? StepParameters { get; set; }
    public string? HandlerType { get; set; }
    public bool IsConditional { get; set; }
    public string? ConditionExpression { get; set; }
    public int? TrueStepID { get; set; }
    public int? FalseStepID { get; set; }
}

public class SetStepConditionDto
{
    public bool IsConditional { get; set; }
    public string? ConditionExpression { get; set; }
    public int? TrueStepID { get; set; }
    public int? FalseStepID { get; set; }
}

public class TestConditionDto
{
    public string ConditionExpression { get; set; } = string.Empty;
    public Dictionary<string, object> TestData { get; set; } = new();
}

public class ConditionTestResultDto
{
    public bool IsValid { get; set; }
    public bool EvaluationResult { get; set; }
    public string Expression { get; set; } = string.Empty;
    public Dictionary<string, object> TestData { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

public class StepConditionDto
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public bool IsConditional { get; set; }
    public string? ConditionExpression { get; set; }
    public int? TrueStepID { get; set; }
    public int? FalseStepID { get; set; }
}

#endregion
