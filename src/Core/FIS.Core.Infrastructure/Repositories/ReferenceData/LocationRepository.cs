using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        // dbo.location in the restored legacy database only has
        // location_code and description. Do not route a vehicle-capture
        // selector through the expanded EF entity, which projects modern
        // audit/address columns that do not exist there.
        return await QueryLegacyLocationsAsync();
    }

    private async Task<List<Location>> QueryLegacyLocationsAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [location_code], [description]
                FROM [dbo].[location]
                ORDER BY [description], [location_code]
                """;

            var locations = new List<Location>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var locationCode = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                var description = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                if (locationCode <= 0 || string.IsNullOrWhiteSpace(description))
                {
                    continue;
                }

                locations.Add(
                    new Location
                    {
                        LocationId = locationCode,
                        LocationName = description,
                        Description = description,
                        IsActive = true,
                    }
                );
            }

            return locations;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<LocationPage> GetPageAsync(int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var activeLocations = _context.Locations.Where(l => l.IsActive).AsNoTracking();
        var total = await activeLocations.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);

        var items = await activeLocations
            .OrderBy(l => l.LocationName)
            .ThenBy(l => l.LocationId)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .ToListAsync();

        return new LocationPage(items, page, pageSize, total);
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
            .ThenBy(l => l.LocationId)
            .ToListAsync();
    }
}
