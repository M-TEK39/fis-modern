using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// dbo.Suppliers is supplier_id, name, address, tel, fax, contact_person.
/// Expanded supplier_name/is_active/email/audit columns are not queried.
/// Third-party rental suppliers still go through vehicle_source procedures.
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
        return await _context.Suppliers.FirstOrDefaultAsync(s => s.supplier_id == supplierId);
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync()
    {
        return await _context.Suppliers.OrderBy(s => s.supplier_name).ToListAsync();
    }

    public Task<IEnumerable<Supplier>> GetActiveAsync() => GetAllAsync();

    public async Task<Supplier?> GetByNameAsync(string supplierName)
    {
        if (string.IsNullOrWhiteSpace(supplierName))
            return null;

        return await _context.Suppliers.FirstOrDefaultAsync(s =>
            s.supplier_name == supplierName
        );
    }

    public async Task<Supplier> CreateAsync(Supplier supplier, int currentUserId)
    {
        _ = currentUserId;
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier;
    }

    public async Task<Supplier> UpdateAsync(Supplier supplier, int currentUserId)
    {
        _ = currentUserId;
        if (supplier == null)
            throw new ArgumentNullException(nameof(supplier));

        var existing = await _context.Suppliers.FindAsync(supplier.supplier_id);
        if (existing == null)
            throw new InvalidOperationException(
                $"Supplier with supplier_id {supplier.supplier_id} not found"
            );

        existing.supplier_name = supplier.supplier_name;
        existing.contact_person = supplier.contact_person;
        existing.phone_number = supplier.phone_number;
        existing.address = supplier.address;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(short supplierId, int currentUserId)
    {
        _ = currentUserId;
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
            return;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF COL_LENGTH('dbo.Suppliers', 'is_deleted') IS NOT NULL
                    UPDATE [dbo].[Suppliers]
                    SET [is_deleted] = 1
                    WHERE [supplier_id] = @supplierId;
                ELSE
                    DELETE FROM [dbo].[Suppliers]
                    WHERE [supplier_id] = @supplierId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@supplierId";
            parameter.DbType = DbType.Int16;
            parameter.Value = supplierId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            _context.Entry(supplier).State = EntityState.Detached;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
