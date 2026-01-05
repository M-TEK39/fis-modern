using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Clearance entity operations
    /// Provides contract for CRUD operations on vehicle clearances
    /// </summary>
    public interface IClearanceRepository
    {
        Task<Clearance?> GetByIdAsync(int clearanceCode);
        Task<IEnumerable<Clearance>> GetAllAsync();
        Task<IEnumerable<Clearance>> GetByVehicleAsync(int vmfCode);
        Task<Clearance> CreateAsync(Clearance clearance);
        Task<Clearance> UpdateAsync(Clearance clearance);
        Task DeleteAsync(int clearanceCode);
    }
}
