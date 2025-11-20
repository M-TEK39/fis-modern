using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelController : ControllerBase
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
    public async Task<ActionResult<IEnumerable<Model>>> GetModels()
    {
        try
        {
            var models = await _modelRepository.GetAllModelsAsync();
            _logger.LogInformation("Retrieved {Count} models", models.Count());
            return Ok(models);
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
    public async Task<ActionResult<Model>> GetModel(short modelCode)
    {
        try
        {
            var model = await _modelRepository.GetByIdAsync(modelCode);

            if (model == null)
            {
                _logger.LogWarning("Model with code {ModelCode} not found", modelCode);
                return NotFound();
            }

            _logger.LogInformation("Retrieved model with code {ModelCode}", modelCode);
            return Ok(model);
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
    public async Task<ActionResult<IEnumerable<Model>>> GetModelsByMake(short makeCode)
    {
        try
        {
            // Verify make exists
            var make = await _makeRepository.GetByIdAsync(makeCode);
            if (make == null)
            {
                _logger.LogWarning("Make with code {MakeCode} not found", makeCode);
                return NotFound($"Make with code {makeCode} not found");
            }

            var models = await _modelRepository.GetModelsByMakeAsync(makeCode);
            _logger.LogInformation("Retrieved {Count} models for make {MakeCode}", 
                models.Count(), makeCode);
            return Ok(models);
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
    public async Task<ActionResult<IEnumerable<Model>>> SearchModels([FromQuery] string? searchTerm)
    {
        try
        {
            var models = await _modelRepository.SearchModelsAsync(searchTerm ?? "");
            _logger.LogInformation("Found {Count} models matching search term '{SearchTerm}'", 
                models.Count(), searchTerm);
            return Ok(models);
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
    public async Task<ActionResult<IEnumerable<Model>>> GetModelsByEngineType(string engineType)
    {
        try
        {
            var models = await _modelRepository.GetModelsByEngineTypeAsync(engineType);
            _logger.LogInformation("Found {Count} models with engine type '{EngineType}'", 
                models.Count(), engineType);
            return Ok(models);
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
    public async Task<ActionResult<Model>> CreateModel([FromBody] Model model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Verify make exists
            var make = await _makeRepository.GetByIdAsync(model.make_code);
            if (make == null)
            {
                return BadRequest($"Make with code {model.make_code} does not exist");
            }

            var existingModel = await _modelRepository.GetByIdAsync(model.model_code);
            if (existingModel != null)
            {
                return Conflict($"Model with code {model.model_code} already exists");
            }

            var createdModel = await _modelRepository.CreateAsync(model);
            _logger.LogInformation("Created new model with code {ModelCode}", createdModel.model_code);
            
            return CreatedAtAction(
                nameof(GetModel), 
                new { modelCode = createdModel.model_code }, 
                createdModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating model with code {ModelCode}", model.model_code);
            return StatusCode(500, "An error occurred while creating the model");
        }
    }

    /// <summary>
    /// Update an existing model
    /// </summary>
    [HttpPut("{modelCode}")]
    public async Task<ActionResult<Model>> UpdateModel(short modelCode, [FromBody] Model model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (modelCode != model.model_code)
            {
                return BadRequest("Model code in URL does not match model code in body");
            }

            var existingModel = await _modelRepository.GetByIdAsync(modelCode);
            if (existingModel == null)
            {
                return NotFound();
            }

            // Verify make exists
            var make = await _makeRepository.GetByIdAsync(model.make_code);
            if (make == null)
            {
                return BadRequest($"Make with code {model.make_code} does not exist");
            }

            var updatedModel = await _modelRepository.UpdateAsync(model);
            _logger.LogInformation("Updated model with code {ModelCode}", modelCode);
            
            return Ok(updatedModel);
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
            var existingModel = await _modelRepository.GetByIdAsync(modelCode);
            if (existingModel == null)
            {
                return NotFound();
            }

            await _modelRepository.DeleteAsync(modelCode);
            _logger.LogInformation("Deleted model with code {ModelCode}", modelCode);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model with code {ModelCode}", modelCode);
            return StatusCode(500, "An error occurred while deleting the model");
        }
    }
}