using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/workflow-notification")]
[Authorize]
public class WorkflowNotificationController : BaseApiController
{
    private readonly IWorkflowNotificationRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WorkflowNotificationController> _logger;

    public WorkflowNotificationController(
        IWorkflowNotificationRepository repository,
        INotificationService notificationService,
        ILogger<WorkflowNotificationController> logger
    )
    {
        _repository = repository;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all workflow notifications
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowNotificationDto>>> GetAll()
    {
        try
        {
            var notifications = await _repository.GetAllAsync();
            var dtos = notifications.Select(n => MapToDto(n));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow notifications");
            return StatusCode(500, "Error retrieving notifications");
        }
    }

    /// <summary>
    /// Get notification by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowNotificationDto>> GetById(int id)
    {
        try
        {
            var notification = await _repository.GetByIdAsync(id);
            return notification == null ? NotFound() : Ok(MapToDto(notification));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification {Id}", id);
            return StatusCode(500, "Error retrieving notification");
        }
    }

    /// <summary>
    /// Get notifications for a specific workflow
    /// </summary>
    [HttpGet("workflow/{workflowId}")]
    public async Task<ActionResult<IEnumerable<WorkflowNotificationDto>>> GetByWorkflow(
        int workflowId
    )
    {
        try
        {
            var notifications = await _repository.GetByWorkflowIdAsync(workflowId);
            var dtos = notifications.Select(n => MapToDto(n));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving notifications for workflow {WorkflowId}",
                workflowId
            );
            return StatusCode(500, "Error retrieving notifications");
        }
    }

    /// <summary>
    /// Get notifications for a specific step
    /// </summary>
    [HttpGet("step/{stepId}")]
    public async Task<ActionResult<IEnumerable<WorkflowNotificationDto>>> GetByStep(int stepId)
    {
        try
        {
            var notifications = await _repository.GetByStepIdAsync(stepId);
            var dtos = notifications.Select(n => MapToDto(n));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for step {StepId}", stepId);
            return StatusCode(500, "Error retrieving notifications");
        }
    }

    /// <summary>
    /// Get notifications by event type
    /// </summary>
    [HttpGet("event/{eventType}")]
    public async Task<ActionResult<IEnumerable<WorkflowNotificationDto>>> GetByEventType(
        string eventType
    )
    {
        try
        {
            var notifications = await _repository.GetByEventTypeAsync(eventType);
            var dtos = notifications.Select(n => MapToDto(n));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for event {EventType}", eventType);
            return StatusCode(500, "Error retrieving notifications");
        }
    }

    /// <summary>
    /// Create a new workflow notification
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowNotificationDto>> Create(
        [FromBody] CreateWorkflowNotificationDto dto
    )
    {
        try
        {
            var notification = new WorkflowNotification
            {
                WorkflowID = dto.WorkflowID,
                StepID = dto.StepID,
                EventType = dto.EventType,
                RecipientType = dto.RecipientType,
                RecipientIdentifier = dto.RecipientIdentifier,
                NotificationTemplateID = dto.NotificationTemplateID,
                Subject = dto.Subject,
                Body = dto.Body,
                IsActive = dto.IsActive,
                SendDelay = dto.SendDelay,
            };

            var created = await _repository.CreateAsync(notification, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.NotificationID },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow notification");
            return StatusCode(500, "Error creating notification");
        }
    }

