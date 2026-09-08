namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Interface for workflow step handlers that execute specific actions
/// </summary>
public interface IStepHandler
{
    /// <summary>
    /// Gets the unique identifier for this handler type
    /// </summary>
    string HandlerType { get; }

    /// <summary>
    /// Executes the step with the provided parameters and context
    /// </summary>
    /// <param name="parameters">Step-specific parameters as dictionary</param>
    /// <param name="context">Workflow execution context</param>
    /// <returns>Execution result with success status and optional data</returns>
    Task<StepExecutionResult> ExecuteAsync(
        Dictionary<string, object> parameters,
        WorkflowExecutionContext context
    );

    /// <summary>
    /// Validates if the provided parameters are valid for this handler
    /// </summary>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters);
}

/// <summary>
/// Result of step execution
/// </summary>
public class StepExecutionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object>? OutputData { get; set; }
    public Exception? Exception { get; set; }
    public DateTime ExecutionTime { get; set; } = DateTime.UtcNow;

    public static StepExecutionResult SuccessResult(
        string? message = null,
        Dictionary<string, object>? outputData = null
    )
    {
        return new StepExecutionResult
        {
            Success = true,
            Message = message ?? "Step executed successfully",
            OutputData = outputData,
        };
    }

    public static StepExecutionResult FailureResult(string message, Exception? exception = null)
    {
        return new StepExecutionResult
        {
            Success = false,
            Message = message,
            Exception = exception,
        };
    }
}

/// <summary>
/// Context information for workflow execution
/// </summary>
public class WorkflowExecutionContext
{
    public int WorkflowID { get; set; }
    public int StepID { get; set; }
    public int StatusID { get; set; }
    public int? CurrentUserId { get; set; }
    public string? CurrentUserName { get; set; }
    public Dictionary<string, object> WorkflowData { get; set; } = new();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Validation result for parameters
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ValidationResult Success()
    {
        return new ValidationResult { IsValid = true };
    }

    public static ValidationResult Failure(params string[] errors)
    {
        return new ValidationResult { IsValid = false, Errors = new List<string>(errors) };
    }

    public void AddError(string error)
    {
        IsValid = false;
        Errors.Add(error);
    }
}
