using MonitorEntity = FIS.Core.Domain.Entities.Monitor;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Monitor entity operations
    /// Provides contract for CRUD operations on fleet monitoring inquiries
    /// </summary>
    public interface IMonitorRepository
    {
        Task<MonitorEntity?> GetByIdAsync(short monitorCode);
        Task<IEnumerable<MonitorEntity>> GetAllAsync();
        Task<MonitorPage> GetPageAsync(MonitorPageQuery query);
        Task<IEnumerable<MonitorEntity>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<MonitorEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<MonitorEntity> CreateAsync(MonitorEntity monitor, int currentUserId);
        Task<MonitorEntity> UpdateAsync(MonitorEntity monitor, int currentUserId);
        Task DeleteAsync(short monitorCode, int currentUserId);
    }

    public sealed record MonitorPageQuery(
        int Page = 1,
        int PageSize = 24,
        string? SearchTerm = null
    );

    public sealed record MonitorPage(
        IReadOnlyList<MonitorEntity> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
