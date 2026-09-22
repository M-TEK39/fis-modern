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
    /// Create a trip authority and its legacy child rows as one operation.
    /// The repository negotiates optional columns and table names at runtime.
    /// </summary>
    Task<Trip> CreateTripAuthorityAsync(
        Trip trip,
        IReadOnlyList<TripAuthorityDriverInput> drivers,
        IReadOnlyList<TripAuthorityPassengerInput> passengers,
        IReadOnlyList<TripAuthorityRouteInput> routes
    );

    /// <summary>
    /// Update an existing trip authority
    /// Legacy: Trip authority modification
    /// Validates that trip is not locked for transfer
    /// </summary>
    Task UpdateTripAsync(Trip trip, IReadOnlySet<short>? allowedSiteCodes = null);

    /// <summary>
    /// Close a trip authority and persist its route end odometer readings.
    /// </summary>
    Task CloseTripAsync(
        int tripAuthorityCode,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer = null,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

    /// <summary>
    /// Get trip authority by ID
    /// </summary>
    Task<Trip?> GetTripByIdAsync(
        int tripAuthorityCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

    /// <summary>
    /// Get the complete persisted trip authority record and its related legacy
    /// rows. Related tables are negotiated at runtime because older client
    /// databases do not contain every later table or column.
    /// </summary>
    Task<TripAuthorityDetails?> GetTripAuthorityDetailsAsync(
        int tripAuthorityCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

    /// <summary>
    /// Create-trip dropdown membership from DEV_SEL_TripTypes.
    /// </summary>
    Task<IReadOnlyList<TripAuthorityTripType>> GetTripTypesAsync();

    /// <summary>
    /// Get all trip authorities with their contract vehicle context.
    /// </summary>
    Task<IEnumerable<Trip>> GetAllTripsAsync(IReadOnlySet<short>? allowedSiteCodes = null);

    /// <summary>
    /// Get active-contract vehicles used by the Trip Authority filter.
    /// </summary>
    Task<IEnumerable<TripAuthorityVehicle>> GetTripAuthorityVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? vmfCode = null,
        int? contractCode = null
    );

    /// <summary>
    /// Get all trips for a vehicle (via contract)
    /// </summary>
    Task<IEnumerable<Trip>> GetTripsByVehicleAsync(
        int vmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

    /// <summary>
    /// Get all trips for a driver
    /// </summary>
    Task<IEnumerable<Trip>> GetTripsByDriverAsync(
        string driverId,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

    /// <summary>
    /// Get trips by date range
    /// </summary>
    Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

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
    Task DeleteTripAsync(
        int tripAuthorityCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

    /// <summary>
    /// Extend trip expiry date
    /// Legacy: DEV_UPD_TripXMLForRenewalOfTrip creates a new authority and
    /// updates the existing authority/routes in one database-owned workflow.
    /// </summary>
    Task ExtendTripExpiryAsync(int tripAuthorityCode, DateTime newExpiryDate);

    /// <summary>
    /// Renew an open trip authority and return the newly created authority.
    /// </summary>
    Task<Trip> RenewTripAsync(
        int tripAuthorityCode,
        DateTime newExpiryDate,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer = null,
        IReadOnlySet<short>? allowedSiteCodes = null
    );

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

public sealed record TripAuthorityVehicle(
    int VmfCode,
    int ContractCode,
    short SiteCode,
    string? FleetNumber,
    string? RegistrationNumber,
    DateTime? LicenceDueDate,
    string? MakeDescription,
    string? ModelDescription,
    string? ContractType,
    int? StartOdometer = null
);

public sealed record TripAuthorityDetails(
    Trip Trip,
    IReadOnlyList<TripAuthorityDriver> Drivers,
    IReadOnlyList<TripAuthorityPassenger> Passengers,
    IReadOnlyList<TripAuthorityRoute> Routes,
    bool HasShowTripVehicleSelector = false,
    TripAuthorityVehicleSnapshot? ShowTripVehicle = null,
    string? ShowTripTypeName = null,
    string? ShowTripIncidentTypeName = null,
    string? ShowTripCapturedBy = null,
    bool HasShowTripIncidentSelector = false,
    IReadOnlyList<TripAuthorityIncidentType>? IncidentTypes = null
);

public sealed record TripAuthorityIncidentType(int Code, string? Name);

public sealed record TripAuthorityTripType(int Code, string? Name);

public sealed record TripAuthorityVehicleSnapshot(
    string? DepartmentName,
    int? SiteCode,
    string? SiteName,
    int? ContractCode,
    int? VmfCode,
    string? FleetNumber,
    string? Make,
    string? Model,
    string? RegistrationNumber,
    int? StartOdometer
);

public sealed record TripAuthorityDriver(
    int TripDriverCode,
    string? Name,
    string? IdentityNumber,
    bool IsPrimary,
    int? SiteCode,
    int? LicenceTypeCode,
    string? PassportNumber,
    string? PersalNumber,
    string? ContractNumber,
    string? LicenceNumber,
    DateTime? LicenceIssueDate,
    DateTime? LicenceLastVerifiedDate,
    bool HasPdp,
    DateTime? PdpExpiryDate,
    DateTime? LicenceExpiryDate,
    bool IsActive
);

public sealed record TripAuthorityPassenger(int TripPassengerCode, string? Name);

public sealed record TripAuthorityRoute(
    int RouteCode,
    DateTime? StartDate,
    DateTime? EndDate,
    int? StartOdometer,
    int? EndOdometer,
    string? ResponsibilityCode,
    string? ObjectiveCode,
    string? StartLocation,
    string? EndLocation,
    int? EstimatedDistance,
    int? Distance,
    string? ProjectNumber,
    string? FundCode,
    int? EditedByUserCode
);

public sealed record TripAuthorityRouteUpdate(
    int RouteCode,
    int EndOdometer,
    int Distance,
    int? StartOdometer = null
);

public sealed record TripAuthorityDriverInput(
    string? Name,
    string? IdentityNumber,
    bool IsPrimary,
    int? SiteCode,
    int? LicenceTypeCode,
    string? PassportNumber,
    string? PersalNumber,
    string? ContractNumber,
    string? LicenceNumber,
    DateTime? LicenceIssueDate,
    DateTime? LicenceLastVerifiedDate,
    bool HasPdp,
    DateTime? PdpExpiryDate,
    DateTime? LicenceExpiryDate,
    bool IsActive
);

public sealed record TripAuthorityPassengerInput(string Name);

public sealed record TripAuthorityRouteInput(
    DateTime StartDate,
    DateTime EndDate,
    string? StartLocation,
    string? EndLocation,
    int? EstimatedDistance,
    string ResponsibilityCode,
    string ObjectiveCode,
    string ProjectNumber,
    string FundCode,
    int? StartOdometer = null
);
