using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface ITrackingRepository
{
    Task<Tracking?> GetByIdAsync(short trackCode);
    Task<IEnumerable<Tracking>> GetAllAsync();
    Task<IEnumerable<Tracking>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<Tracking>> GetActiveTrackingAsync();
    Task<Tracking> CreateAsync(Tracking tracking, int currentUserId);
    Task<Tracking> UpdateAsync(Tracking tracking, int currentUserId);
    Task DeleteAsync(short trackCode, int currentUserId);
}
