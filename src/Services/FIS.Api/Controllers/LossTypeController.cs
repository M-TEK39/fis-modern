using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LossTypeController : BaseApiController
{
    private readonly ILogger<LossTypeController> _logger;
    private readonly ILossTypeRepository _repository;

    public LossTypeController(
        ILogger<LossTypeController> logger,
        ILossTypeRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Get all loss types
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LossTypeDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all loss types");
            var types = await _repository.GetAllAsync();
            var dtos = types.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all loss types");
            return StatusCode(500, "Error retrieving loss types");
        }
    }

    /// <summary>
    /// Get loss type by code
    /// </summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<LossTypeDto>> GetByCode(short code)
    {
        try
        {
            _logger.LogInformation("Getting loss type {Code}", code);
            var lossType = await _repository.GetByIdAsync(code);

            if (lossType == null)
                return NotFound(new { message = $"Loss type with code {code} not found" });

            return Ok(MapToDto(lossType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting loss type {Code}", code);
            return StatusCode(500, "Error retrieving loss type");
        }
    }

    /// <summary>
    /// Create new loss type
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LossTypeDto>> Create([FromBody] CreateLossTypeDto request)
    {
        try
        {
            _logger.LogInformation("Creating loss type: {Description}", request.Description);

            var lossType = new LossType
            {
                loss_type_code = request.LossTypeCode,
                loss_description = request.Description
            };

            var created = await _repository.CreateAsync(lossType, GetCurrentUserId());
            return Ok(MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loss type");
            return StatusCode(500, "Error creating loss type");
        }
    }

    /// <summary>
    /// Update existing loss type
    /// </summary>
    [HttpPut("{code}")]
    public async Task<ActionResult<LossTypeDto>> Update(short code, [FromBody] UpdateLossTypeDto request)
    {
        try
        {
            _logger.LogInformation("Updating loss type {Code}", code);

            if (code != request.LossTypeCode)
                return BadRequest("Code mismatch");

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(new { message = $"Loss type with code {code} not found" });

            existing.loss_description = request.Description;

            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(MapToDto(existing));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loss type {Code}", code);
            return StatusCode(500, "Error updating loss type");
        }
    }

    /// <summary>
    /// Delete loss type
    /// </summary>
    [HttpDelete("{code}")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            _logger.LogInformation("Deleting loss type {Code}", code);

            var existing = await _repository.GetByIdAsync(code);
            if (existing == null)
                return NotFound(new { message = $"Loss type with code {code} not found" });

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return Ok(new { message = "Loss type deleted successfully", code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting loss type {Code}", code);
            return StatusCode(500, "Error deleting loss type");
        }
    }

    private static LossTypeDto MapToDto(LossType type)
    {
        return new LossTypeDto
        {
            LossTypeCode = type.loss_type_code,
            Description = type.loss_description
        };
    }
}

#region Loss Type DTOs

public class LossTypeDto
{
    public short LossTypeCode { get; set; }
    public string? Description { get; set; }
}

public class CreateLossTypeDto
{
    public short LossTypeCode { get; set; }
    public string? Description { get; set; }
}

public class UpdateLossTypeDto
{
    public short LossTypeCode { get; set; }
    public string? Description { get; set; }
}

#endregion
