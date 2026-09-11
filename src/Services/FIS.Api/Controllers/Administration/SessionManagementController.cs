using System.ComponentModel.DataAnnotations;
using FIS.Api.Services;
using FIS.Api.Services.SessionManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers.Administration;

/// <summary>
/// Provides guarded administration of durable FIS sessions. The optional
/// session-token table is probed at runtime; this controller never treats the
/// process-local legacy fallback as durable, manageable state.
/// </summary>
[ApiController]
[Authorize(Roles = "User Administration")]
[Route("api/session-management")]
[Produces("application/json")]
public sealed class SessionManagementController : ControllerBase
{
    private const string RefreshCookieName = "FIS_Refresh_Token";
    private const int MaximumListLimit = 500;
    private readonly IConfiguration _configuration;
    private readonly ISessionManagementService _service;

    public SessionManagementController(
        IConfiguration configuration,
        ISessionManagementService service
    )
    {
        _configuration = configuration;
        _service = service;
    }

    /// <summary>
    /// Returns the safe durable-session administration status and active
    /// sessions. Token values, claims, and generated session ids are never
    /// sent to the browser.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<SessionManagementStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SessionManagementStatusDto>> Get(
        [FromQuery] int? userAccessCode,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default
    )
    {
        if (userAccessCode is <= 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "User access code must be a positive integer."
            );
        }

        if (limit is < 1 or > MaximumListLimit)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Limit must be between 1 and {MaximumListLimit}."
            );
        }

        var storeStatus = await _service.GetStatusAsync(cancellationToken);
        if (!storeStatus.Available)
        {
            return Ok(
                CreateStatus(storeStatus.Description, false, Array.Empty<ActiveSessionDto>())
            );
        }

        var result = await _service.ListActiveSessionsAsync(
            userAccessCode,
            GetActorUserAccessCode(),
            GetAccessToken(),
            limit,
            cancellationToken
        );
        if (result.Status != SessionManagementStatus.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: result.Description
            );
        }

        return Ok(CreateStatus(result.Description, true, result.Sessions.Select(ToDto).ToArray()));
    }

    /// <summary>
    /// Revokes the selected durable session scope. A destructive operation is
    /// explicit so a compromised account can be contained without browser
    /// access to any raw session material.
    /// </summary>
    [HttpPost("revoke")]
    [ProducesResponseType<SessionOperationResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SessionOperationResponseDto>> Revoke(
        [FromBody] RevokeSessionsRequest? request,
        CancellationToken cancellationToken
    )
    {
        if (
            request is null
            || !ModelState.IsValid
            || !RevokeScopeExtensions.TryParse(request.Scope, out var scope)
        )
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Choose a valid session revocation scope."
            );
        }

        SessionOperationResult result;
        switch (scope)
        {
            case RevokeScope.Current:
            {
                var actorCode = GetActorUserAccessCode();
                var accessToken = GetAccessToken();
                if (actorCode <= 0 || string.IsNullOrWhiteSpace(accessToken))
                {
                    return Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "The current session could not be identified."
                    );
                }

                result = await _service.RevokeCurrentSessionAsync(
                    actorCode,
                    accessToken,
                    cancellationToken
                );
                break;
            }
            case RevokeScope.User:
                if (request.UserAccessCode is not > 0)
                {
                    return Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "A valid user access code is required to revoke that user's sessions."
                    );
                }

                result = await _service.RevokeUserSessionsAsync(
                    request.UserAccessCode.Value,
                    cancellationToken
                );
                break;
            case RevokeScope.All:
                result = await _service.RevokeAllSessionsAsync(cancellationToken);
                break;
            default:
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Choose a valid session revocation scope."
                );
        }

        if (result.Status == SessionManagementStatus.Failed)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Description);
        }

        if (result.Status != SessionManagementStatus.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: result.Description
            );
        }

        if (scope == RevokeScope.Current || scope == RevokeScope.All)
        {
            Response.Cookies.Delete(SessionCookieAuthenticationHandler.AccessCookieName);
            Response.Cookies.Delete(RefreshCookieName);
        }

        return Ok(new SessionOperationResponseDto(result.AffectedSessionCount, result.Description));
    }

    private SessionManagementStatusDto CreateStatus(
        string description,
        bool durableSessionManagementAvailable,
        IReadOnlyList<ActiveSessionDto> sessions
    ) =>
        new(
            durableSessionManagementAvailable,
            description,
            AccessTokenLifetimeMinutes: 15,
            StandardRefreshLifetimeMinutes: ReadRefreshLifetime(
                "SystemSettings:SessionRefreshLifetimeMinutes",
                8 * 60
            ),
            RememberRefreshLifetimeMinutes: ReadRefreshLifetime(
                "SystemSettings:RememberRefreshLifetimeMinutes",
                7 * 24 * 60
            ),
            sessions
        );

    private int ReadRefreshLifetime(string key, int fallback) =>
        int.TryParse(_configuration[key], out var lifetime) && lifetime is >= 15 and <= 43_200
            ? lifetime
            : fallback;

    private string? GetAccessToken() =>
        Request.Cookies.TryGetValue(
            SessionCookieAuthenticationHandler.AccessCookieName,
            out var accessToken
        )
            ? accessToken
            : null;

    private int GetActorUserAccessCode()
    {
        var rawValue = User.FindFirst("user_access_code")?.Value;
        return int.TryParse(rawValue, out var userAccessCode) ? userAccessCode : 0;
    }

    private static ActiveSessionDto ToDto(ActiveSession session) =>
        new(
            session.UserAccessCode,
            session.CreatedAtUtc,
            session.ExpiresAtUtc,
            session.RememberMe,
            session.IsCurrent
        );
}

public sealed record SessionManagementStatusDto(
    bool DurableSessionManagementAvailable,
    string Description,
    int AccessTokenLifetimeMinutes,
    int StandardRefreshLifetimeMinutes,
    int RememberRefreshLifetimeMinutes,
    IReadOnlyList<ActiveSessionDto> Sessions
);

public sealed record ActiveSessionDto(
    int UserAccessCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool RememberMe,
    bool IsCurrent
);

public sealed record SessionOperationResponseDto(int AffectedSessionCount, string Description);

public sealed class RevokeSessionsRequest
{
    [Required]
    public string? Scope { get; init; }

    [Range(1, int.MaxValue)]
    public int? UserAccessCode { get; init; }
}

internal enum RevokeScope
{
    Current,
    User,
    All,
}

internal static class RevokeScopeExtensions
{
    public static bool TryParse(string? value, out RevokeScope scope)
    {
        scope = default;
        return value?.Trim().ToLowerInvariant() switch
        {
            "current" => Assign(RevokeScope.Current, out scope),
            "user" => Assign(RevokeScope.User, out scope),
            "all" => Assign(RevokeScope.All, out scope),
            _ => false,
        };
    }

    private static bool Assign(RevokeScope value, out RevokeScope scope)
    {
        scope = value;
        return true;
    }
}
