using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Workflow.NotificationTemplate is not in archive Workflow Setup.
/// </summary>
public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly FisDbContext _context;

    public NotificationTemplateRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplate?> GetByIdAsync(int templateId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            return null;
        }

        return await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateID == templateId
        );
    }

    public async Task<NotificationTemplate?> GetByNameAsync(string templateName)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            return null;
        }

        return await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateName == templateName
        );
    }

    public async Task<IEnumerable<NotificationTemplate>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            return [];
        }

        return await _context.NotificationTemplates.ToListAsync();
    }

    public async Task<IEnumerable<NotificationTemplate>> GetActiveTemplatesAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            return [];
        }

        return await _context.NotificationTemplates.Where(t => t.IsActive).ToListAsync();
    }

    public async Task<IEnumerable<NotificationTemplate>> GetByTypeAsync(string templateType)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            return [];
        }

        return await _context
            .NotificationTemplates.Where(t => t.TemplateType == templateType)
            .ToListAsync();
    }

    public async Task<NotificationTemplate> CreateAsync(
        NotificationTemplate template,
        int currentUserId
    )
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("NotificationTemplate")
            );
        }

        _context.NotificationTemplates.Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task UpdateAsync(NotificationTemplate template, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("NotificationTemplate")
            );
        }

        var existing = await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateID == template.TemplateID
        );

        if (existing == null)
            throw new InvalidOperationException(
                $"NotificationTemplate {template.TemplateID} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(template);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int templateId, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationTemplate"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("NotificationTemplate")
            );
        }

        var template = await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateID == templateId
        );

        if (template != null)
        {
            _context.NotificationTemplates.Remove(template);
            await _context.SaveChangesAsync();
        }
    }
}
