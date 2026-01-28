using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces
{
    public interface IVehicleOrderRepository
    {
        Task<VehicleOrder?> GetByIdAsync(int code);
        Task<IEnumerable<VehicleOrder>> GetAllAsync();
        Task<VehicleOrder> CreateAsync(VehicleOrder item, int currentUserId);
        Task<VehicleOrder> UpdateAsync(VehicleOrder item, int currentUserId);
        Task DeleteAsync(int code, int currentUserId);
    }
}
