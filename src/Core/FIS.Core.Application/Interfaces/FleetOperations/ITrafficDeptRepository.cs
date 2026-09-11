using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface ITrafficDeptRepository
{
    Task<TrafficDept?> GetByIdAsync(short deptCode);
    Task<IEnumerable<TrafficDept>> GetAllAsync();
    Task<TrafficDeptPage> GetPageAsync(TrafficDeptPageQuery query);
    Task<TrafficDept?> GetByNameAsync(string name);
    Task<TrafficDept> CreateAsync(TrafficDept dept, int currentUserId);
    Task<TrafficDept> UpdateAsync(TrafficDept dept, int currentUserId);
    Task DeleteAsync(short deptCode, int currentUserId);
}

public sealed record TrafficDeptPageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchQuery = null
);

public sealed record TrafficDeptPage(
    IReadOnlyList<TrafficDept> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
