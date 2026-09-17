using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Access Level Controller - Manages bitwise permission system
/// Handles CRUD operations for module-based access levels and user permission checking
/// </summary>
[ApiController]
[Authorize(Roles = "User Administration")]
[Route("api/accesslevel")]
public class AccessLevelController : BaseApiController
{
    private readonly IAccessLevelRepository _repository;
    private readonly ILogger<AccessLevelController> _logger;

    public AccessLevelController(
        IAccessLevelRepository repository,
        ILogger<AccessLevelController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all access levels (module permissions)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AccessLevelDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Fetching all access levels");
            var accessLevels = await _repository.GetAllAsync();
            var dtos = accessLevels.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching access levels");
            return StatusCode(500, "Error retrieving access levels");
        }
    }

    /// <summary>
    /// Get access level by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AccessLevelDto>> GetById(short id)
    {
        try
        {
            _logger.LogInformation("Fetching access level {Id}", id);
            var accessLevel = await _repository.GetByIdAsync(id);

            if (accessLevel == null)
                return NotFound(new { message = $"Access level not found with ID: {id}" });

            return Ok(MapToDto(accessLevel));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching access level {Id}", id);
            return StatusCode(500, "Error retrieving access level");
        }
    }

    /// <summary>
    /// Get access level by name
    /// </summary>
    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<AccessLevelDto>> GetByName(string name)
    {
        try
        {
            _logger.LogInformation("Fetching access level by name: {Name}", name);
            var accessLevel = await _repository.GetByNameAsync(name);

            if (accessLevel == null)
                return NotFound(new { message = $"Access level not found with name: {name}" });

            return Ok(MapToDto(accessLevel));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching access level by name {Name}", name);
            return StatusCode(500, "Error retrieving access level");
        }
    }

    /// <summary>
    /// Get all permissions for a user based on their access level value
    /// </summary>
    [HttpGet("user-permissions/{userAccessLevel}")]
    public async Task<ActionResult<UserPermissionsDto>> GetUserPermissions(long userAccessLevel)
    {
        try
        {
            _logger.LogInformation(
                "Fetching permissions for user access level: {AccessLevel}",
                userAccessLevel
            );
            var permissions = await _repository.GetUserPermissionsAsync(userAccessLevel);

            return Ok(
                new UserPermissionsDto
                {
                    UserAccessLevel = userAccessLevel,
                    Permissions = permissions.ToList(),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching user permissions for access level {AccessLevel}",
                userAccessLevel
            );
            return StatusCode(500, "Error retrieving user permissions");
        }
    }

    /// <summary>
    /// Check if a user has a specific permission
    /// </summary>
    [HttpPost("check-permission")]
    public async Task<ActionResult<PermissionCheckDto>> CheckPermission(
        [FromBody] CheckPermissionRequest request
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.PermissionName))
                return BadRequest(new { message = "Permission name is required" });

            _logger.LogInformation(
                "Checking if user access level {AccessLevel} has permission: {Permission}",
                request.UserAccessLevel,
                request.PermissionName
            );

            var hasPermission = await _repository.UserHasPermissionAsync(
                request.UserAccessLevel,
                request.PermissionName
            );

            return Ok(
                new PermissionCheckDto
                {
                    UserAccessLevel = request.UserAccessLevel,
                    PermissionName = request.PermissionName,
                    HasPermission = hasPermission,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking permission {Permission} for access level {AccessLevel}",
                request.PermissionName,
                request.UserAccessLevel
            );
            return StatusCode(500, "Error checking permission");
        }
    }

    /// <summary>
    /// Create new access level (admin only)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AccessLevelDto>> Create([FromBody] CreateAccessLevelDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.AccessLevelName))
                return BadRequest(new { message = "Access level name is required" });

            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} creating access level: {Name}",
                userId,
                dto.AccessLevelName
            );

            // Check if name already exists
            var existing = await _repository.GetByNameAsync(dto.AccessLevelName);
            if (existing != null)
                return Conflict(
                    new
                    {
                        message = $"Access level already exists with name: {dto.AccessLevelName}",
                    }
                );

            var accessLevel = new AccessLevel
            {
                AccessLevelName = dto.AccessLevelName,
                AccessLevelValue = dto.AccessLevelValue,
            };

            var created = await _repository.CreateAsync(accessLevel, userId);
            _logger.LogInformation("Access level created with ID {Id}", created.AccessLevelID);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.AccessLevelID },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating access level");
            return StatusCode(500, "Error creating access level");
        }
    }

    /// <summary>
    /// Update access level (admin only)
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(short id, [FromBody] UpdateAccessLevelDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} updating access level {Id}", userId, id);

            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Access level not found with ID: {id}" });

            // Update fields
            if (!string.IsNullOrWhiteSpace(dto.AccessLevelName))
                existing.AccessLevelName = dto.AccessLevelName;
            if (dto.AccessLevelValue.HasValue)
                existing.AccessLevelValue = dto.AccessLevelValue.Value;

            await _repository.UpdateAsync(existing, userId);

            _logger.LogInformation("Access level {Id} updated by user {UserId}", id, userId);
            return Ok(new { message = "Access level updated successfully", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Access level {Id} not found for update", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating access level {Id}", id);
            return StatusCode(500, "Error updating access level");
        }
    }

    /// <summary>
    /// Delete access level (admin only, soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} deleting access level {Id}", userId, id);

            await _repository.DeleteAsync(id, userId);

            _logger.LogInformation("Access level {Id} deleted by user {UserId}", id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Access level {Id} not found for deletion", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting access level {Id}", id);
            return StatusCode(500, "Error deleting access level");
        }
    }

    private AccessLevelDto MapToDto(AccessLevel a)
    {
        return new AccessLevelDto
        {
            AccessLevelID = a.AccessLevelID,
            AccessLevelName = a.AccessLevelName ?? string.Empty,
            AccessLevelValue = a.AccessLevelValue,
            DateCreated = a.date_created,
            CreatedByUserCode = a.created_by_user_code ?? 0,
        };
    }
}

#region DTOs

public class AccessLevelDto
{
    public short AccessLevelID { get; set; }
    public string AccessLevelName { get; set; } = string.Empty;
    public long AccessLevelValue { get; set; }
    public DateTime DateCreated { get; set; }
    public int CreatedByUserCode { get; set; }
}

public class CreateAccessLevelDto
{
    public string AccessLevelName { get; set; } = string.Empty;
    public long AccessLevelValue { get; set; }
}

public class UpdateAccessLevelDto
{
    public string? AccessLevelName { get; set; }
    public long? AccessLevelValue { get; set; }
}

public class UserPermissionsDto
{
    public long UserAccessLevel { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class CheckPermissionRequest
{
    public long UserAccessLevel { get; set; }
    public string PermissionName { get; set; } = string.Empty;
}

public class PermissionCheckDto
{
    public long UserAccessLevel { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public bool HasPermission { get; set; }
}

#endregion
