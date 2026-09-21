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
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            return null;
        }

        return await _context
            .WorkflowExecutionSummaries.Include(s => s.Workflow)
            .Include(s => s.Status)
            .FirstOrDefaultAsync(s => s.SummaryID == summaryId);
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            return [];
        }

        return await _context
            .WorkflowExecutionSummaries.OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetByWorkflowIdAsync(int workflowId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            return [];
        }

        return await _context
            .WorkflowExecutionSummaries.Where(s => s.WorkflowID == workflowId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetByStatusAsync(string status)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            return [];
        }

        return await _context
            .WorkflowExecutionSummaries.Where(s => s.ExecutionStatus == status)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetActiveExecutionsAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            return [];
        }

        return await _context
            .WorkflowExecutionSummaries.Where(s => s.ExecutionStatus == "Running")
            .OrderBy(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowExecutionSummary>> GetRecentExecutionsAsync(int count)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            return [];
        }

        return await _context
            .WorkflowExecutionSummaries.OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<WorkflowExecutionSummary> CreateAsync(WorkflowExecutionSummary summary)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowExecutionSummary")
            );
        }

        _context.WorkflowExecutionSummaries.Add(summary);
        await _context.SaveChangesAsync();

        return summary;
    }

    public async Task UpdateAsync(WorkflowExecutionSummary summary)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowExecutionSummary"))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("WorkflowExecutionSummary")
            );
        }

        var existing = await _context.WorkflowExecutionSummaries.FirstOrDefaultAsync(s =>
            s.SummaryID == summary.SummaryID
        );

        if (existing == null)
            throw new InvalidOperationException(
                $"WorkflowExecutionSummary {summary.SummaryID} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(summary);
        await _context.SaveChangesAsync();
    }
}
