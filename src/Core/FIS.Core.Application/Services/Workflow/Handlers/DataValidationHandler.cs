using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow.Handlers;

public class DataValidationHandler : StepHandlerBase
{
    public override string HandlerType => "data_validation";

    public DataValidationHandler(ILogger<DataValidationHandler> logger) : base(logger) { }

    protected override Task<StepExecutionResult> ExecuteInternalAsync(Dictionary<string, object> parameters, WorkflowExecutionContext context)
    {
        var fieldName = GetRequiredParameter<string>(parameters, "fieldName");
        var validationType = GetRequiredParameter<string>(parameters, "validationType");

        if (!context.WorkflowData.TryGetValue(fieldName, out var fieldValue))
        {
            return Task.FromResult(StepExecutionResult.FailureResult($"Field '{fieldName}' not found"));
        }

        bool isValid = validationType.ToLower() switch
        {
            "required" => fieldValue != null && !string.IsNullOrWhiteSpace(fieldValue.ToString()),
            "numeric" => decimal.TryParse(fieldValue?.ToString(), out _),
            _ => false
        };

        if (!isValid)
            return Task.FromResult(StepExecutionResult.FailureResult("Validation failed"));

        return Task.FromResult(StepExecutionResult.SuccessResult($"Validation passed for '{fieldName}'"));
    }

    public override Task<Interfaces.Workflow.ValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters)
    {
        var result = new Interfaces.Workflow.ValidationResult { IsValid = true };

        if (!parameters.ContainsKey("fieldName") || string.IsNullOrWhiteSpace(parameters["fieldName"]?.ToString()))
            result.AddError("Parameter 'fieldName' is required");
        if (!parameters.ContainsKey("validationType") || string.IsNullOrWhiteSpace(parameters["validationType"]?.ToString()))
            result.AddError("Parameter 'validationType' is required");

        return Task.FromResult(result);
    }
}
