using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow.Handlers;

public class DelayHandler : StepHandlerBase
{
    public override string HandlerType => "delay";

    public DelayHandler(ILogger<DelayHandler> logger)
        : base(logger) { }

    protected override async Task<StepExecutionResult> ExecuteInternalAsync(
        Dictionary<string, object> parameters,
        WorkflowExecutionContext context
    )
    {
        var delaySeconds = GetRequiredParameter<int>(parameters, "delaySeconds");

        Logger.LogInformation("Executing delay of {Seconds} seconds", delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

        return StepExecutionResult.SuccessResult($"Delay of {delaySeconds} seconds completed");
    }

    public override Task<Interfaces.Workflow.ValidationResult> ValidateParametersAsync(
        Dictionary<string, object> parameters
    )
    {
        var result = new Interfaces.Workflow.ValidationResult { IsValid = true };

        if (!parameters.ContainsKey("delaySeconds"))
            result.AddError("Parameter 'delaySeconds' is required");
        else if (!int.TryParse(parameters["delaySeconds"].ToString(), out var delay) || delay <= 0)
            result.AddError("Parameter 'delaySeconds' must be a positive integer");

        return Task.FromResult(result);
    }
}
