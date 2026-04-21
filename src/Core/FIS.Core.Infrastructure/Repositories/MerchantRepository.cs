using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class MerchantRepository : IMerchantRepository
{
    private readonly FisDbContext _context;

    public MerchantRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<MerchantReference?> GetByIdAsync(int merchantCode)
    {
        return await _context.MerchantReferences
            .FirstOrDefaultAsync(m => m.Merchant_code == merchantCode && !m.is_deleted);
    }

    public async Task<IEnumerable<MerchantReference>> GetAllAsync()
    {
        return await _context.MerchantReferences
            .Where(m => !m.is_deleted)
            .OrderBy(m => m.Merchant_name)
            .ToListAsync();
    }

    public async Task<MerchantReference> CreateAsync(MerchantReference merchant, int currentUserId)
    {
        merchant.date_created = DateTime.UtcNow;
        merchant.created_by_user_code = currentUserId;
        merchant.is_deleted = false;

        _context.MerchantReferences.Add(merchant);
        await _context.SaveChangesAsync();
        return merchant;
    }

    public async Task<MerchantReference> UpdateAsync(MerchantReference merchant, int currentUserId)
    {
        var existing = await _context.MerchantReferences
            .FirstOrDefaultAsync(m => m.Merchant_code == merchant.Merchant_code)
            ?? throw new KeyNotFoundException($"Merchant {merchant.Merchant_code} not found");

        existing.Merchant_name = merchant.Merchant_name;
        existing.date_updated = DateTime.UtcNow;
        existing.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int merchantCode, int currentUserId)
    {
        var merchant = await _context.MerchantReferences
            .FirstOrDefaultAsync(m => m.Merchant_code == merchantCode)
            ?? throw new KeyNotFoundException($"Merchant {merchantCode} not found");

        // Soft delete
        merchant.is_deleted = true;
        merchant.date_updated = DateTime.UtcNow;
        merchant.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }
}
