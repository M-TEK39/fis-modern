using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public int DaysUntilExpiry => ExpiryDate.HasValue ? 
        (int)(ExpiryDate.Value - DateTime.Now).TotalDays : 0;
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
[Authorize]
[Route("api/[controller]")]
public class TripController : BaseApiController
{
    private readonly ITripService _tripService;
    private readonly ILogger<TripController> _logger;

    public TripController(ITripService tripService, ILogger<TripController> logger)
    {
        _tripService = tripService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripDto>>> GetAllTrips()
    {
        try
        {
            var trips = await _tripService.GetAllTripsAsync();
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
            var vehicles = await _tripService.GetTripAuthorityVehiclesAsync();
            return Ok(vehicles.Select(vehicle => new TripAuthorityVehicleDto
            {
                VmfCode = vehicle.VmfCode,
                ContractCode = vehicle.ContractCode,
                SiteCode = vehicle.SiteCode,
                FleetNumber = vehicle.FleetNumber,
                RegistrationNumber = vehicle.RegistrationNumber,
                LicenceDueDate = vehicle.LicenceDueDate,
                MakeDescription = vehicle.MakeDescription,
                ModelDescription = vehicle.ModelDescription,
                ContractType = vehicle.ContractType
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Trip Authority vehicles");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TripDto>> GetTrip(int id)
    {
        try
        {
            var trip = await _tripService.GetTripByIdAsync(id);
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
                TripIsMonthly = trip.Trip_Is_Monthly
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
            var details = await _tripService.GetTripAuthorityDetailsAsync(id);
            if (details == null)
            {
                return NotFound();
            }

            return Ok(new TripAuthorityDetailsDto
            {
                Trip = MapTrip(details.Trip),
                Drivers = details.Drivers.Select(driver => new TripAuthorityDriverDto
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
                    IsActive = driver.IsActive
                }).ToArray(),
                Passengers = details.Passengers.Select(passenger => new TripAuthorityPassengerDto
                {
                    TripPassengerCode = passenger.TripPassengerCode,
                    Name = passenger.Name
                }).ToArray(),
                Routes = details.Routes.Select(route => new TripAuthorityRouteDto
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
                    EditedByUserCode = route.EditedByUserCode
                }).ToArray()
            });
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
            var trips = await _tripService.GetTripsByVehicleAsync(vmfCode);
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
                TripIsMonthly = t.Trip_Is_Monthly
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
            var trips = await _tripService.GetTripsByDriverAsync(driverId);
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
                TripIsMonthly = t.Trip_Is_Monthly
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
        [FromQuery] DateTime endDate)
    {
        try
        {
            var trips = await _tripService.GetTripsByDateRangeAsync(startDate, endDate);
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
                TripIsMonthly = t.Trip_Is_Monthly
            });
            return Ok(tripDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trips for date range {StartDate} to {EndDate}", startDate, endDate);
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
                user_access_code = createTripDto.UserAccessCode,
                locked_for_transfer = createTripDto.LockedForTransfer,
                Trip_Is_Monthly = createTripDto.TripIsMonthly
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
                TripIsMonthly = createdTrip.Trip_Is_Monthly
            };

            return CreatedAtAction(nameof(GetTrip), new { id = createdTrip.trip_authority_code }, tripDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("with-details")]
    public async Task<ActionResult<TripDto>> CreateTripAuthority([FromBody] CreateTripAuthorityDto request)
    {
        try
        {
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
                user_access_code = request.UserAccessCode ?? (short?)GetCurrentUserId(),
                locked_for_transfer = false,
                Trip_Is_Monthly = request.TripIsMonthly
            };

            var createdTrip = await _tripService.CreateTripAuthorityAsync(
                trip,
                request.Drivers.Select(driver => new TripAuthorityDriverInput(
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
                    driver.IsActive)).ToArray(),
                request.Passengers
                    .Where(passenger => !string.IsNullOrWhiteSpace(passenger.Name))
                    .Select(passenger => new TripAuthorityPassengerInput(passenger.Name.Trim()))
                    .ToArray(),
                request.Routes.Select(route => new TripAuthorityRouteInput(
                    route.StartDate,
                    route.EndDate,
                    route.StartLocation,
                    route.EndLocation,
                    route.EstimatedDistance,
                    route.ResponsibilityCode.Trim(),
                    route.ObjectiveCode.Trim(),
                    route.ProjectNumber.Trim(),
                    route.FundCode.Trim())).ToArray());

            return CreatedAtAction(nameof(GetTrip), new { id = createdTrip.trip_authority_code }, MapTrip(createdTrip));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Trip authority creation was rejected for contract {ContractCode}", request.ContractCode);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip authority with related records");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TripDto>> UpdateTrip(int id, [FromBody] UpdateTripDto updateTripDto)
    {
        try
        {
            var existingTrip = await _tripService.GetTripByIdAsync(id);
            if (existingTrip == null)
            {
                return NotFound();
            }

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
            existingTrip.user_access_code = updateTripDto.UserAccessCode;
            existingTrip.locked_for_transfer = updateTripDto.LockedForTransfer;
            existingTrip.Trip_Is_Monthly = updateTripDto.TripIsMonthly;

            await _tripService.UpdateTripAsync(existingTrip);

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
                TripIsMonthly = existingTrip.Trip_Is_Monthly
            };

            return Ok(tripDto);
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
            var details = await _tripService.GetTripAuthorityDetailsAsync(id);
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

                var existingRoute = details.Routes.FirstOrDefault(item => item.RouteCode == route.RouteCode);
                if (existingRoute is null)
                {
                    return BadRequest($"Route {route.RouteCode} does not belong to trip {id}.");
                }

                var distance = existingRoute.StartOdometer.HasValue
                    ? route.EndOdometer.Value - existingRoute.StartOdometer.Value
                    : route.EndOdometer.Value;
                routeUpdates.Add(new TripAuthorityRouteUpdate(route.RouteCode, route.EndOdometer.Value, distance));
            }

            await _tripService.CloseTripAsync(id, routeUpdates, closeTripDto.EndOdometer);
            var trip = await _tripService.GetTripByIdAsync(id);
            return trip is null ? NotFound() : Ok(MapTrip(trip));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Trip {TripId} could not be closed", id);
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Trip {TripId} close request contained duplicate or invalid routes", id);
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
            var existingTrip = await _tripService.GetTripByIdAsync(id);
            if (existingTrip == null)
            {
                return NotFound();
            }

            await _tripService.DeleteTripAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trip with id {TripId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private static TripDto MapTrip(Trip trip)
        => new()
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
            TripIsMonthly = trip.Trip_Is_Monthly
        };
}
