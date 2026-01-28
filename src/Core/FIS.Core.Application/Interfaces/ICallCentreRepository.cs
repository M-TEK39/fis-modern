using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for CallCentre entity operations
    /// Provides contract for CRUD operations on call centre incidents
    /// </summary>
    public interface ICallCentreRepository
    {
        Task<CallCentre?> GetByIdAsync(short callCentreCode);
        Task<IEnumerable<CallCentre>> GetAllAsync();
        Task<IEnumerable<CallCentre>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<CallCentre>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<CallCentre> CreateAsync(CallCentre callCentre, int currentUserId);
        Task<CallCentre> UpdateAsync(CallCentre callCentre, int currentUserId);
        Task DeleteAsync(short callCentreCode, int currentUserId);
    }
}
