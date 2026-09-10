using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FIS.Core.Application.Interfaces.EmailDelivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Provides safe status, Key Vault-backed configuration updates, and provider tests.
/// Secret values are write-only and are never returned by this controller.
/// </summary>
[ApiController]
[Authorize(Roles = "User Administration")]
[Route("api/email-configuration")]
[Produces("application/json")]
public sealed class EmailConfigurationController : ControllerBase
{
    private readonly IEmailConfigurationService _service;
    private readonly ILogger<EmailConfigurationController> _logger;

    public EmailConfigurationController(
        IEmailConfigurationService service,
        ILogger<EmailConfigurationController> logger
    )
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType<EmailConfigurationStatusDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailConfigurationStatusDto>> GetStatus(
        CancellationToken cancellationToken
    ) => Ok(ToDto(await _service.GetStatusAsync(cancellationToken)));

    [HttpPut]
    [Consumes("application/json")]
    [ProducesResponseType<EmailConfigurationUpdateResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmailConfigurationUpdateResultDto>> Update(
        [FromBody] EmailConfigurationUpdateRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!TryMapUpdate(request, out var update, out var error))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: error);

        var result = await _service.UpdateAsync(update!, GetActorId(), cancellationToken);
        var dto = new EmailConfigurationUpdateResultDto(
            result.Status.ToString(),
            result.ConfigurationManagementAvailable,
            result.Description,
            result.ChangedProviders.Select(provider => provider.ToString()).ToArray(),
            result.ChangedFields
        );
        return result.Status switch
        {
            EmailConfigurationUpdateStatus.Updated => Ok(dto),
            EmailConfigurationUpdateStatus.Rejected => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: result.Description
            ),
            _ => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: result.Description
            ),
        };
    }

    [HttpPost("{provider}/test")]
    [Consumes("application/json")]
    [ProducesResponseType<EmailProviderTestResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmailProviderTestResultDto>> TestProvider(
        string provider,
        [FromBody] EmailProviderTestRequestDto request,
        CancellationToken cancellationToken
    )
    {
        if (!TryParseEnum<EmailProvider>(provider, out var parsedProvider))
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Choose a supported email provider."
            );
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var result = await _service.TestProviderAsync(
            parsedProvider,
            new EmailProviderTestRequest(request.RecipientAddress, Guid.NewGuid().ToString("N")),
            GetActorId(),
            cancellationToken
        );
        var dto = new EmailProviderTestResultDto(
            result.Provider.ToString(),
            result.Status.ToString(),
            result.Description,
            result.RecentSuccessfulTestUntilUtc,
            result.MessageId
        );
        return result.Status switch
        {
            EmailDeliveryStatus.Accepted => Ok(dto),
            EmailDeliveryStatus.Rejected => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: result.Description
            ),
            EmailDeliveryStatus.Unavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: result.Description
            ),
            _ => Problem(statusCode: StatusCodes.Status502BadGateway, title: result.Description),
        };
    }

    private string GetActorId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.Identity?.Name
        ?? "authenticated-administrator";

    private static bool TryMapUpdate(
        EmailConfigurationUpdateRequest request,
        out EmailDeliveryConfigurationUpdate? update,
        out string? error
    )
    {
        update = null;
        error = null;
        IReadOnlyList<EmailProvider>? providerOrder = null;
        if (request.ProviderOrder is not null)
        {
            var parsed = new List<EmailProvider>();
            foreach (var item in request.ProviderOrder)
            {
                if (!TryParseEnum<EmailProvider>(item, out var provider))
                {
                    error = "Provider order contains an unsupported email provider.";
                    return false;
                }
                if (!parsed.Contains(provider))
                    parsed.Add(provider);
            }
            providerOrder = parsed;
        }

        if (
            !TryParseOptionalEnum<EmailGraphAuthentication>(
                request.Graph?.Authentication,
                out var graphAuthentication
            )
            || !TryParseOptionalEnum<EmailSmtpSecurityMode>(
                request.Smtp?.SecurityMode,
                out var smtpSecurityMode
            )
            || !TryParseOptionalEnum<EmailSmtpAuthentication>(
                request.Smtp?.Authentication,
                out var smtpAuthentication
            )
        )
        {
            error = "One or more selected email provider options are invalid.";
            return false;
        }

        update = new EmailDeliveryConfigurationUpdate(
            providerOrder,
            request.Graph is null
                ? null
                : new EmailGraphConfigurationUpdate(
                    request.Graph.Endpoint,
                    graphAuthentication,
                    request.Graph.TenantId,
                    request.Graph.ClientId,
                    request.Graph.ManagedIdentityClientId,
                    request.Graph.SenderUserPrincipalName,
                    request.Graph.FromAddress,
                    request.Graph.FromName,
                    request.Graph.ClientSecret
                ),
            request.Smtp is null
                ? null
                : new EmailSmtpConfigurationUpdate(
                    request.Smtp.Host,
                    request.Smtp.Port,
                    smtpSecurityMode,
                    smtpAuthentication,
                    request.Smtp.Username,
                    request.Smtp.Password,
                    request.Smtp.GoogleOAuthClientId,
                    request.Smtp.GoogleOAuthClientSecret,
                    request.Smtp.GoogleOAuthRefreshToken,
                    request.Smtp.FromAddress,
                    request.Smtp.FromName
                ),
            request.SendGrid is null
                ? null
                : new EmailSendGridConfigurationUpdate(
                    request.SendGrid.FromEmail,
                    request.SendGrid.FromName,
                    request.SendGrid.ApiKey
                ),
            request.TimeoutSeconds,
            request.MaxTotalAttachmentBytes
        );
        return true;
    }

    private static bool TryParseOptionalEnum<TEnum>(string? raw, out TEnum? value)
        where TEnum : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (!TryParseEnum(raw, out TEnum parsed))
            return false;
        value = parsed;
        return true;
    }

    private static bool TryParseEnum<TEnum>(string raw, out TEnum value)
        where TEnum : struct, Enum =>
        Enum.TryParse(raw, true, out value) && Enum.IsDefined(typeof(TEnum), value);

    private static EmailConfigurationStatusDto ToDto(EmailDeliveryConfigurationStatus status) =>
        new(
            status.ProviderOrder.Select(provider => provider.ToString()).ToArray(),
            status
                .Providers.Select(provider => new EmailProviderHealthStatusDto(
                    provider.Provider.ToString(),
                    provider.Enabled,
                    provider.Configured,
                    provider.RequiresActivation,
                    provider.Available,
                    provider.Health.ToString(),
                    provider.ConfigurationSource.ToString(),
                    provider.SupportsAttachments,
                    provider.Description,
                    provider.ConsecutiveFailureCount,
                    provider.CircuitOpenUntilUtc,
                    provider.LastSuccessfulSendUtc,
                    provider.LastSuccessfulTestUtc,
                    provider.RecentSuccessfulTestUntilUtc
                ))
                .ToArray(),
            status.ConfigurationManagementAvailable,
            status.ManagementDescription,
            status.TimeoutSeconds,
            status.MaxTotalAttachmentBytes,
            status.GeneratedAtUtc,
            new EmailGraphConfigurationSummaryDto(
                status.Graph.Endpoint,
                status.Graph.Authentication.ToString(),
                status.Graph.TenantId,
                status.Graph.ClientId,
                status.Graph.ManagedIdentityClientId,
                status.Graph.SenderUserPrincipalName,
                status.Graph.FromAddress,
                status.Graph.FromName,
                status.Graph.ClientSecretConfigured
            ),
            new EmailSmtpConfigurationSummaryDto(
                status.Smtp.Host,
                status.Smtp.Port,
                status.Smtp.SecurityMode.ToString(),
                status.Smtp.Authentication.ToString(),
                status.Smtp.Username,
                status.Smtp.FromAddress,
                status.Smtp.FromName,
                status.Smtp.PasswordConfigured,
                status.Smtp.GoogleOAuthClientId,
                status.Smtp.GoogleOAuthClientSecretConfigured,
                status.Smtp.GoogleOAuthRefreshTokenConfigured
            ),
            new EmailSendGridConfigurationSummaryDto(
                status.SendGrid.FromEmail,
                status.SendGrid.FromName,
                status.SendGrid.ApiKeyConfigured
            )
        );
}

