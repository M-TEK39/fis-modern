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
        return await _context
            .Set<WorkflowTemplate>()
            .FirstOrDefaultAsync(t => t.TemplateID == templateId && !t.is_deleted);
    }

    public async Task<IEnumerable<WorkflowTemplate>> GetAllAsync()
    {
        return await _context
            .Set<WorkflowTemplate>()
            .Where(t => !t.is_deleted)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowTemplate>> GetActiveTemplatesAsync()
    {
        return await _context
            .Set<WorkflowTemplate>()
            .Where(t => !t.is_deleted && t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowTemplate>> GetByCategoryAsync(string category)
    {
        return await _context
            .Set<WorkflowTemplate>()
            .Where(t => !t.is_deleted && t.IsActive && t.Category == category)
            .OrderBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, int currentUserId)
    {
        template.date_created = DateTime.Now;
        template.created_by_user_code = currentUserId;
        template.is_deleted = false;

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
        var existingTemplate = await _context
            .Set<WorkflowTemplate>()
            .FirstOrDefaultAsync(t => t.TemplateID == template.TemplateID);

        if (existingTemplate == null)
        {
            throw new InvalidOperationException($"Template {template.TemplateID} not found");
        }

        // Update using CurrentValues.SetValues for tracking-safe updates
        _context.Entry(existingTemplate).CurrentValues.SetValues(template);
        existingTemplate.date_updated = DateTime.Now;
        existingTemplate.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated workflow template {TemplateId}: {TemplateName}",
            template.TemplateID,
            template.TemplateName
        );
    }

    public async Task DeleteAsync(int templateId, int currentUserId)
    {
        var template = await _context
            .Set<WorkflowTemplate>()
            .FirstOrDefaultAsync(t => t.TemplateID == templateId);

        if (template == null)
        {
            throw new InvalidOperationException($"Template {templateId} not found");
        }

        // Soft delete
        template.is_deleted = true;
        template.date_updated = DateTime.Now;
        template.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deleted (soft) workflow template {TemplateId}: {TemplateName}",
            templateId,
            template.TemplateName
        );
    }
}
