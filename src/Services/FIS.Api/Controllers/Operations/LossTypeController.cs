using System.Text.Json.Serialization;
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
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly ILogger<LossTypeController> _logger;
    private readonly ILossTypeRepository _repository;

    public LossTypeController(ILogger<LossTypeController> logger, ILossTypeRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    [HttpGet("page")]
    [Authorize(Roles = "Validation,Losses,Call Centre")]
    public async Task<ActionResult> GetPage(
        [FromQuery] string? searchTerm = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        try
        {
            var result = await _repository.GetPageAsync(
                new LossTypePageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    searchTerm
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(MapToDto).ToList(),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged loss types");
            return StatusCode(500, "Error retrieving loss types");
        }
    }

    [HttpGet]
    [Authorize(Roles = "Validation,Losses,Call Centre")]
    public async Task<ActionResult<IEnumerable<LossTypeDto>>> GetAll()
    {
        try
        {
            return Ok((await _repository.GetAllAsync()).Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loss types");
            return StatusCode(500, "Error retrieving loss types");
        }
    }

    [HttpGet("{code:int}")]
    [Authorize(Roles = "Validation,Losses,Call Centre")]
    public async Task<ActionResult<LossTypeDto>> GetByCode(short code)
    {
        try
        {
            var lossType = await _repository.GetByIdAsync(code);
            return lossType is null
                ? NotFound(new { message = $"Loss type with code {code} not found" })
                : Ok(MapToDto(lossType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loss type {Code}", code);
            return StatusCode(500, "Error retrieving loss type");
        }
    }

    [HttpGet("{code:int}/delete-check")]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult<LossTypeDeleteCheck>> GetDeleteCheck(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound(new { message = $"Loss type with code {code} not found" });
            }

            return Ok(await _repository.GetDeleteCheckAsync(code));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking loss type dependencies for {Code}", code);
            return StatusCode(500, "Error checking loss type dependencies");
        }
    }

    [HttpPost]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult<LossTypeDto>> Create([FromBody] CreateLossTypeDto request)
    {
        try
        {
            var validationError = ValidateDescription(request.Description);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var created = await _repository.CreateAsync(
                new LossType { loss_description = request.Description!.Trim() },
                GetCurrentUserId()
            );
            return CreatedAtAction(
                nameof(GetByCode),
                new { code = created.loss_type_code },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loss type");
            return StatusCode(500, "Error creating loss type");
        }
    }

    [HttpPut("{code:int}")]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult<LossTypeDto>> Update(
        short code,
        [FromBody] UpdateLossTypeDto request
    )
    {
        try
        {
            var validationError = ValidateDescription(request.Description);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            if (code != request.LossTypeCode && code != request.LossCode)
            {
                return BadRequest(new { message = "Loss type code does not match the route." });
            }

            var existing = await _repository.GetByIdAsync(code);
            if (existing is null)
            {
                return NotFound(new { message = $"Loss type with code {code} not found" });
            }

            existing.loss_description = request.Description!.Trim();
            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(MapToDto(existing));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loss type {Code}", code);
            return StatusCode(500, "Error updating loss type");
        }
    }

    [HttpDelete("{code:int}")]
    [Authorize(Roles = "Validation")]
    public async Task<ActionResult> Delete(short code)
    {
        try
        {
            if (await _repository.GetByIdAsync(code) is null)
            {
                return NotFound(new { message = $"Loss type with code {code} not found" });
            }

            var deleteCheck = await _repository.GetDeleteCheckAsync(code);
            if (!deleteCheck.CheckAvailable)
            {
                return StatusCode(
                    503,
                    new
                    {
                        message = "Loss type dependencies could not be verified, so the loss type was not deleted.",
                    }
                );
            }

            if (!deleteCheck.CanDelete)
            {
                return Conflict(
                    new
                    {
                        message = "Delete or change the linked loss records before deleting this loss description.",
                        lossCount = deleteCheck.LossCount,
                        losses = deleteCheck.Losses,
                    }
                );
            }

            await _repository.DeleteAsync(code, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting loss type {Code}", code);
            return StatusCode(500, "Error deleting loss type");
        }
    }

    private static LossTypeDto MapToDto(LossType type) =>
        new()
        {
            LossTypeCode = type.loss_type_code,
            LossCode = type.loss_type_code,
            Description = type.loss_description,
            DateCreated = type.date_created == DateTime.MinValue ? null : type.date_created,
            DateUpdated = type.date_updated,
            CreatedByUserCode = type.created_by_user_code,
            ModifiedByUserCode = type.modified_by_user_code,
            IsDeleted = type.is_deleted,
        };

    private static string? ValidateDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) || description.Trim().Length > 30
            ? "Loss description is required and must be 30 characters or fewer."
            : null;
}

public class LossTypeDto
{
    [JsonPropertyName("loss_type_code")]
    public short LossTypeCode { get; set; }

    [JsonPropertyName("loss_code")]
    public short LossCode { get; set; }

    [JsonPropertyName("loss_description")]
    public string? Description { get; set; }

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

public class CreateLossTypeDto
{
    [JsonPropertyName("loss_type_code")]
    public short LossTypeCode { get; set; }

    [JsonPropertyName("loss_code")]
    public short LossCode { get; set; }

    [JsonPropertyName("loss_description")]
    public string? Description { get; set; }
}

public class UpdateLossTypeDto : CreateLossTypeDto { }
