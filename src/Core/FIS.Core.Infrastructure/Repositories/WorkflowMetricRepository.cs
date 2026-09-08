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
        return await _context
            .WorkflowMetrics.Include(m => m.Workflow)
            .Include(m => m.BottleneckStep)
            .FirstOrDefaultAsync(m => m.MetricID == metricId && !m.is_deleted);
    }

    public async Task<IEnumerable<WorkflowMetric>> GetAllAsync()
    {
        return await _context
            .WorkflowMetrics.Where(m => !m.is_deleted)
            .OrderByDescending(m => m.MetricDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowMetric>> GetByWorkflowIdAsync(int workflowId)
    {
        return await _context
            .WorkflowMetrics.Where(m => m.WorkflowID == workflowId && !m.is_deleted)
            .OrderByDescending(m => m.MetricDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<WorkflowMetric>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        return await _context
            .WorkflowMetrics.Where(m =>
                m.MetricDate >= startDate && m.MetricDate <= endDate && !m.is_deleted
            )
            .OrderBy(m => m.MetricDate)
            .ToListAsync();
    }

    public async Task<WorkflowMetric?> GetLatestMetricAsync(int workflowId)
    {
        return await _context
            .WorkflowMetrics.Where(m => m.WorkflowID == workflowId && !m.is_deleted)
            .OrderByDescending(m => m.MetricDate)
            .FirstOrDefaultAsync();
    }

    public async Task<WorkflowMetric> CreateAsync(WorkflowMetric metric, int currentUserId)
    {
        metric.date_created = DateTime.UtcNow;
        metric.created_by_user_code = currentUserId;
        metric.is_deleted = false;

        _context.WorkflowMetrics.Add(metric);
        await _context.SaveChangesAsync();

        return metric;
    }

    public async Task UpdateAsync(WorkflowMetric metric, int currentUserId)
    {
        var existing = await _context.WorkflowMetrics.FirstOrDefaultAsync(m =>
            m.MetricID == metric.MetricID
        );

        if (existing == null)
            throw new InvalidOperationException($"WorkflowMetric {metric.MetricID} not found");

        _context.Entry(existing).CurrentValues.SetValues(metric);
        existing.date_updated = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
