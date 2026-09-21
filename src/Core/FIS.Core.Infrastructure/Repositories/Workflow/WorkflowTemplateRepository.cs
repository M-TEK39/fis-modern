using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for workflow template operations
/// </summary>
public class WorkflowTemplateRepository : IWorkflowTemplateRepository
{
    private readonly FisDbContext _context;
    private readonly ILogger<WorkflowTemplateRepository> _logger;

    public WorkflowTemplateRepository(
        FisDbContext context,
        ILogger<WorkflowTemplateRepository> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task<WorkflowTemplate?> GetByIdAsync(int templateId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            return null;
        }

        return await _context
            .Set<WorkflowTemplate>()
            .FirstOrDefaultAsync(t => t.TemplateID == templateId);
    }

    public async Task<IEnumerable<WorkflowTemplate>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            return [];
        }

        return await _context
            .Set<WorkflowTemplate>()
            .OrderBy(t => t.Category)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowTemplate>> GetActiveTemplatesAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            return [];
        }

        return await _context
            .Set<WorkflowTemplate>()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowTemplate>> GetByCategoryAsync(string category)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            return [];
        }

        return await _context
            .Set<WorkflowTemplate>()
            .Where(t => t.IsActive && t.Category == category)
            .OrderBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowTemplate")
            );
        }

        _context.Set<WorkflowTemplate>().Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created workflow template {TemplateId}: {TemplateName}",
            template.TemplateID,
            template.TemplateName
        );

        return template;
    }

    public async Task UpdateAsync(WorkflowTemplate template, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowTemplate")
            );
        }

        var existingTemplate = await _context
            .Set<WorkflowTemplate>()
            .FirstOrDefaultAsync(t => t.TemplateID == template.TemplateID);

        if (existingTemplate == null)
        {
            throw new InvalidOperationException($"Template {template.TemplateID} not found");
        }

        // Update using CurrentValues.SetValues for tracking-safe updates
        _context.Entry(existingTemplate).CurrentValues.SetValues(template);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated workflow template {TemplateId}: {TemplateName}",
            template.TemplateID,
            template.TemplateName
        );
    }

    public async Task DeleteAsync(int templateId, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowTemplate"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowTemplate")
            );
        }

        var template = await _context
            .Set<WorkflowTemplate>()
            .FirstOrDefaultAsync(t => t.TemplateID == templateId);

        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        _context.Set<WorkflowTemplate>().Remove(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deleted workflow template {TemplateId}: {TemplateName}",
            templateId,
            template.TemplateName
        );
    }
}
