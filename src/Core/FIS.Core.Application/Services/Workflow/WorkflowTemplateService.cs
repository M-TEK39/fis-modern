using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Service for managing workflow templates
/// </summary>
public class WorkflowTemplateService : IWorkflowTemplateService
{
    private readonly IWorkflowTemplateRepository _templateRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IStepRepository _stepRepository;
    private readonly ILogger<WorkflowTemplateService> _logger;

    public WorkflowTemplateService(
        IWorkflowTemplateRepository templateRepository,
        IWorkflowRepository workflowRepository,
        IStepRepository stepRepository,
        ILogger<WorkflowTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _workflowRepository = workflowRepository;
        _stepRepository = stepRepository;
        _logger = logger;
    }

    public async Task<WorkflowTemplate> CreateTemplateAsync(string templateName, string category, string? description, int userId)
    {
        var template = new WorkflowTemplate
        {
            TemplateName = templateName,
            Category = category,
            Description = description,
            IsActive = true,
            Version = 1
        };

        return await _templateRepository.CreateAsync(template, userId);
    }

    public async Task<Domain.Entities.System.Workflow> InstantiateFromTemplateAsync(int templateId, string workflowName, int userId)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Template {templateId} is not active");
        }

        // Create new workflow from template
        var workflow = new Domain.Entities.System.Workflow
        {
            WorkflowName = workflowName,
            AlwaysExecute = false
        };

        workflow = await _workflowRepository.CreateAsync(workflow, userId);

        _logger.LogInformation("Instantiated workflow {WorkflowId} from template {TemplateId}", 
            workflow.WorkflowID, templateId);

        // If template has step data, create steps
        if (!string.IsNullOrWhiteSpace(template.TemplateData))
        {
            await CreateStepsFromTemplateData(workflow.WorkflowID, template.TemplateData, userId);
        }

        return workflow;
    }

    public async Task<List<WorkflowTemplate>> GetTemplatesByCategoryAsync(string category)
    {
        var templates = await _templateRepository.GetByCategoryAsync(category);
        return templates.ToList();
    }

    public async Task<List<WorkflowTemplate>> GetActiveTemplatesAsync()
    {
        var templates = await _templateRepository.GetActiveTemplatesAsync();
        return templates.ToList();
    }

    public async Task<WorkflowTemplate?> GetTemplateByIdAsync(int templateId)
    {
        return await _templateRepository.GetByIdAsync(templateId);
    }

    public async Task UpdateTemplateAsync(WorkflowTemplate template, int userId)
    {
        await _templateRepository.UpdateAsync(template, userId);
    }

    public async Task<string> ExportTemplateAsync(int templateId)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var exportData = new
        {
            template.TemplateName,
            template.Category,
            template.Description,
            template.IsActive,
            template.Version,
            template.TemplateData
        };

        return JsonSerializer.Serialize(exportData, options);
    }

    public async Task<WorkflowTemplate> ImportTemplateAsync(string templateJson, int userId)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var importData = JsonSerializer.Deserialize<WorkflowTemplateImportData>(templateJson, options);
        if (importData == null)
        {
            throw new InvalidOperationException("Invalid template JSON");
        }

        var template = new WorkflowTemplate
        {
            TemplateName = importData.TemplateName ?? "Imported Template",
            Category = importData.Category ?? "General",
            Description = importData.Description,
            IsActive = importData.IsActive,
            Version = importData.Version,
            TemplateData = importData.TemplateData
        };

        return await _templateRepository.CreateAsync(template, userId);
    }

    private async Task CreateStepsFromTemplateData(int workflowId, string templateData, int userId)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var steps = JsonSerializer.Deserialize<List<StepTemplateData>>(templateData, options);
            if (steps == null || !steps.Any())
            {
                return;
            }

            foreach (var stepData in steps.OrderBy(s => s.Order))
            {
                var step = new Step
                {
                    WorkflowID = workflowId,
                    StepName = stepData.Name,
                    StepOrder = stepData.Order,
                    StepTypeID = stepData.TypeId,
                    HandlerType = stepData.HandlerType,
                    StepParameters = stepData.Parameters
                };

                await _stepRepository.CreateAsync(step, userId);
            }

            _logger.LogInformation("Created {Count} steps for workflow {WorkflowId} from template data", 
                steps.Count, workflowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating steps from template data for workflow {WorkflowId}", workflowId);
            throw;
        }
    }
}

/// <summary>
/// Data structure for template import
/// </summary>
public class WorkflowTemplateImportData
{
    public string? TemplateName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public string? TemplateData { get; set; }
}

/// <summary>
/// Data structure for step template
/// </summary>
public class StepTemplateData
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public int TypeId { get; set; }
    public string? HandlerType { get; set; }
    public string? Parameters { get; set; }
}
