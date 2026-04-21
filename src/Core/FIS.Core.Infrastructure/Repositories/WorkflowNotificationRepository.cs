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
        return await _context.WorkflowNotifications
            .Include(n => n.Workflow)
            .Include(n => n.Step)
            .Include(n => n.NotificationTemplate)
            .FirstOrDefaultAsync(n => n.NotificationID == notificationId && !n.is_deleted);
    }

    public async Task<IEnumerable<WorkflowNotification>> GetAllAsync()
    {
        return await _context.WorkflowNotifications
            .Where(n => !n.is_deleted)
            .Include(n => n.Workflow)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetByWorkflowIdAsync(int workflowId)
    {
        return await _context.WorkflowNotifications
            .Where(n => n.WorkflowID == workflowId && !n.is_deleted)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetByStepIdAsync(int stepId)
    {
        return await _context.WorkflowNotifications
            .Where(n => n.StepID == stepId && !n.is_deleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetByEventTypeAsync(string eventType)
    {
        return await _context.WorkflowNotifications
            .Where(n => n.EventType == eventType && !n.is_deleted && n.IsActive)
            .Include(n => n.Workflow)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowNotification>> GetActiveNotificationsAsync()
    {
        return await _context.WorkflowNotifications
            .Where(n => n.IsActive && !n.is_deleted)
            .Include(n => n.Workflow)
            .Include(n => n.Step)
            .ToListAsync();
    }

    public async Task<WorkflowNotification> CreateAsync(WorkflowNotification notification, int currentUserId)
    {
        notification.date_created = DateTime.UtcNow;
        notification.created_by_user_code = currentUserId;
        notification.is_deleted = false;

        _context.WorkflowNotifications.Add(notification);
        await _context.SaveChangesAsync();

        return notification;
    }

    public async Task UpdateAsync(WorkflowNotification notification, int currentUserId)
    {
        var existing = await _context.WorkflowNotifications
            .FirstOrDefaultAsync(n => n.NotificationID == notification.NotificationID);

        if (existing == null)
            throw new InvalidOperationException($"WorkflowNotification {notification.NotificationID} not found");

        // Tracking-safe update pattern
        _context.Entry(existing).CurrentValues.SetValues(notification);
        existing.date_updated = DateTime.UtcNow;
        existing.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int notificationId, int currentUserId)
    {
        var notification = await _context.WorkflowNotifications
            .FirstOrDefaultAsync(n => n.NotificationID == notificationId);

        if (notification != null)
        {
            notification.is_deleted = true;
            notification.date_updated = DateTime.UtcNow;
            notification.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
