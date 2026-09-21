using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Workflow.NotificationLog is not in archive Workflow Setup.
/// </summary>
public class NotificationLogRepository : INotificationLogRepository
{
    private readonly FisDbContext _context;

    public NotificationLogRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationLog?> GetByIdAsync(int logId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            return null;
        }

        return await _context.NotificationLogs.FirstOrDefaultAsync(l => l.LogID == logId);
    }

    public async Task<IEnumerable<NotificationLog>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            return [];
        }

        return await _context.NotificationLogs.OrderByDescending(l => l.LogID).ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetByWorkflowIdAsync(int workflowId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            return [];
        }

        return await _context
            .NotificationLogs.Where(l => l.WorkflowID == workflowId)
            .OrderByDescending(l => l.LogID)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetByStatusAsync(string status)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            return [];
        }

        return await _context
            .NotificationLogs.Where(l => l.DeliveryStatus == status)
            .OrderByDescending(l => l.LogID)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetFailedNotificationsAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            return [];
        }

        return await _context
            .NotificationLogs.Where(l => l.DeliveryStatus == "Failed")
            .OrderByDescending(l => l.LogID)
            .ToListAsync();
    }

    public async Task<IEnumerable<NotificationLog>> GetPendingNotificationsAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            return [];
        }

        return await _context
            .NotificationLogs.Where(l => l.DeliveryStatus == "Pending")
            .OrderBy(l => l.LogID)
            .ToListAsync();
    }

    public async Task<NotificationLog> CreateAsync(NotificationLog log)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("NotificationLog")
            );
        }

        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync();

        return log;
    }

    public async Task UpdateAsync(NotificationLog log)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("NotificationLog")
            );
        }

        var existing = await _context.NotificationLogs.FirstOrDefaultAsync(l =>
            l.LogID == log.LogID
        );

        if (existing == null)
            throw new InvalidOperationException($"NotificationLog {log.LogID} not found");

        _context.Entry(existing).CurrentValues.SetValues(log);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int logId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "NotificationLog"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("NotificationLog")
            );
        }

        var log = await _context.NotificationLogs.FirstOrDefaultAsync(l => l.LogID == logId);

        if (log != null)
        {
            _context.NotificationLogs.Remove(log);
            await _context.SaveChangesAsync();
        }
    }
}
