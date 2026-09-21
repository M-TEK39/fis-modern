using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class WorkflowMetricRepository : IWorkflowMetricRepository
{
    private readonly FisDbContext _context;

    public WorkflowMetricRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowMetric?> GetByIdAsync(int metricId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            return null;
        }

        return await _context
            .WorkflowMetrics.Include(m => m.Workflow)
            .Include(m => m.BottleneckStep)
            .FirstOrDefaultAsync(m => m.MetricID == metricId);
    }

    public async Task<IEnumerable<WorkflowMetric>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            return [];
        }

        return await _context
            .WorkflowMetrics.OrderByDescending(m => m.MetricDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowMetric>> GetByWorkflowIdAsync(int workflowId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            return [];
        }

        return await _context
            .WorkflowMetrics.Where(m => m.WorkflowID == workflowId)
            .OrderByDescending(m => m.MetricDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowMetric>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            return [];
        }

        return await _context
            .WorkflowMetrics.Where(m =>
                m.MetricDate >= startDate && m.MetricDate <= endDate
            )
            .OrderBy(m => m.MetricDate)
            .ToListAsync();
    }

    public async Task<WorkflowMetric?> GetLatestMetricAsync(int workflowId)
    {
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            return null;
        }

        return await _context
            .WorkflowMetrics.Where(m => m.WorkflowID == workflowId)
            .OrderByDescending(m => m.MetricDate)
            .FirstOrDefaultAsync();
    }

    public async Task<WorkflowMetric> CreateAsync(WorkflowMetric metric, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            throw new InvalidOperationException(WorkflowOptionalTable.MissingMessage("WorkflowMetric"));
        }

        _context.WorkflowMetrics.Add(metric);
        await _context.SaveChangesAsync();

        return metric;
    }

    public async Task UpdateAsync(WorkflowMetric metric, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsAsync(_context, "WorkflowMetric"))
        {
            throw new InvalidOperationException(WorkflowOptionalTable.MissingMessage("WorkflowMetric"));
        }

        var existing = await _context.WorkflowMetrics.FirstOrDefaultAsync(m =>
            m.MetricID == metric.MetricID
        );

        if (existing == null)
            throw new InvalidOperationException($"WorkflowMetric {metric.MetricID} not found");

        _context.Entry(existing).CurrentValues.SetValues(metric);
        await _context.SaveChangesAsync();
    }
}
