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
    private readonly IDriverRepository _driverRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<TripService> _logger;

    public TripService(
        ITripRepository tripRepository,
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        ICurrentUserContext currentUserContext,
        ILogger<TripService> logger
    )
    {
        _tripRepository = tripRepository ?? throw new ArgumentNullException(nameof(tripRepository));
        _contractRepository =
            contractRepository ?? throw new ArgumentNullException(nameof(contractRepository));
        _vehicleRepository =
            vehicleRepository ?? throw new ArgumentNullException(nameof(vehicleRepository));
        _driverRepository =
            driverRepository ?? throw new ArgumentNullException(nameof(driverRepository));
        _currentUserContext =
            currentUserContext ?? throw new ArgumentNullException(nameof(currentUserContext));
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
            _logger.LogInformation(
                "Creating trip authority for contract: {ContractCode}",
                trip.contract_code
            );

            // Validate trip creation
            var isValid = await ValidateTripCreationAsync(trip);
            if (!isValid)
            {
                throw new InvalidOperationException("Trip validation failed");
            }

            // The legacy capture flow records the authenticated operator, not
            // a caller-supplied user_access_code. Keep ownership stable even
            // when this application service is called outside the controller.
            var currentUserId = _currentUserContext.GetCurrentUserIdOrDefault();
            trip.user_access_code = currentUserId is > 0 and <= short.MaxValue
                ? (short)currentUserId
                : null;

            // Set defaults
            trip.issue_date = DateTime.Now;
            trip.locked_for_transfer = false;

            // If no expiry date set and it's monthly, set expiry to end of month
            if (!trip.expiry_date.HasValue && trip.Trip_Is_Monthly)
            {
                trip.expiry_date = new DateTime(
                    DateTime.Now.Year,
                    DateTime.Now.Month,
                    DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)
                );
            }

            var createdTrip = await _tripRepository.CreateAsync(
                trip,
                currentUserId
            );

            _logger.LogInformation(
                "Trip authority created: {TripAuthorityCode} for contract {ContractCode}",
                createdTrip.trip_authority_code,
                createdTrip.contract_code
            );

            return createdTrip;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating trip authority for contract: {ContractCode}",
                trip.contract_code
            );
            throw;
        }
    }

    public async Task<Trip> CreateTripAuthorityAsync(
        Trip trip,
        IReadOnlyList<TripAuthorityDriverInput> drivers,
        IReadOnlyList<TripAuthorityPassengerInput> passengers,
        IReadOnlyList<TripAuthorityRouteInput> routes
    )
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(passengers);
        ArgumentNullException.ThrowIfNull(routes);

        if (drivers.Count == 0)
        {
            throw new ArgumentException("At least one driver is required.", nameof(drivers));
        }

        if (routes.Count == 0)
        {
            throw new ArgumentException("At least one route is required.", nameof(routes));
        }

        if (drivers.Any(driver => string.IsNullOrWhiteSpace(driver.Name)))
        {
            throw new ArgumentException("Every selected driver must have a name.", nameof(drivers));
        }

        if (routes.Any(route => route.StartDate > route.EndDate))
        {
            throw new ArgumentException(
                "A route arrival date cannot be before its departure date.",
                nameof(routes)
            );
        }

        if (
            routes.Any(route =>
                string.IsNullOrWhiteSpace(route.ResponsibilityCode)
                || string.IsNullOrWhiteSpace(route.ObjectiveCode)
                || string.IsNullOrWhiteSpace(route.ProjectNumber)
                || string.IsNullOrWhiteSpace(route.FundCode)
            )
        )
        {
            throw new ArgumentException(
                "Responsibility, Objective, Project, and Fund are required for every route.",
                nameof(routes)
            );
        }

        if (routes.Any(route => route.EstimatedDistance is < 0))
        {
            throw new ArgumentException(
                "Estimated route distance cannot be negative.",
                nameof(routes)
            );
        }

        var contract =
            await _contractRepository.GetByIdAsync(trip.contract_code)
            ?? throw new InvalidOperationException($"Contract {trip.contract_code} was not found");

        // Legacy CreateTrip binds the driver selector to the selected
        // contract site. Do the same check at the service boundary so a
        // crafted API payload cannot assign a Johannesburg driver to a Cape
        // Town vehicle (or omit the site and let the procedure infer one).
        var invalidDriver = drivers.FirstOrDefault(driver =>
            driver.SiteCode is not > 0 || driver.SiteCode != contract.site_code
        );
        if (invalidDriver is not null)
        {
            throw new ArgumentException(
                $"Driver '{invalidDriver.Name}' belongs to site {invalidDriver.SiteCode?.ToString() ?? "unknown"} and cannot be assigned to contract site {contract.site_code}.",
                nameof(drivers)
            );
        }

        var existingDrivers = (await _driverRepository.GetAllDriversAsync()).ToList();
        foreach (var driver in drivers)
        {
            var identities = DriverIdentityValues(driver).Where(value => value is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (identities.Count == 0)
                continue;

            var matchingDriver = existingDrivers.FirstOrDefault(candidate =>
                DriverIdentityValues(candidate).Any(value =>
                    value is not null && identities.Contains(value)
                )
            );
            if (matchingDriver is not null && matchingDriver.site_code != contract.site_code)
            {
                throw new ArgumentException(
                    $"Driver '{driver.Name}' already belongs to site {matchingDriver.site_code} and cannot be assigned to contract site {contract.site_code}.",
                    nameof(drivers)
                );
            }
        }

        // The legacy DEV_INS_TripXML procedure reads the vehicle/site/start
        // odometer from the Contract XML node. Keep that resolved contract on
        // the aggregate so the repository cannot silently emit an incomplete
        // legacy document when the procedure is present.
        trip.Contract = contract;

        if (!await ValidateTripCreationAsync(trip))
        {
            throw new InvalidOperationException("Trip validation failed");
        }

        trip.issue_date = DateTime.Now;
        var currentUserId = _currentUserContext.GetCurrentUserIdOrDefault();
        trip.user_access_code = currentUserId is > 0 and <= short.MaxValue
            ? (short)currentUserId
            : null;
        trip.end_odo_meter = null;
        trip.locked_for_transfer = false;

        if (!trip.expiry_date.HasValue)
        {
            trip.expiry_date = routes.Max(route => route.EndDate);
        }

        var normalizedRoutes = routes
            .Select(route => route with { StartOdometer = contract.start_odometer })
            .ToArray();
        var normalizedPassengers =
            passengers.Count > 0 ? passengers : [new TripAuthorityPassengerInput("None")];

        var createdTrip = await _tripRepository.CreateAuthorityAsync(
            trip,
            drivers,
            normalizedPassengers,
            normalizedRoutes,
            currentUserId
        );

        _logger.LogInformation(
            "Trip authority {TripAuthorityCode} created with {DriverCount} drivers, {PassengerCount} passengers, and {RouteCount} routes",
            createdTrip.trip_authority_code,
            drivers.Count,
            normalizedPassengers.Count,
            normalizedRoutes.Length
        );

        return createdTrip;
    }

    private static IEnumerable<string?> DriverIdentityValues(TripAuthorityDriverInput driver) =>
        new[]
        {
            driver.IdentityNumber,
            driver.PassportNumber,
            driver.PersalNumber,
            driver.ContractNumber,
            driver.LicenceNumber,
        }
        .Select(NormalizeDriverIdentity)
        .Where(value => value is not null);

    private static IEnumerable<string?> DriverIdentityValues(Driver driver) =>
        new[]
        {
            driver.driver_SA_id,
            driver.driver_passportnumber,
            driver.driver_persalnumber,
            driver.driver_contractnumber,
            driver.driver_licence_number,
        }.Select(NormalizeDriverIdentity);

    private static string? NormalizeDriverIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Replace(" ", string.Empty).ToUpperInvariant();

    /// <summary>
    /// Update an existing trip authority
    /// Legacy: Trip authority modification
    /// </summary>
    public async Task UpdateTripAsync(
        Trip trip,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        try
        {
            _logger.LogInformation(
                "Updating trip authority: {TripAuthorityCode}",
                trip.trip_authority_code
            );

            // Check if trip is locked
            var existingTrip = await _tripRepository.GetByIdAsync(
                trip.trip_authority_code,
                allowedSiteCodes
            );
            if (existingTrip == null)
            {
                throw new InvalidOperationException($"Trip {trip.trip_authority_code} not found");
            }

            if (existingTrip.locked_for_transfer)
            {
                _logger.LogWarning(
                    "Cannot update trip {TripAuthorityCode} - locked for transfer",
                    trip.trip_authority_code
                );
                throw new InvalidOperationException(
                    $"Trip {trip.trip_authority_code} is locked for transfer and cannot be modified"
                );
            }

            // Approval/edit actions must not replace the original capturer.
            // The audit actor is carried by modified_by_user_code in the
            // repository; user_access_code remains the legacy owner.
            trip.user_access_code = existingTrip.user_access_code;
            trip.Contract = existingTrip.Contract
                ?? await _contractRepository.GetByIdAsync(trip.contract_code);

            await _tripRepository.UpdateAsync(
                trip,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Trip authority updated: {TripAuthorityCode}",
                trip.trip_authority_code
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating trip authority: {TripAuthorityCode}",
                trip.trip_authority_code
            );
            throw;
        }
    }

    public async Task CloseTripAsync(
        int tripAuthorityCode,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer = null,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var details =
            await _tripRepository.GetDetailsAsync(tripAuthorityCode, allowedSiteCodes)
            ?? throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");

        if (details.Trip.locked_for_transfer)
        {
            throw new InvalidOperationException(
                $"Trip {tripAuthorityCode} is locked for transfer and cannot be modified"
            );
        }

        var submittedByCode = routes.ToDictionary(route => route.RouteCode);
        if (details.Routes.Count == 0 && routes.Count > 0)
        {
            throw new InvalidOperationException("The trip has no persisted routes to close");
        }

        if (
            routes.Any(route =>
                !details.Routes.Any(existing => existing.RouteCode == route.RouteCode)
            )
        )
        {
            throw new InvalidOperationException(
                "One or more submitted routes do not belong to this trip"
            );
        }

        var validatedRoutes = new List<TripAuthorityRouteUpdate>(details.Routes.Count);
        var maxEndOdometer = endOdometer ?? details.Trip.end_odo_meter ?? 0;
        int? previousEndOdometer = null;
        foreach (var route in details.Routes)
        {
            if (!submittedByCode.TryGetValue(route.RouteCode, out var submitted))
            {
                throw new InvalidOperationException(
                    $"End odometer is required for route {route.RouteCode}"
                );
            }

            var startOdometer = route.StartOdometer ?? previousEndOdometer;
            if (startOdometer.HasValue && submitted.EndOdometer < startOdometer.Value)
            {
                throw new InvalidOperationException(
                    $"The end odometer for route {route.RouteCode} must be greater than or equal to {startOdometer.Value}"
                );
            }

            var distance = (long)submitted.EndOdometer - (startOdometer ?? submitted.EndOdometer);
            if (distance < 0 || distance > 25_000)
            {
                throw new InvalidOperationException(
                    $"The distance for route {route.RouteCode} must be between 0 and 25000 kilometres"
                );
            }

            if (
                string.IsNullOrWhiteSpace(route.ResponsibilityCode)
                || string.IsNullOrWhiteSpace(route.ObjectiveCode)
                || string.IsNullOrWhiteSpace(route.ProjectNumber)
                || string.IsNullOrWhiteSpace(route.FundCode)
            )
            {
                throw new InvalidOperationException(
                    $"Responsibility, Objective, Project, and Fund are required before closing route {route.RouteCode}"
                );
            }

            validatedRoutes.Add(
                new TripAuthorityRouteUpdate(
                    route.RouteCode,
                    submitted.EndOdometer,
                    (int)distance,
                    startOdometer
                )
            );
            previousEndOdometer = submitted.EndOdometer;
            maxEndOdometer = Math.Max(maxEndOdometer, submitted.EndOdometer);
        }

        details.Trip.end_odo_meter =
            maxEndOdometer > 0 ? maxEndOdometer : details.Trip.end_odo_meter;
        await _tripRepository.CloseAsync(
            tripAuthorityCode,
            validatedRoutes,
            details.Trip.end_odo_meter,
            _currentUserContext.GetCurrentUserIdOrDefault()
        );
    }

    /// <summary>
    /// Get trip authority by ID
    /// </summary>
    public async Task<Trip?> GetTripByIdAsync(
        int tripAuthorityCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        return await _tripRepository.GetByIdAsync(tripAuthorityCode, allowedSiteCodes);
    }

    public async Task<TripAuthorityDetails?> GetTripAuthorityDetailsAsync(
        int tripAuthorityCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        return await _tripRepository.GetDetailsAsync(tripAuthorityCode, allowedSiteCodes);
    }

    public Task<IReadOnlyList<TripAuthorityTripType>> GetTripTypesAsync() =>
        _tripRepository.GetTripTypesAsync();

    public async Task<IEnumerable<Trip>> GetAllTripsAsync(
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        return await _tripRepository.GetAllAsync(allowedSiteCodes);
    }

    public async Task<IEnumerable<TripAuthorityVehicle>> GetTripAuthorityVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? vmfCode = null,
        int? contractCode = null
    )
    {
        return await _tripRepository.GetTripAuthorityVehiclesAsync(
            allowedSiteCodes,
            vmfCode,
            contractCode
        );
    }

    /// <summary>
    /// Get all trips for a vehicle (via contract)
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByVehicleAsync(
        int vmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        return await _tripRepository.GetTripsByVehicleAsync(vmfCode, allowedSiteCodes);
    }

    /// <summary>
    /// Get all trips for a driver
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(
        string driverId,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        return await _tripRepository.GetTripsByDriverAsync(driverId, allowedSiteCodes);
    }

    /// <summary>
    /// Get trips by date range
    /// </summary>
    public async Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        return await _tripRepository.GetTripsByDateRangeAsync(startDate, endDate, allowedSiteCodes);
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
            _logger.LogInformation(
                "Checking for open trip authorities on contract: {ContractCode}",
                contractCode
            );

            var hasOpenTrips = await _tripRepository.HasOpenTripAuthoritiesAsync(contractCode);

            _logger.LogInformation(
                "Contract {ContractCode} has open trips: {HasOpenTrips}",
                contractCode,
                hasOpenTrips
            );

            return hasOpenTrips;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking open trip authorities for contract: {ContractCode}",
                contractCode
            );
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

            // Trip expiry is captured as a calendar date, so the selected
            // expiry day remains open until that day ends.
            return trips.Where(t => t.expiry_date.HasValue && t.expiry_date.Value.Date >= DateTime.Today);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error getting open trip authorities for contract: {ContractCode}",
                contractCode
            );
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

        return trip.expiry_date.Value.Date < DateTime.Today;
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
            return allTrips.Where(t =>
                t.expiry_date.HasValue && t.expiry_date.Value.Date < DateTime.Today
            );
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
            _logger.LogInformation(
                "Validating trip creation for contract: {ContractCode}",
                trip.contract_code
            );

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
            if (trip.expiry_date.HasValue && trip.expiry_date.Value.Date < DateTime.Today)
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

            _logger.LogInformation(
                "Trip validation passed for contract: {ContractCode}",
                trip.contract_code
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating trip creation for contract: {ContractCode}",
                trip.contract_code
            );
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
            _logger.LogInformation(
                "Locking trip {TripAuthorityCode} for transfer",
                tripAuthorityCode
            );

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            trip.locked_for_transfer = true;
            await _tripRepository.UpdateAsync(
                trip,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Trip {TripAuthorityCode} locked for transfer",
                tripAuthorityCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error locking trip {TripAuthorityCode} for transfer",
                tripAuthorityCode
            );
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
            _logger.LogInformation(
                "Unlocking trip {TripAuthorityCode} from transfer",
                tripAuthorityCode
            );

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            trip.locked_for_transfer = false;
            await _tripRepository.UpdateAsync(
                trip,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Trip {TripAuthorityCode} unlocked from transfer",
                tripAuthorityCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error unlocking trip {TripAuthorityCode} from transfer",
                tripAuthorityCode
            );
            throw;
        }
    }

    /// <summary>
    /// Delete trip authority
    /// Validates that trip can be deleted (not locked, no associated records)
    /// </summary>
    public async Task DeleteTripAsync(
        int tripAuthorityCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        try
        {
            _logger.LogInformation(
                "Deleting trip authority: {TripAuthorityCode}",
                tripAuthorityCode
            );

            var trip = await _tripRepository.GetByIdAsync(tripAuthorityCode, allowedSiteCodes);
            if (trip == null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            // Check if trip is locked
            if (trip.locked_for_transfer)
            {
                _logger.LogWarning(
                    "Cannot delete trip {TripAuthorityCode} - locked for transfer",
                    tripAuthorityCode
                );
                throw new InvalidOperationException(
                    $"Trip {tripAuthorityCode} is locked for transfer and cannot be deleted"
                );
            }

            await _tripRepository.DeleteAsync(
                tripAuthorityCode,
                _currentUserContext.GetCurrentUserIdOrDefault()
            );

            _logger.LogInformation(
                "Trip authority deleted: {TripAuthorityCode}",
                tripAuthorityCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting trip authority: {TripAuthorityCode}",
                tripAuthorityCode
            );
            throw;
        }
    }

    /// <summary>
    /// Extend trip expiry date
    /// Legacy: DEV_UPD_TripXMLForRenewalOfTrip creates a new authority and
    /// updates the existing authority/routes in one database-owned workflow.
    /// </summary>
    public async Task ExtendTripExpiryAsync(int tripAuthorityCode, DateTime newExpiryDate)
    {
        var details = await _tripRepository.GetDetailsAsync(tripAuthorityCode);
        if (details is null)
            throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");

        var routes = details.Routes
            .Select(route =>
            {
                if (!route.EndOdometer.HasValue)
                {
                    throw new InvalidOperationException(
                        $"End odometer is required for route {route.RouteCode} when renewing a trip authority."
                    );
                }

                var start = route.StartOdometer ?? 0;
                return new TripAuthorityRouteUpdate(
                    route.RouteCode,
                    route.EndOdometer.Value,
                    Math.Max(0, route.EndOdometer.Value - start),
                    route.StartOdometer
                );
            })
            .ToArray();

        await RenewTripAsync(tripAuthorityCode, newExpiryDate, routes);
    }

    public async Task<Trip> RenewTripAsync(
        int tripAuthorityCode,
        DateTime newExpiryDate,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer = null,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        try
        {
            _logger.LogInformation(
                "Extending trip {TripAuthorityCode} expiry to {NewExpiryDate}",
                tripAuthorityCode,
                newExpiryDate
            );

            ArgumentNullException.ThrowIfNull(routes);
            var details = await _tripRepository.GetDetailsAsync(tripAuthorityCode, allowedSiteCodes);
            if (details is null)
            {
                throw new InvalidOperationException($"Trip {tripAuthorityCode} not found");
            }

            var trip = details.Trip;

            // Check if trip is locked
            if (trip.locked_for_transfer)
            {
                throw new InvalidOperationException(
                    $"Trip {tripAuthorityCode} is locked for transfer and cannot be extended"
                );
            }

            // Validate new expiry date is in the future
            if (newExpiryDate.Date <= DateTime.Today)
            {
                throw new ArgumentException(
                    "New expiry date must be in the future",
                    nameof(newExpiryDate)
                );
            }

            // Validate new expiry date is after current expiry date (if set)
            if (trip.expiry_date.HasValue && newExpiryDate.Date <= trip.expiry_date.Value.Date)
            {
                throw new ArgumentException(
                    "New expiry date must be after current expiry date",
                    nameof(newExpiryDate)
                );
            }

            if (details.Routes.Count == 0)
            {
                throw new InvalidOperationException(
                    "A trip authority must have at least one persisted route before it can be renewed."
                );
            }

            var submittedRoutes = routes.ToDictionary(route => route.RouteCode);
            var renewalRoutes = new List<TripAuthorityRouteUpdate>(details.Routes.Count);
            var previousEndOdometer = (int?)null;
            foreach (var route in details.Routes)
            {
                if (!submittedRoutes.TryGetValue(route.RouteCode, out var submitted))
                {
                    // Existing callers that only extend the expiry may reuse
                    // the persisted route readings. The explicit API renewal
                    // route can submit corrected end odometers.
                    if (!route.EndOdometer.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"End odometer is required for route {route.RouteCode} when renewing a trip authority."
                        );
                    }

                    submitted = new TripAuthorityRouteUpdate(
                        route.RouteCode,
                        route.EndOdometer.Value,
                        route.Distance ?? 0,
                        route.StartOdometer
                    );
                }

                var startOdometer = submitted.StartOdometer ?? route.StartOdometer ?? previousEndOdometer;
                if (startOdometer.HasValue && submitted.EndOdometer < startOdometer.Value)
                {
                    throw new InvalidOperationException(
                        $"The end odometer for route {route.RouteCode} must be greater than or equal to {startOdometer.Value}."
                    );
                }

                var distance = (long)submitted.EndOdometer - (startOdometer ?? submitted.EndOdometer);
                if (distance < 0 || distance > 25_000)
                {
                    throw new InvalidOperationException(
                        $"The distance for route {route.RouteCode} must be between 0 and 25000 kilometres."
                    );
                }

                renewalRoutes.Add(
                    new TripAuthorityRouteUpdate(
                        route.RouteCode,
                        submitted.EndOdometer,
                        (int)distance,
                        startOdometer
                    )
                );
                previousEndOdometer = submitted.EndOdometer;
            }

            var maximumRouteEnd = renewalRoutes.Max(route => route.EndOdometer);
            var resolvedEndOdometer = endOdometer ?? Math.Max(trip.end_odo_meter ?? 0, maximumRouteEnd);
            if (resolvedEndOdometer < maximumRouteEnd)
            {
                throw new InvalidOperationException(
                    "Trip end odometer cannot be less than the final route end odometer."
                );
            }

            var renewed = await _tripRepository.RenewAsync(
                tripAuthorityCode,
                newExpiryDate,
                renewalRoutes,
                resolvedEndOdometer,
                _currentUserContext.GetCurrentUserIdOrDefault(),
                allowedSiteCodes
            );

            _logger.LogInformation(
                "Trip {TripAuthorityCode} expiry extended to {NewExpiryDate}",
                tripAuthorityCode,
                newExpiryDate
            );
            return renewed;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error extending trip {TripAuthorityCode} expiry",
                tripAuthorityCode
            );
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
            _logger.LogInformation(
                "Validating odometer reading {OdometerReading} for trip {TripAuthorityCode}",
                odometerReading,
                tripAuthorityCode
            );

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
                _logger.LogWarning(
                    "Odometer reading {OdometerReading} is less than contract start odometer {StartOdometer}",
                    odometerReading,
                    contract.start_odometer
                );
                return false;
            }

            if (contract.end_odometer > 0 && odometerReading > contract.end_odometer)
            {
                _logger.LogWarning(
                    "Odometer reading {OdometerReading} exceeds contract end odometer {EndOdometer}",
                    odometerReading,
                    contract.end_odometer
                );
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating odometer reading for trip {TripAuthorityCode}",
                tripAuthorityCode
            );
            return false;
        }
    }
}
