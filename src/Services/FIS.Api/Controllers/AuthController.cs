using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using FIS.Api.Services;

namespace FIS.Api.Controllers;

/// <summary>
/// Authentication API Controller
/// Provides JWT token generation for legacy username/password authentication
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly ISessionTokenStore _sessionTokenStore;

    public AuthController(
        IConfiguration configuration,
        IUserRepository userRepository,
        FisDbContext context,
        ILogger<AuthController> logger,
        ISessionTokenStore sessionTokenStore)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _context = context;
        _logger = logger;
        _sessionTokenStore = sessionTokenStore;
    }

    /// <summary>
    /// Legacy JWT login endpoint
    /// Authenticates user with username/password and returns JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username and password are required" });
        }

        // Accept either user_access_code (numeric) or email/username identifier.
        User? user;
        if (int.TryParse(request.Username.Trim(), out var requestedUserCode) && requestedUserCode > 0)
        {
            user = await _userRepository.GetByIdAsync(requestedUserCode);
        }
        else
        {
            user = await _userRepository.GetByEmailAsync(request.Username.Trim());
        }
        if (user == null)
        {
            _logger.LogWarning("Login failed: user not found for {Username}", request.Username);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        var credential = await _context.LegacyUserCredentials
            .FirstOrDefaultAsync(c => c.user_access_code == user.user_access_code && c.is_active);

        if (credential == null)
        {
            _logger.LogWarning("Login failed: no active credential for user {UserAccessCode}", user.user_access_code);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        // Account lockout check
        if (credential.account_locked_until.HasValue && credential.account_locked_until.Value > DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Account is temporarily locked. Please try again later." });
        }

        // Verify password
        var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, credential.password_hash);
        if (!passwordValid)
        {
            credential.failed_login_attempts++;
            if (credential.failed_login_attempts >= 5)
                credential.account_locked_until = DateTime.UtcNow.AddMinutes(30);
            _context.LegacyUserCredentials.Update(credential);
            await _context.SaveChangesAsync();
            _logger.LogWarning("Login failed: wrong password for user {UserAccessCode}", user.user_access_code);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        // Successful login — reset failed attempts
        credential.failed_login_attempts = 0;
        credential.account_locked_until = null;
        _context.LegacyUserCredentials.Update(credential);
        await _context.SaveChangesAsync();

        // Password expiry check — disabled by default for legacy data compatibility.
        // Set JwtSettings:EnforcePasswordExpiry=true in config once real expiry data is in place.
        bool passwordExpired = false;
        int daysRemaining = int.MaxValue;
        var enforceExpiry = bool.TryParse(_configuration["JwtSettings:EnforcePasswordExpiry"], out var enforceFlag) && enforceFlag;

        if (enforceExpiry)
        {
            var fallbackExpiryDays = int.Parse(_configuration["JwtSettings:PasswordExpiryDays"] ?? "90");

            if (credential.password_expiry_date.HasValue && credential.password_expiry_date.Value > new DateTime(2000, 1, 1))
            {
                passwordExpired = DateTime.UtcNow > credential.password_expiry_date.Value;
                daysRemaining = Math.Max(0, (int)(credential.password_expiry_date.Value - DateTime.UtcNow).TotalDays);
            }
            else if (credential.last_password_change > new DateTime(2000, 1, 1))
            {
                var passwordAge = (DateTime.UtcNow - credential.last_password_change).TotalDays;
                passwordExpired = passwordAge > fallbackExpiryDays;
                daysRemaining = Math.Max(0, (int)(fallbackExpiryDays - passwordAge));
            }
            // else: no usable timestamp on the credential row — treat as "not expired" rather than force-expire legacy users
        }

        var authClaims = BuildAuthClaims(user.user_access_code, user.email ?? request.Username, passwordExpired);
        var tokens = _sessionTokenStore.IssueTokens(authClaims);

        WriteAuthCookies(tokens.AccessToken, tokens.AccessExpiresAt, tokens.RefreshToken, tokens.RefreshExpiresAt);

        return Ok(new LoginResponse
        {
            Token = tokens.AccessToken,
            ExpiresAt = tokens.AccessExpiresAt.UtcDateTime,
            UserAccessCode = user.user_access_code,
            Email = user.email,
            PasswordExpired = passwordExpired,
            PasswordExpiresIn = daysRemaining,
            Message = passwordExpired
                ? "Password has expired. Please change your password to continue."
                : "Login successful"
        });
    }

    /// <summary>
    /// Refresh an existing JWT token
    /// </summary>
    /// <returns>New JWT token</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> RefreshToken()
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new { error = "Refresh token cookie is missing." });
        }

        if (!_sessionTokenStore.TryRefresh(refreshToken, out var refreshedTokens, out var claims))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        WriteAuthCookies(
            refreshedTokens.AccessToken,
            refreshedTokens.AccessExpiresAt,
            refreshedTokens.RefreshToken,
            refreshedTokens.RefreshExpiresAt);

        var userAccessCode = int.TryParse(claims.FirstOrDefault(c => c.Type == "user_access_code")?.Value, out var parsedUserCode)
            ? parsedUserCode
            : 0;
        var emailClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

        return Ok(new LoginResponse
        {
            Token = refreshedTokens.AccessToken,
            ExpiresAt = refreshedTokens.AccessExpiresAt.UtcDateTime,
            UserAccessCode = userAccessCode,
            Email = emailClaim,
            Message = "Token refreshed successfully"
        });
    }

    /// <summary>
    /// Logout endpoint (for client-side token cleanup)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public ActionResult Logout()
    {
        var userAccessCode = User.FindFirst("user_access_code")?.Value;
        _logger.LogInformation("User {UserAccessCode} logged out", userAccessCode);

        if (Request.Cookies.TryGetValue(SessionCookieAuthenticationHandler.AccessCookieName, out var accessToken))
        {
            _sessionTokenStore.RevokeByAccessToken(accessToken);
        }

        if (Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken))
        {
            _sessionTokenStore.RevokeByRefreshToken(refreshToken);
        }

        Response.Cookies.Delete(SessionCookieAuthenticationHandler.AccessCookieName);
        Response.Cookies.Delete(RefreshTokenCookieName);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Validate current token
    /// </summary>
    [HttpGet("validate")]
    [Authorize]
    public ActionResult<object> ValidateToken()
    {
        var userAccessCode = User.FindFirst("user_access_code")?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var expiryClaim = User.FindFirst(ClaimTypes.Expiration)?.Value;
        var expiresAt = DateTimeOffset.TryParse(expiryClaim, out var parsedExpiry) ? parsedExpiry.UtcDateTime : (DateTime?)null;

        return Ok(new
        {
            valid = true,
            userAccessCode,
            email,
            expiresAt,
            authType = User.Identity?.AuthenticationType
        });
    }

    /// <summary>
    /// Change user password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "New passwords do not match"
                });
            }

            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                return NotFound(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == user.user_access_code && c.is_active);

            if (credential == null)
                return BadRequest(new ChangePasswordResponse { Success = false, Message = "No credential record found for this user" });

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, credential.password_hash))
                return BadRequest(new ChangePasswordResponse { Success = false, Message = "Current password is incorrect" });

            var changedByRaw = User.FindFirst("user_access_code")?.Value;
            int.TryParse(changedByRaw, out int changedBy);
            var now = DateTime.UtcNow;

            credential.password_hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            credential.last_password_change = now;
            credential.password_expiry_date = now.AddDays(90);
            credential.changed_by_user_code = changedBy > 0 ? changedBy : null;
            credential.modified_date = now;
            credential.failed_login_attempts = 0;
            credential.account_locked_until = null;
            _context.LegacyUserCredentials.Update(credential);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Password changed successfully for user {Username} by user_access_code {ChangedBy}",
                request.Username, changedBy);

            return Ok(new ChangePasswordResponse
            {
                Success = true,
                Message = "Password changed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {Username}", request.Username);
            return StatusCode(500, new ChangePasswordResponse
            {
                Success = false,
                Message = "Error changing password"
            });
        }
    }

    /// <summary>
    /// Change password and security question
    /// </summary>
    [HttpPost("change-password-question")]
    [Authorize]
    public async Task<ActionResult<ChangePasswordResponse>> ChangePasswordQuestion([FromBody] ChangePasswordQuestionRequest request)
    {
        try
        {
            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "New passwords do not match"
                });
            }

            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            if (string.IsNullOrWhiteSpace(request.SecurityQuestion) ||
                request.SecurityQuestion.Equals("Select Question...", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "Security question is required"
                });
            }

            if (string.IsNullOrWhiteSpace(request.SecurityAnswer))
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "Security answer is required"
                });
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode && c.is_active);
            if (credential == null)
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "No credential record found for this user"
                });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, credential.password_hash))
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "Current password is incorrect"
                });
            }

            var actorUserCode = GetActorUserCode();
            var now = DateTime.UtcNow;

            credential.password_hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            credential.last_password_change = now;
            credential.password_expiry_date = now.AddDays(90);
            credential.changed_by_user_code = actorUserCode > 0 ? actorUserCode : null;
            credential.modified_date = now;
            credential.failed_login_attempts = 0;
            credential.account_locked_until = null;
            credential.password_reset_token = SerializeSecurityQuestionPayload(
                request.SecurityQuestion.Trim(),
                HashSecurityAnswer(request.SecurityAnswer));
            credential.password_reset_token_expiry = null;

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (profile.User != null)
                {
                    profile.User.email = request.Email.Trim();
                    profile.User.date_updated = now;
                    profile.User.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                    _context.Users.Update(profile.User);
                }

                if (profile.UserAccessOld != null)
                {
                    profile.UserAccessOld.E_Mail = request.Email.Trim();
                    profile.UserAccessOld.date_updated = now;
                    profile.UserAccessOld.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                    _context.UserAccessOlds.Update(profile.UserAccessOld);
                }
            }

            _context.LegacyUserCredentials.Update(credential);
            await _context.SaveChangesAsync();

            return Ok(new ChangePasswordResponse
            {
                Success = true,
                Message = "Password and security question changed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password/question for user {Username}", request.Username);
            return StatusCode(500, new ChangePasswordResponse
            {
                Success = false,
                Message = "Error changing password and security question"
            });
        }
    }

    /// <summary>
    /// Reset user login attempts
    /// </summary>
    [HttpPost("reset-login")]
    [Authorize]
    public async Task<ActionResult<UserAdminResponse>> ResetLogin([FromBody] ResetLoginRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var now = DateTime.UtcNow;
            var actorUserCode = GetActorUserCode();

            if (profile.UserAccessOld != null)
            {
                profile.UserAccessOld.user_active = true;
                profile.UserAccessOld.last_log_on = now;
                profile.UserAccessOld.date_updated = now;
                profile.UserAccessOld.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode);
            if (credential != null)
            {
                credential.failed_login_attempts = 0;
                credential.account_locked_until = null;
                credential.is_active = true;
                credential.modified_date = now;
                _context.LegacyUserCredentials.Update(credential);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = "Login reset successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting login for user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error resetting login"
            });
        }
    }

    /// <summary>
    /// Force password change for user (admin function)
    /// </summary>
    [HttpPost("force-password")]
    [Authorize]
    public async Task<ActionResult<UserAdminResponse>> ForcePassword([FromBody] ForcePasswordRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "New password is required"
                });
            }

            var now = DateTime.UtcNow;
            var actorUserCode = GetActorUserCode();

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode);
            if (credential == null)
            {
                credential = new Core.Domain.Entities.Auth.LegacyUserCredential
                {
                    user_access_code = profile.UserAccessCode,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword),
                    password_salt = string.Empty,
                    last_password_change = now,
                    password_expiry_date = now.AddDays(90),
                    changed_by_user_code = actorUserCode > 0 ? actorUserCode : null,
                    created_date = now,
                    modified_date = now,
                    is_active = true,
                    failed_login_attempts = 0
                };
                _context.LegacyUserCredentials.Add(credential);
            }
            else
            {
                credential.password_hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                credential.last_password_change = now;
                credential.password_expiry_date = now.AddDays(90);
                credential.changed_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                credential.failed_login_attempts = 0;
                credential.account_locked_until = null;
                credential.is_active = true;
                credential.modified_date = now;

                var existingPayload = DeserializeSecurityQuestionPayload(credential.password_reset_token);
                if (existingPayload is null)
                {
                    credential.password_reset_token = SerializeSecurityQuestionPayload(
                        DefaultSecurityQuestion,
                        HashSecurityAnswer(profile.ResolvedUsername));
                    credential.password_reset_token_expiry = null;
                }

                _context.LegacyUserCredentials.Update(credential);
            }

            if (profile.UserAccessOld != null)
            {
                profile.UserAccessOld.user_active = true;
                profile.UserAccessOld.PWD_Expires = now.AddDays(90);
                profile.UserAccessOld.date_updated = now;
                profile.UserAccessOld.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = $"Password changed successfully for user {request.Username}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing password for user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error changing password"
            });
        }
    }

    /// <summary>
    /// Start forgot password flow - return security question
    /// </summary>
    [HttpPost("forgot-password/start")]
    [AllowAnonymous]
    public async Task<ActionResult<UserAdminResponse>> ForgotPasswordStart([FromBody] ForgotPasswordStartRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return Ok(new UserAdminResponse
                {
                    Success = false,
                    Message = $"A user for username {request.Username} could not be found."
                });
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode && c.is_active);
            if (credential == null)
            {
                return Ok(new UserAdminResponse
                {
                    Success = false,
                    Message = "No active credential record found for this user."
                });
            }

            var securityPayload = DeserializeSecurityQuestionPayload(credential.password_reset_token);
            if (securityPayload == null || string.IsNullOrWhiteSpace(securityPayload.Question))
            {
                return Ok(new UserAdminResponse
                {
                    Success = false,
                    Message = "Security question is not configured for this user."
                });
            }

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = "Security question retrieved",
                Question = securityPayload.Question
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting forgot password for user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error processing request"
            });
        }
    }

    /// <summary>
    /// Confirm forgot password - verify answer and generate new password
    /// </summary>
    [HttpPost("forgot-password/confirm")]
    [AllowAnonymous]
    public async Task<ActionResult<UserAdminResponse>> ForgotPasswordConfirm([FromBody] ForgotPasswordConfirmRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "Invalid request"
                });
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode && c.is_active);
            if (credential == null)
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "No active credential record found for this user."
                });
            }

            var securityPayload = DeserializeSecurityQuestionPayload(credential.password_reset_token);
            if (securityPayload == null || string.IsNullOrWhiteSpace(securityPayload.AnswerHash))
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "Security question/answer is not configured for this user."
                });
            }

            var providedAnswerHash = HashSecurityAnswer(request.Answer);
            if (!string.Equals(securityPayload.AnswerHash, providedAnswerHash, StringComparison.Ordinal))
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "Did you type the incorrect answer? Re-type a correct answer and try again."
                });
            }

            var newPassword = GenerateTemporaryPassword();
            var now = DateTime.UtcNow;
            credential.password_hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            credential.last_password_change = now;
            credential.password_expiry_date = now.AddDays(90);
            credential.failed_login_attempts = 0;
            credential.account_locked_until = null;
            credential.modified_date = now;
            _context.LegacyUserCredentials.Update(credential);

            if (profile.UserAccessOld != null)
            {
                profile.UserAccessOld.user_active = true;
                profile.UserAccessOld.PWD_Expires = now.AddDays(90);
                profile.UserAccessOld.date_updated = now;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = "Password reset successfully",
                NewPassword = newPassword
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming forgot password for user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error resetting password"
            });
        }
    }

    /// <summary>
    /// Activate a user account
    /// </summary>
    [HttpPost("activate-user")]
    [Authorize]
    public async Task<ActionResult<UserAdminResponse>> ActivateUser([FromBody] ActivateUserRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var now = DateTime.UtcNow;
            var actorUserCode = GetActorUserCode();

            if (profile.UserAccessOld != null)
            {
                profile.UserAccessOld.user_active = true;
                profile.UserAccessOld.last_log_on = now;
                profile.UserAccessOld.date_updated = now;
                profile.UserAccessOld.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode);
            if (credential != null)
            {
                credential.is_active = true;
                credential.failed_login_attempts = 0;
                credential.account_locked_until = null;
                credential.modified_date = now;
                _context.LegacyUserCredentials.Update(credential);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = $"User {request.Username} activated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error activating user"
            });
        }
    }

    /// <summary>
    /// Deactivate user with expired password
    /// </summary>
    [HttpPost("deactivate-user")]
    [Authorize]
    public async Task<ActionResult<UserAdminResponse>> DeactivateUser([FromBody] DeactivateExpiredPasswordRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var now = DateTime.UtcNow;
            var actorUserCode = GetActorUserCode();

            if (profile.UserAccessOld != null)
            {
                profile.UserAccessOld.user_active = false;
                profile.UserAccessOld.date_updated = now;
                profile.UserAccessOld.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode);
            if (credential != null)
            {
                credential.is_active = false;
                credential.modified_date = now;
                _context.LegacyUserCredentials.Update(credential);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = $"User {request.Username} deactivated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error deactivating user"
            });
        }
    }

    /// <summary>
    /// Deactivate user with expired password
    /// </summary>
    [HttpPost("deactivate-expired")]
    [Authorize]
    public async Task<ActionResult<UserAdminResponse>> DeactivateExpiredPassword([FromBody] DeactivateExpiredPasswordRequest request)
    {
        try
        {
            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile is null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == profile.UserAccessCode);
            var baselineDate = profile.UserAccessOld?.last_log_on
                ?? credential?.last_password_change
                ?? DateTime.UtcNow;

            if (baselineDate.AddDays(30) >= DateTime.UtcNow)
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = $"The user account for {request.Username} cannot be deactivated. The password is still valid."
                });
            }

            var now = DateTime.UtcNow;
            var actorUserCode = GetActorUserCode();

            if (profile.UserAccessOld != null)
            {
                profile.UserAccessOld.user_active = false;
                profile.UserAccessOld.date_updated = now;
                profile.UserAccessOld.modified_by_user_code = actorUserCode > 0 ? actorUserCode : null;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            if (credential != null)
            {
                credential.is_active = false;
                credential.modified_date = now;
                _context.LegacyUserCredentials.Update(credential);
            }

            await _context.SaveChangesAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = $"User {request.Username} deactivated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {Username}", request.Username);
            return StatusCode(500, new UserAdminResponse
            {
                Success = false,
                Message = "Error deactivating user"
            });
        }
    }

    #region Private Methods

    private const string DefaultSecurityQuestion = "What is your username?";

    private async Task<ResolvedUserProfile?> ResolveUserProfileAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        username = username.Trim();
        var normalized = username.ToLower();

        UserAccessOld? userAccessOld = null;
        User? user = null;

        if (short.TryParse(username, out var code) && code > 0)
        {
            userAccessOld = await _context.UserAccessOlds
                .FirstOrDefaultAsync(u => u.user_access_code == code && !u.is_deleted);
            user = await _context.Users
                .FirstOrDefaultAsync(u => u.user_access_code == code && !u.is_deleted);
        }
        else
        {
            userAccessOld = await _context.UserAccessOlds
                .FirstOrDefaultAsync(u => !u.is_deleted && (
                    (u.name != null && u.name.ToLower() == normalized) ||
                    (u.E_Mail != null && u.E_Mail.ToLower() == normalized)));

            if (userAccessOld != null)
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.user_access_code == userAccessOld.user_access_code && !u.is_deleted);
            }
            else
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => !u.is_deleted && u.email != null && u.email.ToLower() == normalized);

                if (user != null)
                {
                    userAccessOld = await _context.UserAccessOlds
                        .FirstOrDefaultAsync(u => u.user_access_code == user.user_access_code && !u.is_deleted);
                }
            }
        }

        if (userAccessOld == null && user == null)
        {
            return null;
        }

        var userAccessCode = userAccessOld?.user_access_code ?? (short)(user!.user_access_code);
        var resolvedUsername = userAccessOld?.name
            ?? user?.email
            ?? username;

        return new ResolvedUserProfile((int)userAccessCode, resolvedUsername, userAccessOld, user);
    }

    private int GetActorUserCode()
    {
        var actorRaw = User.FindFirst("user_access_code")?.Value;
        return int.TryParse(actorRaw, out var actorUserCode) ? actorUserCode : 0;
    }

    private static string HashSecurityAnswer(string answer)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(answer ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private static string SerializeSecurityQuestionPayload(string question, string answerHash)
    {
        return JsonSerializer.Serialize(new SecurityQuestionPayload
        {
            Question = question,
            AnswerHash = answerHash
        });
    }

    private static SecurityQuestionPayload? DeserializeSecurityQuestionPayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SecurityQuestionPayload>(raw);
        }
        catch
        {
            return null;
        }
    }

    private static readonly string RefreshTokenCookieName = "FIS_Refresh_Token";

    private static List<Claim> BuildAuthClaims(int userAccessCode, string email, bool passwordExpired = false)
    {
        return
        [
            new Claim("user_access_code", userAccessCode.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email),
            new Claim("password_change_required", passwordExpired.ToString().ToLowerInvariant())
        ];
    }

    private void WriteAuthCookies(
        string accessToken,
        DateTimeOffset accessExpiresAt,
        string refreshToken,
        DateTimeOffset refreshExpiresAt)
    {
        var isHttps = Request.IsHttps;

        Response.Cookies.Append(
            SessionCookieAuthenticationHandler.AccessCookieName,
            accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = SameSiteMode.Lax,
                Expires = accessExpiresAt,
                Path = "/",
                IsEssential = true
            });

        Response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = SameSiteMode.Lax,
                Expires = refreshExpiresAt,
                Path = "/",
                IsEssential = true
            });
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private sealed record ResolvedUserProfile(int UserAccessCode, string ResolvedUsername, UserAccessOld? UserAccessOld, User? User);
    private sealed class SecurityQuestionPayload
    {
        public string Question { get; set; } = string.Empty;
        public string AnswerHash { get; set; } = string.Empty;
    }

    #endregion
}

