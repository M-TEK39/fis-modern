using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Location entity operations
/// </summary>
public class LocationRepository : ILocationRepository
{
    private readonly FisDbContext _context;

    public LocationRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<Location?> GetByIdAsync(int locationId)
    {
        return await _context
            .Locations.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(l => l.LocationId == locationId);
    }

    public async Task<Location?> GetByNameAsync(string locationName)
    {
        return await _context
            .Locations.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(l => l.LocationName == locationName);
    }

    public async Task<IEnumerable<Location>> GetAllLocationsAsync()
    {
        return await _context
            .Locations.Where(l => l.IsActive)
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetByCountryAsync(string country)
    {
        return await _context
            .Locations.Where(l => l.IsActive && l.Country == country)
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Location>> GetByProvinceAsync(string province)
    {
        return await _context
            .Locations.Where(l => l.IsActive && l.Province == province)
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }

    public async Task<Location> CreateAsync(Location location, int currentUserId)
    {
        location.CreatedDate = DateTime.UtcNow;
        // Auto-populate audit fields
        location.date_created = DateTime.UtcNow;
        location.is_deleted = false;

        _context.Locations.Add(location);
        await _context.SaveChangesAsync();
        return location;
    }

    public async Task UpdateAsync(Location location, int currentUserId)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        var existing = await _context.Locations.FindAsync(location.LocationId);
        if (existing == null)
            throw new InvalidOperationException(
                $"Location with LocationId {location.LocationId} not found"
            );

        location.ModifiedDate = DateTime.UtcNow;
        _context.Entry(existing).CurrentValues.SetValues(location);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int locationId, int currentUserId)
    {
        var location = await GetByIdAsync(locationId);
        if (location != null)
        {
            // Soft delete - mark as inactive
            location.IsActive = false;
            location.ModifiedDate = DateTime.UtcNow;
            await UpdateAsync(location, currentUserId);
        }
    }

    public async Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllLocationsAsync();

        return await _context
            .Locations.Where(l =>
                l.IsActive
                && (
                    l.LocationName.Contains(searchTerm)
                    || (l.Description != null && l.Description.Contains(searchTerm))
                    || (l.City != null && l.City.Contains(searchTerm))
                    || (l.Province != null && l.Province.Contains(searchTerm))
                    || (l.Country != null && l.Country.Contains(searchTerm))
                )
            )
            .OrderBy(l => l.LocationName)
            .ToListAsync();
    }
}
