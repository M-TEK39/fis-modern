using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for trip driver assignment operations
/// </summary>
[ApiController]
// Trip drivers are embedded in Trip Authorities. The legacy menu exposes
// that module only to its entitlement (or the separate driver-management
// administrators); do not leave this compatibility controller open to every
// authenticated user.
[Authorize(Roles = "Trip Authorities,TripAuthorities,Driver and Authoriser Management,SystemAdministrator,System Administrator")]
[Route("api/[controller]")]
public class TripDriverController : BaseApiController
{
    private readonly ITripDriverRepository _tripDriverRepository;
    private readonly ITripRepository _tripRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly ILogger<TripDriverController> _logger;

    public TripDriverController(
        ITripDriverRepository tripDriverRepository,
        ITripRepository tripRepository,
        LegacyVehicleScopeService vehicleScope,
        ILogger<TripDriverController> logger
    )
    {
        _tripDriverRepository = tripDriverRepository;
        _tripRepository = tripRepository;
        _vehicleScope = vehicleScope;
        _logger = logger;
    }

    /// <summary>
    /// Get all trip drivers
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TripDriver>>> GetTripDrivers()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var tripDrivers = FilterByAllowedSites(
                await _tripDriverRepository.GetActiveDriversAsync(),
                allowedSites
            ).ToList();
            _logger.LogInformation("Retrieved {Count} active trip drivers", tripDrivers.Count());
            return Ok(tripDrivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trip drivers");
            return StatusCode(500, "An error occurred while retrieving trip drivers");
        }
    }

    /// <summary>
    /// Get a trip driver by code
    /// </summary>
    [HttpGet("{tripDriverCode}")]
    public async Task<ActionResult<TripDriver>> GetTripDriver(int tripDriverCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var tripDriver = await _tripDriverRepository.GetByIdAsync(tripDriverCode);
            if (tripDriver == null)
            {
                _logger.LogWarning(
                    "Trip driver with code {TripDriverCode} not found",
                    tripDriverCode
                );
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            if (!await IsSiteAllowedAsync(tripDriver.site_code))
            {
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            _logger.LogInformation(
                "Retrieved trip driver {TripDriverCode}: {DriverName}",
                tripDriverCode,
                tripDriver.trip_driver_name
            );
            return Ok(tripDriver);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trip driver {TripDriverCode}", tripDriverCode);
            return StatusCode(500, "An error occurred while retrieving the trip driver");
        }
    }

    /// <summary>
    /// Get trip drivers by site
    /// </summary>
    [HttpGet("site/{siteCode}")]
    public async Task<ActionResult<IEnumerable<TripDriver>>> GetTripDriversBySite(int siteCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (!await IsSiteAllowedAsync(siteCode))
            {
                return Forbid();
            }

            var tripDrivers = await _tripDriverRepository.GetBySiteAsync(siteCode);
            _logger.LogInformation(
                "Found {Count} trip drivers for site {SiteCode}",
                tripDrivers.Count(),
                siteCode
            );
            return Ok(tripDrivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trip drivers for site {SiteCode}", siteCode);
            return StatusCode(500, "An error occurred while retrieving trip drivers for the site");
        }
    }

    /// <summary>
    /// Get primary drivers only
    /// </summary>
    [HttpGet("primary")]
    public async Task<ActionResult<IEnumerable<TripDriver>>> GetPrimaryDrivers()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var primaryDrivers = FilterByAllowedSites(
                await _tripDriverRepository.GetPrimaryDriversAsync(),
                await ResolveAllowedSiteCodesAsync()
            ).ToList();
            _logger.LogInformation("Found {Count} primary drivers", primaryDrivers.Count());
            return Ok(primaryDrivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving primary drivers");
            return StatusCode(500, "An error occurred while retrieving primary drivers");
        }
    }

    /// <summary>
    /// Search trip drivers by name or ID
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<TripDriver>>> SearchTripDrivers(
        [FromQuery] string? searchTerm
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var tripDrivers = FilterByAllowedSites(
                await _tripDriverRepository.SearchDriversAsync(searchTerm ?? ""),
                await ResolveAllowedSiteCodesAsync()
            ).ToList();
            _logger.LogInformation(
                "Found {Count} trip drivers matching search term '{SearchTerm}'",
                tripDrivers.Count(),
                searchTerm
            );
            return Ok(tripDrivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching trip drivers with term '{SearchTerm}'",
                searchTerm
            );
            return StatusCode(500, "An error occurred while searching trip drivers");
        }
    }

    /// <summary>
    /// Create a new trip driver
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TripDriver>> CreateTripDriver([FromBody] TripDriver tripDriver)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (tripDriver is null || tripDriver.trip_authority_code <= 0)
            {
                return BadRequest("A valid trip authority is required.");
            }

            if (!await IsSiteAllowedAsync(tripDriver.site_code))
            {
                return Forbid();
            }

            var authority = await _tripRepository.GetByIdAsync(
                tripDriver.trip_authority_code,
                await ResolveAllowedSiteCodesAsync()
            );
            if (authority is null)
            {
                return NotFound($"Trip authority {tripDriver.trip_authority_code} not found");
            }

            if (authority.Contract?.site_code is short authoritySite
                && tripDriver.site_code.HasValue
                && tripDriver.site_code.Value != authoritySite)
            {
                return BadRequest("A trip driver must belong to the same site as its trip authority.");
            }

            var createdTripDriver = await _tripDriverRepository.CreateAsync(
                tripDriver,
                currentUserId
            );
            _logger.LogInformation(
                "Created trip driver {TripDriverCode}: {DriverName}",
                createdTripDriver.trip_driver_code,
                createdTripDriver.trip_driver_name
            );

            return CreatedAtAction(
                nameof(GetTripDriver),
                new { tripDriverCode = createdTripDriver.trip_driver_code },
                createdTripDriver
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating trip driver {DriverName}",
                tripDriver.trip_driver_name
            );
            return StatusCode(500, "An error occurred while creating the trip driver");
        }
    }

    /// <summary>
    /// Update an existing trip driver
    /// </summary>
    [HttpPut("{tripDriverCode}")]
    public async Task<ActionResult<TripDriver>> UpdateTripDriver(
        int tripDriverCode,
        [FromBody] TripDriver tripDriver
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (tripDriver is null)
            {
                return BadRequest("Trip driver data is required");
            }

            if (tripDriverCode != tripDriver.trip_driver_code)
            {
                return BadRequest("Trip driver code mismatch");
            }

            var existingTripDriver = await _tripDriverRepository.GetByIdAsync(tripDriverCode);
            if (existingTripDriver == null)
            {
                _logger.LogWarning(
                    "Trip driver with code {TripDriverCode} not found for update",
                    tripDriverCode
                );
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            if (!await IsSiteAllowedAsync(existingTripDriver.site_code))
            {
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            if (tripDriver.site_code != existingTripDriver.site_code)
            {
                return BadRequest("A trip driver cannot be moved to another site by editing the assignment.");
            }

            await _tripDriverRepository.UpdateAsync(tripDriver, currentUserId);
            _logger.LogInformation(
                "Updated trip driver {TripDriverCode}: {DriverName}",
                tripDriverCode,
                tripDriver.trip_driver_name
            );

            return Ok(tripDriver);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trip driver {TripDriverCode}", tripDriverCode);
            return StatusCode(500, "An error occurred while updating the trip driver");
        }
    }

    /// <summary>
    /// Delete a trip driver
    /// </summary>
    [HttpDelete("{tripDriverCode}")]
    public async Task<ActionResult> DeleteTripDriver(int tripDriverCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingTripDriver = await _tripDriverRepository.GetByIdAsync(tripDriverCode);
            if (existingTripDriver == null)
            {
                _logger.LogWarning(
                    "Trip driver with code {TripDriverCode} not found for deletion",
                    tripDriverCode
                );
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            if (!await IsSiteAllowedAsync(existingTripDriver.site_code))
            {
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            await _tripDriverRepository.DeleteAsync(tripDriverCode, currentUserId);
            _logger.LogInformation(
                "Deleted trip driver {TripDriverCode}: {DriverName}",
                tripDriverCode,
                existingTripDriver.trip_driver_name
            );

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trip driver {TripDriverCode}", tripDriverCode);
            return StatusCode(500, "An error occurred while deleting the trip driver");
        }
    }

    private Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync() =>
        _vehicleScope.ResolveAllowedSiteCodesAsync(User, HttpContext.RequestAborted);

    private async Task<bool> IsSiteAllowedAsync(int? siteCode)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        return allowedSites is null
            || siteCode is > 0 and <= short.MaxValue
                && allowedSites.Contains((short)siteCode.Value);
    }

    private static IEnumerable<TripDriver> FilterByAllowedSites(
        IEnumerable<TripDriver> drivers,
        IReadOnlySet<short>? allowedSites
    ) => allowedSites is null
        ? drivers
        : drivers.Where(driver =>
            driver.site_code is > 0 and <= short.MaxValue
                && allowedSites.Contains((short)driver.site_code.Value));
}
