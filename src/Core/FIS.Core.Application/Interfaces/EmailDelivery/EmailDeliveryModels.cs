namespace FIS.Core.Application.Interfaces.EmailDelivery;

/// <summary>
/// A provider that can deliver email for FIS.
/// </summary>
public enum EmailProvider
{
    Graph,
    Smtp,
    SendGrid,
}

/// <summary>
/// The conservative outcome of an email delivery attempt.
/// </summary>
public enum EmailDeliveryStatus
{
    Accepted,
    Rejected,
    Unavailable,
    Unknown,
}

/// <summary>
/// Safe, non-secret health state exposed to management consumers.
/// </summary>
public enum EmailProviderHealth
{
    Healthy,
    Degraded,
    Unavailable,
    Unknown,
}

/// <summary>
/// The source of the effective provider configuration. This never contains a value.
/// </summary>
public enum EmailConfigurationSource
{
    Environment,
    KeyVault,
    Mixed,
    Unavailable,
}

/// <summary>
/// Microsoft Graph authentication modes supported by the delivery foundation.
/// </summary>
public enum EmailGraphAuthentication
{
    ManagedIdentity,
    ClientSecret,
}

/// <summary>
/// TLS modes accepted by the SMTP provider. Plaintext SMTP is intentionally absent.
/// </summary>
public enum EmailSmtpSecurityMode
{
    StartTls,
    SslOnConnect,
}

/// <summary>
/// SMTP authentication mode. An approved relay may intentionally use no credentials.
/// </summary>
public enum EmailSmtpAuthentication
{
    None,
    Password,
    GoogleOAuth2,
}

/// <summary>
/// A recipient address and optional display name.
/// </summary>
public sealed record EmailRecipient(string Address, string? DisplayName = null);

/// <summary>
/// An attachment held in memory by the application boundary.
/// </summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Provider-neutral message input. Infrastructure translates the bytes to each provider's wire format.
/// </summary>
public sealed record EmailDeliveryRequest(
    IReadOnlyList<EmailRecipient> Recipients,
    string Subject,
    string Body,
    bool IsHtml = true,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    string? Category = null,
    string? CorrelationId = null
);

/// <summary>
/// Result returned to a caller. Unknown means that submission may have happened and must not be retried automatically.
/// </summary>
public sealed record EmailDeliveryResult(
    EmailDeliveryStatus Status,
    EmailProvider? Provider,
    string Description,
    string? MessageId = null,
    bool CanFallback = false,
    bool CanRetry = false,
    TimeSpan? RetryAfter = null,
    IReadOnlyList<EmailProvider>? AttemptedProviders = null
)
{
    public bool IsAccepted => Status == EmailDeliveryStatus.Accepted;
}

/// <summary>
/// Safe provider health and configuration information. Secret values are deliberately not represented.
/// </summary>
public sealed record EmailProviderHealthStatus(
    EmailProvider Provider,
    bool Enabled,
    bool Configured,
    bool RequiresActivation,
    bool Available,
    EmailProviderHealth Health,
    EmailConfigurationSource ConfigurationSource,
    bool SupportsAttachments,
    string Description,
    int ConsecutiveFailureCount,
    DateTimeOffset? CircuitOpenUntilUtc,
    DateTimeOffset? LastSuccessfulSendUtc,
    DateTimeOffset? LastSuccessfulTestUtc,
    DateTimeOffset? RecentSuccessfulTestUntilUtc
);

/// <summary>
/// Safe Microsoft Graph configuration values that may be shown to an administrator.
/// </summary>
public sealed record EmailGraphConfigurationSummary(
    string Endpoint,
    EmailGraphAuthentication Authentication,
    string? TenantId,
    string? ClientId,
    string? ManagedIdentityClientId,
    string? SenderUserPrincipalName,
    string? FromAddress,
    string? FromName,
    bool ClientSecretConfigured
);

