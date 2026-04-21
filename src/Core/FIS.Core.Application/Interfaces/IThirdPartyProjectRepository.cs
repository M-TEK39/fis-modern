using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface IThirdPartyProjectRepository
{
    Task<ThirdPartyProject?> GetByIdAsync(int projectId);
    Task<IEnumerable<ThirdPartyProject>> GetAllAsync();
    Task<IEnumerable<ThirdPartyProject>> GetByDepartmentAsync(short departmentCode);
    Task<ThirdPartyProject> CreateAsync(ThirdPartyProject project, int currentUserId);
    Task<ThirdPartyProject> UpdateAsync(ThirdPartyProject project, int currentUserId);
    Task DeleteAsync(int projectId, int currentUserId);
}
