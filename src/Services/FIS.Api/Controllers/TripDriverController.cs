using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for trip driver assignment operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TripDriverController : ControllerBase
{
    private readonly ITripDriverRepository _tripDriverRepository;
    private readonly ILogger<TripDriverController> _logger;

    public TripDriverController(
        ITripDriverRepository tripDriverRepository,
        ILogger<TripDriverController> logger)
    {
        _tripDriverRepository = tripDriverRepository;
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
            var tripDrivers = await _tripDriverRepository.GetActiveDriversAsync();
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
            var tripDriver = await _tripDriverRepository.GetByIdAsync(tripDriverCode);
            if (tripDriver == null)
            {
                _logger.LogWarning("Trip driver with code {TripDriverCode} not found", tripDriverCode);
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            _logger.LogInformation("Retrieved trip driver {TripDriverCode}: {DriverName}", tripDriverCode, tripDriver.trip_driver_name);
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
            var tripDrivers = await _tripDriverRepository.GetBySiteAsync(siteCode);
            _logger.LogInformation("Found {Count} trip drivers for site {SiteCode}", tripDrivers.Count(), siteCode);
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
            var primaryDrivers = await _tripDriverRepository.GetPrimaryDriversAsync();
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
    public async Task<ActionResult<IEnumerable<TripDriver>>> SearchTripDrivers([FromQuery] string? searchTerm)
    {
        try
        {
            var tripDrivers = await _tripDriverRepository.SearchDriversAsync(searchTerm ?? "");
            _logger.LogInformation("Found {Count} trip drivers matching search term '{SearchTerm}'", 
                tripDrivers.Count(), searchTerm);
            return Ok(tripDrivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching trip drivers with term '{SearchTerm}'", searchTerm);
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
            var createdTripDriver = await _tripDriverRepository.CreateAsync(tripDriver);
            _logger.LogInformation("Created trip driver {TripDriverCode}: {DriverName}", 
                createdTripDriver.trip_driver_code, createdTripDriver.trip_driver_name);
            
            return CreatedAtAction(nameof(GetTripDriver), 
                new { tripDriverCode = createdTripDriver.trip_driver_code }, createdTripDriver);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip driver {DriverName}", tripDriver.trip_driver_name);
            return StatusCode(500, "An error occurred while creating the trip driver");
        }
    }

    /// <summary>
    /// Update an existing trip driver
    /// </summary>
    [HttpPut("{tripDriverCode}")]
    public async Task<ActionResult<TripDriver>> UpdateTripDriver(int tripDriverCode, [FromBody] TripDriver tripDriver)
    {
        try
        {
            if (tripDriverCode != tripDriver.trip_driver_code)
            {
                return BadRequest("Trip driver code mismatch");
            }

            var existingTripDriver = await _tripDriverRepository.GetByIdAsync(tripDriverCode);
            if (existingTripDriver == null)
            {
                _logger.LogWarning("Trip driver with code {TripDriverCode} not found for update", tripDriverCode);
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            await _tripDriverRepository.UpdateAsync(tripDriver);
            _logger.LogInformation("Updated trip driver {TripDriverCode}: {DriverName}", 
                tripDriverCode, tripDriver.trip_driver_name);
            
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
            var existingTripDriver = await _tripDriverRepository.GetByIdAsync(tripDriverCode);
            if (existingTripDriver == null)
            {
                _logger.LogWarning("Trip driver with code {TripDriverCode} not found for deletion", tripDriverCode);
                return NotFound($"Trip driver with code {tripDriverCode} not found");
            }

            await _tripDriverRepository.DeleteAsync(tripDriverCode);
            _logger.LogInformation("Deleted trip driver {TripDriverCode}: {DriverName}", 
                tripDriverCode, existingTripDriver.trip_driver_name);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trip driver {TripDriverCode}", tripDriverCode);
            return StatusCode(500, "An error occurred while deleting the trip driver");
        }
    }
}