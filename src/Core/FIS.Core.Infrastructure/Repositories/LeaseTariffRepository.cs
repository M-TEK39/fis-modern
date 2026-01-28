using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for LeaseTariff table.
/// Handles lease-specific monthly rates and excess kilometer charges.
/// </summary>
public class LeaseTariffRepository : ILeaseTariffRepository
{
    private readonly FisDbContext _context;

    public LeaseTariffRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get lease tariff for a vehicle effective on a specific date.
    /// Finds the active tariff where the date falls within the start/end date range.
    /// </summary>
    public async Task<LeaseTariff?> GetByVehicleAndDateAsync(int vmfCode, DateTime effectiveDate)
    {
        return await _context.Set<LeaseTariff>()
            .Where(lt => lt.vmf_code == vmfCode)
            .Where(lt => lt.active)
            .Where(lt => lt.start_date <= effectiveDate)
            .Where(lt => lt.end_date >= effectiveDate)
            .OrderByDescending(lt => lt.start_date)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all lease tariffs for a vehicle.
    /// </summary>
    public async Task<List<LeaseTariff>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<LeaseTariff>()
            .Where(lt => lt.vmf_code == vmfCode)
            .OrderByDescending(lt => lt.start_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get currently active lease tariff for a vehicle.
    /// </summary>
    public async Task<LeaseTariff?> GetActiveByVehicleAsync(int vmfCode)
    {
        var today = DateTime.Today;
        return await _context.Set<LeaseTariff>()
            .Where(lt => lt.vmf_code == vmfCode)
            .Where(lt => lt.active)
            .Where(lt => lt.start_date <= today)
            .Where(lt => lt.end_date >= today)
            .OrderByDescending(lt => lt.start_date)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get lease tariff by ID.
    /// </summary>
    public async Task<LeaseTariff?> GetByIdAsync(int leaseTariffCode)
    {
        return await _context.Set<LeaseTariff>()
            .FirstOrDefaultAsync(lt => lt.lease_tariff_code == leaseTariffCode);
    }

    /// <summary>
    /// Get all lease tariffs.
    /// </summary>
    public async Task<List<LeaseTariff>> GetAllAsync()
    {
        return await _context.Set<LeaseTariff>()
            .OrderByDescending(lt => lt.start_date)
            .ThenBy(lt => lt.vmf_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get all active lease tariffs.
    /// </summary>
    public async Task<List<LeaseTariff>> GetAllActiveAsync()
    {
        return await _context.Set<LeaseTariff>()
            .Where(lt => lt.active)
            .OrderBy(lt => lt.vmf_code)
            .ThenByDescending(lt => lt.start_date)
            .ToListAsync();
    }

    /// <summary>
    /// Create new lease tariff.
    /// </summary>
    public async Task<LeaseTariff> CreateAsync(LeaseTariff leaseTariff, int currentUserId)
    {
        leaseTariff.date_created = DateTime.Now;
        _context.Set<LeaseTariff>().Add(leaseTariff);
        await _context.SaveChangesAsync();
        return leaseTariff;
    }

    /// <summary>
    /// Update existing lease tariff.
    /// </summary>
    public async Task<LeaseTariff> UpdateAsync(LeaseTariff leaseTariff, int currentUserId)
    {
        if (leaseTariff == null)
            throw new ArgumentNullException(nameof(leaseTariff));

        var existing = await _context.Set<LeaseTariff>().FindAsync(leaseTariff.lease_tariff_code);
        if (existing == null)
            throw new InvalidOperationException($"LeaseTariff with lease_tariff_code {leaseTariff.lease_tariff_code} not found");

        leaseTariff.date_updated = DateTime.Now;
        _context.Entry(existing).CurrentValues.SetValues(leaseTariff);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete lease tariff.
    /// </summary>
    public async Task DeleteAsync(int leaseTariffCode, int currentUserId)
    {
        var leaseTariff = await GetByIdAsync(leaseTariffCode);
        if (leaseTariff != null)
        {
            _context.Set<LeaseTariff>().Remove(leaseTariff);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Deactivate a lease tariff.
    /// </summary>
    public async Task DeactivateAsync(int leaseTariffCode)
    {
        var leaseTariff = await GetByIdAsync(leaseTariffCode);
        if (leaseTariff != null)
        {
            leaseTariff.active = false;
            leaseTariff.date_updated = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}
