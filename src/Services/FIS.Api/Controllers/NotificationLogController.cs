using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/notification-log")]
[Authorize]
public class NotificationLogController : BaseApiController
{
    private readonly INotificationLogRepository _repository;
    private readonly ILogger<NotificationLogController> _logger;

    public NotificationLogController(
        INotificationLogRepository repository,
        ILogger<NotificationLogController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all notification logs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationLogDto>>> GetAll()
    {
        try
        {
            var logs = await _repository.GetAllAsync();
            var dtos = logs.Select(l => MapToDto(l));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification logs");
            return StatusCode(500, "Error retrieving logs");
        }
    }

    /// <summary>
    /// Get log by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationLogDto>> GetById(int id)
    {
        try
        {
            var log = await _repository.GetByIdAsync(id);
            return log == null ? NotFound() : Ok(MapToDto(log));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving log {Id}", id);
            return StatusCode(500, "Error retrieving log");
        }
    }

    /// <summary>
    /// Get logs for a specific workflow
    /// </summary>
    [HttpGet("workflow/{workflowId}")]
    public async Task<ActionResult<IEnumerable<NotificationLogDto>>> GetByWorkflow(int workflowId)
    {
        try
        {
            var logs = await _repository.GetByWorkflowIdAsync(workflowId);
            var dtos = logs.Select(l => MapToDto(l));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs for workflow {WorkflowId}", workflowId);
            return StatusCode(500, "Error retrieving logs");
        }
    }

    /// <summary>
    /// Get logs by delivery status
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<NotificationLogDto>>> GetByStatus(string status)
    {
        try
        {
            var logs = await _repository.GetByStatusAsync(status);
            var dtos = logs.Select(l => MapToDto(l));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving logs by status {Status}", status);
            return StatusCode(500, "Error retrieving logs");
        }
    }

    /// <summary>
    /// Get failed notification logs
    /// </summary>
    [HttpGet("failed")]
    public async Task<ActionResult<IEnumerable<NotificationLogDto>>> GetFailed()
    {
        try
        {
            var logs = await _repository.GetFailedNotificationsAsync();
            var dtos = logs.Select(l => MapToDto(l));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving failed logs");
            return StatusCode(500, "Error retrieving logs");
        }
    }

    /// <summary>
    /// Get pending notification logs
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<NotificationLogDto>>> GetPending()
    {
        try
        {
            var logs = await _repository.GetPendingNotificationsAsync();
            var dtos = logs.Select(l => MapToDto(l));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending logs");
            return StatusCode(500, "Error retrieving logs");
        }
    }

    private NotificationLogDto MapToDto(NotificationLog log)
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
            DateCreated = log.date_created
        };
    }
}
