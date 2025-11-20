using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for legacy Tariff table (pre-2009 tariff system).
/// Handles tariff lookups by vehicle class, year manufactured, and effective date.
/// </summary>
public class TariffRepository : ITariffRepository
{
    private readonly FisDbContext _context;

    public TariffRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get tariff by class code, year manufactured, and effective date.
    /// Finds the tariff where the effective date falls within the start/end date range.
    /// </summary>
    public async Task<Tariff?> GetTariffAsync(
        short classCode,
        short yearManufactured,
        DateTime effectiveDate)
    {
        return await _context.Set<Tariff>()
            .Where(t => t.class_code == classCode)
            .Where(t => t.year_manufactured == yearManufactured)
            .Where(t => t.effective_start_date <= effectiveDate)
            .Where(t => t.effective_end_date == null || t.effective_end_date >= effectiveDate)
            .OrderByDescending(t => t.effective_start_date)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all tariffs for a vehicle class.
    /// </summary>
    public async Task<List<Tariff>> GetTariffsByClassAsync(short classCode)
    {
        return await _context.Set<Tariff>()
            .Where(t => t.class_code == classCode)
            .OrderByDescending(t => t.effective_start_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get all tariffs effective on a specific date.
    /// </summary>
    public async Task<List<Tariff>> GetTariffsByDateAsync(DateTime effectiveDate)
    {
        return await _context.Set<Tariff>()
            .Where(t => t.effective_start_date <= effectiveDate)
            .Where(t => t.effective_end_date == null || t.effective_end_date >= effectiveDate)
            .OrderBy(t => t.class_code)
            .ThenBy(t => t.year_manufactured)
            .ToListAsync();
    }

    /// <summary>
    /// Get tariff by ID.
    /// </summary>
    public async Task<Tariff?> GetByIdAsync(int tariffCode)
    {
        return await _context.Set<Tariff>()
            .FirstOrDefaultAsync(t => t.tariff_code == tariffCode);
    }

    /// <summary>
    /// Get all tariffs.
    /// </summary>
    public async Task<List<Tariff>> GetAllAsync()
    {
        return await _context.Set<Tariff>()
            .OrderByDescending(t => t.effective_start_date)
            .ThenBy(t => t.class_code)
            .ToListAsync();
    }

    /// <summary>
    /// Create new tariff.
    /// </summary>
    public async Task<Tariff> CreateAsync(Tariff tariff)
    {
        tariff.date_created = DateTime.Now;
        _context.Set<Tariff>().Add(tariff);
        await _context.SaveChangesAsync();
        return tariff;
    }

    /// <summary>
    /// Update existing tariff.
    /// </summary>
    public async Task<Tariff> UpdateAsync(Tariff tariff)
    {
        tariff.date_modified = DateTime.Now;
        _context.Set<Tariff>().Update(tariff);
        await _context.SaveChangesAsync();
        return tariff;
    }

    /// <summary>
    /// Delete tariff.
    /// </summary>
    public async Task DeleteAsync(int tariffCode)
    {
        var tariff = await GetByIdAsync(tariffCode);
        if (tariff != null)
        {
            _context.Set<Tariff>().Remove(tariff);
            await _context.SaveChangesAsync();
        }
    }
}
