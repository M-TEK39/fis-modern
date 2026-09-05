using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MakeController : BaseApiController
{
    private readonly IMakeRepository _makeRepository;
    private readonly ILogger<MakeController> _logger;

    public MakeController(
        IMakeRepository makeRepository,
        ILogger<MakeController> logger)
    {
        _makeRepository = makeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicle makes
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MakeResponseDto>>> GetMakes()
    {
        try
        {
            var makes = await _makeRepository.GetAllMakesAsync();
            var makeResponse = makes.Select(m => new MakeResponseDto
            {
                make_code = m.make_code,
                make_description = m.make_description
            });
            _logger.LogInformation("Retrieved {Count} makes", makes.Count());
            return Ok(makeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving makes");
            return StatusCode(500, "An error occurred while retrieving makes");
        }
    }

    /// <summary>
    /// Get a make by code
    /// </summary>
    [HttpGet("{makeCode}")]
    public async Task<ActionResult<MakeResponseDto>> GetMake(short makeCode)
    {
        try
        {
            var make = await _makeRepository.GetByIdAsync(makeCode);

            if (make == null)
            {
                _logger.LogWarning("Make with code {MakeCode} not found", makeCode);
                return NotFound();
            }

            var makeResponse = new MakeResponseDto
            {
                make_code = make.make_code,
                make_description = make.make_description
            };

            _logger.LogInformation("Retrieved make with code {MakeCode}", makeCode);
            return Ok(makeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving make with code {MakeCode}", makeCode);
            return StatusCode(500, "An error occurred while retrieving the make");
        }
    }

    /// <summary>
    /// Search makes by name or code
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MakeResponseDto>>> SearchMakes([FromQuery] string? searchTerm)
    {
        try
        {
            var makes = await _makeRepository.SearchMakesAsync(searchTerm ?? "");
            var makeResponse = makes.Select(m => new MakeResponseDto
            {
                make_code = m.make_code,
                make_description = m.make_description
            });
            _logger.LogInformation("Found {Count} makes matching search term '{SearchTerm}'", 
                makes.Count(), searchTerm);
            return Ok(makeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching makes with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching makes");
        }
    }

    [HttpGet("{makeCode:int}/delete-check")]
    public async Task<ActionResult<MakeDeleteCheck>> GetDeleteCheck(short makeCode)
    {
        try
        {
            var make = await _makeRepository.GetByIdAsync(makeCode);
            if (make is null)
            {
                return NotFound();
            }

            return Ok(await _makeRepository.GetDeleteCheckAsync(makeCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking make dependencies for {MakeCode}", makeCode);
            return StatusCode(500, "An error occurred while checking make dependencies");
        }
    }

    /// <summary>
    /// Get make by name
    /// </summary>
    [HttpGet("name/{makeName}")]
    public async Task<ActionResult<MakeResponseDto>> GetMakeByName(string makeName)
    {
        try
        {
            var make = await _makeRepository.GetByNameAsync(makeName);

            if (make == null)
            {
                _logger.LogWarning("Make with name {MakeName} not found", makeName);
                return NotFound();
            }

            var makeResponse = new MakeResponseDto
            {
                make_code = make.make_code,
                make_description = make.make_description
            };

            _logger.LogInformation("Retrieved make with name {MakeName}", makeName);
            return Ok(makeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving make with name {MakeName}", makeName);
            return StatusCode(500, "An error occurred while retrieving the make");
        }
    }

    /// <summary>
    /// Create a new make
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MakeResponseDto>> CreateMake([FromBody] CreateMakeDto createMakeDto)
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Create Make entity from DTO (ID will be auto-generated)
            var make = new Make
            {
                make_description = createMakeDto.make_description
            };

            var createdMake = await _makeRepository.CreateAsync(make, currentUserId);
            
            var makeResponse = new MakeResponseDto
            {
                make_code = createdMake.make_code,
                make_description = createdMake.make_description
            };
            
            _logger.LogInformation("Created new make with code {MakeCode}", createdMake.make_code);
            
            return CreatedAtAction(
                nameof(GetMake), 
                new { makeCode = createdMake.make_code }, 
                makeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating make: {MakeDescription}", createMakeDto.make_description);
            return StatusCode(500, "An error occurred while creating the make");
        }
    }

    /// <summary>
    /// Update an existing make
    /// </summary>
    [HttpPut("{makeCode}")]
    public async Task<ActionResult<MakeResponseDto>> UpdateMake(short makeCode, [FromBody] Make make)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (makeCode != make.make_code)
            {
                return BadRequest("Make code in URL does not match make code in body");
            }

            var existingMake = await _makeRepository.GetByIdAsync(makeCode);
            if (existingMake == null)
            {
                return NotFound();
            }

            var updatedMake = await _makeRepository.UpdateAsync(make, GetCurrentUserId());
            
            var makeResponse = new MakeResponseDto
            {
                make_code = updatedMake.make_code,
                make_description = updatedMake.make_description
            };
            
            _logger.LogInformation("Updated make with code {MakeCode}", makeCode);
            
            return Ok(makeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating make with code {MakeCode}", makeCode);
            return StatusCode(500, "An error occurred while updating the make");
        }
    }

    /// <summary>
    /// Delete a make
    /// </summary>
    [HttpDelete("{makeCode}")]
    public async Task<ActionResult> DeleteMake(short makeCode)
    {
        try
        {
            var existingMake = await _makeRepository.GetByIdAsync(makeCode);
            if (existingMake == null)
            {
                return NotFound();
            }

            var deleteCheck = await _makeRepository.GetDeleteCheckAsync(makeCode);
            if (!deleteCheck.CanDelete)
            {
                return Conflict(new
                {
                    message = "This make cannot be deleted while models are linked to it.",
                    modelCount = deleteCheck.ModelCount
                });
            }

            await _makeRepository.DeleteAsync(makeCode, GetCurrentUserId());
            _logger.LogInformation("Deleted make with code {MakeCode}", makeCode);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting make with code {MakeCode}", makeCode);
            return StatusCode(500, "An error occurred while deleting the make");
        }
    }
}
