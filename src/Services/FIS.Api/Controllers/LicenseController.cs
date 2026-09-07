using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for License operations
/// Provides endpoints for managing license types and requirements
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LicenseController : BaseApiController
{
    private readonly ILicenseRepository _licenseRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(
        ILicenseRepository licenseRepository,
        IVehicleRepository vehicleRepository,
        ILogger<LicenseController> logger)
    {
        _licenseRepository = licenseRepository;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    [HttpPost("one-vehicle/password")]
    public ActionResult SubmitOneVehiclePassword([FromBody] LicenseOneVehiclePasswordRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { success = false, message = "Password payload is required." });
        }

        var valid = string.Equals(request.password?.Trim(), "passop", StringComparison.Ordinal);
        if (!valid)
        {
            return Unauthorized(new { success = false, message = "Invalid password." });
        }

        return Ok(new { success = true, message = "Password accepted." });
    }

    [HttpPost("one-vehicle/lookup")]
    public async Task<ActionResult<LicenseOneVehicleLookupResponse>> LookupOneVehicle([FromBody] LicenseOneVehicleLookupRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.number_type) || string.IsNullOrWhiteSpace(request.number))
        {
            return BadRequest(new { success = false, message = "number_type and number are required." });
        }

        var numberType = request.number_type.Trim().ToUpperInvariant();
        var number = request.number.Trim();

        Vehicle? vehicle = numberType switch
        {
            "GG" => await _vehicleRepository.GetByFleetNumberAsync(number),
            "GP" => await _vehicleRepository.GetByRegistrationNumberAsync(number),
            _ => null
        };

        if (vehicle is null)
        {
            return NotFound(new { success = false, message = $"Vehicle not found for {numberType} number {number}." });
        }

        return Ok(new LicenseOneVehicleLookupResponse
        {
            vmfCode = vehicle.vmf_code,
            numberType = numberType,
            number = number,
            expDate = vehicle.licence_due_date,
            registerNumber = vehicle.lic_register_number,
            regDoc = vehicle.lic_registration_doc,
            tare = vehicle.tare?.ToString(),
            receiver = vehicle.Licence_receiver,
            receiverId = vehicle.Licence_receiver_id,
            receiverTel = vehicle.Licence_receiver_tel,
            receiverSiteCode = vehicle.Licence_receiver_site,
            dateCollected = vehicle.Licence_date_taken,
            cofRequired = vehicle.cof_required,
            cofExpDate = vehicle.cof_last_done,
            comments = vehicle.licence_comments
        });
    }

    [HttpPost("one-vehicle/save")]
    public async Task<ActionResult> SaveOneVehicle([FromBody] LicenseOneVehicleSaveRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { success = false, message = "Save payload is required." });
        }

        var currentUserId = GetCurrentUserId();
        var vmfCode = request.vmfCode;

        if (!vmfCode.HasValue || vmfCode.Value <= 0)
        {
            if (string.IsNullOrWhiteSpace(request.numberType) || string.IsNullOrWhiteSpace(request.number))
            {
                return BadRequest(new { success = false, message = "vmfCode or numberType/number is required." });
            }

            var fallbackNumberType = request.numberType.Trim().ToUpperInvariant();
            var fallbackNumber = request.number.Trim();
            var fallbackVehicle = fallbackNumberType switch
            {
                "GG" => await _vehicleRepository.GetByFleetNumberAsync(fallbackNumber),
                "GP" => await _vehicleRepository.GetByRegistrationNumberAsync(fallbackNumber),
                _ => null
            };

            if (fallbackVehicle is null)
            {
                return NotFound(new { success = false, message = "Vehicle could not be resolved for save." });
            }

            vmfCode = fallbackVehicle.vmf_code;
        }

        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode.Value);
        if (vehicle is null)
        {
            return NotFound(new { success = false, message = $"Vehicle {vmfCode.Value} not found." });
        }

        var oldDue = vehicle.licence_due_date?.Date;
        var newDue = request.expDate?.Date;
        var cofLastDone = string.Equals(request.cofRequired, "N", StringComparison.OrdinalIgnoreCase)
            ? null
            : request.cofExpDate;

        await _vehicleRepository.UpdateLicenceFieldsAsync(
            vmfCode.Value,
            new VehicleLicenceUpdate(
                request.expDate,
                request.registerNumber,
                request.regDoc,
                ParseNullableInt(request.tare),
                request.receiver,
                request.receiverId,
                request.receiverTel,
                request.receiverSiteCode,
                request.dateCollected,
                request.cofRequired,
                cofLastDone,
                request.comments),
            currentUserId);

        if (oldDue != newDue)
        {
            await _vehicleRepository.AddLicenceReceiveNoteAsync(vmfCode.Value, ResolveCurrentUsername(), currentUserId);
        }

        var updatedVehicle = await _vehicleRepository.GetByIdAsync(vmfCode.Value) ?? vehicle;

        return Ok(new
        {
            success = true,
            vmfCode = updatedVehicle.vmf_code,
            fleetNumber = updatedVehicle.fleet_number,
            registrationNumber = updatedVehicle.registration_number,
            message = "Licence details saved successfully."
        });
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
            int currentUserId = GetCurrentUserId();

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
            int currentUserId = GetCurrentUserId();

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
            int currentUserId = GetCurrentUserId();

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
            int currentUserId = GetCurrentUserId();

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
            int currentUserId = GetCurrentUserId();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var license = new License
            {
                licence_description = createLicenseDto.licence_description,
                licence_category = createLicenseDto.licence_category
            };

            var createdLicense = await _licenseRepository.CreateAsync(license, currentUserId);
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
            int currentUserId = GetCurrentUserId();

            if (licenceCode != license.licence_code)
                return BadRequest("License code in URL does not match license code in body");

            var existingLicense = await _licenseRepository.GetByIdAsync(licenceCode);
            if (existingLicense == null)
                return NotFound($"License with code {licenceCode} not found");

            var updatedLicense = await _licenseRepository.UpdateAsync(license, currentUserId);
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
            int currentUserId = GetCurrentUserId();

            var deleted = await _licenseRepository.DeleteAsync(licenceCode, currentUserId);
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

    private string ResolveCurrentUsername()
    {
        return User.Identity?.Name
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst("preferred_username")?.Value
            ?? "unknown";
    }

    private static int? ParseNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), out var parsed) ? parsed : null;
    }

    public sealed class LicenseOneVehiclePasswordRequest
    {
        public string? password { get; set; }
    }

    public sealed class LicenseOneVehicleLookupRequest
    {
        public string? number_type { get; set; }
        public string? number { get; set; }
    }

    public class LicenseOneVehicleLookupResponse
    {
        public int vmfCode { get; set; }
        public string? numberType { get; set; }
        public string? number { get; set; }
        public DateTime? expDate { get; set; }
        public string? registerNumber { get; set; }
        public string? regDoc { get; set; }
        public string? tare { get; set; }
        public string? receiver { get; set; }
        public string? receiverId { get; set; }
        public string? receiverTel { get; set; }
        public short? receiverSiteCode { get; set; }
        public DateTime? dateCollected { get; set; }
        public string? cofRequired { get; set; }
        public DateTime? cofExpDate { get; set; }
        public string? comments { get; set; }
    }

    public sealed class LicenseOneVehicleSaveRequest
    {
        public int? vmfCode { get; set; }
        public string? numberType { get; set; }
        public string? number { get; set; }
        public DateTime? expDate { get; set; }
        public string? registerNumber { get; set; }
        public string? regDoc { get; set; }
        public string? tare { get; set; }
        public string? receiver { get; set; }
        public string? receiverId { get; set; }
        public string? receiverTel { get; set; }
        public short? receiverSiteCode { get; set; }
        public DateTime? dateCollected { get; set; }
        public string? cofRequired { get; set; }
        public DateTime? cofExpDate { get; set; }
        public string? comments { get; set; }
    }
}
