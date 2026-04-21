using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Base class for step handlers providing common functionality
/// </summary>
public abstract class StepHandlerBase : IStepHandler
{
    protected readonly ILogger Logger;

    protected StepHandlerBase(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Gets the unique identifier for this handler type
    /// </summary>
    public abstract string HandlerType { get; }

    /// <summary>
    /// Executes the step with retry logic and error handling
    /// </summary>
    public async Task<StepExecutionResult> ExecuteAsync(Dictionary<string, object> parameters, WorkflowExecutionContext context)
    {
        try
        {
            Logger.LogInformation("Executing step handler {HandlerType} for Step {StepID}", HandlerType, context.StepID);

            // Validate parameters first
            var validationResult = await ValidateParametersAsync(parameters);
            if (!validationResult.IsValid)
            {
                Logger.LogWarning("Parameter validation failed for {HandlerType}: {Errors}",
                    HandlerType, string.Join(", ", validationResult.Errors));
                return StepExecutionResult.FailureResult(
                    $"Parameter validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            // Execute the handler-specific logic
            var result = await ExecuteInternalAsync(parameters, context);

            if (result.Success)
            {
                Logger.LogInformation("Step handler {HandlerType} executed successfully for Step {StepID}",
                    HandlerType, context.StepID);
            }
            else
            {
                Logger.LogWarning("Step handler {HandlerType} failed for Step {StepID}: {Message}",
                    HandlerType, context.StepID, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing step handler {HandlerType} for Step {StepID}",
                HandlerType, context.StepID);
            return StepExecutionResult.FailureResult($"Handler execution failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handler-specific execution logic to be implemented by derived classes
    /// </summary>
    protected abstract Task<StepExecutionResult> ExecuteInternalAsync(
        Dictionary<string, object> parameters,
        WorkflowExecutionContext context);

    /// <summary>
    /// Validates parameters - override in derived classes for specific validation
    /// </summary>
    public virtual Task<Interfaces.Workflow.ValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters)
    {
        return Task.FromResult(Interfaces.Workflow.ValidationResult.Success());
    }

    /// <summary>
    /// Helper method to get a required parameter
    /// </summary>
    protected T GetRequiredParameter<T>(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.ContainsKey(key))
        {
            throw new ArgumentException($"Required parameter '{key}' not found");
        }

        var value = parameters[key];
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Try to convert
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Parameter '{key}' cannot be converted to type {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Helper method to get an optional parameter with default value
    /// </summary>
    protected T? GetOptionalParameter<T>(Dictionary<string, object> parameters, string key, T? defaultValue = default)
    {
        if (!parameters.ContainsKey(key))
        {
            return defaultValue;
        }

        var value = parameters[key];
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Try to convert
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
