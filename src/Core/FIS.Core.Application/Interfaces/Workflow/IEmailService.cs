namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Service for sending email notifications
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email to a single recipient
    /// </summary>
    Task<EmailResult> SendEmailAsync(string to, string subject, string body, bool isHtml = true);

    /// <summary>
    /// Sends an email to multiple recipients
    /// </summary>
    Task<EmailResult> SendEmailAsync(
        List<string> to,
        string subject,
        string body,
        bool isHtml = true
    );
}

/// <summary>
/// Result of an email send operation
/// </summary>
public class EmailResult
{
    public bool Success { get; set; }
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public FIS.Core.Application.Interfaces.EmailDelivery.EmailDeliveryStatus? DeliveryStatus { get; set; }
    public FIS.Core.Application.Interfaces.EmailDelivery.EmailProvider? Provider { get; set; }
    public string? CorrelationId { get; set; }

    public static EmailResult SuccessResult(
        string messageId,
        FIS.Core.Application.Interfaces.EmailDelivery.EmailProvider? provider = null,
        string? correlationId = null
    )
    {
        return new EmailResult
        {
            Success = true,
            MessageId = messageId,
            StatusCode = 200,
            DeliveryStatus = FIS.Core
                .Application
                .Interfaces
                .EmailDelivery
                .EmailDeliveryStatus
                .Accepted,
            Provider = provider,
            CorrelationId = correlationId,
        };
    }

    public static EmailResult FailureResult(
        string errorMessage,
        int statusCode = 500,
        FIS.Core.Application.Interfaces.EmailDelivery.EmailDeliveryStatus? deliveryStatus = null,
        FIS.Core.Application.Interfaces.EmailDelivery.EmailProvider? provider = null,
        string? correlationId = null
    )
    {
        return new EmailResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            DeliveryStatus = deliveryStatus,
            Provider = provider,
            CorrelationId = correlationId,
        };
    }
}
