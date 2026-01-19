using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TypeEntity = FIS.Data.Entities.Type;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for vehicle type operations
/// Provides endpoints for managing vehicle types/classifications
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TypeController : ControllerBase
{
    private readonly ITypeRepository _typeRepository;
    private readonly ILogger<TypeController> _logger;

    public TypeController(ITypeRepository typeRepository, ILogger<TypeController> logger)
    {
        _typeRepository = typeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all vehicle types
    /// </summary>
    /// <returns>List of all vehicle types</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TypeResponseDto>>> GetAllTypes()
    {
        try
        {
            var types = await _typeRepository.GetAllTypesAsync();
            var typeDtos = types.Select(t => new TypeResponseDto
            {
                type_code = t.type_code,
                type_description = t.type_description
            });
            return Ok(typeDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all types");
            return StatusCode(500, "An error occurred while retrieving types");
        }
    }

    /// <summary>
    /// Gets a specific vehicle type by code
    /// </summary>
    /// <param name="id">The type code</param>
    /// <returns>Type details if found</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TypeResponseDto>> GetType(short id)
    {
        try
        {
            var type = await _typeRepository.GetByIdAsync(id);
            if (type == null)
            {
                return NotFound($"Type with code {id} not found");
            }
            
            var typeDto = new TypeResponseDto
            {
                type_code = type.type_code,
                type_description = type.type_description
            };
            
            return Ok(typeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving type {TypeCode}", id);
            return StatusCode(500, "An error occurred while retrieving the type");
        }
    }

    /// <summary>
    /// Gets a specific vehicle type by name
    /// </summary>
    /// <param name="name">The type name/description</param>
    /// <returns>Type details if found</returns>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<TypeEntity>> GetTypeByName(string name)
    {
        try
        {
            var type = await _typeRepository.GetByNameAsync(name);
            if (type == null)
            {
                return NotFound($"Type with name '{name}' not found");
            }
            return Ok(type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving type by name {TypeName}", name);
            return StatusCode(500, "An error occurred while retrieving the type");
        }
    }

    /// <summary>
    /// Searches vehicle types by description
    /// </summary>
    /// <param name="searchTerm">The term to search for in type descriptions</param>
    /// <returns>List of matching types</returns>
    [HttpGet("search/{searchTerm}")]
    public async Task<ActionResult<IEnumerable<TypeEntity>>> SearchTypes(string searchTerm)
    {
        try
        {
            var types = await _typeRepository.SearchTypesAsync(searchTerm);
            return Ok(types);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching types with term {SearchTerm}", searchTerm);
            return StatusCode(500, "An error occurred while searching types");
        }
    }

    /// <summary>
    /// Creates a new vehicle type
    /// </summary>
    /// <param name="createTypeDto">The type data to create</param>
    /// <returns>Created type with assigned code</returns>
    [HttpPost]
    public async Task<ActionResult<TypeEntity>> CreateType(CreateTypeDto createTypeDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Create Type entity from DTO (ID will be auto-generated)
            var type = new TypeEntity
            {
                type_description = createTypeDto.type_description
            };

            var createdType = await _typeRepository.CreateAsync(type);
            return CreatedAtAction(
                nameof(GetType),
                new { id = createdType.type_code },
                createdType
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating type {TypeDescription}", createTypeDto.type_description);
            return StatusCode(500, "An error occurred while creating the type");
        }
    }

    /// <summary>
    /// Updates an existing vehicle type
    /// </summary>
    /// <param name="id">The type code to update</param>
    /// <param name="type">The updated type data</param>
    /// <returns>Updated type data</returns>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TypeEntity>> UpdateType(short id, TypeEntity type)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id != type.type_code)
            {
                return BadRequest("Type code in URL doesn't match type code in body");
            }

            var existingType = await _typeRepository.GetByIdAsync(id);
            if (existingType == null)
            {
                return NotFound($"Type with code {id} not found");
            }

            var updatedType = await _typeRepository.UpdateAsync(type);
            return Ok(updatedType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating type {TypeCode}", id);
            return StatusCode(500, "An error occurred while updating the type");
        }
    }

    /// <summary>
    /// Deletes a vehicle type
    /// </summary>
    /// <param name="id">The type code to delete</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteType(short id)
    {
        try
        {
            var existingType = await _typeRepository.GetByIdAsync(id);
            if (existingType == null)
            {
                return NotFound($"Type with code {id} not found");
            }

            await _typeRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting type {TypeCode}", id);
            return StatusCode(500, "An error occurred while deleting the type");
        }
    }
}