using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FuelTypeEntity = FIS.Core.Domain.Entities.ReferenceData.FuelType;

namespace FIS.Api.Controllers
{
    /// <summary>
    /// API Controller for FuelType entity operations
    /// Provides REST endpoints for vehicle fuel type management
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FuelTypeController : BaseApiController
    {
        private readonly IFuelTypeRepository _fuelTypeRepository;
        private readonly IFuelTariffRepository _fuelTariffRepository;
        private readonly ILogger<FuelTypeController> _logger;

        public FuelTypeController(
            IFuelTypeRepository fuelTypeRepository,
            IFuelTariffRepository fuelTariffRepository,
            ILogger<FuelTypeController> logger
        )
        {
            _fuelTypeRepository = fuelTypeRepository;
            _fuelTariffRepository = fuelTariffRepository;
            _logger = logger;
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
                int currentUserId = GetCurrentUserId();

                var fuelTypes = await _fuelTypeRepository.GetAllFuelTypesAsync();

                // Fetch current tariffs for all fuel types
                var currentTariffs = await _fuelTariffRepository.GetAllCurrentTariffsAsync();
                var tariffLookup = currentTariffs.ToDictionary(
                    t => t.fuel_type_code,
                    t => t.fuel_tariff
                );

                var fuelTypeDtos = fuelTypes.Select(ft => new FuelTypeResponseDto
                {
                    fuel_type_code = ft.fuel_type_code,
                    fuel_description = ft.fuel_description ?? string.Empty,
                    rate_per_litre = tariffLookup.ContainsKey(ft.fuel_type_code)
                        ? tariffLookup[ft.fuel_type_code]
                        : null,
                    date_created = ft.date_created,
                    date_updated = ft.date_updated,
                    created_by_user_code = ft.created_by_user_code,
                    modified_by_user_code = ft.modified_by_user_code,
                    is_deleted = ft.is_deleted,
                });

                return Ok(fuelTypeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fuel types");
                return StatusCode(
                    500,
                    $"An error occurred while retrieving fuel types: {ex.Message}"
                );
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
                int currentUserId = GetCurrentUserId();

                var fuelType = await _fuelTypeRepository.GetByIdAsync(id);
                if (fuelType == null)
                {
                    return NotFound($"Fuel type with code {id} not found");
                }

                // Fetch current tariff for this fuel type
                var currentTariff = await _fuelTariffRepository.GetCurrentTariffAsync(id);

                var fuelTypeDto = new FuelTypeResponseDto
                {
                    fuel_type_code = fuelType.fuel_type_code,
                    fuel_description = fuelType.fuel_description ?? string.Empty,
                    rate_per_litre = currentTariff?.fuel_tariff,
                    date_created = fuelType.date_created,
                    date_updated = fuelType.date_updated,
                    created_by_user_code = fuelType.created_by_user_code,
                    modified_by_user_code = fuelType.modified_by_user_code,
                    is_deleted = fuelType.is_deleted,
                };

                return Ok(fuelTypeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fuel type {Id}", id);
                return StatusCode(
                    500,
                    $"An error occurred while retrieving fuel type {id}: {ex.Message}"
                );
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
                int currentUserId = GetCurrentUserId();

                var fuelType = await _fuelTypeRepository.GetByDescriptionAsync(description);
                if (fuelType == null)
                {
                    return NotFound($"Fuel type with description '{description}' not found");
                }
                return Ok(fuelType);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"An error occurred while retrieving fuel type by description '{description}': {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Searches fuel types by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term</param>
        /// <returns>List of matching fuel types</returns>
        [HttpGet("search/{searchTerm}")]
        public async Task<ActionResult<IEnumerable<FuelTypeEntity>>> SearchFuelTypes(
            string searchTerm
        )
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                var fuelTypes = await _fuelTypeRepository.SearchFuelTypesAsync(searchTerm);
                return Ok(fuelTypes);
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"An error occurred while searching fuel types with term '{searchTerm}': {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Creates a new fuel type
        /// </summary>
        /// <param name="createFuelTypeDto">The fuel type data to create</param>
        /// <returns>The created fuel type</returns>
        [HttpPost]
        public async Task<ActionResult<FuelTypeResponseDto>> CreateFuelType(
            CreateFuelTypeDto createFuelTypeDto
        )
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Create FuelType entity from DTO (ID will be auto-generated)
                var fuelType = new FuelTypeEntity
                {
                    fuel_description = createFuelTypeDto.fuel_description,
                };

                var createdFuelType = await _fuelTypeRepository.CreateAsync(
                    fuelType,
                    currentUserId
                );

                // Create initial tariff if rate_per_litre is provided
                decimal? currentRate = null;
                if (createFuelTypeDto.rate_per_litre.HasValue)
                {
                    _logger.LogInformation(
                        "Creating initial tariff for fuel type {FuelTypeCode} with rate {Rate}",
                        createdFuelType.fuel_type_code,
                        createFuelTypeDto.rate_per_litre.Value
                    );

                    await _fuelTariffRepository.CreateNewRateAsync(
                        createdFuelType.fuel_type_code,
                        createFuelTypeDto.rate_per_litre.Value,
                        createFuelTypeDto.rate_notes,
                        currentUserId
                    );

                    currentRate = createFuelTypeDto.rate_per_litre.Value;
                }

                var responseDto = new FuelTypeResponseDto
                {
                    fuel_type_code = createdFuelType.fuel_type_code,
                    fuel_description = createdFuelType.fuel_description ?? string.Empty,
                    rate_per_litre = currentRate,
                    date_created = createdFuelType.date_created,
                    date_updated = createdFuelType.date_updated,
                    created_by_user_code = createdFuelType.created_by_user_code,
                    modified_by_user_code = createdFuelType.modified_by_user_code,
                    is_deleted = createdFuelType.is_deleted,
                };

                return CreatedAtAction(
                    nameof(GetFuelType),
                    new { id = createdFuelType.fuel_type_code },
                    responseDto
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fuel type");
                return StatusCode(500, $"An error occurred while creating fuel type: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing fuel type
        /// </summary>
        /// <param name="id">The fuel type code</param>
        /// <param name="updateDto">The fuel type data to update</param>
        /// <returns>The updated fuel type</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<FuelTypeResponseDto>> UpdateFuelType(
            short id,
            UpdateFuelTypeDto updateDto
        )
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingFuelType = await _fuelTypeRepository.GetByIdAsync(id);
                if (existingFuelType == null)
                {
                    return NotFound($"Fuel type with code {id} not found");
                }

                // Update fuel type description
                existingFuelType.fuel_description = updateDto.fuel_description;
                var updatedFuelType = await _fuelTypeRepository.UpdateAsync(
                    existingFuelType,
                    currentUserId
                );

                // Handle rate update if provided
                decimal? currentRate = null;
                if (updateDto.rate_per_litre.HasValue)
                {
                    var existingTariff = await _fuelTariffRepository.GetCurrentTariffAsync(id);

                    // Only create new tariff if rate is different or no tariff exists
                    if (
                        existingTariff == null
                        || existingTariff.fuel_tariff != updateDto.rate_per_litre.Value
                    )
                    {
                        _logger.LogInformation(
                            "Updating tariff for fuel type {FuelTypeCode} from {OldRate} to {NewRate}",
                            id,
                            existingTariff?.fuel_tariff,
                            updateDto.rate_per_litre.Value
                        );

                        await _fuelTariffRepository.CreateNewRateAsync(
                            id,
                            updateDto.rate_per_litre.Value,
                            updateDto.rate_notes,
                            currentUserId
                        );

                        currentRate = updateDto.rate_per_litre.Value;
                    }
                    else
                    {
                        currentRate = existingTariff.fuel_tariff;
                    }
                }
                else
                {
                    // Fetch current rate if not updating
                    var existingTariff = await _fuelTariffRepository.GetCurrentTariffAsync(id);
                    currentRate = existingTariff?.fuel_tariff;
                }

                var responseDto = new FuelTypeResponseDto
                {
                    fuel_type_code = updatedFuelType.fuel_type_code,
                    fuel_description = updatedFuelType.fuel_description ?? string.Empty,
                    rate_per_litre = currentRate,
                    date_created = updatedFuelType.date_created,
                    date_updated = updatedFuelType.date_updated,
                    created_by_user_code = updatedFuelType.created_by_user_code,
                    modified_by_user_code = updatedFuelType.modified_by_user_code,
                    is_deleted = updatedFuelType.is_deleted,
                };

                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fuel type {Id}", id);
                return StatusCode(
                    500,
                    $"An error occurred while updating fuel type {id}: {ex.Message}"
                );
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
                int currentUserId = GetCurrentUserId();

                var existingFuelType = await _fuelTypeRepository.GetByIdAsync(id);
                if (existingFuelType == null)
                {
                    return NotFound($"Fuel type with code {id} not found");
                }

                await _fuelTypeRepository.DeleteAsync(id, currentUserId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    $"An error occurred while deleting fuel type {id}: {ex.Message}"
                );
            }
        }
    }
}
