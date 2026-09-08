using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;
using NCalc;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Implementation of condition evaluator using NCalc library
/// </summary>
public class ConditionEvaluator : IConditionEvaluator
{
    private readonly ILogger<ConditionEvaluator> _logger;

    public ConditionEvaluator(ILogger<ConditionEvaluator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Evaluates a condition expression with the given context data
    /// </summary>
    public async Task<bool> EvaluateAsync(string expression, Dictionary<string, object> contextData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                _logger.LogWarning("Empty or null condition expression provided");
                return true; // Default to true for empty conditions
            }

            _logger.LogInformation("Evaluating condition: {Expression}", expression);

            // Create NCalc expression
            var ncalcExpression = new Expression(expression);

            // Add context parameters to the expression
            foreach (var kvp in contextData)
            {
                ncalcExpression.Parameters[kvp.Key] = kvp.Value;
                _logger.LogDebug("Added parameter: {Key} = {Value}", kvp.Key, kvp.Value);
            }

            // Evaluate the expression
            var result = ncalcExpression.Evaluate();

            // Convert result to boolean
            bool boolResult = Convert.ToBoolean(result);

            _logger.LogInformation("Condition evaluated to: {Result}", boolResult);

            return await Task.FromResult(boolResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition expression: {Expression}", expression);
            return false; // Default to false on error
        }
    }

    /// <summary>
    /// Validates that a condition expression is syntactically correct
    /// </summary>
    public async Task<bool> ValidateExpressionAsync(string expression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return true; // Empty expressions are considered valid
            }

            // Try to parse the expression
            var ncalcExpression = new Expression(expression);

            // Check if it has errors
            if (ncalcExpression.HasErrors())
            {
                _logger.LogWarning(
                    "Invalid expression: {Expression} - {Error}",
                    expression,
                    ncalcExpression.Error
                );
                return false;
            }

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating condition expression: {Expression}", expression);
            return false;
        }
    }
}