/// <summary>
/// Safe SMTP configuration values that may be shown to an administrator.
/// </summary>
public sealed record EmailSmtpConfigurationSummary(
    string? Host,
    int Port,
    EmailSmtpSecurityMode SecurityMode,
    EmailSmtpAuthentication Authentication,
    string? Username,
    string? FromAddress,
    string? FromName,
    bool PasswordConfigured,
    string? GoogleOAuthClientId,
    bool GoogleOAuthClientSecretConfigured,
    bool GoogleOAuthRefreshTokenConfigured
);

/// <summary>
/// Safe SendGrid configuration values that may be shown to an administrator.
/// </summary>
public sealed record EmailSendGridConfigurationSummary(
    string? FromEmail,
    string? FromName,
    bool ApiKeyConfigured
);

/// <summary>
/// Safe configuration status for the management API.
/// </summary>
public sealed record EmailDeliveryConfigurationStatus(
    IReadOnlyList<EmailProvider> ProviderOrder,
    IReadOnlyList<EmailProviderHealthStatus> Providers,
    bool ConfigurationManagementAvailable,
    string ManagementDescription,
    int TimeoutSeconds,
    long MaxTotalAttachmentBytes,
    DateTimeOffset GeneratedAtUtc,
    EmailGraphConfigurationSummary Graph,
    EmailSmtpConfigurationSummary Smtp,
    EmailSendGridConfigurationSummary SendGrid
);

/// <summary>
/// Partial configuration update. Null fields preserve their current runtime/Key Vault value.
/// Secret fields are write-only from an API perspective and are never returned by the status contract.
/// </summary>
public sealed record EmailDeliveryConfigurationUpdate(
    IReadOnlyList<EmailProvider>? ProviderOrder = null,
    EmailGraphConfigurationUpdate? Graph = null,
    EmailSmtpConfigurationUpdate? Smtp = null,
    EmailSendGridConfigurationUpdate? SendGrid = null,
    int? TimeoutSeconds = null,
    long? MaxTotalAttachmentBytes = null
);

public sealed record EmailGraphConfigurationUpdate(
    string? Endpoint = null,
    EmailGraphAuthentication? Authentication = null,
    string? TenantId = null,
    string? ClientId = null,
    string? ManagedIdentityClientId = null,
    string? SenderUserPrincipalName = null,
    string? FromAddress = null,
    string? FromName = null,
    string? ClientSecret = null
);

public sealed record EmailSmtpConfigurationUpdate(
    string? Host = null,
    int? Port = null,
    EmailSmtpSecurityMode? SecurityMode = null,
    EmailSmtpAuthentication? Authentication = null,
    string? Username = null,
    string? Password = null,
    string? GoogleOAuthClientId = null,
    string? GoogleOAuthClientSecret = null,
    string? GoogleOAuthRefreshToken = null,
    string? FromAddress = null,
    string? FromName = null
);

public sealed record EmailSendGridConfigurationUpdate(
    string? FromEmail = null,
    string? FromName = null,
    string? ApiKey = null
);

/// <summary>
/// Result of a Key Vault-backed management update.
/// </summary>
public enum EmailConfigurationUpdateStatus
{
    Updated,
    Rejected,
    Unavailable,
}

public sealed record EmailConfigurationUpdateResult(
    EmailConfigurationUpdateStatus Status,
    bool ConfigurationManagementAvailable,
    string Description,
    IReadOnlyList<EmailProvider> ChangedProviders,
    IReadOnlyList<string> ChangedFields
)
{
    public bool Succeeded => Status == EmailConfigurationUpdateStatus.Updated;
}

/// <summary>
/// The recipient for a fixed, provider-specific test message.
/// </summary>
public sealed record EmailProviderTestRequest(
    string RecipientAddress,
    string? CorrelationId = null
);

public sealed record EmailProviderTestResult(
    EmailProvider Provider,
    EmailDeliveryStatus Status,
    string Description,
    DateTimeOffset? RecentSuccessfulTestUntilUtc,
    string? MessageId = null
)
{
    public bool Succeeded => Status == EmailDeliveryStatus.Accepted;
}
