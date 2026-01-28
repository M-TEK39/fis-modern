using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Towing entity operations
    /// Provides contract for CRUD operations on towing requests
    /// </summary>
    public interface ITowingRepository
    {
        Task<Towing?> GetByIdAsync(short towingCode);
        Task<IEnumerable<Towing>> GetAllAsync();
        Task<IEnumerable<Towing>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Towing>> GetBySiteAsync(short siteCode);
        Task<Towing> CreateAsync(Towing towing, int currentUserId);
        Task<Towing> UpdateAsync(Towing towing, int currentUserId);
        Task DeleteAsync(short towingCode, int currentUserId);
    }
}
