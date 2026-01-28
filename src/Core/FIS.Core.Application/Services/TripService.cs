using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services;

/// <summary>
/// Trip Service implementation
/// Maps legacy GGFleet trip authority business logic
/// Handles trip authorization, validation, driver assignments, and expiry management
/// </summary>
public class TripService : ITripService
{
    private readonly ITripRepository _tripRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<TripService> _logger;

    public TripService(
        ITripRepository tripRepository,
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        ILogger<TripService> logger)
    {
        _tripRepository = tripRepository ?? throw new ArgumentNullException(nameof(tripRepository));
        _contractRepository = contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _vehicleRepository = vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new trip authority
    /// Legacy: Trip authority creation with validation
    /// </summary>
    public async Task<Trip> CreateTripAsync(Trip trip)
    {
        try
        {
            _logger.LogInformation("Creating trip authority for contract: {ContractCode}", trip.contract_code);

            // Validate trip creation
            var isValid = await ValidateTripCreationAsync(trip);
            if (!isValid)
            {
                throw new InvalidOperationException("Trip validation failed");
            }

            // Set defaults
            trip.issue_date = DateTime.Now;
            trip.locked_for_transfer = false;

            // If no expiry date set and it's monthly, set expiry to end of month
            if (!trip.expiry_date.HasValue && trip.Trip_Is_Monthly)
            {
                trip.expiry_date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
            }

            var createdTrip = await _tripRepository.CreateAsync(trip, 1); // TODO: Pass actual user ID from JWT

            _logger.LogInformation("Trip authority created: {TripAuthorityCode} for contract {ContractCode}",
                createdTrip.trip_authority_code, createdTrip.contract_code);

            return createdTrip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip authority for contract: {ContractCode}", trip.contract_code);
            throw;
        }
    }

    /// <summary>
    /// Update an existing trip authority
    /// Legacy: Trip authority modification
    /// </summary>
    public async Task UpdateTripAsync(Trip trip)
    {
        try
        {
            _logger.LogInformation("Updating trip authority: {TripAuthorityCode}", trip.trip_authority_code);

            // Check if trip is locked
            var existingTrip = await _tripRepository.GetByIdAsync(trip.trip_authority_code);
            if (existingTrip == null)
            {
                throw new InvalidOperationException($"Trip {trip.trip_authority_code} not found");
            }

            if (existingTrip.locked_for_transfer)
            {
                _logger.LogWarning("Cannot update trip {TripAuthorityCode} - locked for transfer", trip.trip_authority_code);
                throw new InvalidOperationException($"Trip {trip.trip_authority_code} is locked for transfer and cannot be modified");
            }

            await _tripRepository.UpdateAsync(trip, 1); // TODO: Pass actual user ID from JWT

            _logger.LogInformation("Trip authority updated: {TripAuthorityCode}", trip.trip_authority_code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trip authority: {TripAuthorityCode}", trip.trip_authority_code);
            throw;
        }
    }

    /// <summary>
    /// Get trip authority by ID
    /// </summary>
    public async Task<Trip?> GetTripByIdAsync(int tripAuthorityCode)
    {
        return await _tripRepository.GetByIdAsync(tripAuthorityCode);
    }

    /// <summary>
    /// Get all trips for a vehicle (via contract)
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode)
    {
        return await _tripRepository.GetTripsByVehicleAsync(vmfCode);
    }

    /// <summary>
    /// Get all trips for a driver
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId)
    {
        return await _tripRepository.GetTripsByDriverAsync(driverId);
    }

