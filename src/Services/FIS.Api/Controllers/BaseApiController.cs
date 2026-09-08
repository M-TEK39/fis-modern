using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Base controller with common functionality for all API controllers
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Get current user's access code from JWT claims
    /// </summary>
    protected int GetCurrentUserId()
    {
        var userAccessCodeClaim = User.FindFirst("user_access_code")?.Value;

        if (int.TryParse(userAccessCodeClaim, out int userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException(
            "Authenticated user does not include a valid user_access_code claim."
        );
    }
}
