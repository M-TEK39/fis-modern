using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface IVehicleDamageRepository
{
    Task<VehicleDamage?> GetByIdAsync(short damageId);
    Task<IEnumerable<VehicleDamage>> GetAllAsync();
    Task<IEnumerable<VehicleDamage>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<VehicleDamage>> GetByStatusAsync(string status);
    Task<VehicleDamage> CreateAsync(VehicleDamage damage, int currentUserId);
    Task<VehicleDamage> UpdateAsync(VehicleDamage damage, int currentUserId);
    Task DeleteAsync(short damageId, int currentUserId);
}
