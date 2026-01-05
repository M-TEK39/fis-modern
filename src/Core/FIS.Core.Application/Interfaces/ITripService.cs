using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Trip Service interface for trip authority business operations
/// Maps legacy GGFleet trip authority functionality
/// Handles trip authorization, validation, driver assignments, and expiry management
/// </summary>
public interface ITripService
{
    /// <summary>
    /// Create a new trip authority
    /// Legacy: Trip authority creation with validation
    /// Validates contract status, odometer readings, and authorization requirements
    /// </summary>
    Task<Trip> CreateTripAsync(Trip trip);

    /// <summary>
    /// Update an existing trip authority
    /// Legacy: Trip authority modification
    /// Validates that trip is not locked for transfer
    /// </summary>
    Task UpdateTripAsync(Trip trip);

    /// <summary>
    /// Get trip authority by ID
    /// </summary>
    Task<Trip?> GetTripByIdAsync(int tripAuthorityCode);

    /// <summary>
    /// Get all trips for a vehicle (via contract)
    /// </summary>
    Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode);

    /// <summary>
    /// Get all trips for a driver
    /// </summary>
    Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId);

    /// <summary>
    /// Get trips by date range
    /// </summary>
    Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Check if a contract has open trip authorities
    /// Legacy: NEW_DEV_VAL_OpenTripAuthority stored procedure
    /// Used for contract validation - contracts with open trips cannot be closed
    /// </summary>
    Task<bool> HasOpenTripAuthoritiesAsync(int contractCode);

    /// <summary>
    /// Get open (non-expired) trip authorities for a contract
    /// Legacy: Used in contract close validation
    /// </summary>
    Task<IEnumerable<Trip>> GetOpenTripAuthoritiesAsync(int contractCode);

    /// <summary>
    /// Check if a trip is expired
    /// Legacy: Trip expiry validation logic
    /// </summary>
    bool IsTripExpired(Trip trip);

    /// <summary>
    /// Get all expired trips
    /// Used for reporting and cleanup
    /// </summary>
    Task<IEnumerable<Trip>> GetExpiredTripsAsync();

    /// <summary>
    /// Get monthly trips
    /// Legacy: Monthly trip filtering
    /// </summary>
    Task<IEnumerable<Trip>> GetMonthlyTripsAsync();

    /// <summary>
    /// Validate trip authority creation
    /// Checks contract status, vehicle availability, authorization requirements
    /// </summary>
    Task<bool> ValidateTripCreationAsync(Trip trip);

    /// <summary>
    /// Lock trip for transfer
    /// Legacy: locked_for_transfer flag management
    /// </summary>
    Task LockTripForTransferAsync(int tripAuthorityCode);

    /// <summary>
    /// Unlock trip from transfer
    /// </summary>
    Task UnlockTripFromTransferAsync(int tripAuthorityCode);

    /// <summary>
    /// Delete trip authority
    /// Validates that trip can be deleted (not locked, no associated records)
    /// </summary>
    Task DeleteTripAsync(int tripAuthorityCode);

    /// <summary>
    /// Extend trip expiry date
    /// Legacy: Trip extension functionality
    /// </summary>
    Task ExtendTripExpiryAsync(int tripAuthorityCode, DateTime newExpiryDate);

    /// <summary>
    /// Calculate trip duration in days
    /// </summary>
    int CalculateTripDuration(DateTime issueDate, DateTime? expiryDate);

    /// <summary>
    /// Check if odometer reading is valid for the trip
    /// Validates against contract odometer ranges
    /// </summary>
    Task<bool> ValidateOdometerReadingAsync(int tripAuthorityCode, int odometerReading);
}