public sealed record EmailConfigurationStatusDto(
    IReadOnlyList<string> ProviderOrder,
    IReadOnlyList<EmailProviderHealthStatusDto> Providers,
    bool ConfigurationManagementAvailable,
    string ManagementDescription,
    int TimeoutSeconds,
    long MaxTotalAttachmentBytes,
    DateTimeOffset GeneratedAtUtc,
    EmailGraphConfigurationSummaryDto Graph,
    EmailSmtpConfigurationSummaryDto Smtp,
    EmailSendGridConfigurationSummaryDto SendGrid
);

public sealed record EmailProviderHealthStatusDto(
    string Provider,
    bool Enabled,
    bool Configured,
    bool RequiresActivation,
    bool Available,
    string Health,
    string ConfigurationSource,
    bool SupportsAttachments,
    string Description,
    int ConsecutiveFailureCount,
    DateTimeOffset? CircuitOpenUntilUtc,
    DateTimeOffset? LastSuccessfulSendUtc,
    DateTimeOffset? LastSuccessfulTestUtc,
    DateTimeOffset? RecentSuccessfulTestUntilUtc
);

public sealed record EmailGraphConfigurationSummaryDto(
    string Endpoint,
    string Authentication,
    string? TenantId,
    string? ClientId,
    string? ManagedIdentityClientId,
    string? SenderUserPrincipalName,
    string? FromAddress,
    string? FromName,
    bool ClientSecretConfigured
);

