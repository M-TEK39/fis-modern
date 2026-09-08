using FIS.Core.Domain.Entities.System;

namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Service for managing workflow templates
/// </summary>
public interface IWorkflowTemplateService
{
    /// <summary>
    /// Creates a new workflow template
    /// </summary>
    Task<WorkflowTemplate> CreateTemplateAsync(
        string templateName,
        string category,
        string? description,
        int userId
    );

    /// <summary>
    /// Instantiates a workflow from a template
    /// </summary>
    Task<Domain.Entities.System.Workflow> InstantiateFromTemplateAsync(
        int templateId,
        string workflowName,
        int userId
    );

    /// <summary>
    /// Gets all templates in a category
    /// </summary>
    Task<List<WorkflowTemplate>> GetTemplatesByCategoryAsync(string category);

    /// <summary>
    /// Gets all active templates
    /// </summary>
    Task<List<WorkflowTemplate>> GetActiveTemplatesAsync();

    /// <summary>
    /// Gets a template by ID
    /// </summary>
    Task<WorkflowTemplate?> GetTemplateByIdAsync(int templateId);

    /// <summary>
    /// Updates a template
    /// </summary>
    Task UpdateTemplateAsync(WorkflowTemplate template, int userId);

    /// <summary>
    /// Exports template as JSON
    /// </summary>
    Task<string> ExportTemplateAsync(int templateId);

    /// <summary>
    /// Imports template from JSON
    /// </summary>
    Task<WorkflowTemplate> ImportTemplateAsync(string templateJson, int userId);
}
