using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Service for orchestrating workflow execution with step handlers
/// </summary>
public class WorkflowExecutionService : IWorkflowExecutionService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IStepRepository _stepRepository;
    private readonly IStatusRepository _statusRepository;
    private readonly IStepHandlerFactory _handlerFactory;
    private readonly IConditionEvaluator _conditionEvaluator;
    private readonly INotificationService _notificationService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<WorkflowExecutionService> _logger;

    public WorkflowExecutionService(
        IWorkflowRepository workflowRepository,
        IStepRepository stepRepository,
        IStatusRepository statusRepository,
        IStepHandlerFactory handlerFactory,
        IConditionEvaluator conditionEvaluator,
        INotificationService notificationService,
        IAnalyticsService analyticsService,
        ILogger<WorkflowExecutionService> logger)
    {
        _workflowRepository = workflowRepository;
        _stepRepository = stepRepository;
        _statusRepository = statusRepository;
        _handlerFactory = handlerFactory;
        _conditionEvaluator = conditionEvaluator;
        _notificationService = notificationService;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<StepExecutionResult> ExecuteStepAsync(int stepId, WorkflowExecutionContext context)
    {
        int? executionHistoryId = null;
        
        try
        {
            var step = await _stepRepository.GetByIdAsync(stepId);
            if (step == null)
            {
                return StepExecutionResult.FailureResult($"Step {stepId} not found");
            }

            // Record step execution start (Phase 5 - Analytics)
            executionHistoryId = await _analyticsService.RecordStepExecutionAsync(
                context.StatusID, 
                stepId, 
                step.WorkflowID, 
                step.HandlerType);

            // If no handler type is specified, treat as manual step
            if (string.IsNullOrWhiteSpace(step.HandlerType))
            {
                _logger.LogInformation("Step {StepID} has no handler - treating as manual step", stepId);
                
                // Complete analytics tracking for manual step
                if (executionHistoryId.HasValue)
                {
                    await _analyticsService.CompleteStepExecutionAsync(executionHistoryId.Value, true);
                }
                
                return StepExecutionResult.SuccessResult("Manual step - no handler execution required");
            }

            // Get the handler
            var handler = _handlerFactory.GetHandler(step.HandlerType);
            if (handler == null)
            {
                _logger.LogError("Handler type '{HandlerType}' not found for Step {StepID}", step.HandlerType, stepId);
                
                // Complete analytics tracking with failure
                if (executionHistoryId.HasValue)
                {
                    await _analyticsService.CompleteStepExecutionAsync(executionHistoryId.Value, false, $"Handler type '{step.HandlerType}' not registered");
                }
                
                return StepExecutionResult.FailureResult($"Handler type '{step.HandlerType}' not registered");
            }

            // Parse step parameters
            var parameters = ParseStepParameters(step.StepParameters);

            // Execute the handler
            _logger.LogInformation("Executing handler '{HandlerType}' for Step {StepID}", step.HandlerType, stepId);
            var result = await handler.ExecuteAsync(parameters, context);

            // Complete analytics tracking
            if (executionHistoryId.HasValue)
            {
                await _analyticsService.CompleteStepExecutionAsync(
                    executionHistoryId.Value, 
                    result.Success, 
                    result.Success ? null : result.Message);
            }

            // Merge output data into workflow context
            if (result.Success && result.OutputData != null)
            {
                foreach (var kvp in result.OutputData)
                {
                    context.WorkflowData[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing step {StepID}", stepId);
            
            // Complete analytics tracking with error
            if (executionHistoryId.HasValue)
            {
                await _analyticsService.CompleteStepExecutionAsync(executionHistoryId.Value, false, ex.Message);
            }
            
            // Send error notification
            try
            {
                var errorData = new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["StepID"] = stepId
                };
                await _notificationService.SendStepNotificationAsync(stepId, "Error", errorData);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send Error notification for step {StepId}", stepId);
            }

            return StepExecutionResult.FailureResult($"Step execution failed: {ex.Message}", ex);
        }
    }

    public async Task<WorkflowInstanceResult> StartWorkflowAsync(int workflowId, int userId, Dictionary<string, object>? initialData = null)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null)
            {
                return new WorkflowInstanceResult
                {
                    Success = false,
                    Message = "Workflow not found"
                };
            }

            // Get all steps for this workflow
            var steps = await _stepRepository.GetByWorkflowIdAsync(workflowId);
            var stepsList = steps.OrderBy(s => s.StepOrder).ToList();

            if (!stepsList.Any())
            {
                return new WorkflowInstanceResult
                {
                    Success = false,
                    Message = "Workflow has no steps defined"
                };
            }

            // Get first step
            var firstStep = stepsList.First();

            // Create initial status
            var status = new Status
            {
                StepID = firstStep.StepID,
                DateStarted = DateTime.Now,
                IsBusy = true,
                StartedByUserName = "User" // TODO: Get actual username from user service
            };

            var createdStatus = await _statusRepository.CreateAsync(status, userId);

            // Create execution context
            var context = new WorkflowExecutionContext
            {
                WorkflowID = workflowId,
                StepID = firstStep.StepID,
                StatusID = createdStatus.StatusID,
                CurrentUserId = userId,
                WorkflowData = initialData ?? new Dictionary<string, object>(),
                StartTime = DateTime.UtcNow
            };

            // Send WorkflowStarted notification
            try
            {
                await _notificationService.SendWorkflowNotificationAsync(workflowId, "WorkflowStarted", context.WorkflowData);
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send WorkflowStarted notification for workflow {WorkflowId}", workflowId);
            }

            // Execute first step if it has a handler
            if (!string.IsNullOrWhiteSpace(firstStep.HandlerType))
            {
                var executionResult = await ExecuteStepAsync(firstStep.StepID, context);
                if (!executionResult.Success)
                {
                    _logger.LogWarning("First step execution failed: {Message}", executionResult.Message);
                }
            }

            _logger.LogInformation("Started workflow {WorkflowId} at step {StepId}", workflowId, firstStep.StepID);

            return new WorkflowInstanceResult
            {
                Success = true,
                Message = "Workflow started successfully",
                WorkflowID = workflowId,
                WorkflowName = workflow.WorkflowName,
                CurrentStepID = firstStep.StepID,
                CurrentStepName = firstStep.StepName,
                StatusID = createdStatus.StatusID,
                IsActive = true,
                IsCompleted = false,
                Data = context.WorkflowData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow {WorkflowId}", workflowId);
            return new WorkflowInstanceResult
            {
                Success = false,
                Message = $"Error starting workflow: {ex.Message}"
            };
        }
    }

    public async Task<WorkflowInstanceResult> CompleteStepAsync(int statusId, int userId, Dictionary<string, object>? outputData = null)
    {
        try
        {
            var currentStatus = await _statusRepository.GetByIdAsync(statusId);
            if (currentStatus == null)
            {
                return new WorkflowInstanceResult
                {
                    Success = false,
                    Message = "Status not found"
                };
            }

            if (currentStatus.DateCompleted != null)
            {
                return new WorkflowInstanceResult
                {
                    Success = false,
                    Message = "Step already completed"
                };
            }

            // Mark current step as completed
            currentStatus.DateCompleted = DateTime.Now;
            currentStatus.IsBusy = false;
            await _statusRepository.UpdateAsync(currentStatus, userId);

            var currentStep = await _stepRepository.GetByIdAsync(currentStatus.StepID);
            if (currentStep == null)
            {
                return new WorkflowInstanceResult
                {
                    Success = false,
                    Message = "Current step not found"
                };
            }

            // Send StepCompleted notification
            try
            {
                await _notificationService.SendStepNotificationAsync(currentStep.StepID, "StepCompleted", 
                    outputData ?? new Dictionary<string, object>());
            }
            catch (Exception notifEx)
            {
                _logger.LogWarning(notifEx, "Failed to send StepCompleted notification for step {StepId}", currentStep.StepID);
            }

            // Get all steps for this workflow
            var steps = await _stepRepository.GetByWorkflowIdAsync(currentStep.WorkflowID);
            var stepsList = steps.OrderBy(s => s.StepOrder).ToList();

            // Determine next step (with conditional branching support)
            var nextStep = await DetermineNextStepAsync(currentStep, stepsList, outputData);

            var workflow = await _workflowRepository.GetByIdAsync(currentStep.WorkflowID);

            if (nextStep == null)
            {
                // Workflow completed
                _logger.LogInformation("Workflow {WorkflowId} completed", currentStep.WorkflowID);

                // Send WorkflowCompleted notification
                try
                {
                    await _notificationService.SendWorkflowNotificationAsync(currentStep.WorkflowID, "WorkflowCompleted",
                        outputData ?? new Dictionary<string, object>());
                }
                catch (Exception notifEx)
                {
                    _logger.LogWarning(notifEx, "Failed to send WorkflowCompleted notification for workflow {WorkflowId}", 
                        currentStep.WorkflowID);
                }

                return new WorkflowInstanceResult
                {
                    Success = true,
                    Message = "Workflow completed successfully",
                    WorkflowID = currentStep.WorkflowID,
                    WorkflowName = workflow?.WorkflowName,
                    CurrentStepID = currentStep.StepID,
                    CurrentStepName = currentStep.StepName,
                    StatusID = statusId,
                    IsActive = false,
                    IsCompleted = true,
                    Data = outputData
                };
            }

            // Create status for next step
            var nextStatus = new Status
            {
                StepID = nextStep.StepID,
                DateStarted = DateTime.Now,
                IsBusy = true,
                StartedByUserName = "User" // TODO: Get actual username
            };

            var createdNextStatus = await _statusRepository.CreateAsync(nextStatus, userId);

            // Create execution context for next step
            var context = new WorkflowExecutionContext
            {
                WorkflowID = currentStep.WorkflowID,
                StepID = nextStep.StepID,
                StatusID = createdNextStatus.StatusID,
                CurrentUserId = userId,
                WorkflowData = outputData ?? new Dictionary<string, object>(),
                StartTime = DateTime.UtcNow
            };

            // Execute next step if it has a handler
            if (!string.IsNullOrWhiteSpace(nextStep.HandlerType))
            {
                var executionResult = await ExecuteStepAsync(nextStep.StepID, context);
                if (!executionResult.Success)
                {
                    _logger.LogWarning("Next step execution failed: {Message}", executionResult.Message);
                }
            }

            _logger.LogInformation("Workflow {WorkflowId} advanced to step {StepId}", currentStep.WorkflowID, nextStep.StepID);

            return new WorkflowInstanceResult
            {
                Success = true,
                Message = "Step completed and advanced to next step",
                WorkflowID = currentStep.WorkflowID,
                WorkflowName = workflow?.WorkflowName,
                CurrentStepID = nextStep.StepID,
                CurrentStepName = nextStep.StepName,
                StatusID = createdNextStatus.StatusID,
                IsActive = true,
                IsCompleted = false,
                Data = context.WorkflowData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing step with status {StatusId}", statusId);
            return new WorkflowInstanceResult
            {
                Success = false,
                Message = $"Error completing step: {ex.Message}"
            };
        }
    }

    private Dictionary<string, object> ParseStepParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson, options)
                ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing step parameters: {Parameters}", parametersJson);
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Determines the next step to execute, considering conditional branching
    /// </summary>
    private async Task<Step?> DetermineNextStepAsync(Step currentStep, List<Step> allSteps, Dictionary<string, object>? contextData)
    {
        try
        {
            // Check if current step is conditional
            if (currentStep.IsConditional && !string.IsNullOrWhiteSpace(currentStep.ConditionExpression))
            {
                _logger.LogInformation("Step {StepID} is conditional - evaluating expression: {Expression}", 
                    currentStep.StepID, currentStep.ConditionExpression);

                // Evaluate the condition
                var conditionResult = await _conditionEvaluator.EvaluateAsync(
                    currentStep.ConditionExpression, 
                    contextData ?? new Dictionary<string, object>());

                // Route based on condition result
                if (conditionResult && currentStep.TrueStepID.HasValue)
                {
                    var trueStep = allSteps.FirstOrDefault(s => s.StepID == currentStep.TrueStepID.Value);
                    _logger.LogInformation("Condition evaluated to TRUE - routing to step {StepID}", currentStep.TrueStepID);
                    return trueStep;
                }
                else if (!conditionResult && currentStep.FalseStepID.HasValue)
                {
                    var falseStep = allSteps.FirstOrDefault(s => s.StepID == currentStep.FalseStepID.Value);
                    _logger.LogInformation("Condition evaluated to FALSE - routing to step {StepID}", currentStep.FalseStepID);
                    return falseStep;
                }
                else
                {
                    _logger.LogWarning("Conditional step {StepID} has no valid target step for condition result: {Result}", 
                        currentStep.StepID, conditionResult);
                    
                    // Fall through to sequential logic if no valid conditional target
                }
            }

            // Default sequential logic: Get next step by order
            var nextStep = allSteps.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder);
            
            if (nextStep != null)
            {
                _logger.LogInformation("Sequential routing: next step is {StepID} (Order: {StepOrder})", 
                    nextStep.StepID, nextStep.StepOrder);
            }

            return nextStep;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining next step from {StepID} - falling back to sequential", currentStep.StepID);
            
            // Fallback to sequential logic on error
            return allSteps.FirstOrDefault(s => s.StepOrder > currentStep.StepOrder);
        }
    }
}
