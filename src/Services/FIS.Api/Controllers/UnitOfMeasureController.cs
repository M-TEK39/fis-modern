using FIS.Api.DTOs;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for UnitOfMeasure operations
/// Provides endpoints for managing units of measurement
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UnitOfMeasureController : ControllerBase
{
    private readonly IUnitOfMeasureRepository _unitOfMeasureRepository;
    private readonly ILogger<UnitOfMeasureController> _logger;

    public UnitOfMeasureController(IUnitOfMeasureRepository unitOfMeasureRepository, ILogger<UnitOfMeasureController> logger)
    {
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all units of measure
    /// </summary>
    /// <returns>List of all unit of measure entities</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnitOfMeasure>>> GetAllUnits()
    {
        try
        {
            var units = await _unitOfMeasureRepository.GetAllUnitsAsync();
            return Ok(units);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all units of measure");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get unit of measure by unit code
    /// </summary>
    /// <param name="unitCode">The unit code to retrieve</param>
    /// <returns>UnitOfMeasure entity if found</returns>
    [HttpGet("{unitCode}")]
    public async Task<ActionResult<UnitOfMeasure>> GetUnit(short unitCode)
    {
        try
        {
            var unit = await _unitOfMeasureRepository.GetByIdAsync(unitCode);
            if (unit == null)
                return NotFound($"Unit of measure with code {unitCode} not found");

            return Ok(unit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit of measure with code {UnitCode}", unitCode);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get unit of measure by description
    /// </summary>
    /// <param name="description">The unit description to search for</param>
    /// <returns>UnitOfMeasure entity if found</returns>
    [HttpGet("description/{description}")]
    public async Task<ActionResult<UnitOfMeasure>> GetUnitByDescription(string description)
    {
        try
        {
            var unit = await _unitOfMeasureRepository.GetByDescriptionAsync(description);
            if (unit == null)
                return NotFound($"Unit of measure with description '{description}' not found");

            return Ok(unit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit of measure with description {Description}", description);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get unit of measure by abbreviation
    /// </summary>
    /// <param name="abbreviation">The unit abbreviation to search for</param>
    /// <returns>UnitOfMeasure entity if found</returns>
    [HttpGet("abbreviation/{abbreviation}")]
    public async Task<ActionResult<UnitOfMeasure>> GetUnitByAbbreviation(string abbreviation)
    {
        try
        {
            var unit = await _unitOfMeasureRepository.GetByAbbreviationAsync(abbreviation);
            if (unit == null)
                return NotFound($"Unit of measure with abbreviation '{abbreviation}' not found");

            return Ok(unit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unit of measure with abbreviation {Abbreviation}", abbreviation);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get units of measure by category
    /// </summary>
    /// <param name="category">The unit category to filter by</param>
    /// <returns>List of units in the specified category</returns>
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<UnitOfMeasure>>> GetUnitsByCategory(string category)
    {
        try
        {
            var units = await _unitOfMeasureRepository.GetByCategoryAsync(category);
            return Ok(units);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving units of measure with category {Category}", category);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Search units of measure by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching unit of measure entities</returns>
    [HttpGet("search/{searchTerm}")]
    public async Task<ActionResult<IEnumerable<UnitOfMeasure>>> SearchUnits(string searchTerm)
    {
        try
        {
            var units = await _unitOfMeasureRepository.SearchUnitsAsync(searchTerm);
            return Ok(units);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching units of measure with term {SearchTerm}", searchTerm);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create a new unit of measure
    /// </summary>
    /// <param name="createUnitDto">The unit of measure data to create</param>
    /// <returns>The created unit of measure entity</returns>
    [HttpPost]
    public async Task<ActionResult<UnitOfMeasure>> CreateUnit([FromBody] CreateUnitOfMeasureDto createUnitDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var unit = new UnitOfMeasure
            {
                unit_description = createUnitDto.unit_description,
                unit_abbreviation = createUnitDto.unit_abbreviation,
                unit_category = createUnitDto.unit_category
            };

            var createdUnit = await _unitOfMeasureRepository.CreateAsync(unit);
            return CreatedAtAction(nameof(GetUnit), new { unitCode = createdUnit.unit_of_measure_code }, createdUnit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating unit of measure");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Update an existing unit of measure
    /// </summary>
    /// <param name="unitCode">The unit code to update</param>
    /// <param name="unit">The updated unit of measure data</param>
    /// <returns>The updated unit of measure entity</returns>
    [HttpPut("{unitCode}")]
    public async Task<ActionResult<UnitOfMeasure>> UpdateUnit(short unitCode, [FromBody] UnitOfMeasure unit)
    {
        try
        {
            if (unitCode != unit.unit_of_measure_code)
                return BadRequest("Unit code in URL does not match unit code in body");

            var existingUnit = await _unitOfMeasureRepository.GetByIdAsync(unitCode);
            if (existingUnit == null)
                return NotFound($"Unit of measure with code {unitCode} not found");

            var updatedUnit = await _unitOfMeasureRepository.UpdateAsync(unit);
            return Ok(updatedUnit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating unit of measure with code {UnitCode}", unitCode);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Delete a unit of measure
    /// </summary>
    /// <param name="unitCode">The unit code to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{unitCode}")]
    public async Task<ActionResult> DeleteUnit(short unitCode)
    {
        try
        {
            var deleted = await _unitOfMeasureRepository.DeleteAsync(unitCode);
            if (!deleted)
                return NotFound($"Unit of measure with code {unitCode} not found");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting unit of measure with code {UnitCode}", unitCode);
            return StatusCode(500, "Internal server error");
        }
    }
}