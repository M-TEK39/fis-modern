using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// Development-only endpoint to inspect active authenticated session claims.
[ApiController]
[Route("api/[controller]")]
public class AuthTestController : ControllerBase
{
    private readonly ILogger<AuthTestController> _logger;

    public AuthTestController(ILogger<AuthTestController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if current user is authenticated
    /// </summary>
    [HttpGet("whoami")]
    [Authorize]
    public ActionResult<object> WhoAmI()
    {
        _logger.LogInformation(
            "WhoAmI requested for authenticated user {User}",
            User.Identity?.Name
        );

        var userAccessCode = User.FindFirst("user_access_code")?.Value;
        var userName = User.Identity?.Name;
        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

        return Ok(
            new
            {
                isAuthenticated,
                userAccessCode,
                userName,
                authType = User.Identity?.AuthenticationType,
                allClaims = User.Claims.Select(c => new { c.Type, c.Value }),
            }
        );
    }
}
