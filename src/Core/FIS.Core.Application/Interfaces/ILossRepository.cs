using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Loss entity operations
    /// Provides contract for CRUD operations on vehicle losses
    /// </summary>
    public interface ILossRepository
    {
        Task<Loss?> GetByIdAsync(short lossCode);
        Task<IEnumerable<Loss>> GetAllAsync();
        Task<IEnumerable<Loss>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Loss>> GetBySiteAsync(short siteCode);
        Task<Loss> CreateAsync(Loss loss);
        Task<Loss> UpdateAsync(Loss loss);
        Task DeleteAsync(short lossCode);
    }
}
