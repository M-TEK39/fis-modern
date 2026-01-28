using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(short supplierId);
    Task<IEnumerable<Supplier>> GetAllAsync();
    Task<IEnumerable<Supplier>> GetActiveAsync();
    Task<IEnumerable<Supplier>> GetByTypeAsync(string supplierType);
    Task<Supplier> CreateAsync(Supplier supplier, int currentUserId);
    Task<Supplier> UpdateAsync(Supplier supplier, int currentUserId);
    Task DeleteAsync(short supplierId, int currentUserId);
}
