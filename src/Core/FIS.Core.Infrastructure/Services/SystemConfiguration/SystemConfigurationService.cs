using System.Text;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FIS.Core.Application.Interfaces.SystemConfiguration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services.SystemConfiguration;

/// <summary>
/// Resolves the allow-listed system security configuration from deployment
/// configuration and an optional Key Vault override document. Key Vault is the
/// only writable runtime store; no database state is involved.
/// </summary>
public sealed class SystemConfigurationService : ISystemConfigurationService
{
    private const string ConfigurationSecretName = "fis-system-security-config";
    private const int DefaultSessionRefreshLifetimeMinutes = 8 * 60;
    private const int DefaultRememberRefreshLifetimeMinutes = 7 * 24 * 60;
    private const int MinimumRefreshLifetimeMinutes = 15;
    private const int MaximumRefreshLifetimeMinutes = 30 * 24 * 60;
    private const int MaximumIdentifierLength = 200;
    private const int MaximumSecretLength = 4096;
    private const int MaximumSecretDocumentBytes = 25_000;

    private const string StandardRefreshLifetimeSetting = "standardRefreshLifetime";
    private const string RememberRefreshLifetimeSetting = "rememberRefreshLifetime";
    private const string EntraEnabledSetting = "entraEnabled";
    private const string EntraTenantIdSetting = "entraTenantId";
    private const string EntraClientIdSetting = "entraClientId";
    private const string EntraClientSecretSetting = "entraClientSecret";
    private const string PasswordResetSigningKeySetting = "passwordResetSigningKey";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemConfigurationService> _logger;
    private readonly ISystemConfigurationAuditSink _auditSink;
    private readonly SecretClient? _secretClient;

    public SystemConfigurationService(
        IConfiguration configuration,
        ILogger<SystemConfigurationService> logger,
        ISystemConfigurationAuditSink auditSink
    )
    {
        _configuration = configuration;
        _logger = logger;
        _auditSink = auditSink;
        _secretClient = CreateSecretClient(configuration["SystemSettings:KeyVaultUri"]);
    }

