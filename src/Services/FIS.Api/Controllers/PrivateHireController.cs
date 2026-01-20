using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for private hire vehicle operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PrivateHireController : ControllerBase
{
    private readonly IPrivateHireRepository _privateHireRepository;
    private readonly ILogger<PrivateHireController> _logger;

    public PrivateHireController(
        IPrivateHireRepository privateHireRepository,
        ILogger<PrivateHireController> logger)
    {
        _privateHireRepository = privateHireRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all private hire vehicles
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHires()
    {
        try
        {
            var privateHires = await _privateHireRepository.GetActiveHiresAsync();
            _logger.LogInformation("Retrieved {Count} active private hires", privateHires.Count());
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires");
            return StatusCode(500, "An error occurred while retrieving private hires");
        }
    }

    /// <summary>
    /// Get a private hire vehicle by code
    /// </summary>
    [HttpGet("{privateHireCode}")]
    public async Task<ActionResult<PrivateHire>> GetPrivateHire(int privateHireCode)
    {
        try
        {
            var privateHire = await _privateHireRepository.GetByIdAsync(privateHireCode);
            if (privateHire == null)
            {
                _logger.LogWarning("Private hire with code {PrivateHireCode} not found", privateHireCode);
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            _logger.LogInformation("Retrieved private hire {PrivateHireCode}: {Registration}", 
                privateHireCode, privateHire.registration_number);
            return Ok(privateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hire {PrivateHireCode}", privateHireCode);
            return StatusCode(500, "An error occurred while retrieving the private hire vehicle");
        }
    }

    /// <summary>
    /// Get private hires by vehicle VMF code
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHiresByVehicle(int vmfCode)
    {
        try
        {
            var privateHires = await _privateHireRepository.GetByVehicleAsync(vmfCode);
            _logger.LogInformation("Found {Count} private hires for vehicle {VmfCode}", privateHires.Count(), vmfCode);
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while retrieving private hires for the vehicle");
        }
    }

    /// <summary>
    /// Get private hires by date range
    /// </summary>
    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHiresByDateRange(
        [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate > endDate)
            {
                return BadRequest("Start date must be before or equal to end date");
            }

            var privateHires = await _privateHireRepository.GetByDateRangeAsync(startDate, endDate);
            _logger.LogInformation("Found {Count} private hires between {StartDate} and {EndDate}", 
                privateHires.Count(), startDate.ToShortDateString(), endDate.ToShortDateString());
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires for date range {StartDate} - {EndDate}", 
                startDate, endDate);
            return StatusCode(500, "An error occurred while retrieving private hires for the date range");
        }
    }

    /// <summary>
    /// Search private hire vehicles by registration or details
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> SearchPrivateHires([FromQuery] string? searchTerm)
    {
        try
        {
            var privateHires = await _privateHireRepository.SearchHiresAsync(searchTerm ?? "");
            _logger.LogInformation("Found {Count} private hires matching search term '{SearchTerm}'", 
                privateHires.Count(), searchTerm);
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching private hires with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching private hires");
        }
    }

    /// <summary>
    /// Create a new private hire vehicle
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PrivateHire>> CreatePrivateHire([FromBody] PrivateHire privateHire)
    {
        try
        {
            var createdPrivateHire = await _privateHireRepository.CreateAsync(privateHire);
            _logger.LogInformation("Created private hire {PrivateHireCode}: {Registration}", 
                createdPrivateHire.PHV_code, createdPrivateHire.registration_number);
            
            return CreatedAtAction(nameof(GetPrivateHire), 
                new { privateHireCode = createdPrivateHire.PHV_code }, createdPrivateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating private hire {Registration}", privateHire.registration_number);
            return StatusCode(500, "An error occurred while creating the private hire vehicle");
        }
    }

    /// <summary>
    /// Update an existing private hire vehicle
    /// </summary>
    [HttpPut("{privateHireCode}")]
    public async Task<ActionResult<PrivateHire>> UpdatePrivateHire(int privateHireCode, [FromBody] PrivateHire privateHire)
    {
        try
        {
            if (privateHireCode != privateHire.PHV_code)
            {
                return BadRequest("Private hire code mismatch");
            }

            var existingPrivateHire = await _privateHireRepository.GetByIdAsync(privateHireCode);
            if (existingPrivateHire == null)
            {
                _logger.LogWarning("Private hire with code {PrivateHireCode} not found for update", privateHireCode);
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            await _privateHireRepository.UpdateAsync(privateHire);
            _logger.LogInformation("Updated private hire {PrivateHireCode}: {Registration}", 
                privateHireCode, privateHire.registration_number);
            
            return Ok(privateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating private hire {PrivateHireCode}", privateHireCode);
            return StatusCode(500, "An error occurred while updating the private hire vehicle");
        }
    }

    /// <summary>
    /// Delete a private hire vehicle
    /// </summary>
    [HttpDelete("{privateHireCode}")]
    public async Task<ActionResult> DeletePrivateHire(int privateHireCode)
    {
        try
        {
            var existingPrivateHire = await _privateHireRepository.GetByIdAsync(privateHireCode);
            if (existingPrivateHire == null)
            {
                _logger.LogWarning("Private hire with code {PrivateHireCode} not found for deletion", privateHireCode);
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            await _privateHireRepository.DeleteAsync(privateHireCode);
            _logger.LogInformation("Deleted private hire {PrivateHireCode}: {Registration}", 
                privateHireCode, existingPrivateHire.registration_number);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting private hire {PrivateHireCode}", privateHireCode);
            return StatusCode(500, "An error occurred while deleting the private hire vehicle");
        }
    }
}