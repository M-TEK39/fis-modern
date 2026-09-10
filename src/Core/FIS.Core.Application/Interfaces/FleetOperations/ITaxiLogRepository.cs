using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface ITaxiLogRepository
{
    Task<TaxiLog?> GetByIdAsync(int logId);
    Task<TaxiLog?> GetLatestByRequisitionAsync(string rekNum);
    Task<IEnumerable<TaxiLog>> GetAllAsync();
    Task<TaxiLog> CreateAsync(TaxiLog log, int currentUserId);
    Task<TaxiLog> UpdateAsync(TaxiLog log, int currentUserId);
}
