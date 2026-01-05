using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface IVehicleDamageRepository
{
    Task<VehicleDamage?> GetByIdAsync(short damageId);
    Task<IEnumerable<VehicleDamage>> GetAllAsync();
    Task<IEnumerable<VehicleDamage>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<VehicleDamage>> GetByStatusAsync(string status);
    Task<VehicleDamage> CreateAsync(VehicleDamage damage);
    Task<VehicleDamage> UpdateAsync(VehicleDamage damage);
    Task DeleteAsync(short damageId);
}
