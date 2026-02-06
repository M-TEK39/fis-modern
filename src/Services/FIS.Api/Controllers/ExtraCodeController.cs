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

    /// <summary>
    /// Get all extra codes
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExtraCodeDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all extra codes");
            var codes = await _repository.GetAllAsync();
            var dtos = codes.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all extra codes");
            return StatusCode(500, "Error retrieving extra codes");
        }
    }

    /// <summary>
    /// Get extra code by code
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<ExtraCodeDto>> GetByCode(short code)
    {
        try
        {
            _logger.LogInformation("Getting extra code {Code}", code);
            var extraCode = await _repository.GetByIdAsync(code);

            if (extraCode == null)
                return NotFound(new { message = $"Extra code {code} not found" });

            return Ok(MapToDto(extraCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting extra code {Code}", code);
            return StatusCode(500, "Error retrieving extra code");
        }
    }

    /// <summary>
    /// Create new extra code
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ExtraCodeDto>> Create([FromBody] CreateExtraCodeDto request)
    {
        try
        {
            _logger.LogInformation("Creating extra code: {Code}", request.ExtraCode);

            var code = new ExtraCode
            {
                extra_code = request.ExtraCode,
                extra_description = request.Description,
                category_type_code = request.CategoryTypeCode
            };

            var created = await _repository.CreateAsync(code, GetCurrentUserId());
            return Ok(MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating extra code");
            return StatusCode(500, "Error creating extra code");
        }
    }

    /// <summary>
    /// Update existing extra code
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<ExtraCodeDto>> Update(short code, [FromBody] UpdateExtraCodeDto request)
    {
        try
        {
            _logger.LogInformation("Updating extra code {Code}", code);

            if (code != request.ExtraCode)
                return BadRequest("Code mismatch");

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(new { message = $"Extra code {code} not found" });

            existing.extra_description = request.Description;
            existing.category_type_code = request.CategoryTypeCode;

            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(MapToDto(existing));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating extra code {Code}", code);
            return StatusCode(500, "Error updating extra code");
        }
    }

    /// <summary>
    /// Delete extra code
    /// </summary>
    [HttpDelete("{code}")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            _logger.LogInformation("Deleting extra code {Code}", code);

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(new { message = $"Extra code {code} not found" });

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return Ok(new { message = "Extra code deleted successfully", code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting extra code {Code}", code);
            return StatusCode(500, "Error deleting extra code");
        }
    }

    private static ExtraCodeDto MapToDto(ExtraCode code)
    {
        return new ExtraCodeDto
        {
            ExtraCode = code.extra_code,
            Description = code.extra_description,
            CategoryTypeCode = code.category_type_code
        };
    }
}

#region Extra Code DTOs

public class ExtraCodeDto
{
    public short ExtraCode { get; set; }
    public string? Description { get; set; }
    public int? CategoryTypeCode { get; set; }
}

public class CreateExtraCodeDto
{
    public short ExtraCode { get; set; }
    public string? Description { get; set; }
    public int? CategoryTypeCode { get; set; }
}

public class UpdateExtraCodeDto
{
    public short ExtraCode { get; set; }
    public string? Description { get; set; }
    public int? CategoryTypeCode { get; set; }
}

#endregion
