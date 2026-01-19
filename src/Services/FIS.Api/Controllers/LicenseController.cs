using FIS.Api.DTOs;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for License operations
/// Provides endpoints for managing license types and requirements
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseRepository _licenseRepository;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(ILicenseRepository licenseRepository, ILogger<LicenseController> logger)
    {
        _licenseRepository = licenseRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all licenses
    /// </summary>
    /// <returns>List of all license entities</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<License>>> GetAllLicenses()
    {
        try
        {
            var licenses = await _licenseRepository.GetAllLicensesAsync();
            return Ok(licenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all licenses");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get license by license code
    /// </summary>
    /// <param name="licenceCode">The license code to retrieve</param>
    /// <returns>License entity if found</returns>
    [HttpGet("{licenceCode}")]
    public async Task<ActionResult<License>> GetLicense(short licenceCode)
    {
        try
        {
            var license = await _licenseRepository.GetByIdAsync(licenceCode);
            if (license == null)
                return NotFound($"License with code {licenceCode} not found");

            return Ok(license);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving license with code {LicenceCode}", licenceCode);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get license by description
    /// </summary>
    /// <param name="description">The license description to search for</param>
    /// <returns>License entity if found</returns>
    [HttpGet("description/{description}")]
    public async Task<ActionResult<License>> GetLicenseByDescription(string description)
    {
        try
        {
            var license = await _licenseRepository.GetByDescriptionAsync(description);
            if (license == null)
                return NotFound($"License with description '{description}' not found");

            return Ok(license);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving license with description {Description}", description);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Search licenses by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching license entities</returns>
    [HttpGet("search/{searchTerm}")]
    public async Task<ActionResult<IEnumerable<License>>> SearchLicenses(string searchTerm)
    {
        try
        {
            var licenses = await _licenseRepository.SearchLicensesAsync(searchTerm);
            return Ok(licenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching licenses with term {SearchTerm}", searchTerm);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new license
    /// </summary>
    /// <param name="createLicenseDto">The license data to create</param>
    /// <returns>The created license entity</returns>
    [HttpPost]
    public async Task<ActionResult<License>> CreateLicense([FromBody] CreateLicenseDto createLicenseDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var license = new License
            {
                licence_description = createLicenseDto.licence_description,
                licence_category = createLicenseDto.licence_category
            };

            var createdLicense = await _licenseRepository.CreateAsync(license);
            return CreatedAtAction(nameof(GetLicense), new { licenceCode = createdLicense.licence_code }, createdLicense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating license");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing license
    /// </summary>
    /// <param name="licenceCode">The license code to update</param>
    /// <param name="license">The updated license data</param>
    /// <returns>The updated license entity</returns>
    [HttpPut("{licenceCode}")]
    public async Task<ActionResult<License>> UpdateLicense(short licenceCode, [FromBody] License license)
    {
        try
        {
            if (licenceCode != license.licence_code)
                return BadRequest("License code in URL does not match license code in body");

            var existingLicense = await _licenseRepository.GetByIdAsync(licenceCode);
            if (existingLicense == null)
                return NotFound($"License with code {licenceCode} not found");

            var updatedLicense = await _licenseRepository.UpdateAsync(license);
            return Ok(updatedLicense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating license with code {LicenceCode}", licenceCode);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a license
    /// </summary>
    /// <param name="licenceCode">The license code to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{licenceCode}")]
    public async Task<ActionResult> DeleteLicense(short licenceCode)
    {
        try
        {
            var deleted = await _licenseRepository.DeleteAsync(licenceCode);
            if (!deleted)
                return NotFound($"License with code {licenceCode} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting license with code {LicenceCode}", licenceCode);
            return StatusCode(500, "Internal server error");
        }
    }
}