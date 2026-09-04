using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Auth;
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
    private readonly FisDbContext _context;
    private readonly ILogger<AuthController> _logger;
    private readonly ISessionTokenStore _sessionTokenStore;
    private readonly IEmailNotificationService _emailNotificationService;
    private readonly IPasswordService _passwordService;
    private readonly LegacyCredentialCompatibilityService _legacyCredentialCompatibility;

    public AuthController(
        IConfiguration configuration,
        FisDbContext context,
        ILogger<AuthController> logger,
        ISessionTokenStore sessionTokenStore,
        IEmailNotificationService emailNotificationService,
        IPasswordService passwordService,
        LegacyCredentialCompatibilityService legacyCredentialCompatibility)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _sessionTokenStore = sessionTokenStore;
        _emailNotificationService = emailNotificationService;
        _passwordService = passwordService;
        _legacyCredentialCompatibility = legacyCredentialCompatibility;
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

        // Resolve the legacy profile first. The expanded TS_Users/credential tables
        // are optional during rollout, so login must not require them to exist.
        var profile = await ResolveUserProfileAsync(request.Username);
        if (profile == null)
        {
            _logger.LogWarning("Login failed: user not found for {Username}", request.Username);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
        var credential = modernCredentialLookup.Credential;

        if (credential is not null && !credential.is_active)
        {
            _logger.LogWarning("Login failed: inactive credential for user {UserAccessCode}", profile.UserAccessCode);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        if (profile.UserAccessOld is { user_active: false })
        {
            _logger.LogWarning("Login failed: inactive legacy profile for user {UserAccessCode}", profile.UserAccessCode);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        var usingModernPassword = !string.IsNullOrWhiteSpace(credential?.password_hash);
        var storedPassword = usingModernPassword ? credential!.password_hash : profile.UserAccessOld?.password;
        if (string.IsNullOrWhiteSpace(storedPassword))
        {
            _logger.LogWarning("Login failed: no credential for user {UserAccessCode}", profile.UserAccessCode);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        // Account lockout check
        if (credential?.account_locked_until.HasValue == true && credential.account_locked_until.Value > DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Account is temporarily locked. Please try again later." });
        }

        // Verify password
        var passwordValid = VerifyStoredPassword(
            request.Password,
            storedPassword,
            allowLegacyPlaintext: !usingModernPassword);
        if (!passwordValid)
        {
            if (credential is not null)
            {
                credential.failed_login_attempts++;
                if (credential.failed_login_attempts >= 5)
                    credential.account_locked_until = DateTime.UtcNow.AddMinutes(30);
                _context.LegacyUserCredentials.Update(credential);
            }

            if (profile.UserAccessOld is not null)
            {
                profile.UserAccessOld.Retry = (short)Math.Min(short.MaxValue, (profile.UserAccessOld.Retry ?? 0) + 1);
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            await _context.SaveChangesAsync();
            _logger.LogWarning("Login failed: wrong password for user {UserAccessCode}", profile.UserAccessCode);
            return Unauthorized(new { error = "Invalid username or password" });
        }

        // Successful login — reset failed attempts
        if (credential is not null)
        {
            credential.failed_login_attempts = 0;
            credential.account_locked_until = null;
            _context.LegacyUserCredentials.Update(credential);
        }

        if (profile.UserAccessOld is not null)
        {
            profile.UserAccessOld.Retry = 0;
            profile.UserAccessOld.last_log_on = DateTime.UtcNow;
            profile.UserAccessOld.date_updated = DateTime.UtcNow;
            _context.UserAccessOlds.Update(profile.UserAccessOld);
        }

        await _context.SaveChangesAsync();

        // Password expiry check — disabled by default for legacy data compatibility.
        // Set JwtSettings:EnforcePasswordExpiry=true in config once real expiry data is in place.
        bool passwordExpired = false;
        int daysRemaining = int.MaxValue;
        var enforceExpiry = bool.TryParse(_configuration["JwtSettings:EnforcePasswordExpiry"], out var enforceFlag) && enforceFlag;

        if (enforceExpiry)
        {
            var fallbackExpiryDays = int.Parse(_configuration["JwtSettings:PasswordExpiryDays"] ?? "90");

            if (credential?.password_expiry_date.HasValue == true && credential.password_expiry_date.Value > new DateTime(2000, 1, 1))
            {
                passwordExpired = DateTime.UtcNow > credential.password_expiry_date.Value;
                daysRemaining = Math.Max(0, (int)(credential.password_expiry_date.Value - DateTime.UtcNow).TotalDays);
            }
            else if (profile.UserAccessOld?.PWD_Expires.HasValue == true && profile.UserAccessOld.PWD_Expires.Value > new DateTime(2000, 1, 1))
            {
                passwordExpired = DateTime.UtcNow > profile.UserAccessOld.PWD_Expires.Value;
                daysRemaining = Math.Max(0, (int)(profile.UserAccessOld.PWD_Expires.Value - DateTime.UtcNow).TotalDays);
            }
            else if (credential?.last_password_change > new DateTime(2000, 1, 1))
            {
                var passwordAge = (DateTime.UtcNow - credential.last_password_change).TotalDays;
                passwordExpired = passwordAge > fallbackExpiryDays;
                daysRemaining = Math.Max(0, (int)(fallbackExpiryDays - passwordAge));
            }
            // else: no usable timestamp on the credential row — treat as "not expired" rather than force-expire legacy users
        }

        var accessLevel = profile.UserAccessOld?.AccessLevel ?? 0L;

        var grantedRoles = LegacyRoleMap.RolesForAccessLevel(accessLevel).ToArray();
        _logger.LogInformation(
            "Login: user_access_code={UserAccessCode} access_level={AccessLevel} roles={Roles}",
            profile.UserAccessCode, accessLevel, string.Join(",", grantedRoles));

        var email = profile.User?.email ?? profile.UserAccessOld?.E_Mail ?? request.Username.Trim();
        var authClaims = BuildAuthClaims(profile.UserAccessCode, email, accessLevel, passwordExpired);
        var tokens = _sessionTokenStore.IssueTokens(authClaims);

        WriteAuthCookies(tokens.AccessToken, tokens.AccessExpiresAt, tokens.RefreshToken, tokens.RefreshExpiresAt);

        return Ok(new LoginResponse
        {
            Token = tokens.AccessToken,
            ExpiresAt = tokens.AccessExpiresAt.UtcDateTime,
            UserAccessCode = profile.UserAccessCode,
            Email = profile.User?.email ?? profile.UserAccessOld?.E_Mail,
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
        var claims = User.Claims
            .Select(c => new { type = c.Type, value = c.Value })
            .ToArray();

        return Ok(new
        {
            valid = true,
            authType = User.Identity?.AuthenticationType,
            claims
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

            var profile = await ResolveUserProfileAsync(request.Username);
            if (profile == null)
            {
                return NotFound(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
            var usingModernPassword = !string.IsNullOrWhiteSpace(credential?.password_hash);
            var storedPassword = usingModernPassword ? credential!.password_hash : profile.UserAccessOld?.password;

            if (credential is not null && !credential.is_active)
                return BadRequest(new ChangePasswordResponse { Success = false, Message = "No active credential record found for this user" });

            if (string.IsNullOrWhiteSpace(storedPassword))
                return BadRequest(new ChangePasswordResponse { Success = false, Message = "No credential record found for this user" });

            if (!VerifyStoredPassword(request.CurrentPassword, storedPassword, allowLegacyPlaintext: !usingModernPassword))
                return BadRequest(new ChangePasswordResponse { Success = false, Message = "Current password is incorrect" });

            var changedByRaw = User.FindFirst("user_access_code")?.Value;
            int.TryParse(changedByRaw, out int changedBy);
            var now = DateTime.UtcNow;

            if (credential is not null)
            {
                credential.password_hash = _passwordService.HashPassword(request.NewPassword);
                credential.last_password_change = now;
                credential.password_expiry_date = now.AddDays(90);
                credential.changed_by_user_code = changedBy > 0 ? changedBy : null;
                credential.modified_date = now;
                credential.failed_login_attempts = 0;
                credential.account_locked_until = null;
                _context.LegacyUserCredentials.Update(credential);
            }

            ApplyLegacyPasswordChange(profile.UserAccessOld, request.NewPassword, now);
            await SaveChangesWithOptionalCredentialAsync(credential);

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

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
            var usingModernPassword = !string.IsNullOrWhiteSpace(credential?.password_hash);
            var storedPassword = usingModernPassword ? credential!.password_hash : profile.UserAccessOld?.password;
            if (credential is not null && !credential.is_active)
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "No active credential record found for this user"
                });
            }

            if (string.IsNullOrWhiteSpace(storedPassword))
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "No credential record found for this user"
                });
            }

            if (!VerifyStoredPassword(request.OldPassword, storedPassword, allowLegacyPlaintext: !usingModernPassword))
            {
                return BadRequest(new ChangePasswordResponse
                {
                    Success = false,
                    Message = "Current password is incorrect"
                });
            }

            var actorUserCode = GetActorUserCode();
            var now = DateTime.UtcNow;

            if (credential is not null)
            {
                credential.password_hash = _passwordService.HashPassword(request.NewPassword);
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
                _context.LegacyUserCredentials.Update(credential);
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                if (profile.User != null)
                {
                    profile.User.email = request.Email.Trim();
                    _context.Users.Update(profile.User);
                }

                if (profile.UserAccessOld != null)
                {
                    profile.UserAccessOld.E_Mail = request.Email.Trim();
                    profile.UserAccessOld.date_updated = now;
                    _context.UserAccessOlds.Update(profile.UserAccessOld);
                }
            }

            ApplyLegacyPasswordChange(profile.UserAccessOld, request.NewPassword, now);
            await SaveChangesWithOptionalCredentialAsync(credential);

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
                profile.UserAccessOld.Retry = 0;
                profile.UserAccessOld.last_log_on = now;
                profile.UserAccessOld.date_updated = now;
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
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

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
            if (credential == null && modernCredentialLookup.StoreAvailable && profile.User != null)
            {
                credential = new Core.Domain.Entities.Auth.LegacyUserCredential
                {
                    user_access_code = profile.UserAccessCode,
                    password_hash = _passwordService.HashPassword(request.NewPassword),
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
                if (credential != null)
                {
                    credential.password_hash = _passwordService.HashPassword(request.NewPassword);
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
            }

            ApplyLegacyPasswordChange(profile.UserAccessOld, request.NewPassword, now);

            await SaveChangesWithOptionalCredentialAsync(credential);

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
    /// Start the email-based forgot password flow.
    /// </summary>
    [HttpPost("forgot-password/start")]
    [AllowAnonymous]
    public async Task<ActionResult<UserAdminResponse>> ForgotPasswordStart([FromBody] ForgotPasswordStartRequest request)
    {
        const string genericMessage = "If an account with a registered email address exists, a password reset link has been sent.";

        try
        {
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return Ok(new UserAdminResponse { Success = true, Message = genericMessage });
            }

            var profile = await ResolveUserProfileAsync(request.Username);
            var emailAddress = profile is null ? null : ResolveProfileEmail(profile);
            if (profile is null || string.IsNullOrWhiteSpace(emailAddress))
            {
                return Ok(new UserAdminResponse { Success = true, Message = genericMessage });
            }

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
            if (!modernCredentialLookup.StoreAvailable)
            {
                _logger.LogInformation(
                    "Legacy_User_Credentials is not present; using the legacy user_access_old1 password reset path for user_access_code {UserAccessCode}",
                    profile.UserAccessCode);
            }

            if (!TryGetPasswordResetBaseUrl(out var passwordResetBaseUrl))
            {
                _logger.LogError("Password reset base URL is not configured.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new UserAdminResponse
                {
                    Success = false,
                    Message = "Password reset is temporarily unavailable. Please contact support."
                });
            }

            var tokenExpiry = DateTime.UtcNow.Add(GetPasswordResetTokenLifetime());
            string rawToken;
            if (credential == null)
            {
                if (!TryCreateLegacyPasswordResetToken(
                        profile.UserAccessCode,
                        DateTime.UtcNow,
                        tokenExpiry,
                        out rawToken))
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new UserAdminResponse
                    {
                        Success = false,
                        Message = "Password reset is temporarily unavailable. Please contact support."
                    });
                }
            }
            else
            {
                rawToken = GeneratePasswordResetToken();
                credential.password_reset_token = HashPasswordResetToken(rawToken);
                credential.password_reset_token_expiry = tokenExpiry;
                credential.modified_date = DateTime.UtcNow;
                _context.LegacyUserCredentials.Update(credential);
                await SaveChangesWithOptionalCredentialAsync(modernCredentialLookup.Credential);
            }

            var resetUrl = $"{passwordResetBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
            var displayName = ResolveProfileDisplayName(profile);
            var htmlBody = $"""
                <h2>Password reset request</h2>
                <p>Hello {WebUtility.HtmlEncode(displayName)},</p>
                <p>Someone requested a password reset for your Fleet Information System account.</p>
                <p><a href="{WebUtility.HtmlEncode(resetUrl)}">Reset your password</a></p>
                <p>This link expires in {GetPasswordResetTokenLifetime().TotalMinutes:0} minutes and can be used only once.</p>
                <p>If you did not request this, you can safely ignore this email.</p>
                <p>Fleet Information System</p>
                """;

            var emailSent = await _emailNotificationService.SendHtmlEmailAsync(emailAddress, "Fleet Information System password reset", htmlBody);
            if (!emailSent)
            {
                if (credential != null)
                {
                    credential.password_reset_token = null;
                    credential.password_reset_token_expiry = null;
                    credential.modified_date = DateTime.UtcNow;
                    _context.LegacyUserCredentials.Update(credential);
                    await _context.SaveChangesAsync();
                }

                _logger.LogError("Password reset email could not be sent for user_access_code {UserAccessCode}", profile.UserAccessCode);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new UserAdminResponse
                {
                    Success = false,
                    Message = "Password reset is temporarily unavailable. Please contact support."
                });
            }

            _logger.LogInformation(
                "Password reset email sent for user_access_code {UserAccessCode} using {ResetStorage}",
                profile.UserAccessCode,
                credential == null ? "legacy user_access_old1 password" : "Legacy_User_Credentials");
            return Ok(new UserAdminResponse { Success = true, Message = genericMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting forgot password flow");
            return StatusCode(StatusCodes.Status500InternalServerError, new UserAdminResponse
            {
                Success = false,
                Message = "Password reset is temporarily unavailable. Please contact support."
            });
        }
    }

    /// <summary>
    /// Complete the email-based forgot password flow.
    /// </summary>
    [HttpPost("forgot-password/confirm")]
    [AllowAnonymous]
    public async Task<ActionResult<UserAdminResponse>> ForgotPasswordConfirm([FromBody] ForgotPasswordConfirmRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "The password reset link is invalid or has expired."
                });
            }

            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "New passwords do not match."
                });
            }

            if (!_passwordService.IsPasswordStrong(request.NewPassword, out var passwordError))
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = passwordError
                });
            }

            var now = DateTime.UtcNow;

            if (TryReadLegacyPasswordResetToken(
                    request.Token,
                    out var legacyUserAccessCode,
                    out var legacyIssuedAtUtc,
                    out var legacyTokenExpiryUtc))
            {
                if (legacyTokenExpiryUtc <= now)
                {
                    return BadRequest(new UserAdminResponse
                    {
                        Success = false,
                        Message = "The password reset link is invalid or has expired."
                    });
                }

                var legacyUser = await _context.UserAccessOlds
                    .FirstOrDefaultAsync(u => u.user_access_code == legacyUserAccessCode);
                if (legacyUser == null
                    || (legacyUser.date_updated.HasValue
                        && legacyUser.date_updated.Value >= legacyIssuedAtUtc))
                {
                    return BadRequest(new UserAdminResponse
                    {
                        Success = false,
                        Message = "The password reset link is invalid or has expired."
                    });
                }

                ApplyLegacyPasswordChange(legacyUser, request.NewPassword, now);

                var modernCredentialLookup = await TryGetModernCredentialAsync(legacyUserAccessCode);
                if (modernCredentialLookup.Credential is not null)
                {
                    modernCredentialLookup.Credential.password_hash = _passwordService.HashPassword(request.NewPassword);
                    modernCredentialLookup.Credential.last_password_change = now;
                    modernCredentialLookup.Credential.password_expiry_date = now.AddDays(90);
                    modernCredentialLookup.Credential.failed_login_attempts = 0;
                    modernCredentialLookup.Credential.account_locked_until = null;
                    modernCredentialLookup.Credential.password_reset_token = null;
                    modernCredentialLookup.Credential.password_reset_token_expiry = null;
                    modernCredentialLookup.Credential.is_active = true;
                    modernCredentialLookup.Credential.modified_date = now;
                    _context.LegacyUserCredentials.Update(modernCredentialLookup.Credential);
                }
                await SaveChangesWithOptionalCredentialAsync(modernCredentialLookup.Credential);

                return Ok(new UserAdminResponse
                {
                    Success = true,
                    Message = "Password reset successfully. You can now sign in with your new password."
                });
            }

            var tokenHash = HashPasswordResetToken(request.Token);
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            LegacyUserCredential? credential;
            try
            {
                credential = await _context.LegacyUserCredentials
                    .FirstOrDefaultAsync(c => c.is_active
                        && c.password_reset_token == tokenHash
                        && c.password_reset_token_expiry.HasValue
                        && c.password_reset_token_expiry.Value > now);
            }
            catch (SqlException ex) when (IsMissingSchemaObject(ex))
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "The password reset link is invalid or has expired."
                });
            }
            if (credential == null)
            {
                return BadRequest(new UserAdminResponse
                {
                    Success = false,
                    Message = "The password reset link is invalid or has expired."
                });
            }

            await _legacyCredentialCompatibility.HydrateAsync(credential);

            credential.password_hash = _passwordService.HashPassword(request.NewPassword);
            credential.last_password_change = now;
            credential.password_expiry_date = now.AddDays(90);
            credential.failed_login_attempts = 0;
            credential.account_locked_until = null;
            credential.modified_date = now;
            credential.password_reset_token = null;
            credential.password_reset_token_expiry = null;
            credential.is_active = true;
            _context.LegacyUserCredentials.Update(credential);

            var userAccessOld = await _context.UserAccessOlds
                .FirstOrDefaultAsync(u => u.user_access_code == credential.user_access_code);
            if (userAccessOld != null)
            {
                ApplyLegacyPasswordChange(userAccessOld, request.NewPassword, now);
            }

            await SaveChangesWithOptionalCredentialAsync(credential);
            await transaction.CommitAsync();

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = "Password reset successfully. You can now sign in with your new password."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing forgot password flow");
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
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
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
                _context.UserAccessOlds.Update(profile.UserAccessOld);
            }

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
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

            var modernCredentialLookup = await TryGetModernCredentialAsync(profile.UserAccessCode);
            var credential = modernCredentialLookup.Credential;
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

    private async Task<ModernCredentialLookup> TryGetModernCredentialAsync(int userAccessCode)
    {
        try
        {
            var credential = await _context.LegacyUserCredentials
                .FirstOrDefaultAsync(c => c.user_access_code == userAccessCode);

            if (credential is not null)
            {
                await _legacyCredentialCompatibility.HydrateAsync(credential);
            }

            return new ModernCredentialLookup(true, credential);
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            _logger.LogInformation(
                "Expanded credential store is unavailable; using legacy user_access_old1 for user_access_code {UserAccessCode}",
                userAccessCode);
            return new ModernCredentialLookup(false, null);
        }
    }

    private async Task SaveChangesWithOptionalCredentialAsync(LegacyUserCredential? credential)
    {
        await _context.SaveChangesAsync();
        if (credential is not null)
        {
            await _legacyCredentialCompatibility.PersistAsync(credential);
        }
    }

    private async Task<User?> TryGetModernUserAsync(int userAccessCode)
    {
        try
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.user_access_code == userAccessCode);
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            return null;
        }
    }

    private async Task<User?> TryGetModernUserByEmailAsync(string email)
    {
        try
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.email != null && u.email.ToLower() == email);
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            return null;
        }
    }

    private async Task<UserAccessOld?> TryGetLegacyProfileByCodeAsync(short userAccessCode)
    {
        try
        {
            return await _context.UserAccessOlds
                .FirstOrDefaultAsync(u => u.user_access_code == userAccessCode);
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            return null;
        }
    }

    private async Task<UserAccessOld?> TryGetLegacyProfileByIdentifierAsync(string normalizedIdentifier)
    {
        try
        {
            return await _context.UserAccessOlds
                .FirstOrDefaultAsync(u => (
                    (u.name != null && u.name.ToLower() == normalizedIdentifier) ||
                    (u.E_Mail != null && u.E_Mail.ToLower() == normalizedIdentifier) ||
                    (u.FirstName != null && u.FirstName.ToLower() == normalizedIdentifier)));
        }
        catch (SqlException ex) when (IsMissingSchemaObject(ex))
        {
            return null;
        }
    }

    private bool VerifyStoredPassword(string password, string storedHash, bool allowLegacyPlaintext)
    {
        var normalizedHash = storedHash.Trim();
        if (LooksLikeBcryptHash(normalizedHash))
        {
            return _passwordService.VerifyPassword(password, normalizedHash);
        }

        if (LooksLikeLegacyMd5Hash(normalizedHash))
        {
            var suppliedHash = HashLegacyPassword(password);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedHash),
                Encoding.UTF8.GetBytes(normalizedHash.ToUpperInvariant()));
        }

        return allowLegacyPlaintext
            && string.Equals(storedHash.TrimEnd(), password, StringComparison.Ordinal);
    }

    private static bool LooksLikeBcryptHash(string value)
    {
        return value.StartsWith("$2a$", StringComparison.Ordinal)
            || value.StartsWith("$2b$", StringComparison.Ordinal)
            || value.StartsWith("$2y$", StringComparison.Ordinal);
    }

    private static bool LooksLikeLegacyMd5Hash(string value)
    {
        return value.Length == 32 && value.All(Uri.IsHexDigit);
    }

    private static string HashLegacyPassword(string password)
    {
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));
    }

    private void ApplyLegacyPasswordChange(UserAccessOld? legacyUser, string newPassword, DateTime now)
    {
        if (legacyUser is null)
        {
            return;
        }

        // user_access_old1.password is char(32) and the legacy application
        // verifies the uppercase MD5 representation. It cannot store BCrypt.
        legacyUser.password = HashLegacyPassword(newPassword);
        legacyUser.user_active = true;
        legacyUser.Retry = 0;
        legacyUser.PWD_Expires = now.AddDays(90);
        legacyUser.date_updated = now;
        _context.UserAccessOlds.Update(legacyUser);
    }

    private static bool IsMissingSchemaObject(SqlException exception)
    {
        return exception.Number is 207 or 208;
    }

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
            userAccessOld = await TryGetLegacyProfileByCodeAsync(code);
            user = await TryGetModernUserAsync(code);
        }
        else
        {
            userAccessOld = await TryGetLegacyProfileByIdentifierAsync(normalized);

            if (userAccessOld != null)
            {
                user = await TryGetModernUserAsync(userAccessOld.user_access_code);
            }
            else
            {
                user = await TryGetModernUserByEmailAsync(normalized);

                if (user != null)
                {
                    userAccessOld = await TryGetLegacyProfileByCodeAsync((short)user.user_access_code);
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

    private static string GeneratePasswordResetToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashPasswordResetToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private bool TryCreateLegacyPasswordResetToken(
        int userAccessCode,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        out string token)
    {
        token = string.Empty;
        var signingKey = GetPasswordResetSigningKey();
        if (signingKey == null)
        {
            _logger.LogError("Cannot create a legacy password reset token because JwtSettings:SecretKey is not configured.");
            return false;
        }

        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"FIS-PRT\"}"));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new LegacyPasswordResetTokenPayload
        {
            Purpose = "password-reset",
            UserAccessCode = userAccessCode,
            IssuedAtUnixSeconds = new DateTimeOffset(issuedAtUtc).ToUnixTimeSeconds(),
            ExpiresAtUnixSeconds = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds()
        }));
        var signingInput = $"{header}.{payload}";
        var signature = HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(signingInput));
        token = $"{signingInput}.{Base64UrlEncode(signature)}";
        return true;
    }

    private bool TryReadLegacyPasswordResetToken(
        string token,
        out int userAccessCode,
        out DateTime issuedAtUtc,
        out DateTime expiresAtUtc)
    {
        userAccessCode = 0;
        issuedAtUtc = default;
        expiresAtUtc = default;

        var signingKey = GetPasswordResetSigningKey();
        if (signingKey == null)
        {
            return false;
        }

        var parts = token.Split('.', StringSplitOptions.None);
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        try
        {
            var signingInput = $"{parts[0]}.{parts[1]}";
            var expectedSignature = HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(signingInput));
            var suppliedSignature = Base64UrlDecode(parts[2]);
            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature))
            {
                return false;
            }

            var payload = JsonSerializer.Deserialize<LegacyPasswordResetTokenPayload>(Base64UrlDecode(parts[1]));
            if (payload == null
                || !string.Equals(payload.Purpose, "password-reset", StringComparison.Ordinal)
                || payload.UserAccessCode <= 0
                || payload.IssuedAtUnixSeconds <= 0
                || payload.ExpiresAtUnixSeconds <= payload.IssuedAtUnixSeconds)
            {
                return false;
            }

            userAccessCode = payload.UserAccessCode;
            issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAtUnixSeconds).UtcDateTime;
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAtUnixSeconds).UtcDateTime;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private byte[]? GetPasswordResetSigningKey()
    {
        var secretKey = _configuration["JwtSettings:SecretKey"]?.Trim();
        return string.IsNullOrWhiteSpace(secretKey)
            ? null
            : Encoding.UTF8.GetBytes(secretKey);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }

    private TimeSpan GetPasswordResetTokenLifetime()
    {
        var configuredMinutes = _configuration.GetValue<int?>("EmailSettings:PasswordResetTokenLifetimeMinutes") ?? 30;
        return TimeSpan.FromMinutes(Math.Clamp(configuredMinutes, 5, 1440));
    }

    private bool TryGetPasswordResetBaseUrl(out string baseUrl)
    {
        baseUrl = (_configuration["EmailSettings:PasswordResetBaseUrl"] ?? string.Empty).TrimEnd('/');
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrWhiteSpace(uri.Query)
            && string.IsNullOrWhiteSpace(uri.Fragment);
    }

    private static string? ResolveProfileEmail(ResolvedUserProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.User?.email)
            ? profile.User.email.Trim()
            : profile.UserAccessOld?.E_Mail?.Trim();
    }

    private static string ResolveProfileDisplayName(ResolvedUserProfile profile)
    {
        var fullName = string.Join(
            " ",
            new[] { profile.UserAccessOld?.FirstName, profile.UserAccessOld?.LastName }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(fullName) ? profile.ResolvedUsername : fullName;
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

    internal static List<Claim> BuildAuthClaims(int userAccessCode, string email, long accessLevel = 0, bool passwordExpired = false)
    {
        var claims = new List<Claim>
        {
            new Claim("user_access_code", userAccessCode.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email),
            new Claim("password_change_required", passwordExpired.ToString().ToLowerInvariant()),
            new Claim("access_level", accessLevel.ToString())
        };

        // Derive named legacy role claims from the bitmask so pages using IsInRole() work
        foreach (var role in LegacyRoleMap.RolesForAccessLevel(accessLevel))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return claims;
    }

    // Permission bits (must mirror FIS.Web.Services.LegacyPermissionBits)
    private const long BitVehicleManagement = 1;
    private const long BitContractManagement = 2;
    private const long BitUserAdministration = 4;
    private const long BitReports = 8;
    private const long BitFinancial = 16;
    private const long BitWorkshop = 32;

    internal static class LegacyRoleMap
    {
        // role name (as checked by pages) -> required permission bit
        private static readonly (string Role, long Bit)[] Map =
        [
            ("Vehicle Master",          BitVehicleManagement),
            ("Asset Verification",      BitVehicleManagement),
            ("Accidents",               BitVehicleManagement),
            ("Call Centre",             BitVehicleManagement),
            ("Fines",                   BitVehicleManagement),
            ("Licence",                 BitVehicleManagement),
            ("Losses",                  BitVehicleManagement),
            ("Tracking",                BitVehicleManagement),
            ("Towing",                  BitVehicleManagement),
            ("Trip Authorities",        BitVehicleManagement),
            ("Private Hire Vehicles",   BitVehicleManagement),
            ("Taxis",                   BitVehicleManagement),
            ("Clearance",               BitVehicleManagement),
            ("Contracts",               BitContractManagement),
            ("User Administration",     BitUserAdministration),
            ("Reports",                 BitReports),
            ("Management Reports",      BitReports),
            ("Logbooks",                BitReports),
            ("Logsheets",               BitReports),
            ("Monitor",                 BitReports),
            ("Fuelcards",               BitFinancial),
            ("Auction",                 BitFinancial),
            ("Financial Data (All Departments)", BitFinancial),
            ("Financial Data (Own Department)",  BitFinancial),
            ("Financial Reports",       BitFinancial),
            ("Workshop",                BitWorkshop),
            ("Trouble Shooting",        BitWorkshop),
        ];

        public static IEnumerable<string> RolesForAccessLevel(long accessLevel)
        {
            if (accessLevel <= 0) return Array.Empty<string>();
            return Map.Where(m => (accessLevel & m.Bit) == m.Bit).Select(m => m.Role);
        }
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

    private sealed record ModernCredentialLookup(bool StoreAvailable, LegacyUserCredential? Credential);
    private sealed record ResolvedUserProfile(int UserAccessCode, string ResolvedUsername, UserAccessOld? UserAccessOld, User? User);
    private sealed class SecurityQuestionPayload
    {
        public string Question { get; set; } = string.Empty;
        public string AnswerHash { get; set; } = string.Empty;
    }

    private sealed class LegacyPasswordResetTokenPayload
    {
        public string Purpose { get; set; } = string.Empty;
        public int UserAccessCode { get; set; }
        public long IssuedAtUnixSeconds { get; set; }
        public long ExpiresAtUnixSeconds { get; set; }
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
/// Complete forgot password request
/// </summary>
public class ForgotPasswordConfirmRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
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
}
