using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly FisDbContext _context;

    public NotificationTemplateRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplate?> GetByIdAsync(int templateId)
    {
        return await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateID == templateId && !t.is_deleted
        );
    }

    public async Task<NotificationTemplate?> GetByNameAsync(string templateName)
    {
        return await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateName == templateName && !t.is_deleted
        );
    }

    public async Task<IEnumerable<NotificationTemplate>> GetAllAsync()
    {
        return await _context.NotificationTemplates.Where(t => !t.is_deleted).ToListAsync();
    }

    public async Task<IEnumerable<NotificationTemplate>> GetActiveTemplatesAsync()
    {
        return await _context
            .NotificationTemplates.Where(t => t.IsActive && !t.is_deleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationTemplate>> GetByTypeAsync(string templateType)
    {
        return await _context
            .NotificationTemplates.Where(t => t.TemplateType == templateType && !t.is_deleted)
            .ToListAsync();
    }

    public async Task<NotificationTemplate> CreateAsync(
        NotificationTemplate template,
        int currentUserId
    )
    {
        template.date_created = DateTime.UtcNow;
        template.created_by_user_code = currentUserId;
        template.is_deleted = false;

        _context.NotificationTemplates.Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task UpdateAsync(NotificationTemplate template, int currentUserId)
    {
        var existing = await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateID == template.TemplateID
        );

        if (existing == null)
            throw new InvalidOperationException(
                $"NotificationTemplate {template.TemplateID} not found"
            );

        // Tracking-safe update pattern
        _context.Entry(existing).CurrentValues.SetValues(template);
        existing.date_updated = DateTime.UtcNow;
        existing.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int templateId, int currentUserId)
    {
        var template = await _context.NotificationTemplates.FirstOrDefaultAsync(t =>
            t.TemplateID == templateId
        );

        if (template != null)
        {
            template.is_deleted = true;
            template.date_updated = DateTime.UtcNow;
            template.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
