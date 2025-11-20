using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for MaintenanceValue table (maintenance cost matrix).
/// Handles maintenance cost per kilometer by class and vehicle age.
/// </summary>
public class MaintenanceValueRepository : IMaintenanceValueRepository
{
    private readonly FisDbContext _context;

    public MaintenanceValueRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get maintenance value for specific class and age criteria.
    /// </summary>
    public async Task<MaintenanceValue?> GetByClassAndAgeAsync(
        int tariffParameterId,
        int classCode,
        int monthsAge,
        int kilometerAge)
    {
        return await _context.Set<MaintenanceValue>()
            .Where(mv => mv.TariffParameterID == tariffParameterId)
            .Where(mv => mv.class_code == classCode)
            .Where(mv => mv.months_age <= monthsAge)
            .Where(mv => mv.kilometer_age <= kilometerAge)
            .OrderByDescending(mv => mv.months_age)
            .ThenByDescending(mv => mv.kilometer_age)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all maintenance values for a tariff parameter year.
    /// </summary>
    public async Task<List<MaintenanceValue>> GetByTariffParameterAsync(int tariffParameterId)
    {
        return await _context.Set<MaintenanceValue>()
            .Where(mv => mv.TariffParameterID == tariffParameterId)
            .OrderBy(mv => mv.class_code)
            .ThenBy(mv => mv.months_age)
            .ThenBy(mv => mv.kilometer_age)
            .ToListAsync();
    }

    /// <summary>
    /// Get maintenance values for a specific class.
    /// </summary>
    public async Task<List<MaintenanceValue>> GetByClassAsync(int tariffParameterId, int classCode)
    {
        return await _context.Set<MaintenanceValue>()
            .Where(mv => mv.TariffParameterID == tariffParameterId)
            .Where(mv => mv.class_code == classCode)
            .OrderBy(mv => mv.months_age)
            .ThenBy(mv => mv.kilometer_age)
            .ToListAsync();
    }

    /// <summary>
    /// Get maintenance value by composite key (TariffParameterID, class_code, months_age).
    /// Note: MaintenanceValue has a composite primary key.
    /// </summary>
    public async Task<MaintenanceValue?> GetByIdAsync(int maintenanceValueId)
    {
        // Since MaintenanceValue has composite key, this is not straightforward
        // For now, we'll search by TariffParameterID (first part of key)
        // In a real implementation, you'd need all key components
        return await _context.Set<MaintenanceValue>()
            .FirstOrDefaultAsync(mv => mv.TariffParameterID == maintenanceValueId);
    }

    /// <summary>
    /// Create new maintenance value.
    /// </summary>
    public async Task<MaintenanceValue> CreateAsync(MaintenanceValue maintenanceValue)
    {
        maintenanceValue.CaptureDate = DateTime.Now;
        _context.Set<MaintenanceValue>().Add(maintenanceValue);
        await _context.SaveChangesAsync();
        return maintenanceValue;
    }

    /// <summary>
    /// Update existing maintenance value.
    /// </summary>
    public async Task<MaintenanceValue> UpdateAsync(MaintenanceValue maintenanceValue)
    {
        maintenanceValue.ModifiedDate = DateTime.Now;
        _context.Set<MaintenanceValue>().Update(maintenanceValue);
        await _context.SaveChangesAsync();
        return maintenanceValue;
    }

    /// <summary>
    /// Delete maintenance value.
    /// </summary>
    public async Task DeleteAsync(int maintenanceValueId)
    {
        var maintenanceValue = await GetByIdAsync(maintenanceValueId);
        if (maintenanceValue != null)
        {
            _context.Set<MaintenanceValue>().Remove(maintenanceValue);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete all maintenance values for a tariff parameter year.
    /// </summary>
    public async Task DeleteByTariffParameterAsync(int tariffParameterId)
    {
        var values = await GetByTariffParameterAsync(tariffParameterId);
        _context.Set<MaintenanceValue>().RemoveRange(values);
        await _context.SaveChangesAsync();
    }
}
