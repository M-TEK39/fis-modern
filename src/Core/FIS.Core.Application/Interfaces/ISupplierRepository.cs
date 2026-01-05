using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(short supplierId);
    Task<IEnumerable<Supplier>> GetAllAsync();
    Task<IEnumerable<Supplier>> GetActiveAsync();
    Task<IEnumerable<Supplier>> GetByTypeAsync(string supplierType);
    Task<Supplier> CreateAsync(Supplier supplier);
    Task<Supplier> UpdateAsync(Supplier supplier);
    Task DeleteAsync(short supplierId);
}
