using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow.Handlers;

public class EmailNotificationHandler : StepHandlerBase
{
    private readonly IEmailService _emailService;

    public override string HandlerType => "email_notification";

    public EmailNotificationHandler(
        IEmailService emailService,
        ILogger<EmailNotificationHandler> logger
    )
        : base(logger)
    {
        _emailService = emailService;
    }

    protected override async Task<StepExecutionResult> ExecuteInternalAsync(
        Dictionary<string, object> parameters,
        WorkflowExecutionContext context
    )
    {
        var to = GetRequiredParameter<string>(parameters, "to");
        var subject = GetRequiredParameter<string>(parameters, "subject");
        var body = GetRequiredParameter<string>(parameters, "body");

        var emailResult = await _emailService.SendEmailAsync(to, subject, body, isHtml: true);
        if (!emailResult.Success)
        {
            Logger.LogWarning(
                "Workflow email delivery failed for workflow execution {WorkflowExecutionId}",
                context.WorkflowID
            );
            return StepExecutionResult.FailureResult("The workflow email could not be delivered.");
        }

        Logger.LogInformation(
            "Workflow email delivery accepted for workflow execution {WorkflowExecutionId}",
            context.WorkflowID
        );

        return StepExecutionResult.SuccessResult(
            "Workflow email accepted for delivery.",
            new Dictionary<string, object>
            {
                ["emailSent"] = true,
                ["sentAt"] = DateTime.UtcNow,
                ["messageId"] = emailResult.MessageId ?? "accepted",
            }
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
