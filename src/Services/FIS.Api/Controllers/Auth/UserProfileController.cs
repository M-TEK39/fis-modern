using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using FIS.Api.Services;
using FIS.Api.Services.SessionManagement;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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
    private const int DefaultAdministrationPageSize = 24;
    private const int MaximumAdministrationPageSize = 100;

    private readonly IUserProfileRepository _repository;
    private readonly FisDbContext _context;
    private readonly ILogger<UserProfileController> _logger;
    private readonly ISessionManagementService _sessionManagementService;
    private readonly ISessionTokenStore _sessionTokenStore;

    public UserProfileController(
        IUserProfileRepository repository,
        FisDbContext context,
        ILogger<UserProfileController> logger,
        ISessionManagementService sessionManagementService,
        ISessionTokenStore sessionTokenStore
    )
    {
        _repository = repository;
        _context = context;
        _logger = logger;
        _sessionManagementService = sessionManagementService;
        _sessionTokenStore = sessionTokenStore;
    }

    /// <summary>
    /// Get user profile by user access code
    /// </summary>
    [HttpGet("{userAccessCode}")]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<UserProfileDto>> GetById(short userAccessCode)
    {
        try
        {
            _logger.LogInformation(
                "Fetching user profile for code {UserAccessCode}",
                userAccessCode
            );
            var user = await _repository.GetByIdAsync(userAccessCode);

            if (user == null)
                return NotFound(
                    new { message = $"User profile not found with code: {userAccessCode}" }
                );

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
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<UserProfileDto>> GetByFirstName(string firstName)
    {
        try
        {
            _logger.LogInformation("Fetching user profile by first name: {FirstName}", firstName);
            var user = await _repository.GetByFirstNameAsync(firstName);

            if (user == null)
                return NotFound(
                    new { message = $"User profile not found with first name: {firstName}" }
                );

            return Ok(MapToDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching user profile by first name {FirstName}",
                firstName
            );
            return StatusCode(500, "Error retrieving user profile");
        }
    }

    /// <summary>
    /// Get all active user profiles
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetAllActive()
    {
        try
        {
            _logger.LogInformation("Fetching all active user profiles");
            var users = await _repository.GetAllActiveAsync();
            var dtos = users.Select(user => MapToDto(user));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active user profiles");
            return StatusCode(500, "Error retrieving user profiles");
        }
    }

    /// <summary>
    /// Get the limited active-user fields needed when a workflow selects an
    /// approver or user reference. This deliberately does not expose access
    /// levels or the rest of the administration profile to operational users.
    /// </summary>
    [HttpGet("approver-choices")]
    [Authorize(Roles = "User Administration,Call Centre,Trip Authorities,TripAuthorities")]
    public async Task<ActionResult<IEnumerable<UserApproverChoiceDto>>> GetApproverChoices()
    {
        try
        {
            var users = await _repository.GetAllActiveAsync();
            var positionNames = await GetLookupNamesAsync(
                "Positions",
                "Position_Code",
                "Position_Name"
            );

            return Ok(
                users
                    .Where(user => user.user_active)
                    .Select(user => new UserApproverChoiceDto
                    {
                        UserAccessCode = user.user_access_code,
                        UserName = user.name,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Telephone = user.telephone,
                        PositionName =
                            user.Position_Code is byte positionCode
                            && positionNames.TryGetValue(positionCode, out var positionName)
                                ? positionName
                                : null,
                        SiteCode = user.Site_code,
                    })
                    .OrderBy(user => user.UserName)
                    .ThenBy(user => user.LastName)
                    .ThenBy(user => user.UserAccessCode)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving limited approver choices");
            return StatusCode(500, "Error retrieving approver choices");
        }
    }

    /// <summary>
    /// Get the active user rows used by the legacy User Administration grid.
    /// This endpoint keeps the legacy role boundary while the generic profile
    /// endpoint remains available to non-admin workflows such as approver lookup.
    /// </summary>
    [HttpGet("administration")]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetForAdministration(
        [FromQuery] string? alphabet
    )
    {
        try
        {
            var selectedAlphabet = NormalizeAlphabet(alphabet);
            var users = (await _repository.GetAllActiveAsync())
                .Where(user =>
                    string.IsNullOrWhiteSpace(user.LastName)
                    || user.LastName.StartsWith(
                        selectedAlphabet,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .ToList();

            var siteNames = await GetLookupNamesAsync("site", "Site_code", "description");
            var positionNames = await GetLookupNamesAsync(
                "Positions",
                "Position_Code",
                "Position_Name"
            );

            return Ok(users.Select(user => MapToDto(user, siteNames, positionNames)));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching User Administration rows for alphabet {Alphabet}",
                alphabet
            );
            return StatusCode(500, "Error retrieving user administration rows");
        }
    }

    /// <summary>
    /// Get one server-paginated page of the active user rows used by the
    /// legacy User Administration grid. The unpaged administration endpoint
    /// remains available for existing consumers.
    /// </summary>
    [HttpGet("administration/page")]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult> GetForAdministrationPage(
        [FromQuery] string? alphabet,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultAdministrationPageSize
    )
    {
        try
        {
            var selectedAlphabet = NormalizeAlphabet(alphabet);
            var result = await _repository.GetAdministrationPageAsync(
                selectedAlphabet,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, MaximumAdministrationPageSize)
            );

            var siteNames = await GetLookupNamesAsync("site", "Site_code", "description");
            var positionNames = await GetLookupNamesAsync(
                "Positions",
                "Position_Code",
                "Position_Name"
            );

            return Ok(
                new
                {
                    items = result.Items.Select(user => MapToDto(user, siteNames, positionNames)),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching paged User Administration rows for alphabet {Alphabet}, page {Page}, page size {PageSize}",
                alphabet,
                page,
                pageSize
            );
            return StatusCode(500, "Error retrieving user administration rows");
        }
    }

    /// <summary>
    /// Get user profiles by site
    /// </summary>
    [HttpGet("by-site/{siteCode}")]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> GetBySite(short siteCode)
    {
        try
        {
            _logger.LogInformation("Fetching user profiles for site {SiteCode}", siteCode);
            var users = await _repository.GetBySiteAsync(siteCode);
            var dtos = users.Select(user => MapToDto(user));
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user profiles for site {SiteCode}", siteCode);
            return StatusCode(500, "Error retrieving user profiles");
        }
    }

    /// <summary>
    /// Get legacy position lookup values used by User Administration.
    /// The Positions table is part of the client schema and may be absent from
    /// a partial backup, so active profile codes remain a safe fallback.
    /// </summary>
    [HttpGet("positions")]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<IEnumerable<UserPositionDto>>> GetPositions()
    {
        try
        {
            var positionNames = await GetLookupNamesAsync(
                "Positions",
                "Position_Code",
                "Position_Name"
            );
            if (positionNames.Count > 0)
            {
                return Ok(
                    positionNames
                        .Where(pair => pair.Key >= byte.MinValue && pair.Key <= byte.MaxValue)
                        .OrderBy(pair => pair.Value)
                        .Select(pair => new UserPositionDto
                        {
                            PositionCode = (byte)pair.Key,
                            PositionName = pair.Value,
                        })
                );
            }

            var existingCodes = (await _repository.GetAllActiveAsync())
                .Where(user => user.Position_Code.HasValue)
                .Select(user => user.Position_Code!.Value)
                .Distinct()
                .OrderBy(code => code)
                .Select(code => new UserPositionDto
                {
                    PositionCode = code,
                    PositionName = $"Position ({code})",
                });

            return Ok(existingCodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving legacy user positions");
            return StatusCode(500, "Error retrieving user positions");
        }
    }

    /// <summary>
    /// Search user profiles
    /// </summary>
    [HttpGet("search")]
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<IEnumerable<UserProfileDto>>> Search([FromQuery] string term)
    {
        try
        {
            _logger.LogInformation("Searching user profiles with term: {SearchTerm}", term);
            var users = await _repository.SearchAsync(term);
            var dtos = users.Select(user => MapToDto(user));
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
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult<UserProfileDto>> CreateUserProfile(
        [FromBody] CreateUserProfileDto dto
    )
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} creating user profile for {FirstName} {LastName}",
                userId,
                dto.FirstName,
                dto.LastName
            );

            var username = dto.UserName?.Trim();
            if (username is not null && username.Length > 255)
            {
                return BadRequest(new { message = "Username must be 255 characters or fewer" });
            }

            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            {
                return BadRequest(new { message = "First name and last name are required" });
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                var duplicateUsername = await _context.UserAccessOlds.AnyAsync(user =>
                    user.name != null && user.name.ToLower() == username.ToLower()
                );
                if (duplicateUsername)
                {
                    return Conflict(
                        new { message = $"User profile already exists for username: {username}" }
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var duplicateEmail = await _context.UserAccessOlds.AnyAsync(user =>
                    user.E_Mail != null && user.E_Mail.ToLower() == dto.Email.Trim().ToLower()
                );
                if (duplicateEmail)
                {
                    return Conflict(
                        new { message = $"User profile already exists for email: {dto.Email}" }
                    );
                }
            }

            if (dto.SaIdNumber.HasValue)
            {
                var saIdNumber = ToLegacySaIdNumber(dto.SaIdNumber);
                if (saIdNumber.HasValue)
                {
                    var duplicateId = await _context.UserAccessOlds.AnyAsync(user =>
                        user.sa_id_number == saIdNumber
                    );
                    if (duplicateId)
                    {
                        return Conflict(
                            new { message = "A user profile already exists for this ID number" }
                        );
                    }
                }
            }

            var userProfile = new UserAccessOld
            {
                name = username,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                E_Mail = dto.Email,
                telephone = dto.Telephone,
                Site_code = dto.SiteCode,
                Position_Code = dto.PositionCode,
                Persal_Number = dto.PersalNumber,
                Contract_Number = dto.ContractNumber,
                sa_id_number = ToLegacySaIdNumber(dto.SaIdNumber),
                passport_number = dto.PassportNumber,
                Cellphone_Number = dto.CellphoneNumber,
                Fax_Number = dto.FaxNumber,
                approver_code_at_gfleet = dto.ApproverCodeAtGfleet,
                // user_access_old1.password is char(32) and the legacy login
                // contract stores the uppercase MD5 digest, not BCrypt.
                password = HashLegacyPassword(
                    string.IsNullOrWhiteSpace(dto.Password)
                        ? GenerateInitialPassword()
                        : dto.Password
                ),
                user_status = "Active",
                AccessLevel = dto.AccessLevel ?? 0,
            };

            var created = await _repository.CreateAsync(userProfile, userId);
            _logger.LogInformation(
                "User profile created with code {UserAccessCode} for {FirstName} {LastName}",
                created.user_access_code,
                created.FirstName,
                created.LastName
            );

            await TryMirrorExpandedUserAsync(
                created.user_access_code,
                created.E_Mail,
                created.telephone
            );

            return CreatedAtAction(
                nameof(GetById),
                new { userAccessCode = created.user_access_code },
                MapToDto(created)
            );
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
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult> UpdateUserProfile(
        short userAccessCode,
        [FromBody] UpdateUserProfileDto dto
    )
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} updating user profile {UserAccessCode}",
                userId,
                userAccessCode
            );

            var existing = await _repository.GetByIdAsync(userAccessCode);
            if (existing == null)
                return NotFound(
                    new { message = $"User profile not found with code: {userAccessCode}" }
                );

            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var duplicateEmail = await _context.UserAccessOlds.AnyAsync(user =>
                    user.user_access_code != userAccessCode
                    && user.E_Mail != null
                    && user.E_Mail.ToLower() == dto.Email.Trim().ToLower()
                );
                if (duplicateEmail)
                {
                    return Conflict(
                        new { message = $"User profile already exists for email: {dto.Email}" }
                    );
                }
            }

            if (dto.SaIdNumber.HasValue)
            {
                var saIdNumber = ToLegacySaIdNumber(dto.SaIdNumber);
                if (saIdNumber.HasValue)
                {
                    var duplicateId = await _context.UserAccessOlds.AnyAsync(user =>
                        user.user_access_code != userAccessCode && user.sa_id_number == saIdNumber
                    );
                    if (duplicateId)
                    {
                        return Conflict(
                            new { message = "A user profile already exists for this ID number" }
                        );
                    }

                    existing.sa_id_number = saIdNumber;
                }
            }

            // Update allowed fields
            existing.FirstName = dto.FirstName ?? existing.FirstName;
            existing.LastName = dto.LastName ?? existing.LastName;
            existing.E_Mail = dto.Email ?? existing.E_Mail;
            existing.telephone = dto.Telephone ?? existing.telephone;
            existing.Site_code = dto.SiteCode ?? existing.Site_code;
            existing.Position_Code = dto.PositionCode ?? existing.Position_Code;
            existing.Persal_Number = dto.PersalNumber ?? existing.Persal_Number;
            existing.Contract_Number = dto.ContractNumber ?? existing.Contract_Number;
            existing.passport_number = dto.PassportNumber ?? existing.passport_number;
            existing.Cellphone_Number = dto.CellphoneNumber ?? existing.Cellphone_Number;
            existing.Fax_Number = dto.FaxNumber ?? existing.Fax_Number;
            existing.approver_code_at_gfleet =
                dto.ApproverCodeAtGfleet ?? existing.approver_code_at_gfleet;
            existing.AccessLevel = dto.AccessLevel ?? existing.AccessLevel;

            await _repository.UpdateAsync(existing, userId);
            await TryMirrorExpandedUserAsync(
                existing.user_access_code,
                existing.E_Mail,
                existing.telephone
            );
            await RevokeSessionsAfterProfileChangeAsync(existing.user_access_code);

            _logger.LogInformation(
                "User profile {UserAccessCode} updated by user {UserId}",
                userAccessCode,
                userId
            );
            return Ok(new { message = "User profile updated successfully", userAccessCode });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "User profile {UserAccessCode} not found for update",
                userAccessCode
            );
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
    [Authorize(Roles = "User Administration")]
    public async Task<ActionResult> DeleteUserProfile(short userAccessCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} deleting user profile {UserAccessCode}",
                userId,
                userAccessCode
            );

            await _repository.DeleteAsync(userAccessCode, userId);
            await RevokeSessionsAfterProfileChangeAsync(userAccessCode);

            _logger.LogInformation(
                "User profile {UserAccessCode} deleted by user {UserId}",
                userAccessCode,
                userId
            );
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "User profile {UserAccessCode} not found for deletion",
                userAccessCode
            );
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user profile {UserAccessCode}", userAccessCode);
            return StatusCode(500, "Error deleting user profile");
        }
    }

    private async Task RevokeSessionsAfterProfileChangeAsync(int userAccessCode)
    {
        _sessionTokenStore.RevokeByUserAccessCode(userAccessCode);
        var result = await _sessionManagementService.RevokeUserSessionsAsync(
            userAccessCode,
            HttpContext.RequestAborted
        );
        if (result.Status is SessionManagementStatus.Failed)
        {
            _logger.LogWarning(
                "Could not revoke sessions for user profile {UserAccessCode} after an entitlement change: {Reason}",
                userAccessCode,
                result.Description
            );
        }
    }

    /// <summary>
    /// Validate user credentials (for authentication)
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<ActionResult<ValidationResultDto>> ValidateCredentials(
        [FromBody] CredentialsDto credentials
    )
    {
        try
        {
            _logger.LogInformation(
                "Validating credentials for user: {FirstName}",
                credentials.FirstName
            );

            var isValid = await _repository.ValidateCredentialsAsync(
                credentials.FirstName,
                credentials.Password
            );

            if (isValid)
            {
                var user = await _repository.GetByFirstNameAsync(credentials.FirstName);
                return Ok(
                    new ValidationResultDto
                    {
                        IsValid = true,
                        UserAccessCode = user!.user_access_code,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.E_Mail,
                        SiteCode = user.Site_code,
                    }
                );
            }

            return Ok(new ValidationResultDto { IsValid = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating credentials for user: {FirstName}",
                credentials.FirstName
            );
            return StatusCode(500, "Error validating credentials");
        }
    }

    private UserProfileDto MapToDto(
        UserAccessOld u,
        IReadOnlyDictionary<int, string>? siteNames = null,
        IReadOnlyDictionary<int, string>? positionNames = null
    )
    {
        return new UserProfileDto
        {
            UserAccessCode = u.user_access_code,
            UserName = u.name,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.E_Mail,
            Telephone = u.telephone,
            SiteCode = u.Site_code,
            SiteName =
                u.Site_code is short siteCode
                && siteNames?.TryGetValue(siteCode, out var siteName) == true
                    ? siteName
                    : null,
            PositionCode = u.Position_Code,
            PositionName =
                u.Position_Code is byte positionCode
                && positionNames?.TryGetValue(positionCode, out var positionName) == true
                    ? positionName
                    : null,
            PersalNumber = u.Persal_Number,
            ContractNumber = u.Contract_Number,
            SaIdNumber = u.sa_id_number,
            PassportNumber = u.passport_number,
            CellphoneNumber = u.Cellphone_Number,
            FaxNumber = u.Fax_Number,
            ApproverCodeAtGfleet = u.approver_code_at_gfleet,
            UserStatus = u.user_status,
            AccessLevel = u.AccessLevel,
            UserActive = u.user_active,
            LastLogOn = u.last_log_on,
        };
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table and column identifiers are fixed by the two internal call sites; no request value is interpolated."
    )]
    private async Task<Dictionary<int, string>> GetLookupNamesAsync(
        string tableName,
        string codeColumn,
        string nameColumn
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT [{codeColumn}], [{nameColumn}] FROM [dbo].[{tableName}]";

            var names = new Dictionary<int, string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1))
                {
                    continue;
                }

                var name = reader.GetValue(1).ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names[Convert.ToInt32(reader.GetValue(0))] = name;
                }
            }

            return names;
        }
        catch (DbException ex)
        {
            // Site and Positions are legacy lookup tables, but an incomplete
            // client backup must not hide the user rows themselves.
            _logger.LogWarning(
                ex,
                "Unable to load legacy user lookup table {TableName}",
                tableName
            );
            return new Dictionary<int, string>();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string NormalizeAlphabet(string? alphabet)
    {
        var value = alphabet?.Trim();
        return value?.Length == 1 && value[0] is >= 'A' and <= 'Z' ? value : "A";
    }

    private static string HashLegacyPassword(string password)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));
    }

    private static string GenerateInitialPassword()
    {
        return $"FIS-{Convert.ToHexString(RandomNumberGenerator.GetBytes(18))}!a1";
    }

    private async Task TryMirrorExpandedUserAsync(
        short userAccessCode,
        string? email,
        string? telephone
    )
    {
        try
        {
            var modernUser = await _context.Users.FirstOrDefaultAsync(user =>
                user.user_access_code == userAccessCode
            );

            if (modernUser is null)
            {
                _context.Users.Add(
                    new User
                    {
                        user_access_code = userAccessCode,
                        email = email,
                        tel_no = telephone,
                    }
                );
            }
            else
            {
                modernUser.email = email;
                modernUser.tel_no = telephone;
                _context.Users.Update(modernUser);
            }

            await _context.SaveChangesAsync();
        }
        catch (SqlException ex) when (ex.Number is 207 or 208)
        {
            _logger.LogInformation(
                ex,
                "Expanded TS_Users mirror is unavailable; legacy user_access_old1 remains authoritative for user_access_code {UserAccessCode}",
                userAccessCode
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Expanded TS_Users mirror failed; retaining successful legacy user_access_old1 write for user_access_code {UserAccessCode}",
                userAccessCode
            );
        }
    }

    private static int? ToLegacySaIdNumber(long? value)
    {
        if (value is null or < int.MinValue or > int.MaxValue)
        {
            return null;
        }

        return (int)value.Value;
    }
}

