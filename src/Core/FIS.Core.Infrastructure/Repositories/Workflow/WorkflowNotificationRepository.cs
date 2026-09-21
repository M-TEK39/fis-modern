using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class WorkflowNotificationRepository : IWorkflowNotificationRepository
{
    private readonly FisDbContext _context;

    public WorkflowNotificationRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowNotification?> GetByIdAsync(int notificationId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            return null;
        }

        return await _context
            .WorkflowNotifications.Include(n => n.Workflow)
            .Include(n => n.Step)
            .Include(n => n.NotificationTemplate)
            .FirstOrDefaultAsync(n => n.NotificationID == notificationId);
    }

    public async Task<IEnumerable<WorkflowNotification>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            return [];
        }

        return await _context
            .WorkflowNotifications.Include(n => n.Workflow)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetByWorkflowIdAsync(int workflowId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            return [];
        }

        return await _context
            .WorkflowNotifications.Where(n => n.WorkflowID == workflowId)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetByStepIdAsync(int stepId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            return [];
        }

        return await _context
            .WorkflowNotifications.Where(n => n.StepID == stepId)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetByEventTypeAsync(string eventType)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            return [];
        }

        return await _context
            .WorkflowNotifications.Where(n =>
                n.EventType == eventType && n.IsActive
            )
            .Include(n => n.Workflow)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetActiveNotificationsAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            return [];
        }

        return await _context
            .WorkflowNotifications.Where(n => n.IsActive)
            .Include(n => n.Workflow)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<WorkflowNotification> CreateAsync(
        WorkflowNotification notification,
        int currentUserId
    )
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowNotification")
            );
        }

        _context.WorkflowNotifications.Add(notification);
        await _context.SaveChangesAsync();

        return notification;
    }

    public async Task UpdateAsync(WorkflowNotification notification, int currentUserId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowNotification")
            );
        }

        var existing = await _context.WorkflowNotifications.FirstOrDefaultAsync(n =>
            n.NotificationID == notification.NotificationID
        );

        if (existing == null)
            throw new InvalidOperationException(
                $"WorkflowNotification {notification.NotificationID} not found"
            );

        // Tracking-safe update pattern
        _context.Entry(existing).CurrentValues.SetValues(notification);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int notificationId, int currentUserId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowNotification"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowNotification")
            );
        }

        var notification = await _context.WorkflowNotifications.FirstOrDefaultAsync(n =>
            n.NotificationID == notificationId
        );

        if (notification != null)
        {
            _context.WorkflowNotifications.Remove(notification);
            await _context.SaveChangesAsync();
        }
    }
}
