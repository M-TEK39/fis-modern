using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ClassController : BaseApiController
{
    private readonly IClassRepository _classRepository;
    private readonly ILogger<ClassController> _logger;

    public ClassController(IClassRepository classRepository, ILogger<ClassController> logger)
    {
        _classRepository = classRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClassResponseDto>>> GetAll()
    {
        try
        {
            return Ok((await _classRepository.GetAllAsync()).Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving classes");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClassResponseDto>> GetById(short id)
    {
        try
        {
            var classEntity = await _classRepository.GetByIdAsync(id);
            return classEntity is null ? NotFound() : Ok(MapToDto(classEntity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving class {ClassCode}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id:int}/delete-check")]
    public async Task<ActionResult<ClassDeleteCheck>> GetDeleteCheck(short id)
    {
        try
        {
            if (await _classRepository.GetByIdAsync(id) is null)
            {
                return NotFound();
            }

            return Ok(await _classRepository.GetDeleteCheckAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking class dependencies for {ClassCode}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("search/{searchTerm}")]
    public async Task<ActionResult<IEnumerable<ClassResponseDto>>> Search(string searchTerm)
    {
        try
        {
            return Ok((await _classRepository.SearchAsync(searchTerm)).Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching classes with term {SearchTerm}", searchTerm);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ClassResponseDto>> Create([FromBody] CreateClassDto dto)
    {
        try
        {
            var validationError = ValidateWriteDto(dto);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var currentUserId = GetCurrentUserId();
            var createdClass = await _classRepository.CreateAsync(
                ApplyWriteDto(new Class(), dto),
                currentUserId
            );
            return CreatedAtAction(
                nameof(GetById),
                new { id = createdClass.class_code },
                MapToDto(createdClass)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating class");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClassResponseDto>> Update(
        short id,
        [FromBody] UpdateClassDto dto
    )
    {
        try
        {
            var validationError = ValidateWriteDto(dto);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            if (await _classRepository.GetByIdAsync(id) is null)
            {
                return NotFound();
            }

            var updatedClass = await _classRepository.UpdateAsync(
                ApplyWriteDto(new Class { class_code = id }, dto),
                GetCurrentUserId()
            );
            return Ok(MapToDto(updatedClass));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating class {ClassCode}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(short id)
    {
        try
        {
            if (await _classRepository.GetByIdAsync(id) is null)
            {
                return NotFound();
            }

            var deleteCheck = await _classRepository.GetDeleteCheckAsync(id);
            if (!deleteCheck.CanDelete)
            {
                return Conflict(
                    new
                    {
                        message = "Models and vehicles assigned to this class must be changed before deleting it.",
                        modelCount = deleteCheck.ModelCount,
                        vehicleCount = deleteCheck.VehicleCount,
                    }
                );
            }

            await _classRepository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting class {ClassCode}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private static ClassResponseDto MapToDto(Class classEntity) =>
        new()
        {
            class_code = classEntity.class_code,
            description = classEntity.description,
            class_number = classEntity.class_number,
            bank_number = classEntity.bank_number,
            months_life = classEntity.months_life,
            depreciation_percent = classEntity.depreciation_percent,
            odometer_life = classEntity.odometer_life,
            appreciate_percent = classEntity.appreciate_percent,
            replacement_cost = classEntity.replacement_cost,
            date_created = classEntity.date_created,
            date_updated = classEntity.date_updated,
            created_by_user_code = classEntity.created_by_user_code,
            modified_by_user_code = classEntity.modified_by_user_code,
            is_deleted = classEntity.is_deleted,
        };

    private static Class ApplyWriteDto(Class target, CreateClassDto dto)
    {
        target.description = dto.description?.Trim();
        target.class_number = dto.class_number?.Trim();
        target.bank_number = dto.bank_number?.Trim();
        target.months_life = dto.months_life;
        target.depreciation_percent = dto.depreciation_percent;
        target.odometer_life = dto.odometer_life;
        target.appreciate_percent = dto.appreciate_percent;
        target.replacement_cost = dto.replacement_cost;
        return target;
    }

    private static string? ValidateWriteDto(CreateClassDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.description) || dto.description.Trim().Length > 60)
        {
            return "Description is required and must be 60 characters or fewer.";
        }

        if (
            string.IsNullOrWhiteSpace(dto.class_number)
            || dto.class_number.Trim().Length != 3
            || dto.class_number.Trim().Any(character => !char.IsDigit(character))
        )
        {
            return "Class number is required and must contain exactly 3 digits.";
        }

        if (string.IsNullOrWhiteSpace(dto.bank_number) || dto.bank_number.Trim().Length > 30)
        {
            return "Bank number is required and must be 30 characters or fewer.";
        }

        if (dto.months_life is null or < 0 or > 99)
        {
            return "Months life is required and must be between 0 and 99.";
        }

        if (dto.depreciation_percent is null or < 0 or > 99.99m)
        {
            return "Depreciation percent is required and must be between 0 and 99.99.";
        }

        if (
            dto.odometer_life is null or < 0 or > 999999m
            || decimal.Truncate(dto.odometer_life.Value) != dto.odometer_life.Value
        )
        {
            return "Odometer life is required and must be a whole number between 0 and 999999.";
        }

        if (dto.appreciate_percent is null or < 0 or > 99)
        {
            return "Appreciate percent is required and must be between 0 and 99.";
        }

        if (
            dto.replacement_cost is null or < 0 or > 99999999m
            || decimal.Truncate(dto.replacement_cost.Value) != dto.replacement_cost.Value
        )
        {
            return "Replacement cost is required and must be a whole number between 0 and 99999999.";
        }

        return null;
    }
}
