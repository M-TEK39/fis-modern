using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for location management operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LocationController : BaseApiController
{
    private readonly ILocationRepository _locationRepository;
    private readonly ILogger<LocationController> _logger;

    public LocationController(
        ILocationRepository locationRepository,
        ILogger<LocationController> logger
    )
    {
        _locationRepository = locationRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all locations
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocations()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var locations = await _locationRepository.GetAllLocationsAsync();
            _logger.LogInformation("Retrieved {Count} active locations", locations.Count());
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations");
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Get a page of active locations without changing the legacy collection response.
    /// </summary>
    [HttpGet("page")]
    public async Task<IActionResult> GetLocationsPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        try
        {
            var result = await _locationRepository.GetPageAsync(
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100)
            );

            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged locations");
            return StatusCode(500, "An error occurred while retrieving locations");
        }
    }

    /// <summary>
    /// Get a location by ID
    /// </summary>
    [HttpGet("{locationId}")]
    public async Task<ActionResult<Location>> GetLocation(int locationId)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var location = await _locationRepository.GetByIdAsync(locationId);
            if (location == null)
            {
                _logger.LogWarning("Location with ID {LocationId} not found", locationId);
                return NotFound($"Location with ID {locationId} not found");
            }

            _logger.LogInformation(
                "Retrieved location {LocationId}: {LocationName}",
                locationId,
                location.LocationName
            );
            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while retrieving the location");
        }
    }

    /// <summary>
    /// Get a location by name
    /// </summary>
    [HttpGet("name/{locationName}")]
    public async Task<ActionResult<Location>> GetLocationByName(string locationName)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var location = await _locationRepository.GetByNameAsync(locationName);
            if (location == null)
            {
                _logger.LogWarning("Location with name '{LocationName}' not found", locationName);
                return NotFound($"Location with name '{locationName}' not found");
            }

            _logger.LogInformation(
                "Retrieved location by name '{LocationName}': {LocationId}",
                locationName,
                location.LocationId
            );
            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving location by name '{LocationName}'",
                locationName
            );
            return StatusCode(500, "An error occurred while retrieving the location by name");
        }
    }

    /// <summary>
    /// Get locations by country
    /// </summary>
    [HttpGet("country/{country}")]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocationsByCountry(string country)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var locations = await _locationRepository.GetByCountryAsync(country);
            _logger.LogInformation(
                "Found {Count} locations in country '{Country}'",
                locations.Count(),
                country
            );
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations for country '{Country}'", country);
            return StatusCode(500, "An error occurred while retrieving locations for the country");
        }
    }

    /// <summary>
    /// Get locations by province/state
    /// </summary>
    [HttpGet("province/{province}")]
    public async Task<ActionResult<IEnumerable<Location>>> GetLocationsByProvince(string province)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var locations = await _locationRepository.GetByProvinceAsync(province);
            _logger.LogInformation(
                "Found {Count} locations in province '{Province}'",
                locations.Count(),
                province
            );
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locations for province '{Province}'", province);
            return StatusCode(500, "An error occurred while retrieving locations for the province");
        }
    }

    /// <summary>
    /// Search locations by name, description, city, province, or country
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Location>>> SearchLocations(
        [FromQuery] string? searchTerm
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var locations = await _locationRepository.SearchLocationsAsync(searchTerm ?? "");
            _logger.LogInformation(
                "Found {Count} locations matching search term '{SearchTerm}'",
                locations.Count(),
                searchTerm
            );
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching locations with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching locations");
        }
    }

    /// <summary>
    /// Create a new location
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult<Location>> CreateLocation([FromBody] Location location)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            // Check if location name already exists
            var existingLocation = await _locationRepository.GetByNameAsync(location.LocationName);
            if (existingLocation != null)
            {
                _logger.LogWarning(
                    "Attempt to create location with duplicate name: {LocationName}",
                    location.LocationName
                );
                return Conflict($"Location with name '{location.LocationName}' already exists");
            }

            location.IsActive = true;
            var createdLocation = await _locationRepository.CreateAsync(location, currentUserId);
            _logger.LogInformation(
                "Created location {LocationId}: {LocationName}",
                createdLocation.LocationId,
                createdLocation.LocationName
            );

            return CreatedAtAction(
                nameof(GetLocation),
                new { locationId = createdLocation.LocationId },
                createdLocation
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location {LocationName}", location.LocationName);
            return StatusCode(500, "An error occurred while creating the location");
        }
    }

    /// <summary>
    /// Update an existing location
    /// </summary>
    [HttpPut("{locationId}")]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult<Location>> UpdateLocation(
        int locationId,
        [FromBody] Location location
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (locationId != location.LocationId)
            {
                return BadRequest("Location ID mismatch");
            }

            var existingLocation = await _locationRepository.GetByIdAsync(locationId);
            if (existingLocation == null)
            {
                _logger.LogWarning(
                    "Location with ID {LocationId} not found for update",
                    locationId
                );
                return NotFound($"Location with ID {locationId} not found");
            }

            // Check if new name conflicts with another location
            if (location.LocationName != existingLocation.LocationName)
            {
                var nameConflict = await _locationRepository.GetByNameAsync(location.LocationName);
                if (nameConflict != null && nameConflict.LocationId != locationId)
                {
                    _logger.LogWarning(
                        "Attempt to update location {LocationId} with duplicate name: {LocationName}",
                        locationId,
                        location.LocationName
                    );
                    return Conflict($"Location with name '{location.LocationName}' already exists");
                }
            }

            await _locationRepository.UpdateAsync(location, currentUserId);
            _logger.LogInformation(
                "Updated location {LocationId}: {LocationName}",
                locationId,
                location.LocationName
            );

            return Ok(location);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while updating the location");
        }
    }

    /// <summary>
    /// Delete a location (soft delete - marks as inactive)
    /// </summary>
    [HttpDelete("{locationId}")]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult> DeleteLocation(int locationId)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingLocation = await _locationRepository.GetByIdAsync(locationId);
            if (existingLocation == null)
            {
                _logger.LogWarning(
                    "Location with ID {LocationId} not found for deletion",
                    locationId
                );
                return NotFound($"Location with ID {locationId} not found");
            }

            await _locationRepository.DeleteAsync(locationId, currentUserId); // This performs soft delete
            _logger.LogInformation(
                "Soft deleted location {LocationId}: {LocationName}",
                locationId,
                existingLocation.LocationName
            );

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting location {LocationId}", locationId);
            return StatusCode(500, "An error occurred while deleting the location");
        }
    }
}
