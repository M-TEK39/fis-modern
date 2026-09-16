using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

public class CreateTripDto
{
    public int ContractCode { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRank { get; set; }
    public string? ApproverTelephone { get; set; }
    public int? EndOdometer { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? TripReason { get; set; }
    public string? TripRequestNumber { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.Now;
    public short TripTypeCode { get; set; }
    public short TripIncidentTypeCode { get; set; }
    public short? UserAccessCode { get; set; }
    public bool LockedForTransfer { get; set; } = false;
    public bool TripIsMonthly { get; set; } = false;
}

public sealed class CreateTripAuthorityDto : CreateTripDto
{
    public IReadOnlyList<CreateTripAuthorityDriverDto> Drivers { get; set; } = [];
    public IReadOnlyList<CreateTripAuthorityPassengerDto> Passengers { get; set; } = [];
    public IReadOnlyList<CreateTripAuthorityRouteDto> Routes { get; set; } = [];
}

public sealed class CreateTripAuthorityDriverDto
{
    public string? Name { get; set; }
    public string? IdentityNumber { get; set; }
    public bool IsPrimary { get; set; }
    public int? SiteCode { get; set; }
    public int? LicenceTypeCode { get; set; }
    public string? PassportNumber { get; set; }
    public string? PersalNumber { get; set; }
    public string? ContractNumber { get; set; }
    public string? LicenceNumber { get; set; }
    public DateTime? LicenceIssueDate { get; set; }
    public DateTime? LicenceLastVerifiedDate { get; set; }
    public bool HasPdp { get; set; }
    public DateTime? PdpExpiryDate { get; set; }
    public DateTime? LicenceExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateTripAuthorityPassengerDto
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CreateTripAuthorityRouteDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? StartLocation { get; set; }
    public string? EndLocation { get; set; }
    public int? EstimatedDistance { get; set; }
    public string ResponsibilityCode { get; set; } = string.Empty;
    public string ObjectiveCode { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string FundCode { get; set; } = string.Empty;
}

public class UpdateTripDto : CreateTripDto { }

public class TripDto
{
    public int TripAuthorityCode { get; set; }
    public int ContractCode { get; set; }
    public int? VmfCode { get; set; }
    public short? SiteCode { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRank { get; set; }
    public string? ApproverTelephone { get; set; }
    public int? EndOdometer { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? TripReason { get; set; }
    public string? TripRequestNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public short TripTypeCode { get; set; }
    public short TripIncidentTypeCode { get; set; }
    public short? UserAccessCode { get; set; }
    public bool LockedForTransfer { get; set; }
    public bool TripIsMonthly { get; set; }

    // Computed properties for display
    public string ApproverFullInfo => $"{ApproverName} ({ApproverRank})".Trim();
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Now;
    public int DaysUntilExpiry =>
        ExpiryDate.HasValue ? (int)(ExpiryDate.Value - DateTime.Now).TotalDays : 0;
}

public class TripAuthorityVehicleDto
{
    public int VmfCode { get; set; }
    public int ContractCode { get; set; }
    public short SiteCode { get; set; }
    public string? FleetNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime? LicenceDueDate { get; set; }
    public string? MakeDescription { get; set; }
    public string? ModelDescription { get; set; }
    public string? ContractType { get; set; }
}

public sealed class TripAuthorityVehiclePageItemDto
{
    public int VmfCode { get; set; }
    public int ContractCode { get; set; }
    public short SiteCode { get; set; }
    public string? FleetNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime? LicenceDueDate { get; set; }
    public string? MakeDescription { get; set; }
    public string? ModelDescription { get; set; }
    public string? ContractType { get; set; }
    public int? TripAuthorityCode { get; set; }
}

public class TripAuthorityDetailsDto
{
    public TripDto Trip { get; set; } = new();
    public IReadOnlyList<TripAuthorityDriverDto> Drivers { get; set; } = [];
    public IReadOnlyList<TripAuthorityPassengerDto> Passengers { get; set; } = [];
    public IReadOnlyList<TripAuthorityRouteDto> Routes { get; set; } = [];
}

public class TripAuthorityDriverDto
{
    public int TripDriverCode { get; set; }
    public string? Name { get; set; }
    public string? IdentityNumber { get; set; }
    public bool IsPrimary { get; set; }
    public int? SiteCode { get; set; }
    public int? LicenceTypeCode { get; set; }
    public string? PassportNumber { get; set; }
    public string? PersalNumber { get; set; }
    public string? ContractNumber { get; set; }
    public string? LicenceNumber { get; set; }
    public DateTime? LicenceIssueDate { get; set; }
    public DateTime? LicenceLastVerifiedDate { get; set; }
    public bool HasPdp { get; set; }
    public DateTime? PdpExpiryDate { get; set; }
    public DateTime? LicenceExpiryDate { get; set; }
    public bool IsActive { get; set; }
}

public class TripAuthorityPassengerDto
{
    public int TripPassengerCode { get; set; }
    public string? Name { get; set; }
}

public class TripAuthorityRouteDto
{
    public int RouteCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? StartOdometer { get; set; }
    public int? EndOdometer { get; set; }
    public string? ResponsibilityCode { get; set; }
    public string? ObjectiveCode { get; set; }
    public string? StartLocation { get; set; }
    public string? EndLocation { get; set; }
    public int? EstimatedDistance { get; set; }
    public int? Distance { get; set; }
    public string? ProjectNumber { get; set; }
    public string? FundCode { get; set; }
    public int? EditedByUserCode { get; set; }
}

public class CloseTripDto
{
    public int? EndOdometer { get; set; }
    public IReadOnlyList<CloseTripRouteDto> Routes { get; set; } = [];
}

public class CloseTripRouteDto
{
    public int RouteCode { get; set; }
    public int? EndOdometer { get; set; }
}

[ApiController]
[Authorize(Roles = "Trip Authorities,TripAuthorities,SystemAdministrator,System Administrator")]
[Route("api/[controller]")]
public class TripController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly ITripService _tripService;
    private readonly ITripRepository _tripRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<TripController> _logger;

    public TripController(
        ITripService tripService,
        ITripRepository tripRepository,
        IContractRepository contractRepository,
        ISiteRepository siteRepository,
        FisDbContext context,
        ILogger<TripController> logger
    )
    {
        _tripService = tripService;
        _tripRepository = tripRepository;
        _contractRepository = contractRepository;
        _siteRepository = siteRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetAllTrips()
    {
        try
        {
            var trips = await _tripService.GetAllTripsAsync(await ResolveAllowedSiteCodesAsync());
            return Ok(trips.Select(MapTrip));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all trip authorities");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<IEnumerable<TripAuthorityVehicleDto>>> GetTripAuthorityVehicles()
    {
        try
        {
            var vehicles = await _tripService.GetTripAuthorityVehiclesAsync(
                await ResolveAllowedSiteCodesAsync()
            );
            return Ok(
                vehicles.Select(vehicle => new TripAuthorityVehicleDto
                {
                    VmfCode = vehicle.VmfCode,
                    ContractCode = vehicle.ContractCode,
                    SiteCode = vehicle.SiteCode,
                    FleetNumber = vehicle.FleetNumber,
                    RegistrationNumber = vehicle.RegistrationNumber,
                    LicenceDueDate = vehicle.LicenceDueDate,
                    MakeDescription = vehicle.MakeDescription,
                    ModelDescription = vehicle.ModelDescription,
                    ContractType = vehicle.ContractType,
                })
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Trip Authority vehicles");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a server-paginated page of in-service vehicles that do not have an
    /// open trip authority. Location and number filters are data filters; the
    /// caller must retain the existing role-based access filtering.
    /// </summary>
    [HttpGet("vehicles/page")]
    public async Task<ActionResult> GetTripAuthorityInServicePage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? searchMode = null,
        [FromQuery] string? number = null,
        [FromQuery] short? departmentCode = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] int? authority = null
    )
    {
        if (
            !TryBuildVehiclePageQuery(
                page,
                pageSize,
                searchMode,
                number,
                departmentCode,
                siteCode,
                authority,
                out var query,
                out var error
            )
        )
        {
            return BadRequest(new { message = error });
        }

        try
        {
            query = query with { AllowedSiteCodes = await ResolveAllowedSiteCodesAsync() };
            var result = await _tripRepository.GetTripAuthorityInServicePageAsync(query);
            return Ok(MapTripAuthorityVehiclePage(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged in-service Trip Authority vehicles");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get a server-paginated page of vehicles currently OUT on an open trip
    /// authority. The authority filter is applied to the displayed latest open
    /// authority for each vehicle, matching the legacy table behavior.
    /// Location and number filters are data filters; the caller must retain the
    /// existing role-based access filtering.
    /// </summary>
    [HttpGet("vehicles/out/page")]
    public async Task<ActionResult> GetTripAuthorityOutPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? searchMode = null,
        [FromQuery] string? number = null,
        [FromQuery] short? departmentCode = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] int? authority = null
    )
    {
        if (
            !TryBuildVehiclePageQuery(
                page,
                pageSize,
                searchMode,
                number,
                departmentCode,
                siteCode,
                authority,
                out var query,
                out var error
            )
        )
        {
            return BadRequest(new { message = error });
        }

        try
        {
            query = query with { AllowedSiteCodes = await ResolveAllowedSiteCodesAsync() };
            var result = await _tripRepository.GetTripAuthorityOutPageAsync(query);
            return Ok(MapTripAuthorityVehiclePage(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged OUT Trip Authority vehicles");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TripDto>> GetTrip(int id)
    {
        try
        {
            var trip = await _tripService.GetTripByIdAsync(
                id,
                await ResolveAllowedSiteCodesAsync()
            );
            if (trip == null)
            {
                return NotFound();
            }

            var tripDto = new TripDto
            {
                TripAuthorityCode = trip.trip_authority_code,
                ContractCode = trip.contract_code,
                VmfCode = trip.Contract?.vmf_code,
                SiteCode = trip.Contract?.site_code,
                ApproverName = trip.approver_name,
                ApproverRank = trip.approver_rank,
                ApproverTelephone = trip.approver_tel,
                EndOdometer = trip.end_odo_meter,
                ExpiryDate = trip.expiry_date,
                TripReason = trip.trip_reason,
                TripRequestNumber = trip.trip_request_number,
                IssueDate = trip.issue_date,
                TripTypeCode = trip.trip_type_code,
                TripIncidentTypeCode = trip.trip_incident_type_code,
                UserAccessCode = trip.user_access_code,
                LockedForTransfer = trip.locked_for_transfer,
                TripIsMonthly = trip.Trip_Is_Monthly,
            };

            return Ok(tripDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trip with id {TripId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}/details")]
    public async Task<ActionResult<TripAuthorityDetailsDto>> GetTripDetails(int id)
    {
        try
        {
            var details = await _tripService.GetTripAuthorityDetailsAsync(
                id,
                await ResolveAllowedSiteCodesAsync()
            );
            if (details == null)
            {
                return NotFound();
            }

            return Ok(
                new TripAuthorityDetailsDto
                {
                    Trip = MapTrip(details.Trip),
                    Drivers = details
                        .Drivers.Select(driver => new TripAuthorityDriverDto
                        {
                            TripDriverCode = driver.TripDriverCode,
                            Name = driver.Name,
                            IdentityNumber = driver.IdentityNumber,
                            IsPrimary = driver.IsPrimary,
                            SiteCode = driver.SiteCode,
                            LicenceTypeCode = driver.LicenceTypeCode,
                            PassportNumber = driver.PassportNumber,
                            PersalNumber = driver.PersalNumber,
                            ContractNumber = driver.ContractNumber,
                            LicenceNumber = driver.LicenceNumber,
                            LicenceIssueDate = driver.LicenceIssueDate,
                            LicenceLastVerifiedDate = driver.LicenceLastVerifiedDate,
                            HasPdp = driver.HasPdp,
                            PdpExpiryDate = driver.PdpExpiryDate,
                            LicenceExpiryDate = driver.LicenceExpiryDate,
                            IsActive = driver.IsActive,
                        })
                        .ToArray(),
                    Passengers = details
                        .Passengers.Select(passenger => new TripAuthorityPassengerDto
                        {
                            TripPassengerCode = passenger.TripPassengerCode,
                            Name = passenger.Name,
                        })
                        .ToArray(),
                    Routes = details
                        .Routes.Select(route => new TripAuthorityRouteDto
                        {
                            RouteCode = route.RouteCode,
                            StartDate = route.StartDate,
                            EndDate = route.EndDate,
                            StartOdometer = route.StartOdometer,
                            EndOdometer = route.EndOdometer,
                            ResponsibilityCode = route.ResponsibilityCode,
                            ObjectiveCode = route.ObjectiveCode,
                            StartLocation = route.StartLocation,
                            EndLocation = route.EndLocation,
                            EstimatedDistance = route.EstimatedDistance,
                            Distance = route.Distance,
                            ProjectNumber = route.ProjectNumber,
                            FundCode = route.FundCode,
                            EditedByUserCode = route.EditedByUserCode,
                        })
                        .ToArray(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trip details for id {TripId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTripsByVehicle(int vmfCode)
    {
        try
        {
            var trips = await _tripService.GetTripsByVehicleAsync(
                vmfCode,
                await ResolveAllowedSiteCodesAsync()
            );
            var tripDtos = trips.Select(t => new TripDto
            {
                TripAuthorityCode = t.trip_authority_code,
                ContractCode = t.contract_code,
                VmfCode = t.Contract?.vmf_code,
                SiteCode = t.Contract?.site_code,
                ApproverName = t.approver_name,
                ApproverRank = t.approver_rank,
                ApproverTelephone = t.approver_tel,
                EndOdometer = t.end_odo_meter,
                ExpiryDate = t.expiry_date,
                TripReason = t.trip_reason,
                TripRequestNumber = t.trip_request_number,
                IssueDate = t.issue_date,
                TripTypeCode = t.trip_type_code,
                TripIncidentTypeCode = t.trip_incident_type_code,
                UserAccessCode = t.user_access_code,
                LockedForTransfer = t.locked_for_transfer,
                TripIsMonthly = t.Trip_Is_Monthly,
            });
            return Ok(tripDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trips for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("driver/{driverId}")]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTripsByDriver(string driverId)
    {
        try
        {
            var trips = await _tripService.GetTripsByDriverAsync(
                driverId,
                await ResolveAllowedSiteCodesAsync()
            );
            var tripDtos = trips.Select(t => new TripDto
            {
                TripAuthorityCode = t.trip_authority_code,
                ContractCode = t.contract_code,
                VmfCode = t.Contract?.vmf_code,
                SiteCode = t.Contract?.site_code,
                ApproverName = t.approver_name,
                ApproverRank = t.approver_rank,
                ApproverTelephone = t.approver_tel,
                EndOdometer = t.end_odo_meter,
                ExpiryDate = t.expiry_date,
                TripReason = t.trip_reason,
                TripRequestNumber = t.trip_request_number,
                IssueDate = t.issue_date,
                TripTypeCode = t.trip_type_code,
                TripIncidentTypeCode = t.trip_incident_type_code,
                UserAccessCode = t.user_access_code,
                LockedForTransfer = t.locked_for_transfer,
                TripIsMonthly = t.Trip_Is_Monthly,
            });
            return Ok(tripDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trips for driver {DriverId}", driverId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetTripsByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            var trips = await _tripService.GetTripsByDateRangeAsync(
                startDate,
                endDate,
                await ResolveAllowedSiteCodesAsync()
            );
            var tripDtos = trips.Select(t => new TripDto
            {
                TripAuthorityCode = t.trip_authority_code,
                ContractCode = t.contract_code,
                VmfCode = t.Contract?.vmf_code,
                SiteCode = t.Contract?.site_code,
                ApproverName = t.approver_name,
                ApproverRank = t.approver_rank,
                ApproverTelephone = t.approver_tel,
                EndOdometer = t.end_odo_meter,
                ExpiryDate = t.expiry_date,
                TripReason = t.trip_reason,
                TripRequestNumber = t.trip_request_number,
                IssueDate = t.issue_date,
                TripTypeCode = t.trip_type_code,
                TripIncidentTypeCode = t.trip_incident_type_code,
                UserAccessCode = t.user_access_code,
                LockedForTransfer = t.locked_for_transfer,
                TripIsMonthly = t.Trip_Is_Monthly,
            });
            return Ok(tripDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving trips for date range {StartDate} to {EndDate}",
                startDate,
                endDate
            );
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetRecentTrips([FromQuery] int days = 30)
    {
        try
        {
            var startDate = DateTime.Now.AddDays(-days);
            var endDate = DateTime.Now;

            return await GetTripsByDateRange(startDate, endDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent trips for {Days} days", days);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<TripDto>> CreateTrip([FromBody] CreateTripDto createTripDto)
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var contract = await _contractRepository.GetByIdAsync(createTripDto.ContractCode);
            if (contract is null)
                return BadRequest(new { message = "The selected contract was not found." });
            if (allowedSites is not null && !allowedSites.Contains(contract.site_code))
                return Forbid();

            var trip = new Trip
            {
                contract_code = createTripDto.ContractCode,
                approver_name = createTripDto.ApproverName,
                approver_rank = createTripDto.ApproverRank,
                approver_tel = createTripDto.ApproverTelephone,
                end_odo_meter = createTripDto.EndOdometer,
                expiry_date = createTripDto.ExpiryDate,
                trip_reason = createTripDto.TripReason,
                trip_request_number = createTripDto.TripRequestNumber,
                issue_date = createTripDto.IssueDate,
                trip_type_code = createTripDto.TripTypeCode,
                trip_incident_type_code = createTripDto.TripIncidentTypeCode,
                user_access_code = CurrentUserCode(),
                locked_for_transfer = createTripDto.LockedForTransfer,
                Trip_Is_Monthly = createTripDto.TripIsMonthly,
            };

            var createdTrip = await _tripService.CreateTripAsync(trip);

            var tripDto = new TripDto
            {
                TripAuthorityCode = createdTrip.trip_authority_code,
                ContractCode = createdTrip.contract_code,
                VmfCode = createdTrip.Contract?.vmf_code,
                SiteCode = createdTrip.Contract?.site_code,
                ApproverName = createdTrip.approver_name,
                ApproverRank = createdTrip.approver_rank,
                ApproverTelephone = createdTrip.approver_tel,
                EndOdometer = createdTrip.end_odo_meter,
                ExpiryDate = createdTrip.expiry_date,
                TripReason = createdTrip.trip_reason,
                TripRequestNumber = createdTrip.trip_request_number,
                IssueDate = createdTrip.issue_date,
                TripTypeCode = createdTrip.trip_type_code,
                TripIncidentTypeCode = createdTrip.trip_incident_type_code,
                UserAccessCode = createdTrip.user_access_code,
                LockedForTransfer = createdTrip.locked_for_transfer,
                TripIsMonthly = createdTrip.Trip_Is_Monthly,
            };

            return CreatedAtAction(
                nameof(GetTrip),
                new { id = createdTrip.trip_authority_code },
                tripDto
            );
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy trip/route accounting workflow is unavailable");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The legacy trip/route accounting workflow is unavailable. No trip was written.", source = "legacy-trigger-required" }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("with-details")]
    public async Task<ActionResult<TripDto>> CreateTripAuthority(
        [FromBody] CreateTripAuthorityDto request
    )
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var contract = await _contractRepository.GetByIdAsync(request.ContractCode);
            if (contract is null)
                return BadRequest(new { message = "The selected contract was not found." });
            if (allowedSites is not null && !allowedSites.Contains(contract.site_code))
                return Forbid();

            var trip = new Trip
            {
                contract_code = request.ContractCode,
                approver_name = request.ApproverName,
                approver_rank = request.ApproverRank,
                approver_tel = request.ApproverTelephone,
                expiry_date = request.ExpiryDate,
                trip_reason = request.TripReason,
                trip_request_number = request.TripRequestNumber,
                issue_date = request.IssueDate,
                trip_type_code = request.TripTypeCode,
                trip_incident_type_code = request.TripIncidentTypeCode,
                user_access_code = CurrentUserCode(),
                locked_for_transfer = false,
                Trip_Is_Monthly = request.TripIsMonthly,
            };

            var createdTrip = await _tripService.CreateTripAuthorityAsync(
                trip,
                request
                    .Drivers.Select(driver => new TripAuthorityDriverInput(
                        driver.Name,
                        driver.IdentityNumber,
                        driver.IsPrimary,
                        driver.SiteCode,
                        driver.LicenceTypeCode,
                        driver.PassportNumber,
                        driver.PersalNumber,
                        driver.ContractNumber,
                        driver.LicenceNumber,
                        driver.LicenceIssueDate,
                        driver.LicenceLastVerifiedDate,
                        driver.HasPdp,
                        driver.PdpExpiryDate,
                        driver.LicenceExpiryDate,
                        driver.IsActive
                    ))
                    .ToArray(),
                request
                    .Passengers.Where(passenger => !string.IsNullOrWhiteSpace(passenger.Name))
                    .Select(passenger => new TripAuthorityPassengerInput(passenger.Name.Trim()))
                    .ToArray(),
                request
                    .Routes.Select(route => new TripAuthorityRouteInput(
                        route.StartDate,
                        route.EndDate,
                        route.StartLocation,
                        route.EndLocation,
                        route.EstimatedDistance,
                        route.ResponsibilityCode.Trim(),
                        route.ObjectiveCode.Trim(),
                        route.ProjectNumber.Trim(),
                        route.FundCode.Trim()
                    ))
                    .ToArray()
            );

            return CreatedAtAction(
                nameof(GetTrip),
                new { id = createdTrip.trip_authority_code },
                MapTrip(createdTrip)
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy trip/route accounting workflow is unavailable for contract {ContractCode}", request.ContractCode);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The legacy trip/route accounting workflow is unavailable. No trip was written.", source = "legacy-trigger-required" }
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Trip authority creation was rejected for contract {ContractCode}",
                request.ContractCode
            );
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip authority with related records");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TripDto>> UpdateTrip(
        int id,
        [FromBody] UpdateTripDto updateTripDto
    )
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var existingTrip = await _tripService.GetTripByIdAsync(id, allowedSites);
            if (existingTrip == null)
            {
                return NotFound();
            }

            if (updateTripDto.ContractCode != existingTrip.contract_code)
                return BadRequest(new { message = "A trip authority cannot be moved to another contract." });

            existingTrip.contract_code = updateTripDto.ContractCode;
            existingTrip.approver_name = updateTripDto.ApproverName;
            existingTrip.approver_rank = updateTripDto.ApproverRank;
            existingTrip.approver_tel = updateTripDto.ApproverTelephone;
            existingTrip.end_odo_meter = updateTripDto.EndOdometer;
            existingTrip.expiry_date = updateTripDto.ExpiryDate;
            existingTrip.trip_reason = updateTripDto.TripReason;
            existingTrip.trip_request_number = updateTripDto.TripRequestNumber;
            existingTrip.issue_date = updateTripDto.IssueDate;
            existingTrip.trip_type_code = updateTripDto.TripTypeCode;
            existingTrip.trip_incident_type_code = updateTripDto.TripIncidentTypeCode;
            existingTrip.Trip_Is_Monthly = updateTripDto.TripIsMonthly;

            await _tripService.UpdateTripAsync(existingTrip, allowedSites);

            var tripDto = new TripDto
            {
                TripAuthorityCode = existingTrip.trip_authority_code,
                ContractCode = existingTrip.contract_code,
                VmfCode = existingTrip.Contract?.vmf_code,
                SiteCode = existingTrip.Contract?.site_code,
                ApproverName = existingTrip.approver_name,
                ApproverRank = existingTrip.approver_rank,
                ApproverTelephone = existingTrip.approver_tel,
                EndOdometer = existingTrip.end_odo_meter,
                ExpiryDate = existingTrip.expiry_date,
                TripReason = existingTrip.trip_reason,
                TripRequestNumber = existingTrip.trip_request_number,
                IssueDate = existingTrip.issue_date,
                TripTypeCode = existingTrip.trip_type_code,
                TripIncidentTypeCode = existingTrip.trip_incident_type_code,
                UserAccessCode = existingTrip.user_access_code,
                LockedForTransfer = existingTrip.locked_for_transfer,
                TripIsMonthly = existingTrip.Trip_Is_Monthly,
            };

            return Ok(tripDto);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy trip/route accounting workflow is unavailable for trip {TripId}", id);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The legacy trip/route accounting workflow is unavailable. No trip change was written.", source = "legacy-trigger-required" }
            );
        }
        catch (Exception ex)
            {
            _logger.LogError(ex, "Error updating trip with id {TripId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/close")]
    public async Task<ActionResult<TripDto>> CloseTrip(int id, [FromBody] CloseTripDto closeTripDto)
    {
        try
        {
            if (closeTripDto.EndOdometer is < 0)
            {
                return BadRequest("End odometer cannot be negative.");
            }

            var routeUpdates = new List<TripAuthorityRouteUpdate>();
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var details = await _tripService.GetTripAuthorityDetailsAsync(id, allowedSites);
            if (details is null)
            {
                return NotFound();
            }

            foreach (var route in closeTripDto.Routes ?? [])
            {
                if (route.RouteCode <= 0 || route.EndOdometer is null || route.EndOdometer < 0)
                {
                    return BadRequest("Every route must include a valid end odometer.");
                }

                var existingRoute = details.Routes.FirstOrDefault(item =>
                    item.RouteCode == route.RouteCode
                );
                if (existingRoute is null)
                {
                    return BadRequest($"Route {route.RouteCode} does not belong to trip {id}.");
                }

                var distance = existingRoute.StartOdometer.HasValue
                    ? route.EndOdometer.Value - existingRoute.StartOdometer.Value
                    : route.EndOdometer.Value;
                routeUpdates.Add(
                    new TripAuthorityRouteUpdate(route.RouteCode, route.EndOdometer.Value, distance)
                );
            }

            await _tripService.CloseTripAsync(
                id,
                routeUpdates,
                closeTripDto.EndOdometer,
                allowedSites
            );
            var trip = await _tripService.GetTripByIdAsync(id, allowedSites);
            return trip is null ? NotFound() : Ok(MapTrip(trip));
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy trip/route accounting workflow is unavailable for trip {TripId}", id);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The legacy trip/route accounting workflow is unavailable. No trip close was written.", source = "legacy-trigger-required" }
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Trip {TripId} could not be closed", id);
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Trip {TripId} close request contained duplicate or invalid routes",
                id
            );
            return BadRequest("The trip close request contains duplicate or invalid routes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing trip with id {TripId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteTrip(int id)
    {
        try
        {
            var existingTrip = await _tripService.GetTripByIdAsync(
                id,
                await ResolveAllowedSiteCodesAsync()
            );
            if (existingTrip == null)
            {
                return NotFound();
            }

            await _tripService.DeleteTripAsync(id, await ResolveAllowedSiteCodesAsync());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trip with id {TripId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private short? CurrentUserCode()
    {
        var userId = GetCurrentUserId();
        return userId is > 0 and <= short.MaxValue ? (short)userId : null;
    }

    private async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalTripScope())
            return null;

        var userId = GetCurrentUserId();
        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userId)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return new HashSet<short>();

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
            return new HashSet<short>();

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (
            HasRole("Vehicle List for All Departments in Province")
            && profileSite.province_code.HasValue
        )
        {
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        }
        else if (
            HasRole("Vehicle List for All Sites in Department")
            && profileSite.Depatrment_code.HasValue
        )
        {
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        }
        else
        {
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);
        }

        return sites.Select(site => site.Site_code).ToHashSet();
    }

    private bool HasGlobalTripScope() =>
        HasRole("SystemAdministrator") || HasRole("System Administrator");

    private bool HasRole(string expectedRole) =>
        User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            )
            .Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));

    private static bool TryBuildVehiclePageQuery(
        int page,
        int pageSize,
        string? searchMode,
        string? number,
        short? departmentCode,
        short? siteCode,
        int? authority,
        out TripAuthorityVehiclePageQuery query,
        out string? error
    )
    {
        var normalizedSearchMode = string.IsNullOrWhiteSpace(searchMode)
            ? "GG"
            : searchMode.Trim().ToUpperInvariant();
        if (normalizedSearchMode is not ("GG" or "GP"))
        {
            query = default!;
            error = "Search mode must be GG or GP.";
            return false;
        }

        var normalizedNumber = string.IsNullOrWhiteSpace(number) ? null : number.Trim();
        if (normalizedNumber?.Length > 50)
        {
            query = default!;
            error = "Number search cannot exceed 50 characters.";
            return false;
        }

        if (departmentCode is <= 0 || siteCode is <= 0 || authority is <= 0)
        {
            query = default!;
            error = "Department, site, and authority filters must be positive numbers.";
            return false;
        }

        query = new TripAuthorityVehiclePageQuery(
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, MaximumPageSize),
            normalizedSearchMode,
            normalizedNumber,
            departmentCode,
            siteCode,
            authority
        );
        error = null;
        return true;
    }

    private static object MapTripAuthorityVehiclePage(TripAuthorityVehiclePage result) =>
        new
        {
            items = result.Items.Select(item => new TripAuthorityVehiclePageItemDto
            {
                VmfCode = item.VmfCode,
                ContractCode = item.ContractCode,
                SiteCode = item.SiteCode,
                FleetNumber = item.FleetNumber,
                RegistrationNumber = item.RegistrationNumber,
                LicenceDueDate = item.LicenceDueDate,
                MakeDescription = item.MakeDescription,
                ModelDescription = item.ModelDescription,
                ContractType = item.ContractType,
                TripAuthorityCode = item.TripAuthorityCode,
            }),
            page = result.Page,
            pageSize = result.PageSize,
            total = result.Total,
            totalPages = result.TotalPages,
        };

    private static TripDto MapTrip(Trip trip) =>
        new()
        {
            TripAuthorityCode = trip.trip_authority_code,
            ContractCode = trip.contract_code,
            VmfCode = trip.Contract?.vmf_code,
            SiteCode = trip.Contract?.site_code,
            ApproverName = trip.approver_name,
            ApproverRank = trip.approver_rank,
            ApproverTelephone = trip.approver_tel,
            EndOdometer = trip.end_odo_meter,
            ExpiryDate = trip.expiry_date,
            TripReason = trip.trip_reason,
            TripRequestNumber = trip.trip_request_number,
            IssueDate = trip.issue_date,
            TripTypeCode = trip.trip_type_code,
            TripIncidentTypeCode = trip.trip_incident_type_code,
            UserAccessCode = trip.user_access_code,
            LockedForTransfer = trip.locked_for_transfer,
            TripIsMonthly = trip.Trip_Is_Monthly,
        };
}
