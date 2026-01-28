using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers
{
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
                var classes = await _classRepository.GetAllAsync();
                var classDtos = classes.Select(c => new ClassResponseDto
                {
                    class_code = c.class_code,
                    description = c.description,
                    date_created = c.date_created,
                    date_updated = c.date_updated,
                    created_by_user_code = c.created_by_user_code,
                    modified_by_user_code = c.modified_by_user_code,
                    is_deleted = c.is_deleted
                });
                return Ok(classDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving classes");
                return StatusCode(500, $"An error occurred while retrieving classes: {ex.Message}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClassResponseDto>> GetById(short id)
        {
            try
            {
                var classEntity = await _classRepository.GetByIdAsync(id);
                if (classEntity == null)
                {
                    return NotFound($"Class with code {id} not found");
                }
                
                var classDto = new ClassResponseDto
                {
                    class_code = classEntity.class_code,
                    description = classEntity.description,
                    date_created = classEntity.date_created,
                    date_updated = classEntity.date_updated,
                    created_by_user_code = classEntity.created_by_user_code,
                    modified_by_user_code = classEntity.modified_by_user_code,
                    is_deleted = classEntity.is_deleted
                };
                
                return Ok(classDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving class {ClassCode}", id);
                return StatusCode(500, $"An error occurred while retrieving class {id}: {ex.Message}");
            }
        }

        [HttpGet("search/{searchTerm}")]
        public async Task<ActionResult<IEnumerable<ClassResponseDto>>> Search(string searchTerm)
        {
            try
            {
                var classes = await _classRepository.SearchAsync(searchTerm);
                var classDtos = classes.Select(c => new ClassResponseDto
                {
                    class_code = c.class_code,
                    description = c.description,
                    date_created = c.date_created,
                    date_updated = c.date_updated,
                    created_by_user_code = c.created_by_user_code,
                    modified_by_user_code = c.modified_by_user_code,
                    is_deleted = c.is_deleted
                });
                return Ok(classDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching classes with term '{SearchTerm}'", searchTerm);
                return StatusCode(500, $"An error occurred while searching classes: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<ActionResult<ClassResponseDto>> Create(CreateClassDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                int currentUserId = GetCurrentUserId();

                var classEntity = new Class { description = createDto.description };
                var createdClass = await _classRepository.CreateAsync(classEntity, currentUserId);
                
                var responseDto = new ClassResponseDto
                {
                    class_code = createdClass.class_code,
                    description = createdClass.description,
                    date_created = createdClass.date_created,
                    date_updated = createdClass.date_updated,
                    created_by_user_code = createdClass.created_by_user_code,
                    modified_by_user_code = createdClass.modified_by_user_code,
                    is_deleted = createdClass.is_deleted
                };
                
                return CreatedAtAction(nameof(GetById), new { id = createdClass.class_code }, responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating class");
                return StatusCode(500, $"An error occurred while creating class: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ClassResponseDto>> Update(short id, UpdateClassDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingClass = await _classRepository.GetByIdAsync(id);
                if (existingClass == null)
                {
                    return NotFound($"Class with code {id} not found");
                }

                int currentUserId = GetCurrentUserId();

                var classEntity = new Class { class_code = id, description = updateDto.description };
                var updatedClass = await _classRepository.UpdateAsync(classEntity, currentUserId);
                
                var responseDto = new ClassResponseDto
                {
                    class_code = updatedClass.class_code,
                    description = updatedClass.description,
                    date_created = updatedClass.date_created,
                    date_updated = updatedClass.date_updated,
                    created_by_user_code = updatedClass.created_by_user_code,
                    modified_by_user_code = updatedClass.modified_by_user_code,
                    is_deleted = updatedClass.is_deleted
                };
                
                return Ok(responseDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating class {ClassCode}", id);
                return StatusCode(500, $"An error occurred while updating class {id}: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(short id)
        {
            try
            {
                var existingClass = await _classRepository.GetByIdAsync(id);
                if (existingClass == null)
                {
                    return NotFound($"Class with code {id} not found");
                }

                int currentUserId = GetCurrentUserId();
                await _classRepository.DeleteAsync(id, currentUserId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting class {ClassCode}", id);
                return StatusCode(500, $"An error occurred while deleting class {id}: {ex.Message}");
            }
        }
    }
}
