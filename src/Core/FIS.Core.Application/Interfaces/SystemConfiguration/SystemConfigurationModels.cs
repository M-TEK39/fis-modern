namespace FIS.Core.Application.Interfaces.SystemConfiguration;

/// <summary>
/// Indicates where the effective system configuration value came from.
/// </summary>
public enum SystemConfigurationSource
{
    Environment,
    KeyVault,
    Mixed,
}

/// <summary>
/// Indicates the outcome of a system configuration update.
/// </summary>
public enum SystemConfigurationUpdateStatus
{
    Updated,
    Rejected,
    Unavailable,
}

/// <summary>
/// Safe system security configuration state. Secret values are deliberately not
/// represented; only their configured state is exposed.
/// </summary>
public sealed record SystemConfigurationStatus(
    TimeSpan StandardRefreshLifetime,
    TimeSpan RememberRefreshLifetime,
    bool EntraEnabled,
    string? EntraTenantId,
    string? EntraClientId,
    bool EntraClientSecretConfigured,
    bool PasswordResetSigningKeyConfigured,
    bool RestartRequired,
    SystemConfigurationSource ConfigurationSource,
    bool ConfigurationManagementAvailable,
    string ConfigurationManagementDescription,
    DateTimeOffset GeneratedAtUtc
);

/// <summary>
/// Partial system security configuration update. Null values preserve the
/// current effective value. Secret fields are write-only from the API's
/// perspective and are never returned by a status contract.
/// </summary>
public sealed record SystemConfigurationUpdate(
    TimeSpan? StandardRefreshLifetime = null,
    TimeSpan? RememberRefreshLifetime = null,
    bool? EntraEnabled = null,
    string? EntraTenantId = null,
    string? EntraClientId = null,
    string? EntraClientSecret = null,
    string? PasswordResetSigningKey = null
);

/// <summary>
/// Metadata for one successful update operation. This is an operation id, not
/// a configuration version or an optimistic-concurrency token.
/// </summary>
public sealed record SystemConfigurationUpdateMetadata(
    Guid UpdateId,
    DateTimeOffset UpdatedAtUtc
);

/// <summary>
/// Result of a Key Vault-backed system configuration update.
/// </summary>
public sealed record SystemConfigurationUpdateResult(
    SystemConfigurationUpdateStatus Status,
    bool ConfigurationManagementAvailable,
    string Description,
    IReadOnlyList<string> ChangedSettings,
    SystemConfigurationUpdateMetadata? Metadata = null
)
{
    public bool Succeeded => Status == SystemConfigurationUpdateStatus.Updated;
}

/// <summary>
/// Safe audit payload for one successful configuration update. It intentionally
/// contains setting names and operation metadata only; values are not part of
/// the audit boundary.
/// </summary>
public sealed record SystemConfigurationAuditEntry(
    IReadOnlyList<string> SettingNames,
    string Actor,
    SystemConfigurationSource Source,
    SystemConfigurationUpdateMetadata Metadata
);
