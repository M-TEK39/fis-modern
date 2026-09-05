using System.Text.Json.Serialization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExtraCodeController : BaseApiController
{
    private readonly ILogger<ExtraCodeController> _logger;
    private readonly IExtraCodeRepository _repository;

    public ExtraCodeController(
        ILogger<ExtraCodeController> logger,
        IExtraCodeRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExtraCodeDto>>> GetAll()
    {
        try
        {
            return Ok((await _repository.GetAllAsync()).Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving extra codes");
            return StatusCode(500, "Error retrieving extra codes");
        }
    }

    [HttpGet("{code:int}")]
    public async Task<ActionResult<ExtraCodeDto>> GetByCode(short code)
    {
        try
        {
            var extraCode = await _repository.GetByIdAsync(code);
            return extraCode is null
                ? NotFound(new { message = $"Extra code {code} not found" })
                : Ok(MapToDto(extraCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving extra code {Code}", code);
            return StatusCode(500, "Error retrieving extra code");
        }
    }

    [HttpGet("{code:int}/delete-check")]
    public async Task<ActionResult<ExtraCodeDeleteCheck>> GetDeleteCheck(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound(new { message = $"Extra code {code} not found" });
            }

            return Ok(await _repository.GetDeleteCheckAsync(code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking extra code dependencies for {Code}", code);
            return StatusCode(500, "Error checking extra code dependencies");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ExtraCodeDto>> Create([FromBody] CreateExtraCodeDto request)
    {
        try
        {
            var validationError = ValidateDescription(request.Description);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var created = await _repository.CreateAsync(new ExtraCode
            {
                extra_description = request.Description!.Trim(),
                category_type_code = request.CategoryTypeCode,
                specific = request.Specific,
                Additional = request.Additional
            }, GetCurrentUserId());
            return CreatedAtAction(nameof(GetByCode), new { code = created.extra_code }, MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating extra code");
            return StatusCode(500, "Error creating extra code");
        }
    }

    [HttpPut("{code:int}")]
    public async Task<ActionResult<ExtraCodeDto>> Update(short code, [FromBody] UpdateExtraCodeDto request)
    {
        try
        {
            var validationError = ValidateDescription(request.Description);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            if (code != request.ExtraCode)
            {
                return BadRequest(new { message = "Extra code does not match the route." });
            }

            var existing = await _repository.GetByIdAsync(code);
            if (existing is null)
            {
                return NotFound(new { message = $"Extra code {code} not found" });
            }

            existing.extra_description = request.Description!.Trim();
            existing.category_type_code = request.CategoryTypeCode;
            existing.specific = request.Specific;
            existing.Additional = request.Additional;
            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(MapToDto(existing));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating extra code {Code}", code);
            return StatusCode(500, "Error updating extra code");
        }
    }

    [HttpDelete("{code:int}")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound(new { message = $"Extra code {code} not found" });
            }

            var deleteCheck = await _repository.GetDeleteCheckAsync(code);
            if (!deleteCheck.CheckAvailable)
            {
                return StatusCode(503, new { message = "Extra dependencies could not be verified, so the extra was not deleted." });
            }

            if (!deleteCheck.CanDelete)
            {
                return Conflict(new
                {
                    message = "Remove this extra from the linked vehicle data before deleting it.",
                    vehicleCount = deleteCheck.VehicleCount,
                    fleetNumbers = deleteCheck.FleetNumbers
                });
            }

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting extra code {Code}", code);
            return StatusCode(500, "Error deleting extra code");
        }
    }

    private static ExtraCodeDto MapToDto(ExtraCode code)
        => new()
        {
            ExtraCode = code.extra_code,
            Description = code.extra_description,
            CategoryTypeCode = code.category_type_code,
            Specific = code.specific,
            Additional = code.Additional,
            DateCreated = code.date_created == DateTime.MinValue ? null : code.date_created,
            DateUpdated = code.date_updated,
            CreatedByUserCode = code.created_by_user_code,
            ModifiedByUserCode = code.modified_by_user_code,
            IsDeleted = code.is_deleted
        };

    private static string? ValidateDescription(string? description)
        => string.IsNullOrWhiteSpace(description) || description.Trim().Length > 50
            ? "Extra description is required and must be 50 characters or fewer."
            : null;
}

public class ExtraCodeDto
{
    [JsonPropertyName("extra_code")]
    public short ExtraCode { get; set; }

    [JsonPropertyName("extra_description")]
    public string? Description { get; set; }

    [JsonPropertyName("category_type_code")]
    public int? CategoryTypeCode { get; set; }

    [JsonPropertyName("specific")]
    public int? Specific { get; set; }

    [JsonPropertyName("Additional")]
    public int? Additional { get; set; }

    [JsonPropertyName("date_created")]
    public DateTime? DateCreated { get; set; }

    [JsonPropertyName("date_updated")]
    public DateTime? DateUpdated { get; set; }

    [JsonPropertyName("created_by_user_code")]
    public int? CreatedByUserCode { get; set; }

    [JsonPropertyName("modified_by_user_code")]
    public int? ModifiedByUserCode { get; set; }

    [JsonPropertyName("is_deleted")]
    public bool IsDeleted { get; set; }
}

public class CreateExtraCodeDto
{
    [JsonPropertyName("extra_code")]
    public short ExtraCode { get; set; }

    [JsonPropertyName("extra_description")]
    public string? Description { get; set; }

    [JsonPropertyName("category_type_code")]
    public int? CategoryTypeCode { get; set; }

    [JsonPropertyName("specific")]
    public int? Specific { get; set; }

    [JsonPropertyName("additional")]
    public int? Additional { get; set; }
}

public class UpdateExtraCodeDto : CreateExtraCodeDto
{
}
