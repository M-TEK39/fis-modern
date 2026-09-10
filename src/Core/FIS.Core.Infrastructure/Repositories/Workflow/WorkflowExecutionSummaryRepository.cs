using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class WorkflowExecutionSummaryRepository : IWorkflowExecutionSummaryRepository
{
    private readonly FisDbContext _context;

    public WorkflowExecutionSummaryRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowExecutionSummary?> GetByIdAsync(int summaryId)
    {
        return await _context
            .WorkflowExecutionSummaries.Include(s => s.Workflow)
            .Include(s => s.Status)
            .FirstOrDefaultAsync(s => s.SummaryID == summaryId && !s.is_deleted);
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetAllAsync()
    {
        return await _context
            .WorkflowExecutionSummaries.Where(s => !s.is_deleted)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetByWorkflowIdAsync(int workflowId)
    {
        return await _context
            .WorkflowExecutionSummaries.Where(s => s.WorkflowID == workflowId && !s.is_deleted)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetByStatusAsync(string status)
    {
        return await _context
            .WorkflowExecutionSummaries.Where(s => s.ExecutionStatus == status && !s.is_deleted)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetActiveExecutionsAsync()
    {
        return await _context
            .WorkflowExecutionSummaries.Where(s => s.ExecutionStatus == "Running" && !s.is_deleted)
            .OrderBy(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetRecentExecutionsAsync(int count)
    {
        return await _context
            .WorkflowExecutionSummaries.Where(s => !s.is_deleted)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<WorkflowExecutionSummary> CreateAsync(WorkflowExecutionSummary summary)
    {
        summary.date_created = DateTime.UtcNow;
        summary.is_deleted = false;

        _context.WorkflowExecutionSummaries.Add(summary);
        await _context.SaveChangesAsync();

        return summary;
    }

    public async Task UpdateAsync(WorkflowExecutionSummary summary)
    {
        var existing = await _context.WorkflowExecutionSummaries.FirstOrDefaultAsync(s =>
            s.SummaryID == summary.SummaryID
        );

        if (existing == null)
            throw new InvalidOperationException(
                $"WorkflowExecutionSummary {summary.SummaryID} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(summary);
        existing.date_updated = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
