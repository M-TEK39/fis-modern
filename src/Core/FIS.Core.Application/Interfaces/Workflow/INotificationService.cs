using FIS.Core.Domain.Entities.System;

namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Service for managing workflow notifications
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send notification for a workflow event
    /// </summary>
    Task SendWorkflowNotificationAsync(int workflowId, string eventType, Dictionary<string, object> contextData);

    /// <summary>
    /// Send notification for a step event
    /// </summary>
    Task SendStepNotificationAsync(int stepId, string eventType, Dictionary<string, object> contextData);

    /// <summary>
    /// Process pending notifications (for batch processing)
    /// </summary>
    Task ProcessPendingNotificationsAsync();

    /// <summary>
    /// Retry failed notifications
    /// </summary>
    Task RetryFailedNotificationsAsync(int maxRetries = 3);

    /// <summary>
    /// Get notification log for a workflow
    /// </summary>
    Task<IEnumerable<NotificationLog>> GetNotificationLogsAsync(int workflowId);
}