    /// <summary>
    /// Get trips by date range
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _tripRepository.GetTripsByDateRangeAsync(startDate, endDate);
    }

    /// <summary>
    /// Check if a contract has open trip authorities
    /// Legacy: NEW_DEV_VAL_OpenTripAuthority stored procedure
    /// Used for contract validation - contracts with open trips cannot be closed
    /// </summary>
    public async Task<bool> HasOpenTripAuthoritiesAsync(int contractCode)
    {
        try
        {
            _logger.LogInformation("Checking for open trip authorities on contract: {ContractCode}", contractCode);

            var trips = await _tripRepository.GetTripsByContractAsync(contractCode);

            // Check if any trips have expiry_date > DateTime.Now
            var hasOpenTrips = trips.Any(t => t.expiry_date.HasValue && t.expiry_date.Value > DateTime.Now);

            _logger.LogInformation("Contract {ContractCode} has open trips: {HasOpenTrips}", contractCode, hasOpenTrips);

            return hasOpenTrips;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking open trip authorities for contract: {ContractCode}", contractCode);
            throw;
        }
    }

    /// <summary>
    /// Get open (non-expired) trip authorities for a contract
    /// Legacy: Used in contract close validation
    /// </summary>
    public async Task<IEnumerable<Trip>> GetOpenTripAuthoritiesAsync(int contractCode)
    {
        try
        {
            var trips = await _tripRepository.GetTripsByContractAsync(contractCode);

            // Filter for open trips (expiry_date > DateTime.Now)
            return trips.Where(t => t.expiry_date.HasValue && t.expiry_date.Value > DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting open trip authorities for contract: {ContractCode}", contractCode);
            throw;
        }
    }

    /// <summary>
    /// Check if a trip is expired
    /// Legacy: Trip expiry validation logic
    /// </summary>
    public bool IsTripExpired(Trip trip)
    {
        if (!trip.expiry_date.HasValue)
        {
            return false; // No expiry date means never expires
        }

        return trip.expiry_date.Value < DateTime.Now;
    }

    /// <summary>
    /// Get all expired trips
    /// Used for reporting and cleanup
    /// </summary>
    public async Task<IEnumerable<Trip>> GetExpiredTripsAsync()
    {
        try
        {
            var allTrips = await _tripRepository.GetAllAsync();

            // Filter for expired trips
            return allTrips.Where(t => t.expiry_date.HasValue && t.expiry_date.Value < DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expired trips");
            throw;
        }
    }

    /// <summary>
    /// Get monthly trips
    /// Legacy: Monthly trip filtering
    /// </summary>
    public async Task<IEnumerable<Trip>> GetMonthlyTripsAsync()
    {
        try
        {
            var allTrips = await _tripRepository.GetAllAsync();
            return allTrips.Where(t => t.Trip_Is_Monthly);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting monthly trips");
            throw;
        }
    }

    /// <summary>
    /// Validate trip authority creation
    /// Checks contract status, vehicle availability, authorization requirements
    /// </summary>
    public async Task<bool> ValidateTripCreationAsync(Trip trip)
    {
        try
        {
            _logger.LogInformation("Validating trip creation for contract: {ContractCode}", trip.contract_code);

            // 1. Validate contract exists and is active
            var contract = await _contractRepository.GetByIdAsync(trip.contract_code);
            if (contract == null)
            {
                _logger.LogWarning("Contract {ContractCode} not found", trip.contract_code);
                return false;
            }

            // 2. Check if contract is still current
            if (contract.still_current != "Y")
            {
                _logger.LogWarning("Contract {ContractCode} is not current", trip.contract_code);
                return false;
            }

            // 3. Validate trip type code is provided
            if (trip.trip_type_code <= 0)
            {
                _logger.LogWarning("Invalid trip type code");
                return false;
            }

            // 4. Validate trip incident type code is provided
            if (trip.trip_incident_type_code <= 0)
            {
                _logger.LogWarning("Invalid trip incident type code");
                return false;
            }

            // 5. Validate expiry date if provided
            if (trip.expiry_date.HasValue && trip.expiry_date.Value < DateTime.Now)
            {
                _logger.LogWarning("Trip expiry date is in the past");
                return false;
            }

            // 6. Validate approver information is provided
            if (string.IsNullOrWhiteSpace(trip.approver_name))
            {
                _logger.LogWarning("Approver name is required");
                return false;
            }

            _logger.LogInformation("Trip validation passed for contract: {ContractCode}", trip.contract_code);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating trip creation for contract: {ContractCode}", trip.contract_code);
            return false;
        }
    }

    /// <summary>
    /// Lock trip for transfer
    /// Legacy: locked_for_transfer flag management
    /// </summary>
    public async Task LockTripForTransferAsync(int tripAuthorityCode)
    {
        try
        {
            _logger.LogInformation("Locking trip {TripAuthorityCode} for transfer", tripAuthorityCode);

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            trip.locked_for_transfer = true;
            await _tripRepository.UpdateAsync(trip, 1); // TODO: Pass actual user ID from JWT

            _logger.LogInformation("Trip {TripAuthorityCode} locked for transfer", tripAuthorityCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking trip {TripAuthorityCode} for transfer", tripAuthorityCode);
            throw;
        }
    }

    /// <summary>
    /// Unlock trip from transfer
    /// </summary>
    public async Task UnlockTripFromTransferAsync(int tripAuthorityCode)
    {
        try
        {
            _logger.LogInformation("Unlocking trip {TripAuthorityCode} from transfer", tripAuthorityCode);

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            trip.locked_for_transfer = false;
            await _tripRepository.UpdateAsync(trip, 1); // TODO: Pass actual user ID from JWT

            _logger.LogInformation("Trip {TripAuthorityCode} unlocked from transfer", tripAuthorityCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking trip {TripAuthorityCode} from transfer", tripAuthorityCode);
            throw;
        }
    }

    /// <summary>
    /// Delete trip authority
    /// Validates that trip can be deleted (not locked, no associated records)
    /// </summary>
    public async Task DeleteTripAsync(int tripAuthorityCode)
    {
        try
        {
            _logger.LogInformation("Deleting trip authority: {TripAuthorityCode}", tripAuthorityCode);

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            // Check if trip is locked
            if (trip.locked_for_transfer)
            {
                _logger.LogWarning("Cannot delete trip {TripAuthorityCode} - locked for transfer", tripAuthorityCode);
                throw new InvalidOperationException($"Trip {tripAuthorityCode} is locked for transfer and cannot be deleted");
            }

            await _tripRepository.DeleteAsync(tripAuthorityCode, 1); // TODO: Pass actual user ID from JWT

            _logger.LogInformation("Trip authority deleted: {TripAuthorityCode}", tripAuthorityCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trip authority: {TripAuthorityCode}", tripAuthorityCode);
            throw;
        }
    }

    /// <summary>
    /// Extend trip expiry date
    /// Legacy: Trip extension functionality
    /// </summary>
    public async Task ExtendTripExpiryAsync(int tripAuthorityCode, DateTime newExpiryDate)
    {
        try
        {
            _logger.LogInformation("Extending trip {TripAuthorityCode} expiry to {NewExpiryDate}",
                tripAuthorityCode, newExpiryDate);

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            // Check if trip is locked
            if (trip.locked_for_transfer)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} is locked for transfer and cannot be extended");
            }

            // Validate new expiry date is in the future
            if (newExpiryDate < DateTime.Now)
            {
                throw new ArgumentException("New expiry date must be in the future", nameof(newExpiryDate));
            }

            // Validate new expiry date is after current expiry date (if set)
            if (trip.expiry_date.HasValue && newExpiryDate < trip.expiry_date.Value)
            {
                throw new ArgumentException("New expiry date must be after current expiry date", nameof(newExpiryDate));
            }

            trip.expiry_date = newExpiryDate;
            await _tripRepository.UpdateAsync(trip, 1); // TODO: Pass actual user ID from JWT

            _logger.LogInformation("Trip {TripAuthorityCode} expiry extended to {NewExpiryDate}",
                tripAuthorityCode, newExpiryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending trip {TripAuthorityCode} expiry", tripAuthorityCode);
            throw;
        }
    }

    /// <summary>
    /// Calculate trip duration in days
    /// </summary>
    public int CalculateTripDuration(DateTime issueDate, DateTime? expiryDate)
    {
        if (!expiryDate.HasValue)
        {
            return 0; // No expiry means indefinite/unknown duration
        }

        var duration = (expiryDate.Value - issueDate).Days;
        return duration > 0 ? duration : 0;
    }

    /// <summary>
    /// Check if odometer reading is valid for the trip
    /// Validates against contract odometer ranges
    /// </summary>
    public async Task<bool> ValidateOdometerReadingAsync(int tripAuthorityCode, int odometerReading)
    {
        try
        {
            _logger.LogInformation("Validating odometer reading {OdometerReading} for trip {TripAuthorityCode}",
                odometerReading, tripAuthorityCode);

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                _logger.LogWarning("Trip {TripAuthorityCode} not found", tripAuthorityCode);
                return false;
            }

            // Get contract to check odometer range
            var contract = await _contractRepository.GetByIdAsync(trip.contract_code);
            if (contract == null)
            {
                _logger.LogWarning("Contract {ContractCode} not found", trip.contract_code);
                return false;
            }

            // Validate odometer is within contract range
            if (odometerReading < contract.start_odometer)
            {
                _logger.LogWarning("Odometer reading {OdometerReading} is less than contract start odometer {StartOdometer}",
                    odometerReading, contract.start_odometer);
                return false;
            }

            if (contract.end_odometer > 0 && odometerReading > contract.end_odometer)
            {
                _logger.LogWarning("Odometer reading {OdometerReading} exceeds contract end odometer {EndOdometer}",
                    odometerReading, contract.end_odometer);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating odometer reading for trip {TripAuthorityCode}", tripAuthorityCode);
            return false;
        }
    }
}
