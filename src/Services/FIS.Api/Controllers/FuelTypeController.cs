using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FuelTypeEntity = FIS.Data.Entities.FuelType;

namespace FIS.Api.Controllers
{
    /// <summary>
    /// API Controller for FuelType entity operations
    /// Provides REST endpoints for vehicle fuel type management
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FuelTypeController : ControllerBase
    {
        private readonly IFuelTypeRepository _fuelTypeRepository;

        public FuelTypeController(IFuelTypeRepository fuelTypeRepository)
        {
            _fuelTypeRepository = fuelTypeRepository;
        }

        /// <summary>
        /// Gets all fuel types
        /// </summary>
        /// <returns>List of all fuel types</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FuelTypeResponseDto>>> GetAllFuelTypes()
        {
            try
            {
                var fuelTypes = await _fuelTypeRepository.GetAllFuelTypesAsync();
                var fuelTypeDtos = fuelTypes.Select(ft => new FuelTypeResponseDto
                {
                    fuel_type_code = ft.fuel_type_code,
                    fuel_description = ft.fuel_description
                });
                return Ok(fuelTypeDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving fuel types: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a fuel type by its code
        /// </summary>
        /// <param name="id">The fuel type code</param>
        /// <returns>The fuel type if found</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<FuelTypeResponseDto>> GetFuelType(short id)
        {
            try
            {
                var fuelType = await _fuelTypeRepository.GetByIdAsync(id);
                if (fuelType == null)
                {
                    return NotFound($"Fuel type with code {id} not found");
                }
                
                var fuelTypeDto = new FuelTypeResponseDto
                {
                    fuel_type_code = fuelType.fuel_type_code,
                    fuel_description = fuelType.fuel_description
                };
                
                return Ok(fuelTypeDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving fuel type {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a fuel type by its description
        /// </summary>
        /// <param name="description">The fuel type description</param>
        /// <returns>The fuel type if found</returns>
        [HttpGet("by-description/{description}")]
        public async Task<ActionResult<FuelTypeEntity>> GetFuelTypeByDescription(string description)
        {
            try
            {
                var fuelType = await _fuelTypeRepository.GetByDescriptionAsync(description);
                if (fuelType == null)
                {
                    return NotFound($"Fuel type with description '{description}' not found");
                }
                return Ok(fuelType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving fuel type by description '{description}': {ex.Message}");
            }
        }

        /// <summary>
        /// Searches fuel types by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term</param>
        /// <returns>List of matching fuel types</returns>
        [HttpGet("search/{searchTerm}")]
        public async Task<ActionResult<IEnumerable<FuelTypeEntity>>> SearchFuelTypes(string searchTerm)
        {
            try
            {
                var fuelTypes = await _fuelTypeRepository.SearchFuelTypesAsync(searchTerm);
                return Ok(fuelTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while searching fuel types with term '{searchTerm}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new fuel type
        /// </summary>
        /// <param name="createFuelTypeDto">The fuel type data to create</param>
        /// <returns>The created fuel type</returns>
        [HttpPost]
        public async Task<ActionResult<FuelTypeEntity>> CreateFuelType(CreateFuelTypeDto createFuelTypeDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Create FuelType entity from DTO (ID will be auto-generated)
                var fuelType = new FuelTypeEntity
                {
                    fuel_description = createFuelTypeDto.fuel_description
                };

                var createdFuelType = await _fuelTypeRepository.CreateAsync(fuelType);
                return CreatedAtAction(nameof(GetFuelType), new { id = createdFuelType.fuel_type_code }, createdFuelType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while creating fuel type: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing fuel type
        /// </summary>
        /// <param name="id">The fuel type code</param>
        /// <param name="fuelType">The fuel type data to update</param>
        /// <returns>The updated fuel type</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<FuelTypeEntity>> UpdateFuelType(short id, FuelTypeEntity fuelType)
        {
            try
            {
                if (id != fuelType.fuel_type_code)
                {
                    return BadRequest("Fuel type code mismatch");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingFuelType = await _fuelTypeRepository.GetByIdAsync(id);
                if (existingFuelType == null)
                {
                    return NotFound($"Fuel type with code {id} not found");
                }

                var updatedFuelType = await _fuelTypeRepository.UpdateAsync(fuelType);
                return Ok(updatedFuelType);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while updating fuel type {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a fuel type
        /// </summary>
        /// <param name="id">The fuel type code</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFuelType(short id)
        {
            try
            {
                var existingFuelType = await _fuelTypeRepository.GetByIdAsync(id);
                if (existingFuelType == null)
                {
                    return NotFound($"Fuel type with code {id} not found");
                }

                await _fuelTypeRepository.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting fuel type {id}: {ex.Message}");
            }
        }
    }
}