    /// <summary>
    /// Update a workflow notification
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateWorkflowNotificationDto dto)
    {
        try
        {
            if (id != dto.NotificationID)
                return BadRequest("ID mismatch");

            var notification = new WorkflowNotification
            {
                NotificationID = dto.NotificationID,
                WorkflowID = dto.WorkflowID,
                StepID = dto.StepID,
                EventType = dto.EventType,
                RecipientType = dto.RecipientType,
                RecipientIdentifier = dto.RecipientIdentifier,
                NotificationTemplateID = dto.NotificationTemplateID,
                Subject = dto.Subject,
                Body = dto.Body,
                IsActive = dto.IsActive,
                SendDelay = dto.SendDelay,
            };

            await _repository.UpdateAsync(notification, GetCurrentUserId());
            return Ok(new { message = "Notification updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification {Id}", id);
            return StatusCode(500, "Error updating notification");
        }
    }

    /// <summary>
    /// Delete a workflow notification
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {Id}", id);
            return StatusCode(500, "Error deleting notification");
        }
    }

    /// <summary>
    /// Get notification logs for a workflow
    /// </summary>
    [HttpGet("workflow/{workflowId}/logs")]
    public async Task<ActionResult<IEnumerable<NotificationLogDto>>> GetWorkflowLogs(int workflowId)
    {
        try
        {
            var logs = await _notificationService.GetNotificationLogsAsync(workflowId);
            var dtos = logs.Select(l => MapLogToDto(l));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving notification logs for workflow {WorkflowId}",
                workflowId
            );
            return StatusCode(500, "Error retrieving notification logs");
        }
    }

    /// <summary>
    /// Retry failed notifications
    /// </summary>
    [HttpPost("retry-failed")]
    public async Task<ActionResult> RetryFailedNotifications([FromQuery] int maxRetries = 3)
    {
        try
        {
            await _notificationService.RetryFailedNotificationsAsync(maxRetries);
            return Ok(new { message = "Failed notifications retry initiated", maxRetries });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed notifications");
            return StatusCode(500, "Error retrying failed notifications");
        }
    }

    /// <summary>
    /// Process pending notifications
    /// </summary>
    [HttpPost("process-pending")]
    public async Task<ActionResult> ProcessPending()
    {
        try
        {
            await _notificationService.ProcessPendingNotificationsAsync();
            return Ok(new { message = "Pending notifications processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending notifications");
            return StatusCode(500, "Error processing pending notifications");
        }
    }

    private WorkflowNotificationDto MapToDto(WorkflowNotification notification)
    {
        return new WorkflowNotificationDto
        {
            NotificationID = notification.NotificationID,
            WorkflowID = notification.WorkflowID,
            StepID = notification.StepID,
            EventType = notification.EventType,
            RecipientType = notification.RecipientType,
            RecipientIdentifier = notification.RecipientIdentifier,
            NotificationTemplateID = notification.NotificationTemplateID,
            Subject = notification.Subject,
            Body = notification.Body,
            IsActive = notification.IsActive,
            SendDelay = notification.SendDelay,
            DateCreated = notification.date_created,
            DateUpdated = notification.date_updated,
        };
    }

    private NotificationLogDto MapLogToDto(NotificationLog log)
    {
        return new NotificationLogDto
        {
            LogID = log.LogID,
            NotificationID = log.NotificationID,
            WorkflowID = log.WorkflowID,
            StepID = log.StepID,
            EventType = log.EventType,
            RecipientEmail = log.RecipientEmail,
            Subject = log.Subject,
            SentAt = log.SentAt,
            DeliveryStatus = log.DeliveryStatus,
            ErrorMessage = log.ErrorMessage,
            RetryCount = log.RetryCount,
            ExternalMessageId = log.ExternalMessageId,
            DateCreated = log.date_created,
        };
    }
}

#region WorkflowNotification DTOs

public class WorkflowNotificationDto
{
    public int NotificationID { get; set; }
    public int WorkflowID { get; set; }
    public int? StepID { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientIdentifier { get; set; } = string.Empty;
    public int? NotificationTemplateID { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsActive { get; set; }
    public int? SendDelay { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateWorkflowNotificationDto
{
    public int WorkflowID { get; set; }
    public int? StepID { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientIdentifier { get; set; } = string.Empty;
    public int? NotificationTemplateID { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsActive { get; set; } = true;
    public int? SendDelay { get; set; }
}

public class UpdateWorkflowNotificationDto
{
    public int NotificationID { get; set; }
    public int WorkflowID { get; set; }
    public int? StepID { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientIdentifier { get; set; } = string.Empty;
    public int? NotificationTemplateID { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsActive { get; set; }
    public int? SendDelay { get; set; }
}

public class NotificationLogDto
{
    public int LogID { get; set; }
    public int? NotificationID { get; set; }
    public int WorkflowID { get; set; }
    public int? StepID { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public DateTime? SentAt { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? ExternalMessageId { get; set; }
    public DateTime DateCreated { get; set; }
}

#endregion
