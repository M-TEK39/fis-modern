using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces
{
    public interface IVehiclePhotoRepository
    {
        Task<VehiclePhoto?> GetByIdAsync(int code);
        Task<IEnumerable<VehiclePhoto>> GetAllAsync();
        Task<IEnumerable<VehiclePhoto>> GetByVehicleAsync(int vmfCode);
        Task<VehiclePhoto> CreateAsync(VehiclePhoto item, int currentUserId);
        Task<VehiclePhoto> UpdateAsync(VehiclePhoto item, int currentUserId);
        Task DeleteAsync(int code, int currentUserId);
    }
}
