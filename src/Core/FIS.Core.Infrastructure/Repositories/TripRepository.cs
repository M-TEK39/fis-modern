using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for trip operations against legacy trip_authorities table
/// Handles fleet trip management and authorization tracking
/// </summary>
public class TripRepository : ITripRepository
{
    private readonly FisDbContext _context;

    public TripRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get trip by trip authority code (primary key)
    /// </summary>
    public async Task<Trip?> GetByIdAsync(int tripId)
    {
        return await _context.Trips
            .Include(t => t.Contract)
            .FirstOrDefaultAsync(t => t.trip_authority_code == tripId);
    }

    /// <summary>
    /// Get all trips
    /// </summary>
    public async Task<IEnumerable<Trip>> GetAllAsync()
    {
        return await _context.Trips
            .Include(t => t.Contract)
            .OrderByDescending(t => t.issue_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get trips by contract code
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByContractAsync(int contractCode)
    {
        return await _context.Trips
            .Include(t => t.Contract)
            .Where(t => t.contract_code == contractCode)
            .OrderByDescending(t => t.issue_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get trips by vehicle (contract code)
    /// Note: Interface expects vmfCode but trips are linked via contract_code
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode)
    {
        // We need to find trips by getting contracts first that match the vehicle
        var contractCodes = await _context.Contracts
            .Where(c => c.vmf_code == vmfCode)
            .Select(c => c.contract_code)
            .ToListAsync();

        return await _context.Trips
            .Include(t => t.Contract)
            .Where(t => contractCodes.Contains(t.contract_code))
            .OrderByDescending(t => t.issue_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get trips by driver ID
    /// Note: Interface expects string but we need to find trips through contracts
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId)
    {
        if (!int.TryParse(driverId, out int driverCode))
            return new List<Trip>();

        // Find contracts that have the driver assigned
        var contractCodes = await _context.Contracts
            .Where(c => c.site_driver_code == driverCode)
            .Select(c => c.contract_code)
            .ToListAsync();

        return await _context.Trips
            .Include(t => t.Contract)
            .Where(t => contractCodes.Contains(t.contract_code))
            .OrderByDescending(t => t.issue_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get trips by date range (issue date)
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Trips
            .Include(t => t.Contract)
            .Where(t => t.issue_date >= startDate && t.issue_date <= endDate)
            .OrderByDescending(t => t.issue_date)
            .ToListAsync();
    }

    /// <summary>
    /// Create new trip
    /// </summary>
    public async Task<Trip> CreateAsync(Trip trip)
    {
        if (trip == null)
            throw new ArgumentNullException(nameof(trip));

        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
        
        return trip;
    }

    /// <summary>
    /// Update existing trip
    /// </summary>
    public async Task UpdateAsync(Trip trip)
    {
        if (trip == null)
            throw new ArgumentNullException(nameof(trip));

        _context.Entry(trip).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete trip by ID
    /// </summary>
    public async Task DeleteAsync(int tripId)
    {
        var trip = await _context.Trips.FindAsync(tripId);
        if (trip != null)
        {
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }
    }
}