using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for ClassRequirement entity operations
/// </summary>
public interface IClassRequirementRepository
{
    Task<ClassRequirement?> GetByIdAsync(int classRequirementId);
    Task<IEnumerable<ClassRequirement>> GetAllAsync();
    Task<IEnumerable<ClassRequirement>> GetByProjectIdAsync(int projectId);
    Task<IEnumerable<ClassRequirement>> GetByClassIdAsync(short classId);
    Task<IEnumerable<ClassRequirement>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<ClassRequirement> CreateAsync(ClassRequirement classRequirement, int currentUserId);
    Task<ClassRequirement> UpdateAsync(ClassRequirement classRequirement, int currentUserId);
    Task DeleteAsync(int classRequirementId, int currentUserId);
}
