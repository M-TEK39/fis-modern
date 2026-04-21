using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class NotificationLogRepository : INotificationLogRepository
{
    private readonly FisDbContext _context;

    public NotificationLogRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationLog?> GetByIdAsync(int logId)
    {
        return await _context.NotificationLogs
            .Include(l => l.Notification)
            .Include(l => l.Workflow)
            .Include(l => l.Step)
            .FirstOrDefaultAsync(l => l.LogID == logId && !l.is_deleted);
    }

    public async Task<IEnumerable<NotificationLog>> GetAllAsync()
    {
        return await _context.NotificationLogs
            .Where(l => !l.is_deleted)
            .OrderByDescending(l => l.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetByWorkflowIdAsync(int workflowId)
    {
        return await _context.NotificationLogs
            .Where(l => l.WorkflowID == workflowId && !l.is_deleted)
            .OrderByDescending(l => l.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetByStatusAsync(string status)
    {
        return await _context.NotificationLogs
            .Where(l => l.DeliveryStatus == status && !l.is_deleted)
            .OrderByDescending(l => l.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetFailedNotificationsAsync()
    {
        return await _context.NotificationLogs
            .Where(l => l.DeliveryStatus == "Failed" && !l.is_deleted)
            .OrderByDescending(l => l.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetPendingNotificationsAsync()
    {
        return await _context.NotificationLogs
            .Where(l => l.DeliveryStatus == "Pending" && !l.is_deleted)
            .OrderBy(l => l.date_created)
            .ToListAsync();
    }

    public async Task<NotificationLog> CreateAsync(NotificationLog log)
    {
        log.date_created = DateTime.UtcNow;
        log.is_deleted = false;

        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync();

        return log;
    }

    public async Task UpdateAsync(NotificationLog log)
    {
        var existing = await _context.NotificationLogs
            .FirstOrDefaultAsync(l => l.LogID == log.LogID);

        if (existing == null)
            throw new InvalidOperationException($"NotificationLog {log.LogID} not found");

        // Tracking-safe update pattern
        _context.Entry(existing).CurrentValues.SetValues(log);
        existing.date_updated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int logId)
    {
        var log = await _context.NotificationLogs
            .FirstOrDefaultAsync(l => l.LogID == logId);

        if (log != null)
        {
            log.is_deleted = true;
            log.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
