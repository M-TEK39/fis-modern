using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LicenseFeeController : BaseApiController
{
    private readonly ILogger<LicenseFeeController> _logger;
    private readonly ILicenseFeeRepository _repository;

    public LicenseFeeController(
        ILogger<LicenseFeeController> logger,
        ILicenseFeeRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Get all licence fees
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenseFeeDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all licence fees");
            var fees = await _repository.GetAllAsync();
            var dtos = fees.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all licence fees");
            return StatusCode(500, "Error retrieving licence fees");
        }
    }

    /// <summary>
    /// Get licence fee by code
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<LicenseFeeDto>> GetByCode(short code)
    {
        try
        {
            _logger.LogInformation("Getting licence fee {Code}", code);
            var fee = await _repository.GetByIdAsync(code);

            if (fee == null)
                return NotFound(new { message = $"Licence fee with code {code} not found" });

            return Ok(MapToDto(fee));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting licence fee {Code}", code);
            return StatusCode(500, "Error retrieving licence fee");
        }
    }

    /// <summary>
    /// Create new licence fee
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LicenseFeeDto>> Create([FromBody] CreateLicenseFeeDto request)
    {
        try
        {
            _logger.LogInformation("Creating licence fee: {Description}", request.Description);

            var fee = new LicenseFee
            {
                licence_description = request.Description,
                licence_fee = request.Fee
            };

            var created = await _repository.CreateAsync(fee, GetCurrentUserId());
            return Ok(MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating licence fee");
            return StatusCode(500, "Error creating licence fee");
        }
    }

    /// <summary>
    /// Update existing licence fee
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<LicenseFeeDto>> Update(short code, [FromBody] UpdateLicenseFeeDto request)
    {
        try
        {
            _logger.LogInformation("Updating licence fee {Code}", code);

            if (code != request.LicenceFeeCode)
                return BadRequest("Code mismatch");

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(new { message = $"Licence fee with code {code} not found" });

            existing.licence_description = request.Description;
            existing.licence_fee = request.Fee;

            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(MapToDto(existing));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating licence fee {Code}", code);
            return StatusCode(500, "Error updating licence fee");
        }
    }

    /// <summary>
    /// Delete licence fee
    /// </summary>
    [HttpDelete("{code}")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            _logger.LogInformation("Deleting licence fee {Code}", code);

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(new { message = $"Licence fee with code {code} not found" });

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return Ok(new { message = "Licence fee deleted successfully", code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting licence fee {Code}", code);
            return StatusCode(500, "Error deleting licence fee");
        }
    }

    private static LicenseFeeDto MapToDto(LicenseFee fee)
    {
        return new LicenseFeeDto
        {
            LicenceFeeCode = fee.licence_fee_code,
            Description = fee.licence_description,
            Fee = fee.licence_fee
        };
    }
}

#region Licence Fee DTOs

public class LicenseFeeDto
{
    public short LicenceFeeCode { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
}

public class CreateLicenseFeeDto
{
    public short LicenceFeeCode { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
}

public class UpdateLicenseFeeDto
{
    public short LicenceFeeCode { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
}

#endregion
