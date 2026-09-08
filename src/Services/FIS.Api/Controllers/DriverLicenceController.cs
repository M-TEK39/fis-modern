using System.Text.Json.Serialization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DriverLicenceController : BaseApiController
{
    private readonly ILogger<DriverLicenceController> _logger;
    private readonly IDriverLicenceRepository _repository;

    public DriverLicenceController(
        ILogger<DriverLicenceController> logger,
        IDriverLicenceRepository repository
    )
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Get all driver licence types
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DriverLicenceTypeDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all driver licence types");
            var licences = await _repository.GetAllAsync();
            var dtos = licences.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all driver licence types");
            return StatusCode(500, "Error retrieving driver licence types");
        }
    }

    [HttpGet("{code:int}/delete-check")]
    public async Task<ActionResult<DriverLicenceDeleteCheck>> GetDeleteCheck(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound(
                    new { message = $"Driver licence type with code {code} not found" }
                );
            }

            return Ok(await _repository.GetDeleteCheckAsync(code));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking driver licence type dependencies for {Code}",
                code
            );
            return StatusCode(500, "Error checking driver licence type dependencies");
        }
    }

    /// <summary>
    /// Get driver licence type by code
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<DriverLicenceTypeDto>> GetByCode(short code)
    {
        try
        {
            _logger.LogInformation("Getting driver licence type {Code}", code);
            var licence = await _repository.GetByIdAsync(code);

            if (licence == null)
                return NotFound(
                    new { message = $"Driver licence type with code {code} not found" }
                );

            return Ok(MapToDto(licence));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting driver licence type {Code}", code);
            return StatusCode(500, "Error retrieving driver licence type");
        }
    }

    /// <summary>
    /// Create new driver licence type
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DriverLicenceTypeDto>> Create(
        [FromBody] CreateDriverLicenceTypeDto request
    )
    {
        try
        {
            var validationError = ValidateWriteDto(request.Description);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            _logger.LogInformation(
                "Creating driver licence type: {Description}",
                request.Description
            );

            var licence = new DriverLicence { description = request.Description };

            var created = await _repository.CreateAsync(licence, GetCurrentUserId());
            return Ok(MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver licence type");
            return StatusCode(500, "Error creating driver licence type");
        }
    }

    /// <summary>
    /// Update existing driver licence type
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<DriverLicenceTypeDto>> Update(
        short code,
        [FromBody] UpdateDriverLicenceTypeDto request
    )
    {
        try
        {
            _logger.LogInformation("Updating driver licence type {Code}", code);

            if (code != request.LicenceCode)
                return BadRequest("Code mismatch");

            var validationError = ValidateWriteDto(request.Description);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(
                    new { message = $"Driver licence type with code {code} not found" }
                );

            existing.description = request.Description;

            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(MapToDto(existing));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver licence type {Code}", code);
            return StatusCode(500, "Error updating driver licence type");
        }
    }

    /// <summary>
    /// Delete driver licence type
    /// </summary>
    [HttpDelete("{code}")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            _logger.LogInformation("Deleting driver licence type {Code}", code);

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(
                    new { message = $"Driver licence type with code {code} not found" }
                );

            var deleteCheck = await _repository.GetDeleteCheckAsync(code);
            if (!deleteCheck.CanDelete)
            {
                return Conflict(
                    new
                    {
                        message = "Models using this driver licence type must be changed before deleting it.",
                        modelCount = deleteCheck.ModelCount,
                    }
                );
            }

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return Ok(new { message = "Driver licence type deleted successfully", code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver licence type {Code}", code);
            return StatusCode(500, "Error deleting driver licence type");
        }
    }

    private static DriverLicenceTypeDto MapToDto(DriverLicence licence)
    {
        return new DriverLicenceTypeDto
        {
            LicenceCode = licence.licence_code,
            Description = licence.description,
            DateCreated = licence.date_created == DateTime.MinValue ? null : licence.date_created,
            DateUpdated = licence.date_updated,
            CreatedByUserCode = licence.created_by_user_code,
            ModifiedByUserCode = licence.modified_by_user_code,
            IsDeleted = licence.is_deleted,
        };
    }

    private static string? ValidateWriteDto(string? description)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 30)
            return "Driver licence description is required and must be 30 characters or fewer.";

        return null;
    }
}

#region Driver Licence DTOs

public class DriverLicenceTypeDto
{
    public short LicenceCode { get; set; }
    public string? Description { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public int? CreatedByUserCode { get; set; }
    public int? ModifiedByUserCode { get; set; }
    public bool IsDeleted { get; set; }
}

public class CreateDriverLicenceTypeDto
{
    [JsonPropertyName("licence_code")]
    public short LicenceCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class UpdateDriverLicenceTypeDto
{
    [JsonPropertyName("licence_code")]
    public short LicenceCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

#endregion
