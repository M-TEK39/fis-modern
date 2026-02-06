using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IWorkflowNotificationRepository notificationRepository,
        INotificationTemplateRepository templateRepository,
        INotificationLogRepository logRepository,
        IWorkflowRepository workflowRepository,
        IStepRepository stepRepository,
        IEmailService emailService,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _templateRepository = templateRepository;
        _logRepository = logRepository;
        _workflowRepository = workflowRepository;
        _stepRepository = stepRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendWorkflowNotificationAsync(int workflowId, string eventType, Dictionary<string, object> contextData)
    {
        try
        {
            _logger.LogInformation("Sending workflow notifications for WorkflowID: {WorkflowId}, Event: {EventType}", 
                workflowId, eventType);

            // Get all active notifications for this workflow and event type
            var notifications = await _notificationRepository.GetByWorkflowIdAsync(workflowId);
            var relevantNotifications = notifications.Where(n => 
                n.IsActive && 
                n.EventType == eventType &&
                n.StepID == null // Workflow-level notifications
            ).ToList();

            if (!relevantNotifications.Any())
            {
                _logger.LogInformation("No notifications configured for WorkflowID: {WorkflowId}, Event: {EventType}",
                    workflowId, eventType);
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
            _logger.LogError(ex, "Error sending workflow notifications for WorkflowID: {WorkflowId}", workflowId);
        }
    }

    public async Task SendStepNotificationAsync(int stepId, string eventType, Dictionary<string, object> contextData)
    {
        try
        {
            _logger.LogInformation("Sending step notifications for StepID: {StepId}, Event: {EventType}",
                stepId, eventType);

            // Get all active notifications for this step and event type
            var notifications = await _notificationRepository.GetByStepIdAsync(stepId);
            var relevantNotifications = notifications.Where(n =>
                n.IsActive &&
                n.EventType == eventType
            ).ToList();

            if (!relevantNotifications.Any())
            {
                _logger.LogInformation("No notifications configured for StepID: {StepId}, Event: {EventType}",
                    stepId, eventType);
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

    private async Task SendNotificationAsync(WorkflowNotification notification, Dictionary<string, object> contextData)
    {
        var log = new NotificationLog
        {
            NotificationID = notification.NotificationID,
            WorkflowID = notification.WorkflowID,
            StepID = notification.StepID,
            EventType = notification.EventType,
            DeliveryStatus = "Pending"
        };

        try
        {
            // Resolve recipient email
            var recipientEmail = await ResolveRecipientEmailAsync(notification.RecipientType, notification.RecipientIdentifier);
            if (string.IsNullOrEmpty(recipientEmail))
            {
                log.DeliveryStatus = "Failed";
                log.ErrorMessage = $"Could not resolve recipient: {notification.RecipientType}:{notification.RecipientIdentifier}";
                await _logRepository.CreateAsync(log);
                return;
            }

            log.RecipientEmail = recipientEmail;

            // Get subject and body
            string subject;
            string body;

            if (notification.NotificationTemplateID.HasValue)
            {
                // Use template
                var template = await _templateRepository.GetByIdAsync(notification.NotificationTemplateID.Value);
                if (template == null)
                {
                    log.DeliveryStatus = "Failed";
                    log.ErrorMessage = $"Template {notification.NotificationTemplateID} not found";
                    await _logRepository.CreateAsync(log);
                    return;
                }

                subject = ReplaceVariables(template.Subject, contextData);
                body = ReplaceVariables(template.Body, contextData);
            }
            else
            {
                // Use inline subject/body
                subject = ReplaceVariables(notification.Subject ?? "Workflow Notification", contextData);
                body = ReplaceVariables(notification.Body ?? "A workflow event has occurred.", contextData);
            }

            log.Subject = subject;
            log.Body = body;

            // Send email
            var emailResult = await _emailService.SendEmailAsync(recipientEmail, subject, body, isHtml: true);

            if (emailResult.Success)
            {
                log.DeliveryStatus = "Sent";
                log.SentAt = DateTime.UtcNow;
                log.ExternalMessageId = emailResult.MessageId;
                _logger.LogInformation("Notification sent successfully to {Recipient}", recipientEmail);
            }
            else
            {
                log.DeliveryStatus = "Failed";
                log.ErrorMessage = emailResult.ErrorMessage;
                _logger.LogWarning("Failed to send notification to {Recipient}: {Error}", 
                    recipientEmail, emailResult.ErrorMessage);
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
            await _logRepository.CreateAsync(log);
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

    private async Task<string?> ResolveRecipientEmailAsync(string recipientType, string recipientIdentifier)
    {
        try
        {
            return recipientType.ToLower() switch
            {
                "email" => recipientIdentifier,
                "user" => await GetUserEmailAsync(recipientIdentifier),
                "role" => await GetRoleEmailsAsync(recipientIdentifier),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving recipient email for type: {Type}, identifier: {Identifier}",
                recipientType, recipientIdentifier);
            return null;
        }
    }

    private Task<string?> GetUserEmailAsync(string userIdentifier)
    {
        // TODO: Implement user email lookup
        // For now, return the identifier if it looks like an email
        if (userIdentifier.Contains("@"))
            return Task.FromResult<string?>(userIdentifier);

        _logger.LogWarning("User email lookup not implemented for identifier: {Identifier}", userIdentifier);
        return Task.FromResult<string?>(null);
    }

    private Task<string?> GetRoleEmailsAsync(string roleName)
    {
        // TODO: Implement role-based email lookup (could return multiple emails)
        _logger.LogWarning("Role-based email lookup not implemented for role: {Role}", roleName);
        return Task.FromResult<string?>(null);
    }

    public async Task ProcessPendingNotificationsAsync()
    {
        try
        {
            var pendingNotifications = await _logRepository.GetPendingNotificationsAsync();
            
            _logger.LogInformation("Processing {Count} pending notifications", pendingNotifications.Count());

            foreach (var log in pendingNotifications)
            {
                if (log.NotificationID.HasValue)
                {
                    var notification = await _notificationRepository.GetByIdAsync(log.NotificationID.Value);
                    if (notification != null)
                    {
                        var contextData = new Dictionary<string, object>
                        {
                            ["WorkflowID"] = log.WorkflowID,
                            ["StepID"] = log.StepID ?? 0
                        };

                        await SendNotificationAsync(notification, contextData);
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
            var retriableNotifications = failedNotifications.Where(l => l.RetryCount < maxRetries).ToList();

            _logger.LogInformation("Retrying {Count} failed notifications", retriableNotifications.Count);

            foreach (var log in retriableNotifications)
            {
                if (log.NotificationID.HasValue)
                {
                    var notification = await _notificationRepository.GetByIdAsync(log.NotificationID.Value);
                    if (notification != null)
                    {
                        log.RetryCount++;
                        log.DeliveryStatus = "Pending";
                        await _logRepository.UpdateAsync(log);

                        var contextData = new Dictionary<string, object>
                        {
                            ["WorkflowID"] = log.WorkflowID,
                            ["StepID"] = log.StepID ?? 0
                        };

                        await SendNotificationAsync(notification, contextData);
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
