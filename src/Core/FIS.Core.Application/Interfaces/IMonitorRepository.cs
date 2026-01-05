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
        Task<IEnumerable<MonitorEntity>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<MonitorEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<MonitorEntity> CreateAsync(MonitorEntity monitor);
        Task<MonitorEntity> UpdateAsync(MonitorEntity monitor);
        Task DeleteAsync(short monitorCode);
    }
}
