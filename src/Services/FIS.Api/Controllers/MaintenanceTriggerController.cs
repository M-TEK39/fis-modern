using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using MaintenanceTriggerEntity = FIS.Data.Entities.MaintenanceTrigger;

namespace FIS.Api.Controllers
{
    /// <summary>
    /// API Controller for MaintenanceTrigger entity operations
    /// Provides REST endpoints for vehicle maintenance trigger management
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MaintenanceTriggerController : ControllerBase
    {
        private readonly IMaintenanceTriggerRepository _maintenanceTriggerRepository;

        public MaintenanceTriggerController(IMaintenanceTriggerRepository maintenanceTriggerRepository)
        {
            _maintenanceTriggerRepository = maintenanceTriggerRepository;
        }

        /// <summary>
        /// Gets all maintenance triggers
        /// </summary>
        /// <returns>List of all maintenance triggers</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaintenanceTriggerEntity>>> GetAllMaintenanceTriggers()
        {
            try
            {
                var triggers = await _maintenanceTriggerRepository.GetAllMaintenanceTriggersAsync();
                return Ok(triggers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving maintenance triggers: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a maintenance trigger by its code
        /// </summary>
        /// <param name="id">The maintenance trigger code</param>
        /// <returns>The maintenance trigger if found</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<MaintenanceTriggerEntity>> GetMaintenanceTrigger(short id)
        {
            try
            {
                var trigger = await _maintenanceTriggerRepository.GetByIdAsync(id);
                if (trigger == null)
                {
                    return NotFound($"Maintenance trigger with code {id} not found");
                }
                return Ok(trigger);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving maintenance trigger {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a maintenance trigger by its description
        /// </summary>
        /// <param name="description">The maintenance trigger description</param>
        /// <returns>The maintenance trigger if found</returns>
        [HttpGet("by-description/{description}")]
        public async Task<ActionResult<MaintenanceTriggerEntity>> GetMaintenanceTriggerByDescription(string description)
        {
            try
            {
                var trigger = await _maintenanceTriggerRepository.GetByDescriptionAsync(description);
                if (trigger == null)
                {
                    return NotFound($"Maintenance trigger with description '{description}' not found");
                }
                return Ok(trigger);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving maintenance trigger by description '{description}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a maintenance trigger by its trigger ID
        /// </summary>
        /// <param name="triggerId">The trigger ID</param>
        /// <returns>The maintenance trigger if found</returns>
        [HttpGet("by-trigger-id/{triggerId}")]
        public async Task<ActionResult<MaintenanceTriggerEntity>> GetMaintenanceTriggerByTriggerId(string triggerId)
        {
            try
            {
                var trigger = await _maintenanceTriggerRepository.GetByTriggerIdAsync(triggerId);
                if (trigger == null)
                {
                    return NotFound($"Maintenance trigger with trigger ID '{triggerId}' not found");
                }
                return Ok(trigger);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while retrieving maintenance trigger by trigger ID '{triggerId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Searches maintenance triggers by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term</param>
        /// <returns>List of matching maintenance triggers</returns>
        [HttpGet("search/{searchTerm}")]
        public async Task<ActionResult<IEnumerable<MaintenanceTriggerEntity>>> SearchMaintenanceTriggers(string searchTerm)
        {
            try
            {
                var triggers = await _maintenanceTriggerRepository.SearchMaintenanceTriggersAsync(searchTerm);
                return Ok(triggers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while searching maintenance triggers with term '{searchTerm}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new maintenance trigger
        /// </summary>
        /// <param name="createMaintenanceTriggerDto">The maintenance trigger data to create</param>
        /// <returns>The created maintenance trigger</returns>
        [HttpPost]
        public async Task<ActionResult<MaintenanceTriggerEntity>> CreateMaintenanceTrigger(CreateMaintenanceTriggerDto createMaintenanceTriggerDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Create MaintenanceTrigger entity from DTO (ID will be auto-generated)
                var trigger = new MaintenanceTriggerEntity
                {
                    description = createMaintenanceTriggerDto.description,
                    trigger_id = createMaintenanceTriggerDto.trigger_id
                };

                var createdTrigger = await _maintenanceTriggerRepository.CreateAsync(trigger);
                return CreatedAtAction(nameof(GetMaintenanceTrigger), new { id = createdTrigger.maint_trigger_code }, createdTrigger);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while creating maintenance trigger: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates an existing maintenance trigger
        /// </summary>
        /// <param name="id">The maintenance trigger code</param>
        /// <param name="trigger">The maintenance trigger data to update</param>
        /// <returns>The updated maintenance trigger</returns>
        [HttpPut("{id}")]
        public async Task<ActionResult<MaintenanceTriggerEntity>> UpdateMaintenanceTrigger(short id, MaintenanceTriggerEntity trigger)
        {
            try
            {
                if (id != trigger.maint_trigger_code)
                {
                    return BadRequest("Maintenance trigger code mismatch");
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingTrigger = await _maintenanceTriggerRepository.GetByIdAsync(id);
                if (existingTrigger == null)
                {
                    return NotFound($"Maintenance trigger with code {id} not found");
                }

                var updatedTrigger = await _maintenanceTriggerRepository.UpdateAsync(trigger);
                return Ok(updatedTrigger);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while updating maintenance trigger {id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a maintenance trigger
        /// </summary>
        /// <param name="id">The maintenance trigger code</param>
        /// <returns>No content if successful</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMaintenanceTrigger(short id)
        {
            try
            {
                var existingTrigger = await _maintenanceTriggerRepository.GetByIdAsync(id);
                if (existingTrigger == null)
                {
                    return NotFound($"Maintenance trigger with code {id} not found");
                }

                await _maintenanceTriggerRepository.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while deleting maintenance trigger {id}: {ex.Message}");
            }
        }
    }
}