    /// <summary>
    /// Applies the allow-listed Key Vault values before authentication and
    /// session services are composed. If the vault cannot be reached, the
    /// deployment configuration remains the safe bootstrap fallback.
    /// </summary>
    public static void ApplyStartupOverrides(ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var client = CreateSecretClient(configuration["SystemSettings:KeyVaultUri"]);
        if (client is null)
            return;

        try
        {
            var response = client.GetSecret(ConfigurationSecretName);
            var rawDocument = response.Value.Value;
            if (string.IsNullOrWhiteSpace(rawDocument))
                return;

            var document = JsonSerializer.Deserialize<SystemConfigurationDocument>(
                rawDocument,
                JsonOptions
            );
            if (document is null || !TryValidateDocument(document, out _))
            {
                Console.Error.WriteLine(
                    "FIS system configuration in Azure Key Vault is invalid; deployment configuration remains active."
                );
                return;
            }

            var overrides = new Dictionary<string, string?>();
            if (document.SessionRefreshLifetimeMinutes is { } sessionLifetime)
                overrides["SystemSettings:SessionRefreshLifetimeMinutes"] =
                    sessionLifetime.ToString();
            if (document.RememberRefreshLifetimeMinutes is { } rememberLifetime)
                overrides["SystemSettings:RememberRefreshLifetimeMinutes"] =
                    rememberLifetime.ToString();
            if (document.EntraEnabled is { } entraEnabled)
                overrides["SystemSettings:EntraEnabled"] = entraEnabled.ToString();
            if (TryNormalizeNonEmpty(document.EntraTenantId, out var tenantId))
                overrides["AzureAd:TenantId"] = tenantId;
            if (TryNormalizeNonEmpty(document.EntraClientId, out var clientId))
                overrides["AzureAd:ClientId"] = clientId;
            if (TryNormalizeNonEmpty(document.EntraClientSecret, out var clientSecret))
                overrides["AzureAd:ClientSecret"] = clientSecret;
            if (TryNormalizeNonEmpty(document.PasswordResetSigningKey, out var signingKey))
                overrides["JwtSettings:SecretKey"] = signingKey;
            if (
                TryNormalizeNonEmpty(
                    document.PreviousPasswordResetSigningKey,
                    out var previousSigningKey
                )
                && document.PreviousPasswordResetSigningKeyExpiresAtUtc is { } previousExpiresAtUtc
            )
            {
                overrides["SystemSettings:PreviousPasswordResetSigningKey"] = previousSigningKey;
                overrides["SystemSettings:PreviousPasswordResetSigningKeyExpiresAtUtc"] =
                    previousExpiresAtUtc.ToString("O");
            }
            if (document.UpdatedAtUtc is { } updatedAtUtc)
                overrides["SystemSettings:AppliedConfigurationUpdatedAtUtc"] =
                    updatedAtUtc.ToString("O");

            if (overrides.Count > 0)
                configuration.AddInMemoryCollection(overrides);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // An empty, configured vault is a valid bootstrap state.
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"FIS system configuration could not be loaded from Azure Key Vault ({exception.GetType().Name}); deployment configuration remains active."
            );
        }
    }

    public async Task<SystemConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var environment = ReadEnvironmentConfiguration();

        if (_secretClient is null)
        {
            return CreateStatus(
                Resolve(environment, null),
                restartRequired: false,
                configurationManagementAvailable: false,
                configurationManagementDescription: "Azure Key Vault is not configured. Deployment configuration values are active and system configuration is read-only."
            );
        }

        var secret = await ReadSecretDocumentAsync(cancellationToken);
        if (!secret.Operational)
        {
            return CreateStatus(
                Resolve(environment, null),
                restartRequired: false,
                configurationManagementAvailable: false,
                configurationManagementDescription: "Azure Key Vault is unavailable. Deployment configuration values remain active and system configuration is read-only."
            );
        }

        return CreateStatus(
            Resolve(environment, secret.Document),
            restartRequired: RequiresRestart(secret.Document),
            configurationManagementAvailable: true,
            configurationManagementDescription: "Azure Key Vault is available for secure system configuration management."
        );
    }

    public async Task<SystemConfigurationUpdateResult> UpdateAsync(
        SystemConfigurationUpdate update,
        string actor,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(update);

        if (_secretClient is null)
        {
            return Unavailable(
                "Azure Key Vault is required for system configuration changes. No values were changed."
            );
        }

        if (!TryValidateUpdate(update, out var validationError))
        {
            return Rejected(validationError!);
        }

        var secret = await ReadSecretDocumentAsync(cancellationToken);
        if (!secret.Operational)
        {
            return Unavailable(
                "Azure Key Vault is unavailable. No system configuration changes were saved."
            );
        }

        var environment = ReadEnvironmentConfiguration();
        var existingConfiguration = Resolve(environment, secret.Document);
        var document = secret.Document ?? new SystemConfigurationDocument();
        var changedSettings = new List<string>();
        ApplyUpdate(
            document,
            update,
            existingConfiguration.PasswordResetSigningKey,
            GetPasswordResetTokenLifetime(),
            changedSettings
        );

        if (changedSettings.Count == 0)
        {
            return Rejected("Provide at least one system configuration value to update.");
        }

        string serializedDocument;
        try
        {
            document.UpdatedAtUtc = DateTimeOffset.UtcNow;
            serializedDocument = JsonSerializer.Serialize(document, JsonOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "System configuration could not be serialized; no values were saved."
            );
            return Unavailable(
                "System configuration could not be prepared for secure storage. No values were changed."
            );
        }

        if (Encoding.UTF8.GetByteCount(serializedDocument) > MaximumSecretDocumentBytes)
        {
            return Rejected(
                "The secure system configuration is too large for Azure Key Vault. Shorten a configuration value and try again."
            );
        }

        try
        {
            await _secretClient.SetSecretAsync(
                ConfigurationSecretName,
                serializedDocument,
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RequestFailedException exception)
        {
            _logger.LogWarning(
                "System configuration update could not be stored in Azure Key Vault. Status: {Status}",
                exception.Status
            );
            return Unavailable(
                "Azure Key Vault is unavailable. No system configuration changes were saved."
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "System configuration update failed without saving a configuration value."
            );
            return Unavailable(
                "System configuration could not be saved. No system configuration changes were applied."
            );
        }

        var metadata = new SystemConfigurationUpdateMetadata(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "unknown-actor" : actor.Trim();
        var changedSettingArray = changedSettings.ToArray();

        try
        {
            await _auditSink.RecordAsync(
                new SystemConfigurationAuditEntry(
                    changedSettingArray,
                    normalizedActor,
                    SystemConfigurationSource.KeyVault,
                    metadata
                ),
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "System configuration update {UpdateId} succeeded, but its audit sink was canceled.",
                metadata.UpdateId
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "System configuration update {UpdateId} succeeded, but its audit sink failed.",
                metadata.UpdateId
            );
        }

        _logger.LogInformation(
            "System configuration update {UpdateId} completed through Azure Key Vault by {Actor}. Settings: {Settings}",
            metadata.UpdateId,
            normalizedActor,
            string.Join(',', changedSettingArray)
        );

        return new SystemConfigurationUpdateResult(
            SystemConfigurationUpdateStatus.Updated,
            true,
            "System configuration was stored securely.",
            changedSettingArray,
            metadata
        );
    }

    private static SecretClient? CreateSecretClient(string? configuredUri)
    {
        var keyVaultUri = configuredUri?.Trim();
        if (
            !Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var vaultUri)
            || vaultUri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(vaultUri.Host)
        )
        {
            return null;
        }

        return new SecretClient(
            vaultUri,
            new DefaultAzureCredential(
                new DefaultAzureCredentialOptions { ExcludeInteractiveBrowserCredential = true }
            )
        );
    }

    private async Task<SecretReadResult> ReadSecretDocumentAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _secretClient!.GetSecretAsync(
                ConfigurationSecretName,
                cancellationToken: cancellationToken
            );
            var value = response.Value.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                return new SecretReadResult(true, null);
            }

            var document = JsonSerializer.Deserialize<SystemConfigurationDocument>(
                value,
                JsonOptions
            );
            if (document is not null && !TryValidateDocument(document, out var validationError))
            {
                _logger.LogError(
                    "The existing Azure Key Vault system configuration is invalid: {Reason}",
                    validationError
                );
                return new SecretReadResult(false, null);
            }
            return new SecretReadResult(true, document);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return new SecretReadResult(true, null);
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "The existing Azure Key Vault system configuration is invalid and was not overwritten."
            );
            return new SecretReadResult(false, null);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogWarning(
                "System configuration Azure Key Vault read failed. Status: {Status}",
                exception.Status
            );
            return new SecretReadResult(false, null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "System configuration Azure Key Vault read failed. Deployment configuration remains the fallback."
            );
            return new SecretReadResult(false, null);
        }
    }

    private SystemConfigurationEnvironmentConfiguration ReadEnvironmentConfiguration()
    {
        var sessionRefreshLifetimeMinutes = ReadFirstValidMinutes(
            [
                "SystemSettings:SessionRefreshLifetimeMinutes",
                "AuthenticationSettings:SessionRefreshLifetimeMinutes",
                "JwtSettings:RefreshTokenLifetimeMinutes",
                "JwtSettings:ExpirationMinutes",
            ],
            DefaultSessionRefreshLifetimeMinutes
        );
        var rememberRefreshLifetimeMinutes = ReadFirstValidMinutes(
            [
                "SystemSettings:RememberRefreshLifetimeMinutes",
                "AuthenticationSettings:RememberRefreshLifetimeMinutes",
                "JwtSettings:RememberRefreshLifetimeMinutes",
            ],
            DefaultRememberRefreshLifetimeMinutes
        );

        var entraTenantId = NullIfWhitespace(_configuration["AzureAd:TenantId"]);
        var entraClientId = NullIfWhitespace(_configuration["AzureAd:ClientId"]);
        var entraClientSecret = NullIfWhitespace(_configuration["AzureAd:ClientSecret"]);
        var configuredEntraEnabled =
            ReadBoolean("SystemSettings:EntraEnabled")
            ?? ReadBoolean("AuthenticationSettings:EntraEnabled")
            ?? ReadBoolean("AzureAd:Enabled");
        var entraEnabled =
            configuredEntraEnabled
            ?? (
                !string.IsNullOrWhiteSpace(entraTenantId)
                && !string.IsNullOrWhiteSpace(entraClientId)
                && !string.IsNullOrWhiteSpace(entraClientSecret)
            );

        return new SystemConfigurationEnvironmentConfiguration(
            sessionRefreshLifetimeMinutes,
            rememberRefreshLifetimeMinutes,
            entraEnabled,
            entraTenantId,
            entraClientId,
            entraClientSecret,
            NullIfWhitespace(_configuration["JwtSettings:SecretKey"])
        );
    }

    private static ResolvedSystemConfiguration Resolve(
        SystemConfigurationEnvironmentConfiguration environment,
        SystemConfigurationDocument? overrides
    )
    {
        var keyVaultValueCount = 0;
        var fallbackValueCount = 0;

        var sessionRefreshLifetimeMinutes = environment.SessionRefreshLifetimeMinutes;
        if (
            overrides?.SessionRefreshLifetimeMinutes is int sessionOverride
            && IsValidRefreshLifetimeMinutes(sessionOverride)
        )
        {
            sessionRefreshLifetimeMinutes = sessionOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var rememberRefreshLifetimeMinutes = environment.RememberRefreshLifetimeMinutes;
        if (
            overrides?.RememberRefreshLifetimeMinutes is int rememberOverride
            && IsValidRefreshLifetimeMinutes(rememberOverride)
        )
        {
            rememberRefreshLifetimeMinutes = rememberOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var entraEnabled = environment.EntraEnabled;
        if (overrides?.EntraEnabled is bool entraEnabledOverride)
        {
            entraEnabled = entraEnabledOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var entraTenantId = environment.EntraTenantId;
        if (TryNormalizeNonEmpty(overrides?.EntraTenantId, out var tenantOverride))
        {
            entraTenantId = tenantOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var entraClientId = environment.EntraClientId;
        if (TryNormalizeNonEmpty(overrides?.EntraClientId, out var clientOverride))
        {
            entraClientId = clientOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var entraClientSecret = environment.EntraClientSecret;
        if (TryNormalizeNonEmpty(overrides?.EntraClientSecret, out var clientSecretOverride))
        {
            entraClientSecret = clientSecretOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var passwordResetSigningKey = environment.PasswordResetSigningKey;
        if (
            TryNormalizeNonEmpty(
                overrides?.PasswordResetSigningKey,
                out var passwordResetSigningKeyOverride
            )
        )
        {
            passwordResetSigningKey = passwordResetSigningKeyOverride;
            keyVaultValueCount++;
        }
        else
        {
            fallbackValueCount++;
        }

        var source = keyVaultValueCount switch
        {
            0 => SystemConfigurationSource.Environment,
            _ when fallbackValueCount == 0 => SystemConfigurationSource.KeyVault,
            _ => SystemConfigurationSource.Mixed,
        };

        return new ResolvedSystemConfiguration(
            TimeSpan.FromMinutes(sessionRefreshLifetimeMinutes),
            TimeSpan.FromMinutes(rememberRefreshLifetimeMinutes),
            entraEnabled,
            entraTenantId,
            entraClientId,
            !string.IsNullOrWhiteSpace(entraClientSecret),
            !string.IsNullOrWhiteSpace(passwordResetSigningKey),
            passwordResetSigningKey,
            source
        );
    }

    private static SystemConfigurationStatus CreateStatus(
        ResolvedSystemConfiguration resolved,
        bool restartRequired,
        bool configurationManagementAvailable,
        string configurationManagementDescription
    ) =>
        new(
            resolved.SessionRefreshLifetime,
            resolved.RememberRefreshLifetime,
            resolved.EntraEnabled,
            resolved.EntraTenantId,
            resolved.EntraClientId,
            resolved.EntraClientSecretConfigured,
            resolved.PasswordResetSigningKeyConfigured,
            restartRequired,
            resolved.ConfigurationSource,
            configurationManagementAvailable,
            configurationManagementDescription,
            DateTimeOffset.UtcNow
        );

    private static void ApplyUpdate(
        SystemConfigurationDocument document,
        SystemConfigurationUpdate update,
        string? currentPasswordResetSigningKey,
        TimeSpan passwordResetTokenLifetime,
        ICollection<string> changedSettings
    )
    {
        if (update.StandardRefreshLifetime is { } standardRefreshLifetime)
        {
            document.SessionRefreshLifetimeMinutes = (int)standardRefreshLifetime.TotalMinutes;
            changedSettings.Add(StandardRefreshLifetimeSetting);
        }

        if (update.RememberRefreshLifetime is { } rememberRefreshLifetime)
        {
            document.RememberRefreshLifetimeMinutes = (int)rememberRefreshLifetime.TotalMinutes;
            changedSettings.Add(RememberRefreshLifetimeSetting);
        }

        if (update.EntraEnabled is { } entraEnabled)
        {
            document.EntraEnabled = entraEnabled;
            changedSettings.Add(EntraEnabledSetting);
        }

        if (update.EntraTenantId is not null)
        {
            document.EntraTenantId = update.EntraTenantId.Trim();
            changedSettings.Add(EntraTenantIdSetting);
        }

        if (update.EntraClientId is not null)
        {
            document.EntraClientId = update.EntraClientId.Trim();
            changedSettings.Add(EntraClientIdSetting);
        }

        if (update.EntraClientSecret is not null)
        {
            document.EntraClientSecret = update.EntraClientSecret.Trim();
            changedSettings.Add(EntraClientSecretSetting);
        }

        if (update.PasswordResetSigningKey is not null)
        {
            var nextSigningKey = update.PasswordResetSigningKey.Trim();
            if (
                !string.Equals(
                    currentPasswordResetSigningKey,
                    nextSigningKey,
                    StringComparison.Ordinal
                ) && !string.IsNullOrWhiteSpace(currentPasswordResetSigningKey)
            )
            {
                document.PreviousPasswordResetSigningKey = currentPasswordResetSigningKey;
                document.PreviousPasswordResetSigningKeyExpiresAtUtc = DateTimeOffset.UtcNow.Add(
                    passwordResetTokenLifetime
                );
            }

            document.PasswordResetSigningKey = nextSigningKey;
            changedSettings.Add(PasswordResetSigningKeySetting);
        }
    }

    private static bool TryValidateUpdate(SystemConfigurationUpdate update, out string? error)
    {
        error = null;

        if (
            update.StandardRefreshLifetime is { } standardRefreshLifetime
            && !IsValidRefreshLifetime(standardRefreshLifetime)
        )
        {
            error =
                "Session refresh lifetime must be between 15 minutes and 30 days in whole minutes.";
            return false;
        }

        if (
            update.RememberRefreshLifetime is { } rememberRefreshLifetime
            && !IsValidRefreshLifetime(rememberRefreshLifetime)
        )
        {
            error =
                "Remember refresh lifetime must be between 15 minutes and 30 days in whole minutes.";
            return false;
        }

        if (
            !TryValidateIdentifier(update.EntraTenantId, "Microsoft Entra tenant ID", out error)
            || !TryValidateIdentifier(update.EntraClientId, "Microsoft Entra client ID", out error)
            || !TryValidateSecret(
                update.EntraClientSecret,
                "Microsoft Entra client secret",
                out error
            )
            || !TryValidateSecret(
                update.PasswordResetSigningKey,
                "password-reset signing key",
                out error
            )
        )
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateIdentifier(string? value, string label, out string? error)
    {
        error = null;
        if (value is null)
            return true;

        var normalized = value.Trim();
        if (
            normalized.Length == 0
            || normalized.Length > MaximumIdentifierLength
            || normalized.Any(char.IsControl)
        )
        {
            error = $"{label} must contain between 1 and {MaximumIdentifierLength} characters.";
            return false;
        }

        return true;
    }

    private static bool TryValidateSecret(string? value, string label, out string? error)
    {
        error = null;
        if (value is null)
            return true;

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > MaximumSecretLength)
        {
            error = $"{label} must contain between 1 and {MaximumSecretLength} characters.";
            return false;
        }

        return true;
    }

    private static bool IsValidRefreshLifetime(TimeSpan value) =>
        value >= TimeSpan.FromMinutes(MinimumRefreshLifetimeMinutes)
        && value <= TimeSpan.FromMinutes(MaximumRefreshLifetimeMinutes)
        && value.Ticks % TimeSpan.TicksPerMinute == 0;

    private static bool IsValidRefreshLifetimeMinutes(int value) =>
        value >= MinimumRefreshLifetimeMinutes && value <= MaximumRefreshLifetimeMinutes;

    private static bool TryValidateDocument(SystemConfigurationDocument document, out string? error)
    {
        error = null;

        if (
            document.SessionRefreshLifetimeMinutes is int sessionLifetime
            && !IsValidRefreshLifetimeMinutes(sessionLifetime)
        )
        {
            error = "The stored session refresh lifetime is outside the supported range.";
            return false;
        }

        if (
            document.RememberRefreshLifetimeMinutes is int rememberLifetime
            && !IsValidRefreshLifetimeMinutes(rememberLifetime)
        )
        {
            error = "The stored remember refresh lifetime is outside the supported range.";
            return false;
        }

        return TryValidateIdentifier(document.EntraTenantId, "Microsoft Entra tenant ID", out error)
            && TryValidateIdentifier(document.EntraClientId, "Microsoft Entra client ID", out error)
            && TryValidateSecret(
                document.EntraClientSecret,
                "Microsoft Entra client secret",
                out error
            )
            && TryValidateSecret(
                document.PasswordResetSigningKey,
                "password-reset signing key",
                out error
            )
            && TryValidateSecret(
                document.PreviousPasswordResetSigningKey,
                "previous password-reset signing key",
                out error
            );
    }

    private static bool HasKeyVaultOverrides(SystemConfigurationDocument? document) =>
        document is not null
        && (
            document.SessionRefreshLifetimeMinutes.HasValue
            || document.RememberRefreshLifetimeMinutes.HasValue
            || document.EntraEnabled.HasValue
            || !string.IsNullOrWhiteSpace(document.EntraTenantId)
            || !string.IsNullOrWhiteSpace(document.EntraClientId)
            || !string.IsNullOrWhiteSpace(document.EntraClientSecret)
            || !string.IsNullOrWhiteSpace(document.PasswordResetSigningKey)
            || !string.IsNullOrWhiteSpace(document.PreviousPasswordResetSigningKey)
        );

    private bool RequiresRestart(SystemConfigurationDocument? document)
    {
        if (document?.UpdatedAtUtc is not { } updatedAtUtc)
            return false;

        return !DateTimeOffset.TryParse(
                _configuration["SystemSettings:AppliedConfigurationUpdatedAtUtc"],
                out var appliedAtUtc
            )
            || appliedAtUtc < updatedAtUtc;
    }

    private TimeSpan GetPasswordResetTokenLifetime()
    {
        var configuredMinutes = _configuration.GetValue<int?>(
            "EmailSettings:PasswordResetTokenLifetimeMinutes"
        );
        return TimeSpan.FromMinutes(Math.Clamp(configuredMinutes ?? 30, 5, 1440));
    }

    private int ReadFirstValidMinutes(IReadOnlyList<string> keys, int fallback)
    {
        foreach (var key in keys)
        {
            if (
                int.TryParse(_configuration[key], out var value)
                && IsValidRefreshLifetimeMinutes(value)
            )
            {
                return value;
            }
        }

        return fallback;
    }

    private bool? ReadBoolean(string key)
    {
        return bool.TryParse(_configuration[key], out var value) ? value : null;
    }

    private static string? NullIfWhitespace(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool TryNormalizeNonEmpty(string? value, out string? normalized)
    {
        normalized = NullIfWhitespace(value);
        return normalized is not null;
    }

    private static SystemConfigurationUpdateResult Rejected(string description) =>
        new(SystemConfigurationUpdateStatus.Rejected, true, description, Array.Empty<string>());

    private static SystemConfigurationUpdateResult Unavailable(string description) =>
        new(SystemConfigurationUpdateStatus.Unavailable, false, description, Array.Empty<string>());

    private sealed record SecretReadResult(bool Operational, SystemConfigurationDocument? Document);

    private sealed record SystemConfigurationEnvironmentConfiguration(
        int SessionRefreshLifetimeMinutes,
        int RememberRefreshLifetimeMinutes,
        bool EntraEnabled,
        string? EntraTenantId,
        string? EntraClientId,
        string? EntraClientSecret,
        string? PasswordResetSigningKey
    );

    private sealed record ResolvedSystemConfiguration(
        TimeSpan SessionRefreshLifetime,
        TimeSpan RememberRefreshLifetime,
        bool EntraEnabled,
        string? EntraTenantId,
        string? EntraClientId,
        bool EntraClientSecretConfigured,
        bool PasswordResetSigningKeyConfigured,
        string? PasswordResetSigningKey,
        SystemConfigurationSource ConfigurationSource
    );

    /// <summary>
    /// The only values accepted from the dedicated Key Vault configuration
    /// document. Unknown JSON properties are ignored and never copied through.
    /// </summary>
    private sealed class SystemConfigurationDocument
    {
        public int? SessionRefreshLifetimeMinutes { get; set; }
        public int? RememberRefreshLifetimeMinutes { get; set; }
        public bool? EntraEnabled { get; set; }
        public string? EntraTenantId { get; set; }
        public string? EntraClientId { get; set; }
        public string? EntraClientSecret { get; set; }
        public string? PasswordResetSigningKey { get; set; }
        public string? PreviousPasswordResetSigningKey { get; set; }
        public DateTimeOffset? PreviousPasswordResetSigningKeyExpiresAtUtc { get; set; }
        public DateTimeOffset? UpdatedAtUtc { get; set; }
    }
}
