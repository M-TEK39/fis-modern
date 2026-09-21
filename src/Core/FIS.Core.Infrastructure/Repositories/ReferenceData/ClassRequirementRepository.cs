using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// ClassRequirements is not in the 2012 archive. Legacy class counts live in
/// third_party_projects.ClassConfiguration through ThirdPartyRentalRepository.
/// </summary>
public class ClassRequirementRepository : IClassRequirementRepository
{
    private const string TableName = "ClassRequirements";

    private readonly FisDbContext _context;

    public ClassRequirementRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<ClassRequirement?> GetByIdAsync(int classRequirementId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await _context.ClassRequirements.FirstOrDefaultAsync(cr =>
            cr.class_requirement_id == classRequirementId
        );
    }

    public async Task<IEnumerable<ClassRequirement>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .ClassRequirements.OrderBy(cr => cr.project_id)
            .ThenBy(cr => cr.class_id)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClassRequirement>> GetByProjectIdAsync(int projectId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .ClassRequirements.Where(cr => cr.project_id == projectId)
            .OrderBy(cr => cr.class_id)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClassRequirement>> GetByClassIdAsync(short classId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .ClassRequirements.Where(cr => cr.class_id == classId)
            .OrderByDescending(cr => cr.start_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClassRequirement>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .ClassRequirements.Where(cr =>
                (cr.start_date <= endDate) && (cr.end_date == null || cr.end_date >= startDate)
            )
            .OrderBy(cr => cr.start_date)
            .ToListAsync();
    }

    public async Task<ClassRequirement> CreateAsync(
        ClassRequirement classRequirement,
        int currentUserId
    )
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        _context.ClassRequirements.Add(classRequirement);
        await _context.SaveChangesAsync();
        return classRequirement;
    }

    public async Task<ClassRequirement> UpdateAsync(
        ClassRequirement classRequirement,
        int currentUserId
    )
    {
        _ = currentUserId;
        if (classRequirement == null)
            throw new ArgumentNullException(nameof(classRequirement));
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var existing = await _context.ClassRequirements.FindAsync(
            classRequirement.class_requirement_id
        );
        if (existing == null)
            throw new InvalidOperationException(
                $"ClassRequirement with class_requirement_id {classRequirement.class_requirement_id} not found"
            );

        existing.project_id = classRequirement.project_id;
        existing.class_id = classRequirement.class_id;
        existing.required_count = classRequirement.required_count;
        existing.start_date = classRequirement.start_date;
        existing.end_date = classRequirement.end_date;
        existing.notes = classRequirement.notes;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int classRequirementId, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var classRequirement = await _context.ClassRequirements.FindAsync(classRequirementId);
        if (classRequirement != null)
        {
            _context.ClassRequirements.Remove(classRequirement);
            await _context.SaveChangesAsync();
        }
    }
}
