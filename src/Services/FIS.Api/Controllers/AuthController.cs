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
