using System.Text.Json;
using System.Text.RegularExpressions;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.EmailDelivery;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Service for managing and sending workflow notifications
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IWorkflowNotificationRepository _notificationRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationLogRepository _logRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IStepRepository _stepRepository;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IAccessLevelRepository _accessLevelRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IWorkflowNotificationRepository notificationRepository,
        INotificationTemplateRepository templateRepository,
        INotificationLogRepository logRepository,
        IWorkflowRepository workflowRepository,
        IStepRepository stepRepository,
        IEmailService emailService,
        IUserRepository userRepository,
        IUserProfileRepository userProfileRepository,
        IAccessLevelRepository accessLevelRepository,
        ILogger<NotificationService> logger
    )
    {
        _notificationRepository = notificationRepository;
        _templateRepository = templateRepository;
        _logRepository = logRepository;
        _workflowRepository = workflowRepository;
        _stepRepository = stepRepository;
        _emailService = emailService;
        _userRepository = userRepository;
        _userProfileRepository = userProfileRepository;
        _accessLevelRepository = accessLevelRepository;
        _logger = logger;
    }

    public async Task SendWorkflowNotificationAsync(
        int workflowId,
        string eventType,
        Dictionary<string, object> contextData
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending workflow notifications for WorkflowID: {WorkflowId}, Event: {EventType}",
                workflowId,
                eventType
            );

            // Get all active notifications for this workflow and event type
            var notifications = await _notificationRepository.GetByWorkflowIdAsync(workflowId);
            var relevantNotifications = notifications
                .Where(n =>
                    n.IsActive && n.EventType == eventType && n.StepID == null // Workflow-level notifications
                )
                .ToList();

            if (!relevantNotifications.Any())
            {
                _logger.LogInformation(
                    "No notifications configured for WorkflowID: {WorkflowId}, Event: {EventType}",
                    workflowId,
                    eventType
                );
                return;
            }

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            contextData["WorkflowName"] = workflow?.WorkflowName ?? "Unknown";
            contextData["WorkflowID"] = workflowId;

            foreach (var notification in relevantNotifications)
            {
                await SendNotificationAsync(notification, contextData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending workflow notifications for WorkflowID: {WorkflowId}",
                workflowId
            );
        }
    }

    public async Task SendStepNotificationAsync(
        int stepId,
        string eventType,
        Dictionary<string, object> contextData
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending step notifications for StepID: {StepId}, Event: {EventType}",
                stepId,
                eventType
            );

            // Get all active notifications for this step and event type
            var notifications = await _notificationRepository.GetByStepIdAsync(stepId);
            var relevantNotifications = notifications
                .Where(n => n.IsActive && n.EventType == eventType)
                .ToList();

            if (!relevantNotifications.Any())
            {
                _logger.LogInformation(
                    "No notifications configured for StepID: {StepId}, Event: {EventType}",
                    stepId,
                    eventType
                );
                return;
            }

            var step = await _stepRepository.GetByIdAsync(stepId);
            contextData["StepName"] = step?.StepName ?? "Unknown";
            contextData["StepID"] = stepId;

            if (step != null)
            {
                var workflow = await _workflowRepository.GetByIdAsync(step.WorkflowID);
                contextData["WorkflowName"] = workflow?.WorkflowName ?? "Unknown";
                contextData["WorkflowID"] = step.WorkflowID;
            }

            foreach (var notification in relevantNotifications)
            {
                await SendNotificationAsync(notification, contextData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending step notifications for StepID: {StepId}", stepId);
        }
    }

    private async Task SendNotificationAsync(
        WorkflowNotification notification,
        Dictionary<string, object> contextData,
        NotificationLog? existingLog = null
    )
    {
        var log =
            existingLog
            ?? new NotificationLog
            {
                NotificationID = notification.NotificationID,
                WorkflowID = notification.WorkflowID,
                StepID = notification.StepID,
                EventType = notification.EventType,
                DeliveryStatus = "Pending",
            };

        try
        {
            // Resolve recipient email
            var recipientEmails = await ResolveRecipientEmailsAsync(
                notification.RecipientType,
                notification.RecipientIdentifier
            );
            if (recipientEmails.Count == 0)
            {
                log.DeliveryStatus = "Failed";
                log.ErrorMessage =
                    $"Could not resolve recipient: {notification.RecipientType}:{notification.RecipientIdentifier}";
                return;
            }

            log.RecipientEmail = string.Join(";", recipientEmails);

            // Get subject and body
            string subject;
            string body;

            if (notification.NotificationTemplateID.HasValue)
            {
                // Use template
                var template = await _templateRepository.GetByIdAsync(
                    notification.NotificationTemplateID.Value
                );
                if (template == null)
                {
                    log.DeliveryStatus = "Failed";
                    log.ErrorMessage = $"Template {notification.NotificationTemplateID} not found";
                    return;
                }

                subject = ReplaceVariables(template.Subject, contextData);
                body = ReplaceVariables(template.Body, contextData);
            }
            else
            {
                // Use inline subject/body
                subject = ReplaceVariables(
                    notification.Subject ?? "Workflow Notification",
                    contextData
                );
                body = ReplaceVariables(
                    notification.Body ?? "A workflow event has occurred.",
                    contextData
                );
            }

            log.Subject = subject;
            log.Body = body;

            // Send email
            var emailResult =
                recipientEmails.Count == 1
                    ? await _emailService.SendEmailAsync(
                        recipientEmails[0],
                        subject,
                        body,
                        isHtml: true
                    )
                    : await _emailService.SendEmailAsync(
                        recipientEmails,
                        subject,
                        body,
                        isHtml: true
                    );

            if (emailResult.Success)
            {
                log.DeliveryStatus = "Sent";
                log.SentAt = DateTime.UtcNow;
                log.ExternalMessageId = emailResult.MessageId;
                _logger.LogInformation(
                    "Notification sent successfully to {RecipientCount} recipient(s)",
                    recipientEmails.Count
                );
            }
            else
            {
                log.DeliveryStatus =
                    emailResult.DeliveryStatus == EmailDeliveryStatus.Unknown
                        ? "Unknown"
                        : "Failed";
                log.ErrorMessage = emailResult.ErrorMessage;
                _logger.LogWarning(
                    "Workflow notification was not accepted. Outcome {Outcome}; provider {Provider}; correlation {CorrelationId}",
                    emailResult.DeliveryStatus?.ToString() ?? "Unknown",
                    emailResult.Provider?.ToString() ?? "none",
                    emailResult.CorrelationId ?? "none"
                );
            }
        }
        catch (Exception ex)
        {
            log.DeliveryStatus = "Failed";
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Exception while sending notification");
        }
        finally
        {
            if (existingLog is null)
                await _logRepository.CreateAsync(log);
            else
                await _logRepository.UpdateAsync(log);
        }
    }

    private string ReplaceVariables(string template, Dictionary<string, object> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;
        foreach (var kvp in variables)
        {
            var placeholder = $"{{{kvp.Key}}}";
            result = result.Replace(placeholder, kvp.Value?.ToString() ?? string.Empty);
        }

        return result;
    }

    private async Task<List<string>> ResolveRecipientEmailsAsync(
        string recipientType,
        string recipientIdentifier
    )
    {
        try
        {
            var resolved = recipientType.ToLower() switch
            {
                "email" => ResolveDirectEmails(recipientIdentifier),
                "user" => await GetUserEmailsAsync(recipientIdentifier),
                "role" => await GetRoleEmailsAsync(recipientIdentifier),
                _ => new List<string>(),
            };

            return resolved
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .Select(email => email.Trim())
                .Where(IsLikelyEmail)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error resolving recipient email for type: {Type}, identifier: {Identifier}",
                recipientType,
                recipientIdentifier
            );
            return new List<string>();
        }
    }

    private async Task<List<string>> GetUserEmailsAsync(string userIdentifier)
    {
        var emails = ResolveDirectEmails(userIdentifier);
        if (emails.Count > 0)
        {
            return emails;
        }

        if (int.TryParse(userIdentifier, out var userCode))
        {
            var userByCode = await _userRepository.GetByIdAsync(userCode);
            if (!string.IsNullOrWhiteSpace(userByCode?.email))
            {
                return new List<string> { userByCode.email! };
            }

            if (userCode <= short.MaxValue)
            {
                var legacyUserByCode = await _userProfileRepository.GetByIdAsync((short)userCode);
                if (!string.IsNullOrWhiteSpace(legacyUserByCode?.E_Mail))
                {
                    return new List<string> { legacyUserByCode.E_Mail! };
                }
            }
        }

        var userByEmail = await _userRepository.GetByEmailAsync(userIdentifier);
        if (!string.IsNullOrWhiteSpace(userByEmail?.email))
        {
            return new List<string> { userByEmail.email! };
        }

        var legacyUserByEmail = await _userProfileRepository.GetByEmailAsync(userIdentifier);
        if (!string.IsNullOrWhiteSpace(legacyUserByEmail?.E_Mail))
        {
            return new List<string> { legacyUserByEmail.E_Mail! };
        }

        _logger.LogWarning(
            "No user email could be resolved for identifier: {Identifier}",
            userIdentifier
        );
        return new List<string>();
    }

    private async Task<List<string>> GetRoleEmailsAsync(string roleName)
    {
        var role = await _accessLevelRepository.GetByNameAsync(roleName);
        if (role == null)
        {
            _logger.LogWarning(
                "Role/access level not found for notification recipient role: {Role}",
                roleName
            );
            return new List<string>();
        }

        var users = await _userProfileRepository.GetAllActiveAsync();
        return users
            .Where(user => !string.IsNullOrWhiteSpace(user.E_Mail))
            .Where(user => (user.AccessLevel & role.AccessLevelValue) == role.AccessLevelValue)
            .Select(user => user.E_Mail!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ResolveDirectEmails(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
            .Where(IsLikelyEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsLikelyEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(value.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    public async Task ProcessPendingNotificationsAsync()
    {
        try
        {
            var pendingNotifications = await _logRepository.GetPendingNotificationsAsync();

            _logger.LogInformation(
                "Processing {Count} pending notifications",
                pendingNotifications.Count()
            );

            foreach (var log in pendingNotifications)
            {
                if (log.NotificationID.HasValue)
                {
                    var notification = await _notificationRepository.GetByIdAsync(
                        log.NotificationID.Value
                    );
                    if (notification != null)
                    {
                        var contextData = new Dictionary<string, object>
                        {
                            ["WorkflowID"] = log.WorkflowID,
                            ["StepID"] = log.StepID ?? 0,
                        };

                        await SendNotificationAsync(notification, contextData, log);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending notifications");
        }
    }

    public async Task RetryFailedNotificationsAsync(int maxRetries = 3)
    {
        try
        {
            var failedNotifications = await _logRepository.GetFailedNotificationsAsync();
            var retriableNotifications = failedNotifications
                .Where(l => l.RetryCount < maxRetries)
                .ToList();

            _logger.LogInformation(
                "Retrying {Count} failed notifications",
                retriableNotifications.Count
            );

            foreach (var log in retriableNotifications)
            {
                if (log.NotificationID.HasValue)
                {
                    var notification = await _notificationRepository.GetByIdAsync(
                        log.NotificationID.Value
                    );
                    if (notification != null)
                    {
                        log.RetryCount++;
                        log.DeliveryStatus = "Pending";

                        var contextData = new Dictionary<string, object>
                        {
                            ["WorkflowID"] = log.WorkflowID,
                            ["StepID"] = log.StepID ?? 0,
                        };

                        await SendNotificationAsync(notification, contextData, log);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed notifications");
        }
    }

    public async Task<IEnumerable<NotificationLog>> GetNotificationLogsAsync(int workflowId)
    {
        return await _logRepository.GetByWorkflowIdAsync(workflowId);
    }
}
