using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface ITrafficDeptRepository
{
    Task<TrafficDept?> GetByIdAsync(short deptCode);
    Task<IEnumerable<TrafficDept>> GetAllAsync();
    Task<TrafficDept?> GetByNameAsync(string name);
    Task<TrafficDept> CreateAsync(TrafficDept dept, int currentUserId);
    Task<TrafficDept> UpdateAsync(TrafficDept dept, int currentUserId);
    Task DeleteAsync(short deptCode, int currentUserId);
}
