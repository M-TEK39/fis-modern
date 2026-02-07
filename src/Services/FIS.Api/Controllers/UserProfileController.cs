using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// User Profile Controller - Manages user profile data from user_access_old1 table
/// Provides endpoints for user profile CRUD and authentication lookup
/// </summary>
[ApiController]
[Authorize]
[Route("api/userprofile")]
public class UserProfileController : BaseApiController
{
    private readonly IUserProfileRepository _repository;
    private readonly ILogger<UserProfileController> _logger;

    public UserProfileController(
        IUserProfileRepository repository,
        ILogger<UserProfileController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get user profile by user access code
    /// </summary>
    [HttpGet("{userAccessCode}")]
    public async Task<ActionResult<UserProfileDto>> GetById(short userAccessCode)
    {
        try
        {
            _logger.LogInformation("Fetching user profile for code {UserAccessCode}", userAccessCode);
            var user = await _repository.GetByIdAsync(userAccessCode);

            if (user == null)
                return NotFound(new { message = $"User profile not found with code: {userAccessCode}" });

            return Ok(MapToDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user profile {UserAccessCode}", userAccessCode);
            return StatusCode(500, "Error retrieving user profile");
        }
    }

    /// <summary>
    /// Get user profile by first name (for login lookup)
    /// </summary>
    [HttpGet("by-name/{firstName}")]
    [AllowAnonymous] // Allow for login flow
    public async Task<ActionResult<UserProfileDto>> GetByFirstName(string firstName)
    {
        try
        {
            _logger.LogInformation("Fetching user profile by first name: {FirstName}", firstName);
            var user = await _repository.GetByFirstNameAsync(firstName);

            if (user == null)
                return NotFound(new { message = $"User profile not found with first name: {firstName}" });

            return Ok(MapToDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user profile by first name {FirstName}", firstName);
            return StatusCode(500, "Error retrieving user profile");
        }
    }

    /// <summary>
    /// Get all active user profiles
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetAllActive()
    {
        try
        {
            _logger.LogInformation("Fetching all active user profiles");
            var users = await _repository.GetAllActiveAsync();
            var dtos = users.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active user profiles");
            return StatusCode(500, "Error retrieving user profiles");
        }
    }

    /// <summary>
    /// Get user profiles by site
    /// </summary>
    [HttpGet("by-site/{siteCode}")]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetBySite(short siteCode)
    {
        try
        {
            _logger.LogInformation("Fetching user profiles for site {SiteCode}", siteCode);
            var users = await _repository.GetBySiteAsync(siteCode);
            var dtos = users.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user profiles for site {SiteCode}", siteCode);
            return StatusCode(500, "Error retrieving user profiles");
        }
    }

    /// <summary>
    /// Search user profiles
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> Search([FromQuery] string term)
    {
        try
        {
            _logger.LogInformation("Searching user profiles with term: {SearchTerm}", term);
            var users = await _repository.SearchAsync(term);
            var dtos = users.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching user profiles with term {SearchTerm}", term);
            return StatusCode(500, "Error searching user profiles");
        }
    }

    /// <summary>
    /// Create new user profile
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<UserProfileDto>> CreateUserProfile([FromBody] CreateUserProfileDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} creating user profile for {FirstName} {LastName}",
                userId, dto.FirstName, dto.LastName);

            // Check if user already exists
            var existing = await _repository.GetByFirstNameAsync(dto.FirstName);
            if (existing != null && !existing.is_deleted)
            {
                return Conflict(new { message = $"User profile already exists for first name: {dto.FirstName}" });
            }

            var userProfile = new UserAccessOld
            {
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                E_Mail = dto.Email,
                telephone = dto.Telephone,
                Site_code = dto.SiteCode,
                Position_Code = dto.PositionCode,
                Persal_Number = dto.PersalNumber,
                Contract_Number = dto.ContractNumber,
                sa_id_number = dto.SaIdNumber,
                passport_number = dto.PassportNumber,
                Cellphone_Number = dto.CellphoneNumber,
                Fax_Number = dto.FaxNumber,
                password = dto.Password, // TODO: Hash password
                user_status = "Active",
                AccessLevel = dto.AccessLevel ?? 1
            };

            var created = await _repository.CreateAsync(userProfile, userId);
            _logger.LogInformation("User profile created with code {UserAccessCode} for {FirstName} {LastName}",
                created.user_access_code, created.FirstName, created.LastName);

            return CreatedAtAction(
                nameof(GetById),
                new { userAccessCode = created.user_access_code },
                MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user profile");
            return StatusCode(500, "Error creating user profile");
        }
    }

    /// <summary>
    /// Update user profile
    /// </summary>
    [HttpPut("{userAccessCode}")]
    public async Task<ActionResult> UpdateUserProfile(short userAccessCode, [FromBody] UpdateUserProfileDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} updating user profile {UserAccessCode}", userId, userAccessCode);

            var existing = await _repository.GetByIdAsync(userAccessCode);
            if (existing == null)
                return NotFound(new { message = $"User profile not found with code: {userAccessCode}" });

            // Update allowed fields
            existing.FirstName = dto.FirstName ?? existing.FirstName;
            existing.LastName = dto.LastName ?? existing.LastName;
            existing.E_Mail = dto.Email ?? existing.E_Mail;
            existing.telephone = dto.Telephone ?? existing.telephone;
            existing.Site_code = dto.SiteCode ?? existing.Site_code;
            existing.Position_Code = dto.PositionCode ?? existing.Position_Code;
            existing.Persal_Number = dto.PersalNumber ?? existing.Persal_Number;
            existing.Contract_Number = dto.ContractNumber ?? existing.Contract_Number;
            existing.sa_id_number = dto.SaIdNumber ?? existing.sa_id_number;
            existing.passport_number = dto.PassportNumber ?? existing.passport_number;
            existing.Cellphone_Number = dto.CellphoneNumber ?? existing.Cellphone_Number;
            existing.Fax_Number = dto.FaxNumber ?? existing.Fax_Number;

            await _repository.UpdateAsync(existing, userId);

            _logger.LogInformation("User profile {UserAccessCode} updated by user {UserId}", userAccessCode, userId);
            return Ok(new { message = "User profile updated successfully", userAccessCode });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "User profile {UserAccessCode} not found for update", userAccessCode);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile {UserAccessCode}", userAccessCode);
            return StatusCode(500, "Error updating user profile");
        }
    }

    /// <summary>
    /// Delete user profile (soft delete)
    /// </summary>
    [HttpDelete("{userAccessCode}")]
    public async Task<ActionResult> DeleteUserProfile(short userAccessCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} deleting user profile {UserAccessCode}", userId, userAccessCode);

            await _repository.DeleteAsync(userAccessCode, userId);

            _logger.LogInformation("User profile {UserAccessCode} deleted by user {UserId}", userAccessCode, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "User profile {UserAccessCode} not found for deletion", userAccessCode);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user profile {UserAccessCode}", userAccessCode);
            return StatusCode(500, "Error deleting user profile");
        }
    }

    /// <summary>
    /// Validate user credentials (for authentication)
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<ActionResult<ValidationResultDto>> ValidateCredentials([FromBody] CredentialsDto credentials)
    {
        try
        {
            _logger.LogInformation("Validating credentials for user: {FirstName}", credentials.FirstName);

            var isValid = await _repository.ValidateCredentialsAsync(credentials.FirstName, credentials.Password);

            if (isValid)
            {
                var user = await _repository.GetByFirstNameAsync(credentials.FirstName);
                return Ok(new ValidationResultDto
                {
                    IsValid = true,
                    UserAccessCode = user!.user_access_code,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.E_Mail,
                    SiteCode = user.Site_code
                });
            }

            return Ok(new ValidationResultDto { IsValid = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credentials for user: {FirstName}", credentials.FirstName);
            return StatusCode(500, "Error validating credentials");
        }
    }

    private UserProfileDto MapToDto(UserAccessOld u)
    {
        return new UserProfileDto
        {
            UserAccessCode = u.user_access_code,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.E_Mail,
            Telephone = u.telephone,
            SiteCode = u.Site_code,
            PositionCode = u.Position_Code,
            PersalNumber = u.Persal_Number,
            ContractNumber = u.Contract_Number,
            SaIdNumber = u.sa_id_number,
            PassportNumber = u.passport_number,
            CellphoneNumber = u.Cellphone_Number,
            FaxNumber = u.Fax_Number,
            UserStatus = u.user_status,
            AccessLevel = u.AccessLevel,
            UserActive = u.user_active,
            LastLogOn = u.last_log_on
        };
    }
}

#region DTOs

public class UserProfileDto
{
    public short UserAccessCode { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public short? SiteCode { get; set; }
    public byte? PositionCode { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public int? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
    public string? UserStatus { get; set; }
    public long AccessLevel { get; set; }
    public bool UserActive { get; set; }
    public DateTime? LastLogOn { get; set; }
}

public class CreateUserProfileDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public short? SiteCode { get; set; }
    public byte? PositionCode { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public int? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
    public string Password { get; set; } = string.Empty;
    public long? AccessLevel { get; set; }
}

public class UpdateUserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public short? SiteCode { get; set; }
    public byte? PositionCode { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public int? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
}

public class CredentialsDto
{
    public string FirstName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public short? UserAccessCode { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public short? SiteCode { get; set; }
}

#endregion
