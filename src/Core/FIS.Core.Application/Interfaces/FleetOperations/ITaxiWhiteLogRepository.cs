using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface ITaxiWhiteLogRepository
{
    Task<TaxiWhiteLog?> GetByIdAsync(int logId);
    Task<IEnumerable<TaxiWhiteLog>> GetAllAsync();
    Task<IEnumerable<TaxiWhiteLog>> GetByVehicleAsync(int vmfCode);
    Task<TaxiWhiteLog> CreateAsync(TaxiWhiteLog log, int currentUserId);
    Task DeleteAsync(int logId, int currentUserId);
}
