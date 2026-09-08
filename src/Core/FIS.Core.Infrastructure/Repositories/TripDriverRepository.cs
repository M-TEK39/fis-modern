using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for TripDriver entity operations
/// </summary>
public class TripDriverRepository : ITripDriverRepository
{
    private readonly FisDbContext _context;

    public TripDriverRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<TripDriver?> GetByIdAsync(int tripDriverCode)
    {
        return await _context
            .TripDrivers.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(td => td.trip_driver_code == tripDriverCode);
    }

    public async Task<TripDriver?> GetByNameAsync(string tripDriverName)
    {
        return await _context
            .TripDrivers.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(td => td.trip_driver_name == tripDriverName);
    }

    public async Task<IEnumerable<TripDriver>> GetByTripAuthorityAsync(int tripAuthorityCode)
    {
        return await _context
            .TripDrivers.Where(td => td.trip_authority_code == tripAuthorityCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<TripDriver>> GetBySiteAsync(int siteCode)
    {
        return await _context.TripDrivers.Where(td => td.site_code == siteCode).ToListAsync();
    }

    public async Task<IEnumerable<TripDriver>> GetPrimaryDriversAsync()
    {
        return await _context.TripDrivers.Where(td => td.trip_driver_primary).ToListAsync();
    }

    public async Task<IEnumerable<TripDriver>> GetActiveDriversAsync()
    {
        return await _context.TripDrivers.Where(td => td.driver_active).ToListAsync();
    }

    public async Task<IEnumerable<TripDriver>> GetByLicenseTypeAsync(int licenseTypeId)
    {
        return await _context
            .TripDrivers.Where(td => td.driver_licence_type_id == licenseTypeId)
            .ToListAsync();
    }

    public async Task<TripDriver> CreateAsync(TripDriver tripDriver, int currentUserId)
    {
        // Auto-populate audit fields
        tripDriver.date_created = DateTime.UtcNow;
        tripDriver.is_deleted = false;

        _context.TripDrivers.Add(tripDriver);
        await _context.SaveChangesAsync();
        return tripDriver;
    }

    public async Task UpdateAsync(TripDriver tripDriver, int currentUserId)
    {
        if (tripDriver == null)
            throw new ArgumentNullException(nameof(tripDriver));

        var existing = await _context.TripDrivers.FindAsync(tripDriver.trip_driver_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"TripDriver with trip_driver_code {tripDriver.trip_driver_code} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(tripDriver);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int tripDriverCode, int currentUserId)
    {
        var tripDriver = await GetByIdAsync(tripDriverCode);
        if (tripDriver != null)
        {
            // Soft delete instead of hard delete
            tripDriver.is_deleted = true;
            tripDriver.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<TripDriver>> SearchDriversAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await _context.TripDrivers.ToListAsync();

        return await _context
            .TripDrivers.Where(td =>
                (td.trip_driver_name != null && td.trip_driver_name.Contains(searchTerm))
                || (td.trip_driver_id != null && td.trip_driver_id.Contains(searchTerm))
                || (
                    td.driver_licence_number != null
                    && td.driver_licence_number.Contains(searchTerm)
                )
            )
            .ToListAsync();
    }
}
