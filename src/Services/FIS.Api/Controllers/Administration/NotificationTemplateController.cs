using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/notification-template")]
[Authorize(Roles = "User Administration")]
public class NotificationTemplateController : BaseApiController
{
    private readonly INotificationTemplateRepository _repository;
    private readonly ILogger<NotificationTemplateController> _logger;

    public NotificationTemplateController(
        INotificationTemplateRepository repository,
        ILogger<NotificationTemplateController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all notification templates
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationTemplateDto>>> GetAll()
    {
        try
        {
            var templates = await _repository.GetAllAsync();
            var dtos = templates.Select(t => MapToDto(t));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification templates");
            return StatusCode(500, "Error retrieving templates");
        }
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationTemplateDto>> GetById(int id)
    {
        try
        {
            var template = await _repository.GetByIdAsync(id);
            return template == null ? NotFound() : Ok(MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {Id}", id);
            return StatusCode(500, "Error retrieving template");
        }
    }

    /// <summary>
    /// Get template by name
    /// </summary>
    [HttpGet("name/{name}")]
    public async Task<ActionResult<NotificationTemplateDto>> GetByName(string name)
    {
        try
        {
            var template = await _repository.GetByNameAsync(name);
            return template == null ? NotFound() : Ok(MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template by name {Name}", name);
            return StatusCode(500, "Error retrieving template");
        }
    }

    /// <summary>
    /// Get active templates
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<NotificationTemplateDto>>> GetActive()
    {
        try
        {
            var templates = await _repository.GetActiveTemplatesAsync();
            var dtos = templates.Select(t => MapToDto(t));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active templates");
            return StatusCode(500, "Error retrieving templates");
        }
    }

    /// <summary>
    /// Get templates by type
    /// </summary>
    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<NotificationTemplateDto>>> GetByType(string type)
    {
        try
        {
            var templates = await _repository.GetByTypeAsync(type);
            var dtos = templates.Select(t => MapToDto(t));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates by type {Type}", type);
            return StatusCode(500, "Error retrieving templates");
        }
    }

    /// <summary>
    /// Create a new notification template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<NotificationTemplateDto>> Create(
        [FromBody] CreateNotificationTemplateDto dto
    )
    {
        try
        {
            var template = new NotificationTemplate
            {
                TemplateName = dto.TemplateName,
                Description = dto.Description,
                Subject = dto.Subject,
                Body = dto.Body,
                TemplateType = dto.TemplateType,
                Variables = dto.Variables,
                IsActive = dto.IsActive,
            };

            var created = await _repository.CreateAsync(template, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.TemplateID },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification template");
            return StatusCode(500, "Error creating template");
        }
    }

    /// <summary>
    /// Update a notification template
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateNotificationTemplateDto dto)
    {
        try
        {
            if (id != dto.TemplateID)
                return BadRequest("ID mismatch");

            var template = new NotificationTemplate
            {
                TemplateID = dto.TemplateID,
                TemplateName = dto.TemplateName,
                Description = dto.Description,
                Subject = dto.Subject,
                Body = dto.Body,
                TemplateType = dto.TemplateType,
                Variables = dto.Variables,
                IsActive = dto.IsActive,
            };

            await _repository.UpdateAsync(template, GetCurrentUserId());
            return Ok(new { message = "Template updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {Id}", id);
            return StatusCode(500, "Error updating template");
        }
    }

    /// <summary>
    /// Delete a notification template
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
            _logger.LogError(ex, "Error deleting template {Id}", id);
            return StatusCode(500, "Error deleting template");
        }
    }

    private NotificationTemplateDto MapToDto(NotificationTemplate template)
    {
        return new NotificationTemplateDto
        {
            TemplateID = template.TemplateID,
            TemplateName = template.TemplateName,
            Description = template.Description,
            Subject = template.Subject,
            Body = template.Body,
            TemplateType = template.TemplateType,
            Variables = template.Variables,
            IsActive = template.IsActive,
            DateCreated = template.date_created,
            DateUpdated = template.date_updated,
        };
    }
}

#region NotificationTemplate DTOs

public class NotificationTemplateDto
{
    public int TemplateID { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string? Variables { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateNotificationTemplateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "Email";
    public string? Variables { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateNotificationTemplateDto
{
    public int TemplateID { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string? Variables { get; set; }
    public bool IsActive { get; set; }
}

#endregion