/// <summary>
/// Login request payload
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Username (can be user_access_code or email)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Refresh token request payload
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Current JWT token to refresh
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Login response with JWT token
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// JWT Bearer token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration timestamp
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// User access code from database
    /// </summary>
    public int UserAccessCode { get; set; }

    /// <summary>
    /// User email address
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Optional message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// True when the user's password has expired and must be changed before using the system.
    /// The returned token includes a password_change_required claim for the frontend to enforce redirection.
    /// </summary>
    public bool PasswordExpired { get; set; } = false;

    /// <summary>
    /// Days remaining before the password expires (0 if already expired).
    /// </summary>
    public int PasswordExpiresIn { get; set; }
}

/// <summary>
/// Change password request
/// </summary>
public class ChangePasswordRequest
{
    public string Username { get; set; } = string.Empty;
    /// <summary>Current (old) password — required to verify identity before changing.</summary>
    public string CurrentPassword { get; set; } = string.Empty;
    /// <summary>Kept for backwards compat — maps to CurrentPassword if provided.</summary>
    public string OldPassword
    {
        get => CurrentPassword;
        set { if (!string.IsNullOrEmpty(value)) CurrentPassword = value; }
    }
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Change password and security question request
/// </summary>
public class ChangePasswordQuestionRequest
{
    public string Username { get; set; } = string.Empty;
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
    public string SecurityQuestion { get; set; } = string.Empty;
    public string SecurityAnswer { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Change password response
/// </summary>
public class ChangePasswordResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Reset login request
/// </summary>
public class ResetLoginRequest
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Force password request
/// </summary>
public class ForcePasswordRequest
{
    public string Username { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Forgot password start request
/// </summary>
public class ForgotPasswordStartRequest
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Forgot password confirm request
/// </summary>
public class ForgotPasswordConfirmRequest
{
    public string Username { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

/// <summary>
/// Activate user request
/// </summary>
public class ActivateUserRequest
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Deactivate expired password request
/// </summary>
public class DeactivateExpiredPasswordRequest
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// User admin response
/// </summary>
public class UserAdminResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Question { get; set; }
    public string? NewPassword { get; set; }
}
