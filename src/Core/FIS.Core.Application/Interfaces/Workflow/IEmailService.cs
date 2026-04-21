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
    Task<EmailResult> SendEmailAsync(List<string> to, string subject, string body, bool isHtml = true);

    /// <summary>
    /// Sends an email using a template with variable substitution
    /// </summary>
    Task<EmailResult> SendTemplatedEmailAsync(string to, string templateId, Dictionary<string, string> variables);
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

    public static EmailResult SuccessResult(string messageId)
    {
        return new EmailResult
        {
            Success = true,
            MessageId = messageId,
            StatusCode = 200
        };
    }

    public static EmailResult FailureResult(string errorMessage, int statusCode = 500)
    {
        return new EmailResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };
    }
}
