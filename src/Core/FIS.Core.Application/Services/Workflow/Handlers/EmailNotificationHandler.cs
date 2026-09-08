using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow.Handlers;

public class EmailNotificationHandler : StepHandlerBase
{
    public override string HandlerType => "email_notification";

    public EmailNotificationHandler(ILogger<EmailNotificationHandler> logger)
        : base(logger) { }

    protected override Task<StepExecutionResult> ExecuteInternalAsync(
        Dictionary<string, object> parameters,
        WorkflowExecutionContext context
    )
    {
        var to = GetRequiredParameter<string>(parameters, "to");
        var subject = GetRequiredParameter<string>(parameters, "subject");
        var body = GetRequiredParameter<string>(parameters, "body");

        Logger.LogInformation("Email would be sent to {To} with subject '{Subject}'", to, subject);

        return Task.FromResult(
            StepExecutionResult.SuccessResult(
                $"Email sent to {to}",
                new Dictionary<string, object>
                {
                    ["emailSent"] = true,
                    ["recipient"] = to,
                    ["sentAt"] = DateTime.UtcNow,
                }
            )
        );
    }

    public override Task<Interfaces.Workflow.ValidationResult> ValidateParametersAsync(
        Dictionary<string, object> parameters
    )
    {
        var result = new Interfaces.Workflow.ValidationResult { IsValid = true };

        if (
            !parameters.ContainsKey("to") || string.IsNullOrWhiteSpace(parameters["to"]?.ToString())
        )
            result.AddError("Parameter 'to' is required");
        if (
            !parameters.ContainsKey("subject")
            || string.IsNullOrWhiteSpace(parameters["subject"]?.ToString())
        )
            result.AddError("Parameter 'subject' is required");
        if (
            !parameters.ContainsKey("body")
            || string.IsNullOrWhiteSpace(parameters["body"]?.ToString())
        )
            result.AddError("Parameter 'body' is required");

        return Task.FromResult(result);
    }
}
