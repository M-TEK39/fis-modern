using FIS.Core.Application.Interfaces.EmailDelivery;
using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services.EmailDelivery;

/// <summary>
/// Adapts the legacy workflow email contract to the provider-neutral delivery path.
/// </summary>
public sealed class WorkflowEmailService : IEmailService
{
    private readonly IEmailDeliveryService _deliveryService;
    private readonly ILogger<WorkflowEmailService> _logger;

    public WorkflowEmailService(
        IEmailDeliveryService deliveryService,
        ILogger<WorkflowEmailService> logger
    )
    {
        _deliveryService = deliveryService;
        _logger = logger;
    }

    public Task<EmailResult> SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true
    ) => SendEmailAsync([to], subject, body, isHtml);

    public async Task<EmailResult> SendEmailAsync(
        List<string> to,
        string subject,
        string body,
        bool isHtml = true
    )
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var result = await _deliveryService.SendAsync(
            new EmailDeliveryRequest(
                to.Where(address => !string.IsNullOrWhiteSpace(address))
                    .Select(address => new EmailRecipient(address.Trim()))
                    .ToArray(),
                subject,
                body,
                isHtml,
                Category: "workflow",
                CorrelationId: correlationId
            )
        );
        if (result.IsAccepted)
            return EmailResult.SuccessResult(
                result.MessageId ?? "accepted",
                result.Provider,
                correlationId
            );

        _logger.LogWarning(
            "Workflow email delivery failed with {Status} through {Provider}",
            result.Status,
            result.Provider?.ToString() ?? "no provider"
        );
        return EmailResult.FailureResult(
            result.Description,
            ToLegacyStatusCode(result.Status),
            result.Status,
            result.Provider,
            correlationId
        );
    }

    private static int ToLegacyStatusCode(EmailDeliveryStatus status) =>
        status switch
        {
            EmailDeliveryStatus.Rejected => 400,
            EmailDeliveryStatus.Unavailable => 503,
            EmailDeliveryStatus.Unknown => 502,
            _ => 500,
        };
}
