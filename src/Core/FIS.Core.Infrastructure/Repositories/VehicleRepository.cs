using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for vehicle operations against legacy vehicle_master table
/// Provides full CRUD operations with EF Core integration
/// </summary>
public class VehicleRepository : IVehicleRepository
{
    private readonly FisDbContext _context;

    public VehicleRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get vehicle by VMF code (primary key)
    /// </summary>
    public async Task<Vehicle?> GetByIdAsync(int vmfCode)
    {
        return await _context.Vehicles
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(v => v.vmf_code == vmfCode);
    }

    /// <summary>
    /// Get vehicle by fleet number
    /// </summary>
    public async Task<Vehicle?> GetByFleetNumberAsync(string fleetNumber)
    {
        if (string.IsNullOrWhiteSpace(fleetNumber))
            return null;

        return await _context.Vehicles
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(v => v.fleet_number == fleetNumber);
    }

    /// <summary>
    /// Get vehicle by registration number
    /// </summary>
    public async Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
            return null;

        return await _context.Vehicles
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(v => v.registration_number == registrationNumber);
    }

    /// <summary>
    /// Get all active vehicles (not deleted/inactive)
    /// Uses legacy status code field - for now return all vehicles
    /// In full implementation, this would check vehicle_status_code against active status codes
    /// </summary>
    public async Task<IEnumerable<Vehicle>> GetActiveVehiclesAsync()
    {
        return await _context.Vehicles
            .Where(v => v.vehicle_status_code > 0) // Simple filter - active status codes are typically > 0
            .OrderBy(v => v.fleet_number)
            .ToListAsync();
    }

    /// <summary>
    /// Get available vehicles (active and not currently hired)
    /// This would typically check contract status in a full implementation
    /// </summary>
    public async Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync()
    {
        // For now, return active vehicles - in full implementation,
        // this would cross-reference with active contracts
        return await GetActiveVehiclesAsync();
    }

    /// <summary>
    /// Get all vehicles including inactive ones
    /// </summary>
    public async Task<IEnumerable<Vehicle>> GetAllAsync()
    {
        return await _context.Vehicles
            .Include(v => v.Model)
            .ToListAsync();
    }

    /// <summary>
    /// Search vehicles by multiple criteria
    /// Searches fleet number, registration, etc.
    /// Note: Legacy system uses codes for make/model, so we search basic fields only
    /// </summary>
    public async Task<IEnumerable<Vehicle>> SearchVehiclesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetActiveVehiclesAsync();

        var term = searchTerm.ToLower().Trim();

        return await _context.Vehicles
            .Where(v => 
                (v.fleet_number != null && v.fleet_number.ToLower().Contains(term)) ||
                (v.registration_number != null && v.registration_number.ToLower().Contains(term)) ||
                (v.chassis_number != null && v.chassis_number.ToLower().Contains(term)) ||
                (v.engine_number_1 != null && v.engine_number_1.ToLower().Contains(term))
            )
            .OrderBy(v => v.fleet_number)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new vehicle
    /// </summary>
    public async Task<Vehicle> CreateAsync(Vehicle vehicle, int currentUserId)
    {
        if (vehicle == null)
            throw new ArgumentNullException(nameof(vehicle));

        // Auto-populate audit fields
            vehicle.date_created = DateTime.UtcNow;
            vehicle.is_deleted = false;
            
            _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();
        return vehicle;
    }

    /// <summary>
    /// Update an existing vehicle
    /// </summary>
    public async Task UpdateAsync(Vehicle vehicle, int currentUserId)
    {
        if (vehicle == null)
            throw new ArgumentNullException(nameof(vehicle));

        var existing = await _context.Vehicles.FindAsync(vehicle.vmf_code);
        if (existing == null)
            throw new InvalidOperationException($"Vehicle with vmf_code {vehicle.vmf_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(vehicle);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete a vehicle by VMF code
    /// In legacy systems, this might be a soft delete (status change)
    /// rather than hard delete
    /// </summary>
    public async Task DeleteAsync(int vmfCode, int currentUserId)
    {
        var vehicle = await GetByIdAsync(vmfCode);
        if (vehicle != null)
        {
            // Legacy systems often use soft delete
            // For now, we'll do hard delete, but this could be changed
            // to vehicle.status = "DELETED" for soft delete
            // Soft delete instead of hard delete
                vehicle.is_deleted = true;
                vehicle.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}