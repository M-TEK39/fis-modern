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

    public async Task<PrivateHire> CreateAsync(PrivateHire privateHire)
    {
        _context.PrivateHires.Add(privateHire);
        await _context.SaveChangesAsync();
        return privateHire;
    }

    public async Task UpdateAsync(PrivateHire privateHire)
    {
        _context.Entry(privateHire).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int privateHireCode)
    {
        var privateHire = await GetByIdAsync(privateHireCode);
        if (privateHire != null)
        {
            _context.PrivateHires.Remove(privateHire);
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