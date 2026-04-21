namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Service for executing workflows with step handlers
/// </summary>
public interface IWorkflowExecutionService
{
    /// <summary>
    /// Executes a step with its configured handler
    /// </summary>
    /// <param name="stepId">ID of the step to execute</param>
    /// <param name="context">Execution context</param>
    /// <returns>Execution result</returns>
    Task<StepExecutionResult> ExecuteStepAsync(int stepId, WorkflowExecutionContext context);

    /// <summary>
    /// Starts a workflow instance
    /// </summary>
    /// <param name="workflowId">ID of the workflow to start</param>
    /// <param name="userId">User starting the workflow</param>
    /// <param name="initialData">Initial workflow data</param>
    /// <returns>Workflow instance information</returns>
    Task<WorkflowInstanceResult> StartWorkflowAsync(int workflowId, int userId, Dictionary<string, object>? initialData = null);

    /// <summary>
    /// Completes a step and advances to the next step
    /// </summary>
    /// <param name="statusId">Current status ID</param>
    /// <param name="userId">User completing the step</param>
    /// <param name="outputData">Output data from completed step</param>
    /// <returns>Next step information or completion status</returns>
    Task<WorkflowInstanceResult> CompleteStepAsync(int statusId, int userId, Dictionary<string, object>? outputData = null);
}

/// <summary>
/// Result of workflow instance operation
/// </summary>
public class WorkflowInstanceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public int? CurrentStepID { get; set; }
    public string? CurrentStepName { get; set; }
    public int? StatusID { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}
