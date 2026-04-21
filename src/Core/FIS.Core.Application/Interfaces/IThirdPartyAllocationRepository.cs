using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface IThirdPartyAllocationRepository
{
    Task<ThirdPartyAllocation?> GetByIdAsync(int allocationId);
    Task<IEnumerable<ThirdPartyAllocation>> GetByProjectAsync(int projectId);
    Task<ThirdPartyAllocation> CreateAsync(ThirdPartyAllocation allocation, int currentUserId);
    Task DeleteAsync(int allocationId, int currentUserId);
}
