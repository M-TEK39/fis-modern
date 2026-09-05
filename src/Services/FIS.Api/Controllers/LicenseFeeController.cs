using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LicenseFeeController : BaseApiController
{
    private readonly ILogger<LicenseFeeController> _logger;
    private readonly ILicenseFeeRepository _repository;

    public LicenseFeeController(ILogger<LicenseFeeController> logger, ILicenseFeeRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LicenseFeeDto>>> GetAll()
    {
        try
        {
            return Ok((await _repository.GetAllAsync()).Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving licence fees");
            return StatusCode(500, "Error retrieving licence fees");
        }
    }

    [HttpGet("{code:int}")]
    public async Task<ActionResult<LicenseFeeDto>> GetByCode(short code)
    {
        try
        {
            var fee = await _repository.GetByIdAsync(code);
            return fee is null
                ? NotFound(new { message = $"Licence fee with code {code} not found" })
                : Ok(MapToDto(fee));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving licence fee {Code}", code);
            return StatusCode(500, "Error retrieving licence fee");
        }
    }

    [HttpGet("{code:int}/delete-check")]
    public async Task<ActionResult<LicenseFeeDeleteCheck>> GetDeleteCheck(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound();
            }

            return Ok(await _repository.GetDeleteCheckAsync(code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking licence fee dependencies for {Code}", code);
            return StatusCode(500, "Error checking licence fee dependencies");
        }
    }

    [HttpPost]
    public async Task<ActionResult<LicenseFeeDto>> Create([FromBody] CreateLicenseFeeDto request)
    {
        try
        {
            var validationError = ValidateWriteDto(request);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var created = await _repository.CreateAsync(new LicenseFee
            {
                licence_description = request.Description?.Trim(),
                licence_fee = request.Fee
            }, GetCurrentUserId());
            return CreatedAtAction(nameof(GetByCode), new { code = created.licence_fee_code }, MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating licence fee");
            return StatusCode(500, "Error creating licence fee");
        }
    }

    [HttpPut("{code:int}")]
    public async Task<ActionResult<LicenseFeeDto>> Update(short code, [FromBody] UpdateLicenseFeeDto request)
    {
        try
        {
            var validationError = ValidateWriteDto(request);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            if (code != request.LicenceFeeCode)
            {
                return BadRequest(new { message = "Licence fee code does not match the route." });
            }

            var existing = await _repository.GetByIdAsync(code);
            if (existing is null)
            {
                return NotFound(new { message = $"Licence fee with code {code} not found" });
            }

            existing.licence_description = request.Description?.Trim();
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

    [HttpDelete("{code:int}")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound(new { message = $"Licence fee with code {code} not found" });
            }

            var deleteCheck = await _repository.GetDeleteCheckAsync(code);
            if (!deleteCheck.CanDelete)
            {
                return Conflict(new
                {
                    message = "Models using this licence fee must be changed before deleting it.",
                    modelCount = deleteCheck.ModelCount
                });
            }

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting licence fee {Code}", code);
            return StatusCode(500, "Error deleting licence fee");
        }
    }

    private static LicenseFeeDto MapToDto(LicenseFee fee)
        => new()
        {
            LicenceFeeCode = fee.licence_fee_code,
            Description = fee.licence_description,
            Fee = fee.licence_fee,
            DateCreated = fee.date_created == DateTime.MinValue ? null : fee.date_created,
            DateUpdated = fee.date_updated,
            CreatedByUserCode = fee.created_by_user_code,
            ModifiedByUserCode = fee.modified_by_user_code,
            IsDeleted = fee.is_deleted
        };

    private static string? ValidateWriteDto(CreateLicenseFeeDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 50)
        {
            return "Licence fee description is required and must be 50 characters or fewer.";
        }

        if (request.Fee is < 0)
        {
            return "Licence fee cannot be negative.";
        }

        if (request.Fee is not null && decimal.Round(request.Fee.Value, 2) != request.Fee.Value)
        {
            return "Licence fee must have no more than 2 decimal places.";
        }

        return null;
    }
}

public class LicenseFeeDto
{
    public short LicenceFeeCode { get; set; }
    public string? Description { get; set; }
    public decimal? Fee { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public int? CreatedByUserCode { get; set; }
    public int? ModifiedByUserCode { get; set; }
    public bool IsDeleted { get; set; }
}

public class CreateLicenseFeeDto
{
    [JsonPropertyName("licence_fee_code")]
    public short LicenceFeeCode { get; set; }
    [JsonPropertyName("licence_description")]
    public string? Description { get; set; }
    [JsonPropertyName("licence_fee")]
    public decimal? Fee { get; set; }
}

public class UpdateLicenseFeeDto : CreateLicenseFeeDto
{
}
