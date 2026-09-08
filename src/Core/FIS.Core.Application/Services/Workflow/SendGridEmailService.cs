using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Email service implementation using SendGrid
/// </summary>
public class SendGridEmailService : IEmailService
{
    private readonly ISendGridClient _sendGridClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SendGridEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailService(
        ISendGridClient sendGridClient,
        IConfiguration configuration,
        ILogger<SendGridEmailService> logger
    )
    {
        _sendGridClient = sendGridClient;
        _configuration = configuration;
        _logger = logger;

        // Load from configuration
        _fromEmail = _configuration["SendGrid:FromEmail"] ?? "noreply@fis.gov.za";
        _fromName = _configuration["SendGrid:FromName"] ?? "Fleet Information System";
    }

    public async Task<EmailResult> SendEmailAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true
    )
    {
        return await SendEmailAsync(new List<string> { to }, subject, body, isHtml);
    }

    public async Task<EmailResult> SendEmailAsync(
        List<string> to,
        string subject,
        string body,
        bool isHtml = true
    )
    {
        try
        {
            var from = new EmailAddress(_fromEmail, _fromName);
            var recipients = to.Select(email => new EmailAddress(email)).ToList();

            var msg = new SendGridMessage { From = from, Subject = subject };

            msg.AddTos(recipients);

            if (isHtml)
            {
                msg.HtmlContent = body;
            }
            else
            {
                msg.PlainTextContent = body;
            }

            _logger.LogInformation(
                "Sending email to {RecipientCount} recipients: {Subject}",
                to.Count,
                subject
            );

            var response = await _sendGridClient.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.GetValues("X-Message-Id").FirstOrDefault();
                _logger.LogInformation(
                    "Email sent successfully. MessageId: {MessageId}",
                    messageId
                );
                return EmailResult.SuccessResult(messageId ?? "unknown");
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to send email. StatusCode: {StatusCode}, Error: {Error}",
                    (int)response.StatusCode,
                    errorBody
                );
                return EmailResult.FailureResult(
                    $"SendGrid error: {errorBody}",
                    (int)response.StatusCode
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending email");
            return EmailResult.FailureResult($"Exception: {ex.Message}");
        }
    }

    public async Task<EmailResult> SendTemplatedEmailAsync(
        string to,
        string templateId,
        Dictionary<string, string> variables
    )
    {
        try
        {
            var from = new EmailAddress(_fromEmail, _fromName);
            var toAddress = new EmailAddress(to);

            var msg = new SendGridMessage { From = from, TemplateId = templateId };

            msg.AddTo(toAddress);
            msg.SetTemplateData(variables);

            _logger.LogInformation(
                "Sending templated email to {Recipient} using template {TemplateId}",
                to,
                templateId
            );

            var response = await _sendGridClient.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.GetValues("X-Message-Id").FirstOrDefault();
                _logger.LogInformation(
                    "Templated email sent successfully. MessageId: {MessageId}",
                    messageId
                );
                return EmailResult.SuccessResult(messageId ?? "unknown");
            }
            else
            {
                var errorBody = await response.Body.ReadAsStringAsync();
                _logger.LogError(
                    "Failed to send templated email. StatusCode: {StatusCode}, Error: {Error}",
                    (int)response.StatusCode,
                    errorBody
                );
                return EmailResult.FailureResult(
                    $"SendGrid error: {errorBody}",
                    (int)response.StatusCode
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending templated email");
            return EmailResult.FailureResult($"Exception: {ex.Message}");
        }
    }
}
