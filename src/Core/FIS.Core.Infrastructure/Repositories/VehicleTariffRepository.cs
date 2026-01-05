using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for VehicleTariff table (modern fin.vehicle_tariff, post-2009 system).
/// Handles calculated vehicle-specific tariffs with all cost components.
/// </summary>
public class VehicleTariffRepository : IVehicleTariffRepository
{
    private readonly FisDbContext _context;

    public VehicleTariffRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get vehicle tariff for a specific vehicle and parameter year.
    /// </summary>
    public async Task<VehicleTariff?> GetByVehicleAndYearAsync(int vmfCode, short parameterYear)
    {
        return await _context.Set<VehicleTariff>()
            .FirstOrDefaultAsync(vt => vt.vmf_code == vmfCode && vt.parameter_year == parameterYear);
    }

    /// <summary>
    /// Get vehicle tariff for a specific vehicle and effective date.
    /// Finds the tariff where the date falls within the start/end date range.
    /// </summary>
    public async Task<VehicleTariff?> GetByVehicleAndDateAsync(int vmfCode, DateTime effectiveDate)
    {
        return await _context.Set<VehicleTariff>()
            .Where(vt => vt.vmf_code == vmfCode)
            .Where(vt => vt.start_date <= effectiveDate)
            .Where(vt => vt.end_date == null || vt.end_date >= effectiveDate)
            .OrderByDescending(vt => vt.start_date)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all vehicle tariffs for a parameter year.
    /// </summary>
    public async Task<List<VehicleTariff>> GetByParameterYearAsync(short parameterYear)
    {
        return await _context.Set<VehicleTariff>()
            .Where(vt => vt.parameter_year == parameterYear)
            .OrderBy(vt => vt.vmf_code)
            .ToListAsync();
    }

    /// <summary>
    /// Get vehicle tariff by ID.
    /// </summary>
    public async Task<VehicleTariff?> GetByIdAsync(int vehicleTariffCode)
    {
        return await _context.Set<VehicleTariff>()
            .FirstOrDefaultAsync(vt => vt.vehicle_tariff_code == vehicleTariffCode);
    }

    /// <summary>
    /// Get all vehicle tariffs for a specific vehicle.
    /// </summary>
    public async Task<List<VehicleTariff>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<VehicleTariff>()
            .Where(vt => vt.vmf_code == vmfCode)
            .OrderByDescending(vt => vt.parameter_year)
            .ToListAsync();
    }

    /// <summary>
    /// Create new vehicle tariff.
    /// </summary>
    public async Task<VehicleTariff> CreateAsync(VehicleTariff vehicleTariff)
    {
        vehicleTariff.calculation_date = DateTime.Now;
        _context.Set<VehicleTariff>().Add(vehicleTariff);
        await _context.SaveChangesAsync();
        return vehicleTariff;
    }

    /// <summary>
    /// Update existing vehicle tariff.
    /// </summary>
    public async Task<VehicleTariff> UpdateAsync(VehicleTariff vehicleTariff)
    {
        vehicleTariff.calculation_date = DateTime.Now;
        _context.Set<VehicleTariff>().Update(vehicleTariff);
        await _context.SaveChangesAsync();
        return vehicleTariff;
    }

    /// <summary>
    /// Delete vehicle tariff.
    /// </summary>
    public async Task DeleteAsync(int vehicleTariffCode)
    {
        var vehicleTariff = await GetByIdAsync(vehicleTariffCode);
        if (vehicleTariff != null)
        {
            _context.Set<VehicleTariff>().Remove(vehicleTariff);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete all vehicle tariffs for a parameter year.
    /// Used when recalculating tariffs for a year.
    /// </summary>
    public async Task DeleteByParameterYearAsync(short parameterYear)
    {
        var tariffs = await GetByParameterYearAsync(parameterYear);
        _context.Set<VehicleTariff>().RemoveRange(tariffs);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Check if vehicle has tariff for a parameter year.
    /// </summary>
    public async Task<bool> ExistsAsync(int vmfCode, short parameterYear)
    {
        return await _context.Set<VehicleTariff>()
            .AnyAsync(vt => vt.vmf_code == vmfCode && vt.parameter_year == parameterYear);
    }
}
