using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for Supplier entity operations
/// </summary>
public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(short supplierId);
    Task<IEnumerable<Supplier>> GetAllAsync();
    Task<IEnumerable<Supplier>> GetActiveAsync();
    Task<Supplier?> GetByNameAsync(string supplierName);
    Task<Supplier> CreateAsync(Supplier supplier, int currentUserId);
    Task<Supplier> UpdateAsync(Supplier supplier, int currentUserId);
    Task DeleteAsync(short supplierId, int currentUserId);
}
