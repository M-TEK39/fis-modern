using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class StatusRepository : IStatusRepository
{
    private readonly FisDbContext _context;

    public StatusRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Status?> GetByIdAsync(int statusId)
    {
        return await _context.Statuses.FirstOrDefaultAsync(s =>
            s.StatusID == statusId && !s.is_deleted
        );
    }

    public async Task<IEnumerable<Status>> GetAllAsync()
    {
        return await _context
            .Statuses.Where(s => !s.is_deleted)
            .OrderByDescending(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Status>> GetByStepIdAsync(int stepId)
    {
        return await _context
            .Statuses.Where(s => s.StepID == stepId && !s.is_deleted)
            .OrderByDescending(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Status>> GetActiveStatusesAsync()
    {
        return await _context
            .Statuses.Where(s => !s.is_deleted && s.IsBusy && s.DateCompleted == null)
            .OrderBy(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Status>> GetByUserAsync(string userName)
    {
        return await _context
            .Statuses.Where(s => !s.is_deleted && s.StartedByUserName == userName)
            .OrderByDescending(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<Status> CreateAsync(Status status, int currentUserId)
    {
        status.date_created = DateTime.Now;
        status.created_by_user_code = currentUserId;
        status.is_deleted = false;

        await _context.Statuses.AddAsync(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task UpdateAsync(Status status, int currentUserId)
    {
        if (status == null)
            throw new ArgumentNullException(nameof(status));

        var existing = await _context.Statuses.FindAsync(status.StatusID);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"Status with ID {status.StatusID} not found");

        status.date_updated = DateTime.Now;
        status.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(status);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int statusId, int currentUserId)
    {
        var status = await _context.Statuses.FindAsync(statusId);
        if (status != null && !status.is_deleted)
        {
            status.is_deleted = true;
            status.date_updated = DateTime.Now;
            status.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
