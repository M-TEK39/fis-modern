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

        return await _context.Drivers.FirstOrDefaultAsync(d => d.site_driver_code == driverCode);
    }

    /// <summary>
    /// Get driver by licence number
    /// </summary>
    public async Task<Driver?> GetByLicenceNumberAsync(string licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
            return null;

        return await _context.Drivers
            .FirstOrDefaultAsync(d => d.driver_licence_number == licenceNumber);
    }

    /// <summary>
    /// Get all active drivers
    /// </summary>
    public async Task<IEnumerable<Driver>> GetActiveDriversAsync()
    {
        return await _context.Drivers
            .Where(d => d.driver_active)
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
        return await _context.Drivers
            .Where(d => d.driver_surname != null && d.driver_surname.ToLower().Contains(search) ||
                       d.driver_firstname != null && d.driver_firstname.ToLower().Contains(search) ||
                       d.driver_licence_number != null && d.driver_licence_number.ToLower().Contains(search) ||
                       d.driver_persalnumber != null && d.driver_persalnumber.ToLower().Contains(search) ||
                       d.driver_SA_id != null && d.driver_SA_id.ToLower().Contains(search))
            .OrderBy(d => d.driver_surname)
            .ThenBy(d => d.driver_firstname)
            .ToListAsync();
    }

    /// <summary>
    /// Create new driver
    /// </summary>
    public async Task<Driver> CreateAsync(Driver driver)
    {
        if (driver == null)
            throw new ArgumentNullException(nameof(driver));

        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();
        
        return driver;
    }

    /// <summary>
    /// Update existing driver
    /// </summary>
    public async Task UpdateAsync(Driver driver)
    {
        if (driver == null)
            throw new ArgumentNullException(nameof(driver));

        _context.Entry(driver).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete driver by ID
    /// Note: Interface expects string but entity uses int - convert accordingly
    /// </summary>
    public async Task DeleteAsync(string driverId)
    {
        if (!int.TryParse(driverId, out int driverCode))
            return;

        var driver = await _context.Drivers.FindAsync(driverCode);
        if (driver != null)
        {
            _context.Drivers.Remove(driver);
            await _context.SaveChangesAsync();
        }
    }
}