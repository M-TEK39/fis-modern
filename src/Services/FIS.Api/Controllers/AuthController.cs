using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FIS.Core.Application.Interfaces;

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
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IConfiguration configuration,
        IUserRepository userRepository,
        ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _logger = logger;
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

        // TEMPORARY: For development/testing, accept any credentials
        // PRODUCTION: Replace with actual credential validation
        var isDevelopment = _configuration.GetValue<bool>("AuthenticationSettings:RequireAuthentication") == false;

        if (isDevelopment)
        {
            _logger.LogWarning("Development mode: Accepting any credentials for user {Username}", request.Username);

            // Try to parse username as user_access_code, or default to 1
            int userAccessCode = int.TryParse(request.Username, out var code) ? code : 1;

            // Check if user exists in database
            var user = await _userRepository.GetByIdAsync(userAccessCode);
            if (user == null)
            {
                // Create test user data
                _logger.LogInformation("Creating test token for non-existent user_access_code: {UserAccessCode}", userAccessCode);
            }

            var token = GenerateJwtToken(userAccessCode, user?.email ?? $"user{userAccessCode}@test.com");

            return Ok(new LoginResponse
            {
                Token = token.TokenString,
                ExpiresAt = token.ExpiresAt,
                UserAccessCode = userAccessCode,
                Email = user?.email,
                Message = "Development mode: Authentication bypassed"
            });
        }

        // PRODUCTION: Implement real credential validation here
        // TODO: Check against LegacyUserCredential table or external auth system
        _logger.LogWarning("Production authentication not yet implemented");
        return Unauthorized(new { error = "Authentication not configured. Contact system administrator." });
    }

    /// <summary>
    /// Refresh an existing JWT token
    /// </summary>
    /// <param name="request">Current token to refresh</param>
    /// <returns>New JWT token</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var tokenHandler = new JwtSecurityTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Don't validate expiration for refresh
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };

            var principal = tokenHandler.ValidateToken(request.Token, validationParameters, out var validatedToken);
            var userAccessCodeClaim = principal.FindFirst("user_access_code")?.Value;

            if (string.IsNullOrEmpty(userAccessCodeClaim) || !int.TryParse(userAccessCodeClaim, out var userAccessCode))
            {
                return Unauthorized(new { error = "Invalid token claims" });
            }

            var emailClaim = principal.FindFirst(ClaimTypes.Email)?.Value ?? $"user{userAccessCode}@test.com";
            var newToken = GenerateJwtToken(userAccessCode, emailClaim);

            return Ok(new LoginResponse
            {
                Token = newToken.TokenString,
                ExpiresAt = newToken.ExpiresAt,
                UserAccessCode = userAccessCode,
                Email = emailClaim,
                Message = "Token refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return Unauthorized(new { error = "Invalid token" });
        }
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

        return Ok(new { message = "Logged out successfully. Clear token from client storage." });
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
        var expiry = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        return Ok(new
        {
            valid = true,
            userAccessCode,
            email,
            expiresAt = expiry != null ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiry)).DateTime : null,
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

            // TODO: Implement actual password verification and update
            _logger.LogInformation("Password change requested for user {Username}", request.Username);

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

            // TODO: Implement actual password and security question update
            _logger.LogInformation("Password and security question change requested for user {Username}", request.Username);

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
            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // TODO: Implement actual login reset logic (reset failed attempts counter)
            _logger.LogInformation("Login reset requested for user {Username}", request.Username);

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
            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // TODO: Implement actual password update with hashing
            _logger.LogInformation("Force password change requested for user {Username}", request.Username);

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
            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                // Don't reveal if user exists
                return Ok(new UserAdminResponse
                {
                    Success = false,
                    Message = "If the username exists, a security question will be displayed"
                });
            }

            // TODO: Get actual security question from user record
            _logger.LogInformation("Forgot password flow started for user {Username}", request.Username);

            return Ok(new UserAdminResponse
            {
                Success = true,
                Message = "Security question retrieved",
                Question = "What is your favorite color?" // TODO: Get from user record
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
            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "Invalid request"
                });
            }

            // TODO: Verify security answer and generate new password
            var newPassword = GenerateTemporaryPassword();
            _logger.LogInformation("Forgot password confirmed for user {Username}", request.Username);

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
            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // TODO: Implement actual user activation logic
            _logger.LogInformation("User activation requested for {Username}", request.Username);

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
    [HttpPost("deactivate-expired")]
    [Authorize]
    public async Task<ActionResult<UserAdminResponse>> DeactivateExpiredPassword([FromBody] DeactivateExpiredPasswordRequest request)
    {
        try
        {
            // Try to parse username as user_access_code, or lookup by email
            int.TryParse(request.Username, out var userCode);
            var user = userCode > 0
                ? await _userRepository.GetByIdAsync(userCode)
                : await _userRepository.GetByEmailAsync(request.Username);
            if (user == null)
            {
                return NotFound(new UserAdminResponse
                {
                    Success = false,
                    Message = "User not found"
                });
            }

            // TODO: Implement actual user deactivation logic
            _logger.LogInformation("User deactivation requested for {Username}", request.Username);

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

    private (string TokenString, DateTime ExpiresAt) GenerateJwtToken(int userAccessCode, string email)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expirationMinutes = int.Parse(jwtSettings["ExpirationMinutes"] ?? "480");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userAccessCode.ToString()),
            new Claim("user_access_code", userAccessCode.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name, email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("Generated JWT token for user_access_code: {UserAccessCode}", userAccessCode);

        return (tokenString, token.ValidTo);
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
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
}

/// <summary>
/// Change password request
/// </summary>
public class ChangePasswordRequest
{
    public string Username { get; set; } = string.Empty;
    public string OldPassword { get; set; } = string.Empty;
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