#region DTOs

public class UserProfileDto
{
    public short UserAccessCode { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public short? SiteCode { get; set; }
    public string? SiteName { get; set; }
    public byte? PositionCode { get; set; }
    public string? PositionName { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public long? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
    public int? ApproverCodeAtGfleet { get; set; }
    public string? UserStatus { get; set; }
    public long AccessLevel { get; set; }
    public bool UserActive { get; set; }
    public DateTime? LastLogOn { get; set; }
}

public class UserApproverChoiceDto
{
    public short UserAccessCode { get; set; }
    public string? UserName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Telephone { get; set; }
    public string? PositionName { get; set; }
    public short? SiteCode { get; set; }
}

public class CreateUserProfileDto
{
    public string? UserName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public short? SiteCode { get; set; }
    public byte? PositionCode { get; set; }
    public int? PersalNumber { get; set; }
    public int? ContractNumber { get; set; }
    public long? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
    public int? ApproverCodeAtGfleet { get; set; }
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
    public long? SaIdNumber { get; set; }
    public int? PassportNumber { get; set; }
    public int? CellphoneNumber { get; set; }
    public int? FaxNumber { get; set; }
    public int? ApproverCodeAtGfleet { get; set; }
    public long? AccessLevel { get; set; }
}

public class UserPositionDto
{
    public byte PositionCode { get; set; }
    public string PositionName { get; set; } = string.Empty;
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
