using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Supplier entity operations
/// Provides CRUD operations for third party suppliers and vendors
/// </summary>
public class SupplierRepository : ISupplierRepository
{
    private readonly FisDbContext _context;

    public SupplierRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<Supplier?> GetByIdAsync(short supplierId)
    {
        return await _context
            .Suppliers.Where(s => !s.is_deleted)
            .FirstOrDefaultAsync(s => s.supplier_id == supplierId);
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync()
    {
        return await _context
            .Suppliers.Where(s => !s.is_deleted)
            .OrderBy(s => s.supplier_name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Supplier>> GetActiveAsync()
    {
        return await _context
            .Suppliers.Where(s => !s.is_deleted && s.is_active)
            .OrderBy(s => s.supplier_name)
            .ToListAsync();
    }

    public async Task<Supplier?> GetByNameAsync(string supplierName)
    {
        return await _context
            .Suppliers.Where(s => !s.is_deleted)
            .FirstOrDefaultAsync(s =>
                s.supplier_name != null && s.supplier_name.ToLower() == supplierName.ToLower()
            );
    }

    public async Task<Supplier> CreateAsync(Supplier supplier, int currentUserId)
    {
        supplier.date_created = DateTime.UtcNow;
        supplier.created_by_user_code = currentUserId;
        supplier.is_deleted = false;

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier;
    }

    public async Task<Supplier> UpdateAsync(Supplier supplier, int currentUserId)
    {
        if (supplier == null)
            throw new ArgumentNullException(nameof(supplier));

        var existing = await _context.Suppliers.FindAsync(supplier.supplier_id);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException(
                $"Supplier with supplier_id {supplier.supplier_id} not found"
            );

        supplier.date_updated = DateTime.UtcNow;
        supplier.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(supplier);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(short supplierId, int currentUserId)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier != null)
        {
            supplier.is_deleted = true;
            supplier.date_updated = DateTime.UtcNow;
            supplier.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
