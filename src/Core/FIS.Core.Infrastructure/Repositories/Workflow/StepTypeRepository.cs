using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class StepTypeRepository : IStepTypeRepository
{
    private readonly FisDbContext _context;

    public StepTypeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<StepType?> GetByIdAsync(int stepTypeId)
    {
        return await _context.StepTypes.FirstOrDefaultAsync(st =>
            st.StepTypeID == stepTypeId && !st.is_deleted
        );
    }

    public async Task<StepType?> GetByNameAsync(string stepTypeName)
    {
        return await _context.StepTypes.FirstOrDefaultAsync(st =>
            st.StepTypeName == stepTypeName && !st.is_deleted
        );
    }

    public async Task<IEnumerable<StepType>> GetAllAsync()
    {
        return await _context
            .StepTypes.Where(st => !st.is_deleted)
            .OrderBy(st => st.StepTypeName)
            .ToListAsync();
    }

    public async Task<StepType> CreateAsync(StepType stepType, int currentUserId)
    {
        stepType.date_created = DateTime.Now;
        stepType.created_by_user_code = currentUserId;
        stepType.is_deleted = false;

        await _context.StepTypes.AddAsync(stepType);
        await _context.SaveChangesAsync();
        return stepType;
    }

    public async Task UpdateAsync(StepType stepType, int currentUserId)
    {
        if (stepType == null)
            throw new ArgumentNullException(nameof(stepType));

        var existing = await _context.StepTypes.FindAsync(stepType.StepTypeID);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException(
                $"StepType with ID {stepType.StepTypeID} not found"
            );

        stepType.date_updated = DateTime.Now;
        stepType.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(stepType);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int stepTypeId, int currentUserId)
    {
        var stepType = await _context.StepTypes.FindAsync(stepTypeId);
        if (stepType != null && !stepType.is_deleted)
        {
            stepType.is_deleted = true;
            stepType.date_updated = DateTime.Now;
            stepType.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
