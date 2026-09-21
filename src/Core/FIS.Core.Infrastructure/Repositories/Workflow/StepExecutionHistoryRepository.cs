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
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            return null;
        }

        return await _context
            .StepExecutionHistories.Include(h => h.Step)
            .Include(h => h.Workflow)
            .FirstOrDefaultAsync(h => h.ExecutionHistoryID == executionHistoryId);
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            return [];
        }

        return await _context
            .StepExecutionHistories.OrderByDescending(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetByWorkflowIdAsync(int workflowId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            return [];
        }

        return await _context
            .StepExecutionHistories.Where(h => h.WorkflowID == workflowId)
            .OrderBy(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetByStepIdAsync(int stepId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            return [];
        }

        return await _context
            .StepExecutionHistories.Where(h => h.StepID == stepId)
            .OrderByDescending(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetByStatusAsync(string status)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            return [];
        }

        return await _context
            .StepExecutionHistories.Where(h => h.ExecutionStatus == status)
            .OrderByDescending(h => h.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StepExecutionHistory>> GetRecentExecutionsAsync(int count)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            return [];
        }

        return await _context
            .StepExecutionHistories.OrderByDescending(h => h.StartedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<StepExecutionHistory> CreateAsync(StepExecutionHistory history)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("StepExecutionHistory")
            );
        }

        _context.StepExecutionHistories.Add(history);
        await _context.SaveChangesAsync();

        return history;
    }

    public async Task UpdateAsync(StepExecutionHistory history)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "StepExecutionHistory"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("StepExecutionHistory")
            );
        }

        var existing = await _context.StepExecutionHistories.FirstOrDefaultAsync(h =>
            h.ExecutionHistoryID == history.ExecutionHistoryID
        );

        if (existing == null)
            throw new InvalidOperationException(
                $"StepExecutionHistory {history.ExecutionHistoryID} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(history);
        await _context.SaveChangesAsync();
    }
}
