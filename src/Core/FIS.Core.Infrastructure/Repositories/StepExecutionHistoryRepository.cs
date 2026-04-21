using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class StepExecutionHistoryRepository : IStepExecutionHistoryRepository
{
    private readonly FisDbContext _context;

    public StepExecutionHistoryRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<StepExecutionHistory?> GetByIdAsync(int executionHistoryId)
    {
        return await _context.StepExecutionHistories
            .Include(h => h.Step)
            .Include(h => h.Workflow)
            .FirstOrDefaultAsync(h => h.ExecutionHistoryID == executionHistoryId && !h.is_deleted);
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetAllAsync()
    {
        return await _context.StepExecutionHistories
            .Where(h => !h.is_deleted)
            .OrderByDescending(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetByWorkflowIdAsync(int workflowId)
    {
        return await _context.StepExecutionHistories
            .Where(h => h.WorkflowID == workflowId && !h.is_deleted)
            .OrderBy(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetByStepIdAsync(int stepId)
    {
        return await _context.StepExecutionHistories
            .Where(h => h.StepID == stepId && !h.is_deleted)
            .OrderByDescending(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetByStatusAsync(string status)
    {
        return await _context.StepExecutionHistories
            .Where(h => h.ExecutionStatus == status && !h.is_deleted)
            .OrderByDescending(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetRecentExecutionsAsync(int count)
    {
        return await _context.StepExecutionHistories
            .Where(h => !h.is_deleted)
            .OrderByDescending(h => h.StartedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<StepExecutionHistory> CreateAsync(StepExecutionHistory history)
    {
        history.date_created = DateTime.UtcNow;
        history.is_deleted = false;

        _context.StepExecutionHistories.Add(history);
        await _context.SaveChangesAsync();

        return history;
    }

    public async Task UpdateAsync(StepExecutionHistory history)
    {
        var existing = await _context.StepExecutionHistories
            .FirstOrDefaultAsync(h => h.ExecutionHistoryID == history.ExecutionHistoryID);

        if (existing == null)
            throw new InvalidOperationException($"StepExecutionHistory {history.ExecutionHistoryID} not found");

        _context.Entry(existing).CurrentValues.SetValues(history);
        await _context.SaveChangesAsync();
    }
}
