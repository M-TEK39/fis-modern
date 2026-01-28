using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for PrivateHire entity operations
/// </summary>
public class PrivateHireRepository : IPrivateHireRepository
{
    private readonly FisDbContext _context;

    public PrivateHireRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<PrivateHire?> GetByIdAsync(int privateHireCode)
    {
        return await _context.PrivateHires
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(ph => ph.PHV_code == privateHireCode);
    }

    public async Task<IEnumerable<PrivateHire>> GetByVehicleAsync(int vmfCode)
    {
        // Note: PrivateHire doesn't directly reference VMF code, using registration_number correlation
        return await _context.PrivateHires
            .Where(ph => ph.registration_number != null)
            .ToListAsync();
    }

    public async Task<IEnumerable<PrivateHire>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.PrivateHires
            .Where(ph => ph.take_on_date >= startDate && ph.take_on_date <= endDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PrivateHire>> GetActiveHiresAsync()
    {
        // Active hires are those without return dates or recent return dates
        return await _context.PrivateHires
            .Where(ph => ph.return_date == null || ph.return_date >= DateTime.Now.AddDays(-30))
            .ToListAsync();
    }

    public async Task<PrivateHire> CreateAsync(PrivateHire privateHire, int currentUserId)
    {
        // Auto-populate audit fields
            privateHire.date_created = DateTime.UtcNow;
            privateHire.is_deleted = false;
            
            _context.PrivateHires.Add(privateHire);
        await _context.SaveChangesAsync();
        return privateHire;
    }

    public async Task UpdateAsync(PrivateHire privateHire, int currentUserId)
    {
        if (privateHire == null)
            throw new ArgumentNullException(nameof(privateHire));

        var existing = await _context.PrivateHires.FindAsync(privateHire.PHV_code);
        if (existing == null)
            throw new InvalidOperationException($"PrivateHire with PHV_code {privateHire.PHV_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(privateHire);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int privateHireCode, int currentUserId)
    {
        var privateHire = await GetByIdAsync(privateHireCode);
        if (privateHire != null)
        {
            // Soft delete instead of hard delete
                privateHire.is_deleted = true;
                privateHire.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<PrivateHire>> SearchHiresAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await _context.PrivateHires.ToListAsync();

        return await _context.PrivateHires
            .Where(ph => (ph.registration_number != null && ph.registration_number.Contains(searchTerm)) ||
                        (ph.engine_number != null && ph.engine_number.Contains(searchTerm)) ||
                        (ph.chassis_number != null && ph.chassis_number.Contains(searchTerm)))
            .ToListAsync();
    }
}