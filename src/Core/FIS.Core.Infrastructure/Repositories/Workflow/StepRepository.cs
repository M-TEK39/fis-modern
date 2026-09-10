using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class StepRepository : IStepRepository
{
    private readonly FisDbContext _context;

    public StepRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Step?> GetByIdAsync(int stepId)
    {
        return await _context.Steps.FirstOrDefaultAsync(s => s.StepID == stepId && !s.is_deleted);
    }

    public async Task<IEnumerable<Step>> GetAllAsync()
    {
        return await _context
            .Steps.Where(s => !s.is_deleted)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<Step>> GetByWorkflowIdAsync(int workflowId)
    {
        return await _context
            .Steps.Where(s => s.WorkflowID == workflowId && !s.is_deleted)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<Step>> GetByStepTypeIdAsync(int stepTypeId)
    {
        return await _context
            .Steps.Where(s => s.StepTypeID == stepTypeId && !s.is_deleted)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<Step>> GetChildStepsAsync(int parentStepId)
    {
        return await _context
            .Steps.Where(s => s.ParentStepID == parentStepId && !s.is_deleted)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<Step> CreateAsync(Step step, int currentUserId)
    {
        step.date_created = DateTime.Now;
        step.created_by_user_code = currentUserId;
        step.is_deleted = false;

        await _context.Steps.AddAsync(step);
        await _context.SaveChangesAsync();
        return step;
    }

    public async Task UpdateAsync(Step step, int currentUserId)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));

        var existing = await _context.Steps.FindAsync(step.StepID);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"Step with ID {step.StepID} not found");

        step.date_updated = DateTime.Now;
        step.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(step);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int stepId, int currentUserId)
    {
        var step = await _context.Steps.FindAsync(stepId);
        if (step != null && !step.is_deleted)
        {
            step.is_deleted = true;
            step.date_updated = DateTime.Now;
            step.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
