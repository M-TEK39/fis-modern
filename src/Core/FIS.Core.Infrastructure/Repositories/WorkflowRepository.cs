using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly FisDbContext _context;

    public WorkflowRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Workflow?> GetByIdAsync(int workflowId)
    {
        return await _context.Workflows
            .FirstOrDefaultAsync(w => w.WorkflowID == workflowId && !w.is_deleted);
    }

    public async Task<Workflow?> GetByNameAsync(string workflowName)
    {
        return await _context.Workflows
            .FirstOrDefaultAsync(w => w.WorkflowName == workflowName && !w.is_deleted);
    }

    public async Task<IEnumerable<Workflow>> GetAllAsync()
    {
        return await _context.Workflows
            .Where(w => !w.is_deleted)
            .OrderBy(w => w.WorkflowName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Workflow>> GetActiveWorkflowsAsync()
    {
        return await _context.Workflows
            .Where(w => !w.is_deleted && w.AlwaysExecute)
            .OrderBy(w => w.WorkflowName)
            .ToListAsync();
    }

    public async Task<Workflow> CreateAsync(Workflow workflow, int currentUserId)
    {
        workflow.date_created = DateTime.Now;
        workflow.created_by_user_code = currentUserId;
        workflow.is_deleted = false;

        await _context.Workflows.AddAsync(workflow);
        await _context.SaveChangesAsync();
        return workflow;
    }

    public async Task UpdateAsync(Workflow workflow, int currentUserId)
    {
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));

        var existing = await _context.Workflows.FindAsync(workflow.WorkflowID);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"Workflow with ID {workflow.WorkflowID} not found");

        workflow.date_updated = DateTime.Now;
        workflow.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(workflow);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int workflowId, int currentUserId)
    {
        var workflow = await _context.Workflows.FindAsync(workflowId);
        if (workflow != null && !workflow.is_deleted)
        {
            workflow.is_deleted = true;
            workflow.date_updated = DateTime.Now;
            workflow.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
