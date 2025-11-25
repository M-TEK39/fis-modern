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
        Task<Towing> CreateAsync(Towing towing);
        Task<Towing> UpdateAsync(Towing towing);
        Task DeleteAsync(short towingCode);
    }
}