public sealed record EmailSmtpConfigurationSummaryDto(
    string? Host,
    int Port,
    string SecurityMode,
    string Authentication,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool PasswordConfigured,
    string? GoogleOAuthClientId,
    bool GoogleOAuthClientSecretConfigured,
    bool GoogleOAuthRefreshTokenConfigured
);

public sealed record EmailSendGridConfigurationSummaryDto(
    string? FromEmail,
    string? FromName,
    bool ApiKeyConfigured
);

public sealed record EmailConfigurationUpdateResultDto(
    string Status,
    bool ConfigurationManagementAvailable,
    string Description,
    IReadOnlyList<string> ChangedProviders,
    IReadOnlyList<string> ChangedFields
);

public sealed record EmailProviderTestResultDto(
    string Provider,
    string Status,
    string Description,
    DateTimeOffset? RecentSuccessfulTestUntilUtc,
    string? MessageId
);

public sealed class EmailConfigurationUpdateRequest
{
    public List<string>? ProviderOrder { get; init; }
    public EmailGraphConfigurationUpdateRequest? Graph { get; init; }
    public EmailSmtpConfigurationUpdateRequest? Smtp { get; init; }
    public EmailSendGridConfigurationUpdateRequest? SendGrid { get; init; }
    public int? TimeoutSeconds { get; init; }
    public long? MaxTotalAttachmentBytes { get; init; }
}

public sealed class EmailGraphConfigurationUpdateRequest
{
    public string? Endpoint { get; init; }
    public string? Authentication { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ManagedIdentityClientId { get; init; }
    public string? SenderUserPrincipalName { get; init; }
    public string? FromAddress { get; init; }
    public string? FromName { get; init; }
    public string? ClientSecret { get; init; }
}

public sealed class EmailSmtpConfigurationUpdateRequest
{
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? SecurityMode { get; init; }
    public string? Authentication { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? GoogleOAuthClientId { get; init; }
    public string? GoogleOAuthClientSecret { get; init; }
    public string? GoogleOAuthRefreshToken { get; init; }
    public string? FromAddress { get; init; }
    public string? FromName { get; init; }
}

public sealed class EmailSendGridConfigurationUpdateRequest
{
    public string? FromEmail { get; init; }
    public string? FromName { get; init; }
    public string? ApiKey { get; init; }
}

public sealed class EmailProviderTestRequestDto
{
    [Required]
    [EmailAddress]
    public string RecipientAddress { get; init; } = string.Empty;
}
