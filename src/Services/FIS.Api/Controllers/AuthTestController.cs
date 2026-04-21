using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FIS.Api.Controllers;

/// <summary>
/// TEMPORARY: Test endpoint to generate JWT tokens for Swagger testing
/// DELETE THIS CONTROLLER IN PRODUCTION
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]  // No auth required to get a token
public class AuthTestController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthTestController> _logger;

    public AuthTestController(IConfiguration configuration, ILogger<AuthTestController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generate a test JWT token for Swagger testing
    /// </summary>
    /// <param name="userAccessCode">Optional user_access_code (default: 1)</param>
    /// <returns>JWT token valid for 8 hours</returns>
    [HttpGet("generate-test-token")]
    public ActionResult<object> GenerateTestToken([FromQuery] int userAccessCode = 1)
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
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, $"TestUser{userAccessCode}")
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

        _logger.LogInformation("Generated test JWT token for user_access_code: {UserAccessCode}", userAccessCode);

        return Ok(new
        {
            token = tokenString,
            expiresAt = token.ValidTo,
            userAccessCode,
            instructions = "Copy the token value and paste it into Swagger's 'Authorize' button (click the lock icon at the top right). Format: Bearer <token>"
        });
    }

    /// <summary>
    /// Check if current user is authenticated
    /// </summary>
    [HttpGet("whoami")]
    [Authorize]  // This endpoint REQUIRES authentication
    public ActionResult<object> WhoAmI()
    {
        var userAccessCode = User.FindFirst("user_access_code")?.Value;
        var userName = User.Identity?.Name;
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        return Ok(new
        {
            isAuthenticated,
            userAccessCode,
            userName,
            authType = User.Identity?.AuthenticationType,
            allClaims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}
