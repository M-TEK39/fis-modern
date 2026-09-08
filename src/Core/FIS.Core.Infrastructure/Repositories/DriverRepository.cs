using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for driver operations against legacy site_drivers table
/// Handles fleet driver management and license tracking
/// </summary>
public class DriverRepository : IDriverRepository
{
    private readonly FisDbContext _context;

    public DriverRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get driver by ID (site_driver_code)
    /// Note: Interface expects string but entity uses int - convert accordingly
    /// </summary>
    public async Task<Driver?> GetByIdAsync(string driverId)
    {
        if (!int.TryParse(driverId, out int driverCode))
            return null;

        return await _context
            .Drivers.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(d => d.site_driver_code == driverCode);
    }

    /// <summary>
    /// Get driver by licence number
    /// </summary>
    public async Task<Driver?> GetByLicenceNumberAsync(string licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
            return null;

        return await _context
            .Drivers.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(d => d.driver_licence_number == licenceNumber);
    }

    /// <summary>
    /// Get all active drivers
    /// </summary>
    public async Task<IEnumerable<Driver>> GetActiveDriversAsync()
    {
        return await _context
            .Drivers.Where(d => d.driver_active && !d.is_deleted)
            .OrderBy(d => d.driver_surname)
            .ThenBy(d => d.driver_firstname)
            .ToListAsync();
    }

    /// <summary>
    /// Search drivers by surname, firstname, licence number, or personal number
    /// </summary>
    public async Task<IEnumerable<Driver>> SearchDriversAsync(string searchTerm)
    {
        if (string.IsNullOrEmpty(searchTerm))
            return await GetActiveDriversAsync();

        var search = searchTerm.ToLower();
        return await _context
            .Drivers.Where(d =>
                !d.is_deleted
                && (
                    (d.driver_surname != null && d.driver_surname.ToLower().Contains(search))
                    || (d.driver_firstname != null && d.driver_firstname.ToLower().Contains(search))
                    || (
                        d.driver_licence_number != null
                        && d.driver_licence_number.ToLower().Contains(search)
                    )
                    || (
                        d.driver_persalnumber != null
                        && d.driver_persalnumber.ToLower().Contains(search)
                    )
                    || (d.driver_SA_id != null && d.driver_SA_id.ToLower().Contains(search))
                )
            )
            .OrderBy(d => d.driver_surname)
            .ThenBy(d => d.driver_firstname)
            .ToListAsync();
    }

    /// <summary>
    /// Create new driver
    /// </summary>
    public async Task<Driver> CreateAsync(Driver driver, int currentUserId)
    {
        if (driver == null)
            throw new ArgumentNullException(nameof(driver));

        // Auto-populate audit fields
        driver.date_created = DateTime.UtcNow;
        driver.is_deleted = false;

        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();

        return driver;
    }

    /// <summary>
    /// Update existing driver
    /// </summary>
    public async Task UpdateAsync(Driver driver, int currentUserId)
    {
        if (driver == null)
            throw new ArgumentNullException(nameof(driver));

        var existing = await _context.Drivers.FindAsync(driver.site_driver_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"Driver with site_driver_code {driver.site_driver_code} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(driver);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete driver by ID
    /// Note: Interface expects string but entity uses int - convert accordingly
    /// </summary>
    public async Task DeleteAsync(string driverId, int currentUserId)
    {
        if (!int.TryParse(driverId, out int driverCode))
            return;

        var driver = await _context.Drivers.FindAsync(driverCode);
        if (driver != null)
        {
            // Soft delete instead of hard delete
            driver.is_deleted = true;
            driver.driver_active = false;
            driver.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
