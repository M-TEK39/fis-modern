using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ClassRequirement entity operations
/// Provides CRUD operations for vehicle class requirements for projects
/// </summary>
public class ClassRequirementRepository : IClassRequirementRepository
{
    private readonly FisDbContext _context;

    public ClassRequirementRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<ClassRequirement?> GetByIdAsync(int classRequirementId)
    {
        return await _context.ClassRequirements
            .Include(cr => cr.Class)
            .Where(cr => !cr.is_deleted)
            .FirstOrDefaultAsync(cr => cr.class_requirement_id == classRequirementId);
    }

    public async Task<IEnumerable<ClassRequirement>> GetAllAsync()
    {
        return await _context.ClassRequirements
            .Include(cr => cr.Class)
            .Where(cr => !cr.is_deleted)
            .OrderBy(cr => cr.project_id)
            .ThenBy(cr => cr.class_id)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClassRequirement>> GetByProjectIdAsync(int projectId)
    {
        return await _context.ClassRequirements
            .Include(cr => cr.Class)
            .Where(cr => !cr.is_deleted)
            .Where(cr => cr.project_id == projectId)
            .OrderBy(cr => cr.class_id)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClassRequirement>> GetByClassIdAsync(short classId)
    {
        return await _context.ClassRequirements
            .Include(cr => cr.Class)
            .Where(cr => !cr.is_deleted)
            .Where(cr => cr.class_id == classId)
            .OrderByDescending(cr => cr.start_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClassRequirement>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.ClassRequirements
            .Include(cr => cr.Class)
            .Where(cr => !cr.is_deleted)
            .Where(cr => 
                (cr.start_date <= endDate) &&
                (cr.end_date == null || cr.end_date >= startDate))
            .OrderBy(cr => cr.start_date)
            .ToListAsync();
    }

    public async Task<ClassRequirement> CreateAsync(ClassRequirement classRequirement, int currentUserId)
    {
        classRequirement.date_created = DateTime.UtcNow;
        classRequirement.created_by_user_code = currentUserId;
        classRequirement.is_deleted = false;

        _context.ClassRequirements.Add(classRequirement);
        await _context.SaveChangesAsync();
        return classRequirement;
    }

    public async Task<ClassRequirement> UpdateAsync(ClassRequirement classRequirement, int currentUserId)
    {
        if (classRequirement == null)
            throw new ArgumentNullException(nameof(classRequirement));

        var existing = await _context.ClassRequirements.FindAsync(classRequirement.class_requirement_id);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"ClassRequirement with class_requirement_id {classRequirement.class_requirement_id} not found");

        classRequirement.date_updated = DateTime.UtcNow;
        classRequirement.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(classRequirement);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int classRequirementId, int currentUserId)
    {
        var classRequirement = await _context.ClassRequirements.FindAsync(classRequirementId);
        if (classRequirement != null)
        {
            classRequirement.is_deleted = true;
            classRequirement.date_updated = DateTime.UtcNow;
            classRequirement.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
