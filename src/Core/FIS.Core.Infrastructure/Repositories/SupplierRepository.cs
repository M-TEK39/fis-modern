using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class SupplierRepository : ISupplierRepository
{
    private readonly FisDbContext _context;
    public SupplierRepository(FisDbContext context) { _context = context; }
    public async Task<Supplier?> GetByIdAsync(short supplierId) { return await _context.Set<Supplier>().FirstOrDefaultAsync(s => s.supplier_id == supplierId); }
    public async Task<IEnumerable<Supplier>> GetAllAsync() { return await _context.Set<Supplier>().ToListAsync(); }
    public async Task<IEnumerable<Supplier>> GetActiveAsync() { return await _context.Set<Supplier>().Where(s => s.active == true).ToListAsync(); }
    public async Task<IEnumerable<Supplier>> GetByTypeAsync(string supplierType) { return await _context.Set<Supplier>().Where(s => s.supplier_type == supplierType).ToListAsync(); }
    public async Task<Supplier> CreateAsync(Supplier supplier) { _context.Set<Supplier>().Add(supplier); await _context.SaveChangesAsync(); return supplier; }
    public async Task<Supplier> UpdateAsync(Supplier supplier) { _context.Set<Supplier>().Update(supplier); await _context.SaveChangesAsync(); return supplier; }
    public async Task DeleteAsync(short supplierId) { var supplier = await GetByIdAsync(supplierId); if (supplier != null) { _context.Set<Supplier>().Remove(supplier); await _context.SaveChangesAsync(); } }
}
