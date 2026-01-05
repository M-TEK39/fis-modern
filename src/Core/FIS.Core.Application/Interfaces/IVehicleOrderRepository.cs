using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces
{
    public interface IVehicleOrderRepository
    {
        Task<VehicleOrder?> GetByIdAsync(int code);
        Task<IEnumerable<VehicleOrder>> GetAllAsync();
        Task<VehicleOrder> CreateAsync(VehicleOrder item);
        Task<VehicleOrder> UpdateAsync(VehicleOrder item);
        Task DeleteAsync(int code);
    }
}
