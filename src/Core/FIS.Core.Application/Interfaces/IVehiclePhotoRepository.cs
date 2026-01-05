using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces
{
    public interface IVehiclePhotoRepository
    {
        Task<VehiclePhoto?> GetByIdAsync(int code);
        Task<IEnumerable<VehiclePhoto>> GetAllAsync();
        Task<IEnumerable<VehiclePhoto>> GetByVehicleAsync(int vmfCode);
        Task<VehiclePhoto> CreateAsync(VehiclePhoto item);
        Task<VehiclePhoto> UpdateAsync(VehiclePhoto item);
        Task DeleteAsync(int code);
    }
}
