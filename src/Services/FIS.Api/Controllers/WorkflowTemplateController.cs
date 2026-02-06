using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Workflow Template Controller - Manages reusable workflow templates
/// </summary>
[ApiController]
[Route("api/workflow-template")]
[Authorize]
public class WorkflowTemplateController : BaseApiController
{
    private readonly IWorkflowTemplateService _templateService;
    private readonly ILogger<WorkflowTemplateController> _logger;

    public WorkflowTemplateController(
        IWorkflowTemplateService templateService,
        ILogger<WorkflowTemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Get all workflow templates
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkflowTemplateDto>>> GetAll()
    {
        try
        {
            var templates = await _templateService.GetActiveTemplatesAsync();
            return Ok(templates.Select(t => MapToDto(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow templates");
            return StatusCode(500, new { error = "Error retrieving templates", message = ex.Message });
        }
    }

    /// <summary>
    /// Get workflow template by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WorkflowTemplateDto>> GetById(int id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null)
                return NotFound(new { error = "Template not found" });

            return Ok(MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow template {TemplateId}", id);
            return StatusCode(500, new { error = "Error retrieving template", message = ex.Message });
        }
    }

    /// <summary>
    /// Get templates by category
    /// </summary>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<WorkflowTemplateDto>>> GetByCategory(string category)
    {
        try
        {
            var templates = await _templateService.GetTemplatesByCategoryAsync(category);
            return Ok(templates.Select(t => MapToDto(t)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates for category {Category}", category);
            return StatusCode(500, new { error = "Error retrieving templates", message = ex.Message });
        }
    }

    /// <summary>
    /// Create new workflow template
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WorkflowTemplateDto>> Create([FromBody] CreateWorkflowTemplateDto request)
    {
        try
        {
            var template = await _templateService.CreateTemplateAsync(
                request.TemplateName,
                request.Category,
                request.Description,
                GetCurrentUserId());

            _logger.LogInformation("Created workflow template {TemplateId}: {TemplateName}", 
                template.TemplateID, template.TemplateName);

            return CreatedAtAction(nameof(GetById), new { id = template.TemplateID }, MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow template");
            return StatusCode(500, new { error = "Error creating template", message = ex.Message });
        }
    }

    /// <summary>
    /// Update workflow template
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateWorkflowTemplateDto request)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null)
                return NotFound(new { error = "Template not found" });

            template.TemplateName = request.TemplateName;
            template.Category = request.Category;
            template.Description = request.Description;
            template.IsActive = request.IsActive;
            template.TemplateData = request.TemplateData;

            await _templateService.UpdateTemplateAsync(template, GetCurrentUserId());

            _logger.LogInformation("Updated workflow template {TemplateId}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow template {TemplateId}", id);
            return StatusCode(500, new { error = "Error updating template", message = ex.Message });
        }
    }

    /// <summary>
    /// Instantiate workflow from template
    /// </summary>
    [HttpPost("{id}/instantiate")]
    public async Task<ActionResult<WorkflowFromTemplateDto>> Instantiate(int id, [FromBody] InstantiateWorkflowDto request)
    {
        try
        {
            var workflow = await _templateService.InstantiateFromTemplateAsync(
                id,
                request.WorkflowName,
                GetCurrentUserId());

            _logger.LogInformation("Instantiated workflow {WorkflowId} from template {TemplateId}", 
                workflow.WorkflowID, id);

            return Ok(new WorkflowFromTemplateDto
            {
                WorkflowID = workflow.WorkflowID,
                WorkflowName = workflow.WorkflowName,
                Message = $"Workflow '{workflow.WorkflowName}' created from template"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error instantiating workflow from template {TemplateId}", id);
            return StatusCode(500, new { error = "Error instantiating workflow", message = ex.Message });
        }
    }

    /// <summary>
    /// Export template as JSON
    /// </summary>
    [HttpGet("{id}/export")]
    public async Task<ActionResult<string>> Export(int id)
    {
        try
        {
            var json = await _templateService.ExportTemplateAsync(id);
            return Ok(new { templateJson = json });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting template {TemplateId}", id);
            return StatusCode(500, new { error = "Error exporting template", message = ex.Message });
        }
    }

    /// <summary>
    /// Import template from JSON
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<WorkflowTemplateDto>> Import([FromBody] ImportTemplateDto request)
    {
        try
        {
            var template = await _templateService.ImportTemplateAsync(request.TemplateJson, GetCurrentUserId());

            _logger.LogInformation("Imported workflow template {TemplateId}: {TemplateName}", 
                template.TemplateID, template.TemplateName);

            return CreatedAtAction(nameof(GetById), new { id = template.TemplateID }, MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing template");
            return StatusCode(500, new { error = "Error importing template", message = ex.Message });
        }
    }

    private WorkflowTemplateDto MapToDto(WorkflowTemplate template)
    {
        return new WorkflowTemplateDto
        {
            TemplateID = template.TemplateID,
            TemplateName = template.TemplateName,
            Category = template.Category,
            Description = template.Description,
            IsActive = template.IsActive,
            Version = template.Version,
            DateCreated = template.date_created,
            DateUpdated = template.date_updated
        };
    }
}

#region Workflow Template DTOs

public class WorkflowTemplateDto
{
    public int TemplateID { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public class CreateWorkflowTemplateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
}

public class UpdateWorkflowTemplateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? TemplateData { get; set; }
}

public class InstantiateWorkflowDto
{
    public string WorkflowName { get; set; } = string.Empty;
}

public class ImportTemplateDto
{
    public string TemplateJson { get; set; } = string.Empty;
}

public class WorkflowFromTemplateDto
{
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public string? Message { get; set; }
}

#endregion
