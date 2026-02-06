namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Service for evaluating conditional expressions in workflow steps
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// Evaluates a condition expression with the given context data
    /// </summary>
    /// <param name="expression">The condition expression (e.g., "[Amount] &gt; 1000 &amp;&amp; [Status] == 'Pending'")</param>
    /// <param name="contextData">Dictionary containing context variables (e.g., Amount, Status, etc.)</param>
    /// <returns>True if condition is met, False otherwise</returns>
    Task<bool> EvaluateAsync(string expression, Dictionary<string, object> contextData);

    /// <summary>
    /// Validates that a condition expression is syntactically correct
    /// </summary>
    /// <param name="expression">The condition expression to validate</param>
    /// <returns>True if valid, False if invalid</returns>
    Task<bool> ValidateExpressionAsync(string expression);
}
