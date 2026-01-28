using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Logbook entity operations
    /// Provides contract for CRUD operations on vehicle logbooks
    /// </summary>
    public interface ILogbookRepository
    {
        Task<Logbook?> GetByIdAsync(short logbookCode);
        Task<IEnumerable<Logbook>> GetAllAsync();
        Task<IEnumerable<Logbook>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Logbook>> GetBySiteAsync(short siteCode);
        Task<Logbook> CreateAsync(Logbook logbook, int currentUserId);
        Task<Logbook> UpdateAsync(Logbook logbook, int currentUserId);
        Task DeleteAsync(short logbookCode, int currentUserId);
    }
}
