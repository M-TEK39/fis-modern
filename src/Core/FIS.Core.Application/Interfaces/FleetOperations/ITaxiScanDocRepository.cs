using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface ITaxiScanDocRepository
{
    Task<TaxiScanDoc?> GetByIdAsync(int scanDocCode);
    Task<IEnumerable<TaxiScanDoc>> GetAllAsync();
    Task<IEnumerable<TaxiScanDoc>> GetByVehicleAsync(int vmfCode);
    Task<TaxiScanDoc> CreateAsync(TaxiScanDoc document, int currentUserId);
    Task DeleteAsync(int scanDocCode, int currentUserId);
}
