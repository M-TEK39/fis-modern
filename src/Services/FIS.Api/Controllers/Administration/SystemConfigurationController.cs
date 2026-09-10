using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FIS.Core.Application.Interfaces.SystemConfiguration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers.Administration;

/// <summary>
/// Exposes safe system security configuration state and Key Vault-backed
/// updates. Secret values are accepted only on update and are never returned.
/// </summary>
[ApiController]
[Authorize(Roles = "User Administration")]
[Route("api/system-configuration")]
[Produces("application/json")]
public sealed class SystemConfigurationController : ControllerBase
{
    private readonly ISystemConfigurationService _service;

    public SystemConfigurationController(ISystemConfigurationService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType<SystemConfigurationStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemConfigurationStatusDto>> GetStatus(
        CancellationToken cancellationToken
    )
    {
        var status = await _service.GetStatusAsync(cancellationToken);
        return Ok(ToDto(status));
    }

    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<SystemConfigurationUpdateResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemConfigurationUpdateResultDto>> Update(
        [FromBody] SystemConfigurationUpdateRequest? request,
        CancellationToken cancellationToken
    )
    {
        if (request is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A system configuration update is required."
            );
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _service.UpdateAsync(
            ToUpdate(request),
            GetActor(),
            cancellationToken
        );
        var dto = ToDto(result);

        return result.Status switch
        {
            SystemConfigurationUpdateStatus.Updated => Ok(dto),
            SystemConfigurationUpdateStatus.Rejected => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: result.Description
            ),
            _ => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: result.Description
            ),
        };
    }

    private string GetActor() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("user_access_code")
        ?? User.Identity?.Name
        ?? "authenticated-administrator";

    private static SystemConfigurationUpdate ToUpdate(
        SystemConfigurationUpdateRequest request
    ) =>
        new(
            request.Session?.StandardRefreshLifetimeMinutes is { } standardRefreshLifetimeMinutes
                ? TimeSpan.FromMinutes(standardRefreshLifetimeMinutes)
                : null,
            request.Session?.RememberRefreshLifetimeMinutes is { } rememberRefreshLifetimeMinutes
                ? TimeSpan.FromMinutes(rememberRefreshLifetimeMinutes)
                : null,
            request.Authentication?.EntraEnabled,
            request.Authentication?.TenantId,
            request.Authentication?.ClientId,
            request.Authentication?.ClientSecret,
            request.Authentication?.PasswordResetSigningKey
        );

    private static SystemConfigurationStatusDto ToDto(SystemConfigurationStatus status) =>
        new(
            status.ConfigurationManagementAvailable,
            status.ConfigurationManagementDescription,
            new(
                checked((int)status.StandardRefreshLifetime.TotalMinutes),
                checked((int)status.RememberRefreshLifetime.TotalMinutes)
            ),
            new(
                status.EntraEnabled,
                status.EntraTenantId,
                status.EntraClientId,
                status.EntraClientSecretConfigured,
                status.PasswordResetSigningKeyConfigured,
                status.RestartRequired
            ),
            status.ConfigurationSource.ToString(),
            status.GeneratedAtUtc
        );

    private static SystemConfigurationUpdateResultDto ToDto(
        SystemConfigurationUpdateResult result
    ) =>
        new(
            result.Status.ToString(),
            result.ConfigurationManagementAvailable,
            result.Description,
            result.ChangedSettings,
            result.Metadata?.UpdateId,
            result.Metadata?.UpdatedAtUtc
        );
}

public sealed record SystemConfigurationStatusDto(
    bool ConfigurationManagementAvailable,
    string ManagementDescription,
    SystemConfigurationSessionStatusDto Session,
    SystemConfigurationAuthenticationStatusDto Authentication,
    string ConfigurationSource,
    DateTimeOffset GeneratedAtUtc
);

public sealed record SystemConfigurationSessionStatusDto(
    int StandardRefreshLifetimeMinutes,
    int RememberRefreshLifetimeMinutes
);

public sealed record SystemConfigurationAuthenticationStatusDto(
    bool EntraEnabled,
    string? TenantId,
    string? ClientId,
    bool ClientSecretConfigured,
    bool PasswordResetSigningKeyConfigured,
    bool RestartRequired
);

public sealed record SystemConfigurationUpdateResultDto(
    string Status,
    bool ConfigurationManagementAvailable,
    string Description,
    IReadOnlyList<string> ChangedSettings,
    Guid? UpdateId,
    DateTimeOffset? UpdatedAtUtc
);

public sealed class SystemConfigurationUpdateRequest
{
    public SystemConfigurationSessionUpdateRequest? Session { get; init; }

    public SystemConfigurationAuthenticationUpdateRequest? Authentication { get; init; }
}

public sealed class SystemConfigurationSessionUpdateRequest
{
    [Range(15, 43200)]
    public int? StandardRefreshLifetimeMinutes { get; init; }

    [Range(15, 43200)]
    public int? RememberRefreshLifetimeMinutes { get; init; }
}

public sealed class SystemConfigurationAuthenticationUpdateRequest
{
    public bool? EntraEnabled { get; init; }

    [StringLength(200)]
    public string? TenantId { get; init; }

    [StringLength(200)]
    public string? ClientId { get; init; }

    [StringLength(4096)]
    public string? ClientSecret { get; init; }

    [StringLength(4096)]
    public string? PasswordResetSigningKey { get; init; }
}
