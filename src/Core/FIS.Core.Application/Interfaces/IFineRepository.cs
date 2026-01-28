using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Fine entity operations
    /// Provides contract for CRUD operations on traffic fines
    /// </summary>
    public interface IFineRepository
    {
        Task<Fine?> GetByIdAsync(int fineCode);
        Task<IEnumerable<Fine>> GetAllAsync();
        Task<IEnumerable<Fine>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Fine>> GetBySiteAsync(short siteCode);
        Task<IEnumerable<Fine>> GetUnpaidFinesAsync();
        Task<Fine> CreateAsync(Fine fine, int currentUserId);
        Task<Fine> UpdateAsync(Fine fine, int currentUserId);
        Task DeleteAsync(int fineCode, int currentUserId);
    }
}
