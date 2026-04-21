using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ModelController : BaseApiController
{
    private readonly IModelRepository _modelRepository;
    private readonly IMakeRepository _makeRepository;
    private readonly ILogger<ModelController> _logger;

    public ModelController(
        IModelRepository modelRepository,
        IMakeRepository makeRepository,
        ILogger<ModelController> logger)
    {
        _modelRepository = modelRepository;
        _makeRepository = makeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicle models
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ModelResponseDto>>> GetModels()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var models = await _modelRepository.GetAllModelsAsync();
            var modelDtos = models.Select(m => MapToDto(m));
            _logger.LogInformation("Retrieved {Count} models", models.Count());
            return Ok(modelDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models");
            return StatusCode(500, "An error occurred while retrieving models");
        }
    }

    /// <summary>
    /// Get a model by code
    /// </summary>
    [HttpGet("{modelCode}")]
    public async Task<ActionResult<ModelResponseDto>> GetModel(short modelCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var model = await _modelRepository.GetByIdAsync(modelCode);

            if (model == null)
            {
                _logger.LogWarning("Model with code {ModelCode} not found", modelCode);
                return NotFound();
            }

            var modelDto = MapToDto(model);
            _logger.LogInformation("Retrieved model with code {ModelCode}", modelCode);
            return Ok(modelDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model with code {ModelCode}", modelCode);
            return StatusCode(500, "An error occurred while retrieving the model");
        }
    }

    /// <summary>
    /// Get all models for a specific make
    /// </summary>
    [HttpGet("make/{makeCode}")]
    public async Task<ActionResult<IEnumerable<ModelResponseDto>>> GetModelsByMake(short makeCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            // Verify make exists
            var make = await _makeRepository.GetByIdAsync(makeCode);
            if (make == null)
            {
                _logger.LogWarning("Make with code {MakeCode} not found", makeCode);
                return NotFound($"Make with code {makeCode} not found");
            }

            var models = await _modelRepository.GetModelsByMakeAsync(makeCode);
            var modelDtos = models.Select(m => MapToDto(m));
            _logger.LogInformation("Retrieved {Count} models for make {MakeCode}",
                models.Count(), makeCode);
            return Ok(modelDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models for make {MakeCode}", makeCode);
            return StatusCode(500, "An error occurred while retrieving models for the make");
        }
    }

    /// <summary>
    /// Search models by name or specifications
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ModelResponseDto>>> SearchModels([FromQuery] string? searchTerm)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var models = await _modelRepository.SearchModelsAsync(searchTerm ?? "");
            var modelDtos = models.Select(m => MapToDto(m));
            _logger.LogInformation("Found {Count} models matching search term '{SearchTerm}'",
                models.Count(), searchTerm);
            return Ok(modelDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching models with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching models");
        }
    }

    /// <summary>
    /// Get models by engine type
    /// </summary>
    [HttpGet("engine/{engineType}")]
    public async Task<ActionResult<IEnumerable<ModelResponseDto>>> GetModelsByEngineType(string engineType)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var models = await _modelRepository.GetModelsByEngineTypeAsync(engineType);
            var modelDtos = models.Select(m => MapToDto(m));
            _logger.LogInformation("Found {Count} models with engine type '{EngineType}'",
                models.Count(), engineType);
            return Ok(modelDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving models with engine type '{EngineType}'", engineType);
            return StatusCode(500, "An error occurred while retrieving models by engine type");
        }
    }

    /// <summary>
    /// Create a new model
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ModelResponseDto>> CreateModel([FromBody] CreateModelDto createDto)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verify make exists
            var make = await _makeRepository.GetByIdAsync(createDto.make_code);
            if (make == null)
            {
                return BadRequest($"Make with code {createDto.make_code} does not exist");
            }

            // Map DTO to entity
            var model = new Model
            {
                make_code = createDto.make_code,
                unit_of_measure_code = createDto.unit_of_measure_code,
                fuel_type_code = createDto.fuel_type_code,
                licence_code = createDto.licence_code,
                maint_trigger_code = createDto.maint_trigger_code,
                class_code = createDto.class_code,
                model_description = createDto.model_description,
                engine_type = createDto.engine_type,
                engine_capacity = createDto.engine_capacity,
                rated_power = createDto.rated_power,
                fuel_tank_capacity = createDto.fuel_tank_capacity,
                target_consumption = createDto.target_consumption,
                target_tyre_life = createDto.target_tyre_life,
                service_interval = createDto.service_interval,
                vemm_code = createDto.vemm_code,
                licence_fee_code = createDto.licence_fee_code,
                gvm = createDto.gvm,
                transmission = createDto.transmission,
                wesbank_kilos_per_litre = createDto.wesbank_kilos_per_litre
            };

            var createdModel = await _modelRepository.CreateAsync(model, currentUserId);
            _logger.LogInformation("Created new model with code {ModelCode}", createdModel.model_code);

            var responseDto = MapToDto(createdModel);
            return CreatedAtAction(
                nameof(GetModel),
                new { modelCode = createdModel.model_code },
                responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model");
            return StatusCode(500, "An error occurred while creating the model");
        }
    }

    /// <summary>
    /// Update an existing model
    /// </summary>
    [HttpPut("{modelCode}")]
    public async Task<ActionResult<ModelResponseDto>> UpdateModel(short modelCode, [FromBody] UpdateModelDto updateDto)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (modelCode != updateDto.model_code)
            {
                return BadRequest("Model code in URL does not match model code in body");
            }

            var existingModel = await _modelRepository.GetByIdAsync(modelCode);
            if (existingModel == null)
            {
                return NotFound();
            }

            // Verify make exists
            var make = await _makeRepository.GetByIdAsync(updateDto.make_code);
            if (make == null)
            {
                return BadRequest($"Make with code {updateDto.make_code} does not exist");
            }

            // Map DTO to entity
            var model = new Model
            {
                model_code = updateDto.model_code,
                make_code = updateDto.make_code,
                unit_of_measure_code = updateDto.unit_of_measure_code,
                fuel_type_code = updateDto.fuel_type_code,
                licence_code = updateDto.licence_code,
                maint_trigger_code = updateDto.maint_trigger_code,
                class_code = updateDto.class_code,
                model_description = updateDto.model_description,
                engine_type = updateDto.engine_type,
                engine_capacity = updateDto.engine_capacity,
                rated_power = updateDto.rated_power,
                fuel_tank_capacity = updateDto.fuel_tank_capacity,
                target_consumption = updateDto.target_consumption,
                target_tyre_life = updateDto.target_tyre_life,
                service_interval = updateDto.service_interval,
                vemm_code = updateDto.vemm_code,
                licence_fee_code = updateDto.licence_fee_code,
                gvm = updateDto.gvm,
                transmission = updateDto.transmission,
                wesbank_kilos_per_litre = updateDto.wesbank_kilos_per_litre
            };

            var updatedModel = await _modelRepository.UpdateAsync(model, currentUserId);
            _logger.LogInformation("Updated model with code {ModelCode}", modelCode);

            var responseDto = MapToDto(updatedModel);
            return Ok(responseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating model with code {ModelCode}", modelCode);
            return StatusCode(500, "An error occurred while updating the model");
        }
    }

    /// <summary>
    /// Delete a model
    /// </summary>
    [HttpDelete("{modelCode}")]
    public async Task<ActionResult> DeleteModel(short modelCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingModel = await _modelRepository.GetByIdAsync(modelCode);
            if (existingModel == null)
            {
                return NotFound();
            }

            await _modelRepository.DeleteAsync(modelCode, currentUserId);
            _logger.LogInformation("Deleted model with code {ModelCode}", modelCode);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model with code {ModelCode}", modelCode);
            return StatusCode(500, "An error occurred while deleting the model");
        }
    }

    /// <summary>
    /// Maps a Model entity to ModelResponseDto
    /// Breaks circular reference by excluding navigation properties
    /// </summary>
    private ModelResponseDto MapToDto(Model model)
    {
        return new ModelResponseDto
        {
            model_code = model.model_code,
            model_description = model.model_description,
            make_code = model.make_code,
            make_description = model.Make?.make_description ?? string.Empty,
            unit_of_measure_code = model.unit_of_measure_code,
            fuel_type_code = model.fuel_type_code,
            licence_code = model.licence_code,
            maint_trigger_code = model.maint_trigger_code,
            class_code = model.class_code,
            engine_type = model.engine_type,
            engine_capacity = model.engine_capacity,
            rated_power = model.rated_power,
            fuel_tank_capacity = model.fuel_tank_capacity,
            target_consumption = model.target_consumption,
            target_tyre_life = model.target_tyre_life,
            service_interval = model.service_interval,
            vemm_code = model.vemm_code,
            licence_fee_code = model.licence_fee_code,
            gvm = model.gvm,
            transmission = model.transmission,
            wesbank_kilos_per_litre = model.wesbank_kilos_per_litre
        };
    }
}
