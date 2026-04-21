using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Workflow Execution Controller - Orchestrates workflow execution and step transitions
/// </summary>
[ApiController]
[Route("api/workflow-execution")]
[Authorize]
public class WorkflowExecutionController : BaseApiController
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IStepRepository _stepRepository;
    private readonly IStatusRepository _statusRepository;
    private readonly ILogger<WorkflowExecutionController> _logger;

    public WorkflowExecutionController(
        IWorkflowRepository workflowRepository,
        IStepRepository stepRepository,
        IStatusRepository statusRepository,
        ILogger<WorkflowExecutionController> logger)
    {
        _workflowRepository = workflowRepository;
        _stepRepository = stepRepository;
        _statusRepository = statusRepository;
        _logger = logger;
    }

    /// <summary>
    /// Start a new workflow instance
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<WorkflowInstanceDto>> StartWorkflow([FromBody] StartWorkflowDto request)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowID);
            if (workflow == null)
                return NotFound(new { error = "Workflow not found" });

            // Get all steps for this workflow
            var steps = await _stepRepository.GetByWorkflowIdAsync(request.WorkflowID);
            var stepsList = steps.ToList();

            if (!stepsList.Any())
                return BadRequest(new { error = "Workflow has no steps defined" });

            // Get first step (lowest order)
            var firstStep = stepsList.OrderBy(s => s.StepOrder).First();

            // Create initial status for first step
            var status = new Status
            {
                StepID = firstStep.StepID,
                DateStarted = DateTime.Now,
                IsBusy = true,
                StartedByUserName = User.Identity?.Name ?? "Unknown"
            };

            var createdStatus = await _statusRepository.CreateAsync(status, GetCurrentUserId());

            _logger.LogInformation("Started workflow {WorkflowId} at step {StepId}", workflow.WorkflowID, firstStep.StepID);

            return Ok(new WorkflowInstanceDto
            {
                WorkflowID = workflow.WorkflowID,
                WorkflowName = workflow.WorkflowName,
                CurrentStepID = firstStep.StepID,
                CurrentStepName = firstStep.StepName,
                StatusID = createdStatus.StatusID,
                IsActive = true,
                StartedDate = status.DateStarted,
                TotalSteps = stepsList.Count,
                CurrentStepOrder = firstStep.StepOrder
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow");
            return StatusCode(500, new { error = "Error starting workflow", message = ex.Message });
        }
    }

    /// <summary>
    /// Complete current step and move to next step
    /// </summary>
    [HttpPost("complete-step")]
    public async Task<ActionResult<WorkflowInstanceDto>> CompleteStep([FromBody] CompleteStepDto request)
    {
        try
        {
            var currentStatus = await _statusRepository.GetByIdAsync(request.StatusID);
            if (currentStatus == null)
                return NotFound(new { error = "Status not found" });

            if (currentStatus.DateCompleted != null)
                return BadRequest(new { error = "Step already completed" });

            // Mark current step as completed
            currentStatus.DateCompleted = DateTime.Now;
            currentStatus.IsBusy = false;
            await _statusRepository.UpdateAsync(currentStatus, GetCurrentUserId());

            var currentStep = await _stepRepository.GetByIdAsync(currentStatus.StepID);
            if (currentStep == null)
                return NotFound(new { error = "Current step not found" });

            // Get all steps for this workflow
            var steps = await _stepRepository.GetByWorkflowIdAsync(currentStep.WorkflowID);
            var stepsList = steps.OrderBy(s => s.StepOrder).ToList();

            // Find next step
            var nextStep = stepsList.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder);

            if (nextStep == null)
            {
                // Workflow completed
                _logger.LogInformation("Workflow {WorkflowId} completed", currentStep.WorkflowID);

                var workflow = await _workflowRepository.GetByIdAsync(currentStep.WorkflowID);

                return Ok(new WorkflowInstanceDto
                {
                    WorkflowID = currentStep.WorkflowID,
                    WorkflowName = workflow?.WorkflowName,
                    CurrentStepID = currentStep.StepID,
                    CurrentStepName = currentStep.StepName,
                    StatusID = currentStatus.StatusID,
                    IsActive = false,
                    IsCompleted = true,
                    CompletedDate = DateTime.Now,
                    TotalSteps = stepsList.Count,
                    CurrentStepOrder = currentStep.StepOrder
                });
            }

            // Create status for next step
            var nextStatus = new Status
            {
                StepID = nextStep.StepID,
                DateStarted = DateTime.Now,
                IsBusy = true,
                StartedByUserName = User.Identity?.Name ?? "Unknown"
            };

            var createdNextStatus = await _statusRepository.CreateAsync(nextStatus, GetCurrentUserId());

            _logger.LogInformation("Workflow {WorkflowId} advanced to step {StepId}", currentStep.WorkflowID, nextStep.StepID);

            var workflowForNext = await _workflowRepository.GetByIdAsync(currentStep.WorkflowID);

            return Ok(new WorkflowInstanceDto
            {
                WorkflowID = currentStep.WorkflowID,
                WorkflowName = workflowForNext?.WorkflowName,
                CurrentStepID = nextStep.StepID,
                CurrentStepName = nextStep.StepName,
                StatusID = createdNextStatus.StatusID,
                IsActive = true,
                StartedDate = nextStatus.DateStarted,
                TotalSteps = stepsList.Count,
                CurrentStepOrder = nextStep.StepOrder
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing step");
            return StatusCode(500, new { error = "Error completing step", message = ex.Message });
        }
    }

    /// <summary>
    /// Get current workflow instance status
    /// </summary>
    [HttpGet("status/{statusId}")]
    public async Task<ActionResult<WorkflowStatusDto>> GetWorkflowStatus(int statusId)
    {
        try
        {
            var status = await _statusRepository.GetByIdAsync(statusId);
            if (status == null)
                return NotFound(new { error = "Status not found" });

            var step = await _stepRepository.GetByIdAsync(status.StepID);
            if (step == null)
                return NotFound(new { error = "Step not found" });

            var workflow = await _workflowRepository.GetByIdAsync(step.WorkflowID);
            var allSteps = await _stepRepository.GetByWorkflowIdAsync(step.WorkflowID);
            var stepsList = allSteps.OrderBy(s => s.StepOrder).ToList();

            return Ok(new WorkflowStatusDto
            {
                StatusID = status.StatusID,
                WorkflowID = step.WorkflowID,
                WorkflowName = workflow?.WorkflowName,
                CurrentStepID = step.StepID,
                CurrentStepName = step.StepName,
                CurrentStepOrder = step.StepOrder,
                TotalSteps = stepsList.Count,
                IsBusy = status.IsBusy,
                DateStarted = status.DateStarted,
                DateCompleted = status.DateCompleted,
                StartedByUserName = status.StartedByUserName,
                IsCompleted = status.DateCompleted != null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow status");
            return StatusCode(500, new { error = "Error retrieving workflow status", message = ex.Message });
        }
    }

    /// <summary>
    /// Get workflow execution history
    /// </summary>
    [HttpGet("history/workflow/{workflowId}")]
    public async Task<ActionResult<WorkflowHistoryDto>> GetWorkflowHistory(int workflowId)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null)
                return NotFound(new { error = "Workflow not found" });

            var steps = await _stepRepository.GetByWorkflowIdAsync(workflowId);
            var stepsList = steps.OrderBy(s => s.StepOrder).ToList();

            var history = new List<StepHistoryDto>();

            foreach (var step in stepsList)
            {
                var statuses = await _statusRepository.GetByStepIdAsync(step.StepID);
                var statusesList = statuses.OrderByDescending(s => s.DateStarted).ToList();

                history.Add(new StepHistoryDto
                {
                    StepID = step.StepID,
                    StepName = step.StepName,
                    StepOrder = step.StepOrder,
                    ExecutionCount = statusesList.Count,
                    LastExecution = statusesList.FirstOrDefault()?.DateStarted,
                    LastCompletion = statusesList.FirstOrDefault()?.DateCompleted,
                    Executions = statusesList.Select(s => new StatusExecutionDto
                    {
                        StatusID = s.StatusID,
                        DateStarted = s.DateStarted,
                        DateCompleted = s.DateCompleted,
                        StartedByUserName = s.StartedByUserName,
                        IsBusy = s.IsBusy
                    }).ToList()
                });
            }

            return Ok(new WorkflowHistoryDto
            {
                WorkflowID = workflow.WorkflowID,
                WorkflowName = workflow.WorkflowName,
                TotalSteps = stepsList.Count,
                StepHistory = history
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow history");
            return StatusCode(500, new { error = "Error retrieving workflow history", message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel/abort workflow execution
    /// </summary>
    [HttpPost("cancel")]
    public async Task<ActionResult> CancelWorkflow([FromBody] CancelWorkflowDto request)
    {
        try
        {
            var status = await _statusRepository.GetByIdAsync(request.StatusID);
            if (status == null)
                return NotFound(new { error = "Status not found" });

            if (status.DateCompleted != null)
                return BadRequest(new { error = "Workflow already completed" });

            // Mark as completed with cancellation
            status.DateCompleted = DateTime.Now;
            status.IsBusy = false;
            await _statusRepository.UpdateAsync(status, GetCurrentUserId());

            _logger.LogInformation("Workflow cancelled at status {StatusId}", request.StatusID);

            return Ok(new { message = "Workflow cancelled successfully", statusId = request.StatusID, cancelledBy = User.Identity?.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling workflow");
            return StatusCode(500, new { error = "Error cancelling workflow", message = ex.Message });
        }
    }

    /// <summary>
    /// Get all active workflow instances
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ActiveWorkflowDto>>> GetActiveWorkflows()
    {
        try
        {
            var activeStatuses = await _statusRepository.GetActiveStatusesAsync();
            var activeWorkflows = new List<ActiveWorkflowDto>();

            foreach (var status in activeStatuses)
            {
                var step = await _stepRepository.GetByIdAsync(status.StepID);
                if (step == null) continue;

                var workflow = await _workflowRepository.GetByIdAsync(step.WorkflowID);

                activeWorkflows.Add(new ActiveWorkflowDto
                {
                    StatusID = status.StatusID,
                    WorkflowID = step.WorkflowID,
                    WorkflowName = workflow?.WorkflowName,
                    CurrentStepID = step.StepID,
                    CurrentStepName = step.StepName,
                    DateStarted = status.DateStarted,
                    StartedByUserName = status.StartedByUserName
                });
            }

            return Ok(activeWorkflows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active workflows");
            return StatusCode(500, new { error = "Error retrieving active workflows", message = ex.Message });
        }
    }
}

#region Workflow Execution DTOs

public class StartWorkflowDto
{
    public int WorkflowID { get; set; }
}

public class CompleteStepDto
{
    public int StatusID { get; set; }
}

public class CancelWorkflowDto
{
    public int StatusID { get; set; }
    public string? Reason { get; set; }
}

public class WorkflowInstanceDto
{
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public int CurrentStepID { get; set; }
    public string? CurrentStepName { get; set; }
    public int StatusID { get; set; }
    public bool IsActive { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int TotalSteps { get; set; }
    public int CurrentStepOrder { get; set; }
}

public class WorkflowStatusDto
{
    public int StatusID { get; set; }
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public int CurrentStepID { get; set; }
    public string? CurrentStepName { get; set; }
    public int CurrentStepOrder { get; set; }
    public int TotalSteps { get; set; }
    public bool IsBusy { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public string? StartedByUserName { get; set; }
    public bool IsCompleted { get; set; }
}

public class WorkflowHistoryDto
{
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public int TotalSteps { get; set; }
    public List<StepHistoryDto> StepHistory { get; set; } = new();
}

public class StepHistoryDto
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public int StepOrder { get; set; }
    public int ExecutionCount { get; set; }
    public DateTime? LastExecution { get; set; }
    public DateTime? LastCompletion { get; set; }
    public List<StatusExecutionDto> Executions { get; set; } = new();
}

public class StatusExecutionDto
{
    public int StatusID { get; set; }
    public DateTime? DateStarted { get; set; }
    public DateTime? DateCompleted { get; set; }
    public string? StartedByUserName { get; set; }
    public bool IsBusy { get; set; }
}

public class ActiveWorkflowDto
{
    public int StatusID { get; set; }
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public int CurrentStepID { get; set; }
    public string? CurrentStepName { get; set; }
    public DateTime? DateStarted { get; set; }
    public string? StartedByUserName { get; set; }
}

#endregion
