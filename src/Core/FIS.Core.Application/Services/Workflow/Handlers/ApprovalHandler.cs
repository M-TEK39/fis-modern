using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow.Handlers;

public class ApprovalHandler : StepHandlerBase
{
    public override string HandlerType => "approval";

    public ApprovalHandler(ILogger<ApprovalHandler> logger) : base(logger) { }

    protected override Task<StepExecutionResult> ExecuteInternalAsync(Dictionary<string, object> parameters, WorkflowExecutionContext context)
    {
        var approverUserId = GetOptionalParameter<int?>(parameters, "approverUserId", null);
        var approverRole = GetOptionalParameter<string>(parameters, "approverRole", null);

        Logger.LogInformation("Approval pending for StepID {StepID}", context.StepID);

        return Task.FromResult(StepExecutionResult.SuccessResult(
            "Approval step initiated",
            new Dictionary<string, object>
            {
                ["approved"] = false,
                ["pending"] = true,
                ["approverUserId"] = approverUserId ?? 0,
                ["approverRole"] = approverRole ?? "Unknown"
            }));
    }

    public override Task<Interfaces.Workflow.ValidationResult> ValidateParametersAsync(Dictionary<string, object> parameters)
    {
        var result = new Interfaces.Workflow.ValidationResult { IsValid = true };
        var hasApproverUserId = parameters.ContainsKey("approverUserId") && parameters["approverUserId"] != null;
        var hasApproverRole = parameters.ContainsKey("approverRole") && !string.IsNullOrWhiteSpace(parameters["approverRole"]?.ToString());

        if (!hasApproverUserId && !hasApproverRole)
            result.AddError("At least one approval method must be specified: approverUserId or approverRole");

        return Task.FromResult(result);
    }
}
