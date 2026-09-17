using System.Text;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using FIS.Core.Application.Interfaces.EmailDelivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services.EmailDelivery;

/// <summary>
/// Resolves the effective mail configuration. Deployment environment values are
/// always the bootstrap fallback; Key Vault is the only writable runtime store.
/// </summary>
public sealed class EmailDeliveryConfigurationStore : IDisposable
{
    private const string ConfigurationSecretName = "fis-email-delivery-config";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailDeliveryConfigurationStore> _logger;
    private readonly SecretClient? _secretClient;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private EffectiveEmailDeliveryConfiguration? _cached;
    private DateTimeOffset _cacheExpiresAtUtc = DateTimeOffset.MinValue;

    public EmailDeliveryConfigurationStore(
        IConfiguration configuration,
        ILogger<EmailDeliveryConfigurationStore> logger
    )
    {
        _configuration = configuration;
        _logger = logger;
        var keyVaultUri = configuration["SystemSettings:KeyVaultUri"]?.Trim();
        if (
            Uri.TryCreate(keyVaultUri, UriKind.Absolute, out var vaultUri)
            && vaultUri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrWhiteSpace(vaultUri.Host)
        )
        {
            _secretClient = new SecretClient(
                vaultUri,
                new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions { ExcludeInteractiveBrowserCredential = true }
                )
            );
        }
        else if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            _logger.LogWarning(
                "Email configuration Key Vault URI is invalid; environment configuration remains active."
            );
        }
    }

    public bool CanManageConfiguration => _secretClient is not null;

    public async Task<EffectiveEmailDeliveryConfiguration> GetEffectiveAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (_cached is not null && DateTimeOffset.UtcNow < _cacheExpiresAtUtc)
            return _cached;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow < _cacheExpiresAtUtc)
                return _cached;

            var environment = ReadEnvironment();
            if (_secretClient is null)
            {
                _cached = environment;
                _cacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(CacheLifetime);
                return _cached;
            }

            try
            {
                var document = await GetSecretValueAsync(
                    ConfigurationSecretName,
                    cancellationToken
                );
                var overrides = string.IsNullOrWhiteSpace(document)
                    ? null
                    : JsonSerializer.Deserialize<KeyVaultEmailDeliveryConfiguration>(document);
                _cached = Merge(environment, overrides);
                _cacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(CacheLifetime);
                return _cached;
            }
            catch (RequestFailedException exception)
            {
                _logger.LogWarning(
                    "Email configuration Key Vault could not be read. Environment configuration remains active. Status: {Status}",
                    exception.Status
                );
                _cached = environment with { KeyVaultOperational = false };
                _cacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(20));
                return _cached;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Email configuration Key Vault could not be read. Environment configuration remains active."
                );
                _cached = environment with { KeyVaultOperational = false };
                _cacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(20));
                return _cached;
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<EmailConfigurationUpdateResult> UpdateAsync(
        EmailDeliveryConfigurationUpdate update,
        string actorUserId,
        CancellationToken cancellationToken = default
    )
    {
        if (_secretClient is null)
        {
            return new EmailConfigurationUpdateResult(
                EmailConfigurationUpdateStatus.Unavailable,
                false,
                "Runtime configuration requires Azure Key Vault. Deployment environment values remain active.",
                [],
                []
            );
        }

        try
        {
            var changedProviders = new List<EmailProvider>();
            var changedFields = new List<string>();
            var existingDocument = await GetSecretValueAsync(
                ConfigurationSecretName,
                cancellationToken
            );
            var document = string.IsNullOrWhiteSpace(existingDocument)
                ? new KeyVaultEmailDeliveryConfiguration()
                : JsonSerializer.Deserialize<KeyVaultEmailDeliveryConfiguration>(existingDocument)
                    ?? new KeyVaultEmailDeliveryConfiguration();

            if (update.ProviderOrder is not null)
            {
                var order = NormalizeProviderOrder(update.ProviderOrder);
                if (order.Count == 0)
                    return Rejected("Choose at least one email provider.");
                document.ProviderOrder = order;
                changedProviders.AddRange(order);
                changedFields.Add("providerOrder");
            }

            if (update.TimeoutSeconds.HasValue)
            {
                if (update.TimeoutSeconds is < 5 or > 120)
                    return Rejected("Email timeout must be between 5 and 120 seconds.");
                document.TimeoutSeconds = update.TimeoutSeconds.Value;
                changedFields.Add("timeoutSeconds");
            }

            if (update.MaxTotalAttachmentBytes.HasValue)
            {
                if (update.MaxTotalAttachmentBytes is < 1 or > 20 * 1024 * 1024)
                    return Rejected("Total email attachments must be between 1 byte and 20 MiB.");
                document.MaxTotalAttachmentBytes = update.MaxTotalAttachmentBytes.Value;
                changedFields.Add("maxTotalAttachmentBytes");
            }

            if (update.Graph is not null)
            {
                document.Graph ??= new KeyVaultGraphConfiguration();
                if (!ApplyGraphUpdate(document.Graph, update.Graph, changedFields, out var message))
                    return Rejected(message);
                changedProviders.Add(EmailProvider.Graph);
            }

            if (update.Smtp is not null)
            {
                document.Smtp ??= new KeyVaultSmtpConfiguration();
                if (!ApplySmtpUpdate(document.Smtp, update.Smtp, changedFields, out var message))
                    return Rejected(message);
                changedProviders.Add(EmailProvider.Smtp);
            }

            if (update.SendGrid is not null)
            {
                document.SendGrid ??= new KeyVaultSendGridConfiguration();
                if (
                    !ApplySendGridUpdate(
                        document.SendGrid,
                        update.SendGrid,
                        changedFields,
                        out var message
                    )
                )
                    return Rejected(message);
                changedProviders.Add(EmailProvider.SendGrid);
            }

            if (changedFields.Count == 0)
                return Rejected("Provide at least one email configuration value to update.");

            document.ProviderVersions ??= new();
            document.VerifiedProviderVersions ??= new();
            foreach (var provider in changedProviders.Distinct())
            {
                if (
                    !changedFields.Contains("providerOrder")
                    && !changedFields.Any(field =>
                        field.StartsWith(
                            $"{provider.ToString().ToLowerInvariant()}.",
                            StringComparison.Ordinal
                        )
                    )
                )
                    continue;

                document.ProviderVersions.TryGetValue(provider, out var version);
                document.ProviderVersions[provider] = checked(version + 1);
                document.VerifiedProviderVersions.Remove(provider);
            }

            var serializedDocument = JsonSerializer.Serialize(document);
            if (Encoding.UTF8.GetByteCount(serializedDocument) > 25_000)
            {
                return Rejected(
                    "The encrypted email configuration is too large for Azure Key Vault. Shorten a configuration value and try again."
                );
            }
            await SetSecretAsync(ConfigurationSecretName, serializedDocument, cancellationToken);
            Invalidate();
            _logger.LogInformation(
                "Email configuration updated through Key Vault by {ActorUserId}. Providers: {Providers}; fields: {Fields}",
                actorUserId,
                string.Join(',', changedProviders.Distinct()),
                string.Join(',', changedFields)
            );
            return new EmailConfigurationUpdateResult(
                EmailConfigurationUpdateStatus.Updated,
                true,
                "Email configuration was stored securely. Test each changed provider before it becomes active.",
                changedProviders.Distinct().ToArray(),
                changedFields
            );
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "The existing Key Vault email configuration is invalid and was not overwritten."
            );
            return new EmailConfigurationUpdateResult(
                EmailConfigurationUpdateStatus.Unavailable,
                false,
                "The existing secure email configuration is invalid. Restore it from Azure Key Vault before saving changes.",
                [],
                []
            );
        }
        catch (RequestFailedException exception)
        {
            _logger.LogWarning(
                "Email configuration update could not be stored in Key Vault. Status: {Status}",
                exception.Status
            );
            return new EmailConfigurationUpdateResult(
                EmailConfigurationUpdateStatus.Unavailable,
                false,
                "Azure Key Vault is unavailable. No configuration changes were saved.",
                [],
                []
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Email configuration update failed without saving a configuration value."
            );
            return new EmailConfigurationUpdateResult(
                EmailConfigurationUpdateStatus.Unavailable,
                false,
                "Email configuration could not be saved. No configuration changes were applied.",
                [],
                []
            );
        }
    }

    public void Invalidate()
    {
        _cached = null;
        _cacheExpiresAtUtc = DateTimeOffset.MinValue;
    }

    public void Dispose() => _cacheLock.Dispose();

    /// <summary>
    /// Marks the current Key Vault version of a provider as activated only after
    /// that exact configuration accepted a controlled test message.
    /// </summary>
    public async Task<bool> MarkProviderTestedAsync(
        EmailProvider provider,
        int? expectedConfigurationVersion,
        string actorUserId,
        CancellationToken cancellationToken = default
    )
    {
        if (_secretClient is null)
            return true; // Deployment-owned environment configuration remains the bootstrap path.

        try
        {
            var value = await GetSecretValueAsync(ConfigurationSecretName, cancellationToken);
            if (string.IsNullOrWhiteSpace(value))
                return true;

            var document = JsonSerializer.Deserialize<KeyVaultEmailDeliveryConfiguration>(value);
            if (document is null)
                return false;

            document.ProviderVersions ??= new();
            document.VerifiedProviderVersions ??= new();
            if (!document.ProviderVersions.TryGetValue(provider, out var version))
                return true;

            if (expectedConfigurationVersion != version)
                return false;

            document.VerifiedProviderVersions[provider] = version;
            await SetSecretAsync(
                ConfigurationSecretName,
                JsonSerializer.Serialize(document),
                cancellationToken
            );
            Invalidate();
            _logger.LogInformation(
                "Email provider {Provider} configuration version {ConfigurationVersion} was activated after a controlled test by {ActorUserId}",
                provider,
                version,
                actorUserId
            );
            return true;
        }
        catch (Exception exception) when (exception is RequestFailedException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Email provider {Provider} test was accepted but its activation state could not be stored.",
                provider
            );
            return false;
        }
    }

    private static EmailConfigurationUpdateResult Rejected(string message) =>
        new(EmailConfigurationUpdateStatus.Rejected, true, message, [], []);

    private async Task<string?> GetSecretValueAsync(
        string name,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _secretClient!.GetSecretAsync(
                name,
                cancellationToken: cancellationToken
            );
            return response.Value.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private async Task SetSecretAsync(
        string name,
        string value,
        CancellationToken cancellationToken
    )
    {
        await _secretClient!.SetSecretAsync(name, value, cancellationToken);
    }

    private EffectiveEmailDeliveryConfiguration ReadEnvironment()
    {
        var delivery = _configuration.GetSection("EmailDelivery");
        var email = _configuration.GetSection("EmailSettings");
        var smtp = delivery.GetSection("Smtp");
        var sendGrid = delivery.GetSection("SendGrid");
        var legacyProvider = email["Provider"]?.Trim();
        var legacyOrder = legacyProvider?.ToLowerInvariant() switch
        {
            "smtp" => new List<EmailProvider> { EmailProvider.Smtp },
            "sendgrid" => new List<EmailProvider> { EmailProvider.SendGrid },
            "graph" or "microsoftgraph" => new List<EmailProvider> { EmailProvider.Graph },
            _ => new List<EmailProvider>(),
        };
        var order = NormalizeProviderOrder(
            delivery.GetSection("ProviderOrder").Get<string[]>() ?? []
        );
        var hasEnvironmentProviderOrder = Enumerable
            .Range(0, 16)
            .Any(index =>
                !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable($"EmailDelivery__ProviderOrder__{index}")
                )
            );
        if (
            legacyOrder.Count > 0
            && !hasEnvironmentProviderOrder
            && (order.Count == 0 || order.SequenceEqual([EmailProvider.Graph]))
        )
        {
            order = legacyOrder;
        }
        if (order.Count == 0)
        {
            order = [EmailProvider.Graph];
        }

        // The archived notification service used EmailSettings:SmtpServer,
        // SmtpPort, Username, Password, and UseSSL. Keep those names as a
        // read-only bootstrap compatibility path so a client deployment can
        // move to the provider-neutral EmailDelivery section incrementally.
        var smtpHost = FirstConfigured(smtp["Host"], email["SmtpHost"], email["SmtpServer"]);
        var smtpPort = ParsePort(FirstConfigured(smtp["Port"], email["SmtpPort"]));
        var smtpUsername = FirstConfigured(
            smtp["Username"],
            email["SmtpUsername"],
            email["Username"]
        );
        var smtpPassword = FirstConfigured(
            smtp["Password"],
            email["SmtpPassword"],
            email["Password"]
        );
        var smtpFromAddress = FirstConfigured(
            smtp["FromAddress"],
            email["SmtpFromAddress"],
            email["FromAddress"]
        );
        var smtpFromName = FirstConfigured(
            smtp["FromName"],
            email["SmtpFromName"],
            email["FromName"]
        );
        var smtpTlsMode = FirstConfigured(smtp["TlsMode"], email["SmtpTlsMode"])
            ?? (smtpPort == 465 ? "SslOnConnect" : "StartTls");
        var smtpAuthentication = FirstConfigured(
            smtp["Authentication"],
            email["SmtpAuthentication"]
        ) ?? (!string.IsNullOrWhiteSpace(smtpUsername) ? "Password" : "None");

        return new EffectiveEmailDeliveryConfiguration(
            order,
            new EffectiveGraphConfiguration(
                NormalizeGraphEndpoint(email["GraphEndpoint"]),
                ParseGraphAuthentication(email["GraphAuthentication"]),
                NullIfWhitespace(email["GraphTenantId"]),
                NullIfWhitespace(email["GraphClientId"]),
                NullIfWhitespace(email["GraphManagedIdentityClientId"]),
                NullIfWhitespace(email["GraphSenderUserPrincipalName"] ?? email["FromAddress"]),
                NullIfWhitespace(email["FromAddress"]),
                NullIfWhitespace(email["FromName"]) ?? "Fleet Information System",
                NullIfWhitespace(email["GraphClientSecret"]),
                EmailConfigurationSource.Environment
            ),
            new EffectiveSmtpConfiguration(
                smtpHost,
                smtpPort,
                ParseSmtpSecurityMode(smtpTlsMode),
                ParseSmtpAuthentication(smtpAuthentication),
                smtpUsername,
                smtpPassword,
                NullIfWhitespace(smtp["GoogleOAuthClientId"]),
                NullIfWhitespace(smtp["GoogleOAuthClientSecret"]),
                NullIfWhitespace(smtp["GoogleOAuthRefreshToken"]),
                smtpFromAddress,
                smtpFromName ?? "Fleet Information System",
                EmailConfigurationSource.Environment
            ),
            new EffectiveSendGridConfiguration(
                NullIfWhitespace(_configuration["SendGrid:FromEmail"] ?? sendGrid["FromAddress"]),
                NullIfWhitespace(_configuration["SendGrid:FromName"] ?? sendGrid["FromName"])
                    ?? "Fleet Information System",
                NullIfWhitespace(_configuration["SendGrid:ApiKey"]),
                EmailConfigurationSource.Environment
            ),
            Math.Clamp(ParsePositiveInt(delivery["TimeoutSeconds"], 30), 5, 120),
            Math.Clamp(
                ParsePositiveLong(delivery["MaxTotalAttachmentBytes"], 20 * 1024 * 1024),
                1,
                20 * 1024 * 1024
            ),
            _secretClient is not null,
            _secretClient is not null
        );
    }

    private static EffectiveEmailDeliveryConfiguration Merge(
        EffectiveEmailDeliveryConfiguration environment,
        KeyVaultEmailDeliveryConfiguration? overrides
    )
    {
        if (overrides is null)
            return environment with { KeyVaultOperational = true };

        var graphOverrides = overrides.Graph ?? new KeyVaultGraphConfiguration();
        var smtpOverrides = overrides.Smtp ?? new KeyVaultSmtpConfiguration();
        var sendGridOverrides = overrides.SendGrid ?? new KeyVaultSendGridConfiguration();
        var graph = environment.Graph with
        {
            Endpoint = NormalizeGraphEndpoint(
                graphOverrides.Endpoint ?? environment.Graph.Endpoint
            ),
            Authentication =
                graphOverrides.Authentication is { } graphAuthentication
                && Enum.IsDefined(graphAuthentication)
                    ? graphAuthentication
                    : environment.Graph.Authentication,
            TenantId = graphOverrides.TenantId ?? environment.Graph.TenantId,
            ClientId = graphOverrides.ClientId ?? environment.Graph.ClientId,
            ManagedIdentityClientId =
                graphOverrides.ManagedIdentityClientId ?? environment.Graph.ManagedIdentityClientId,
            SenderUserPrincipalName =
                graphOverrides.SenderUserPrincipalName ?? environment.Graph.SenderUserPrincipalName,
            FromAddress = graphOverrides.FromAddress ?? environment.Graph.FromAddress,
            FromName = graphOverrides.FromName ?? environment.Graph.FromName,
            ClientSecret = graphOverrides.ClientSecret ?? environment.Graph.ClientSecret,
            Source = ResolveSource(true, !string.IsNullOrWhiteSpace(graphOverrides.ClientSecret)),
        };
        var smtp = environment.Smtp with
        {
            Host = smtpOverrides.Host ?? environment.Smtp.Host,
            Port = smtpOverrides.Port ?? environment.Smtp.Port,
            SecurityMode =
                smtpOverrides.SecurityMode is { } smtpSecurityMode
                && Enum.IsDefined(smtpSecurityMode)
                    ? smtpSecurityMode
                    : environment.Smtp.SecurityMode,
            Authentication =
                smtpOverrides.Authentication is { } smtpAuthentication
                && Enum.IsDefined(smtpAuthentication)
                    ? smtpAuthentication
                    : environment.Smtp.Authentication,
            Username = smtpOverrides.Username ?? environment.Smtp.Username,
            FromAddress = smtpOverrides.FromAddress ?? environment.Smtp.FromAddress,
            FromName = smtpOverrides.FromName ?? environment.Smtp.FromName,
            Password = smtpOverrides.Password ?? environment.Smtp.Password,
            GoogleOAuthClientId =
                smtpOverrides.GoogleOAuthClientId ?? environment.Smtp.GoogleOAuthClientId,
            GoogleOAuthClientSecret =
                smtpOverrides.GoogleOAuthClientSecret ?? environment.Smtp.GoogleOAuthClientSecret,
            GoogleOAuthRefreshToken =
                smtpOverrides.GoogleOAuthRefreshToken ?? environment.Smtp.GoogleOAuthRefreshToken,
            Source = ResolveSource(true, !string.IsNullOrWhiteSpace(smtpOverrides.Password)),
        };
        var sendGrid = environment.SendGrid with
        {
            FromEmail = sendGridOverrides.FromEmail ?? environment.SendGrid.FromEmail,
            FromName = sendGridOverrides.FromName ?? environment.SendGrid.FromName,
            ApiKey = sendGridOverrides.ApiKey ?? environment.SendGrid.ApiKey,
            Source = ResolveSource(true, !string.IsNullOrWhiteSpace(sendGridOverrides.ApiKey)),
        };

        return environment with
        {
            ProviderOrder = GetProviderOrder(overrides.ProviderOrder, environment.ProviderOrder),
            TimeoutSeconds = overrides?.TimeoutSeconds ?? environment.TimeoutSeconds,
            MaxTotalAttachmentBytes =
                overrides?.MaxTotalAttachmentBytes ?? environment.MaxTotalAttachmentBytes,
            Graph = graph,
            Smtp = smtp,
            SendGrid = sendGrid,
            ProviderVersions = overrides?.ProviderVersions ?? new(),
            VerifiedProviderVersions = overrides?.VerifiedProviderVersions ?? new(),
            KeyVaultOperational = true,
        };
    }

    private static EmailConfigurationSource ResolveSource(
        bool hasDocumentOverride,
        bool hasSecretOverride
    ) =>
        hasDocumentOverride && hasSecretOverride ? EmailConfigurationSource.KeyVault
        : hasDocumentOverride || hasSecretOverride ? EmailConfigurationSource.Mixed
        : EmailConfigurationSource.Environment;

    private static bool ApplyGraphUpdate(
        KeyVaultGraphConfiguration target,
        EmailGraphConfigurationUpdate update,
        List<string> changedFields,
        out string message
    )
    {
        message = string.Empty;
        if (
            !TryValidateLength(update.Endpoint, "Microsoft Graph endpoint", 200, out message)
            || !TryValidateLength(update.TenantId, "Microsoft Entra tenant ID", 200, out message)
            || !TryValidateLength(update.ClientId, "Microsoft Entra client ID", 200, out message)
            || !TryValidateLength(
                update.ManagedIdentityClientId,
                "managed identity client ID",
                200,
                out message
            )
            || !TryValidateLength(
                update.SenderUserPrincipalName,
                "sender mailbox",
                320,
                out message
            )
            || !TryValidateLength(update.FromAddress, "from address", 320, out message)
            || !TryValidateLength(update.FromName, "from name", 120, out message)
            || !TryValidateLength(
                update.ClientSecret,
                "Microsoft Graph client secret",
                4096,
                out message
            )
        )
            return false;

        if (update.Endpoint is not null)
        {
            var endpoint = NormalizeGraphEndpoint(update.Endpoint);
            if (endpoint is null)
            {
                message = "Microsoft Graph endpoint must be https://graph.microsoft.com/v1.0.";
                return false;
            }
            target.Endpoint = endpoint;
            changedFields.Add("graph.endpoint");
        }
        if (update.Authentication.HasValue)
        {
            target.Authentication = update.Authentication.Value;
            changedFields.Add("graph.authentication");
        }
        if (update.TenantId is not null)
        {
            target.TenantId = NullIfWhitespace(update.TenantId);
            changedFields.Add("graph.tenantId");
        }
        if (update.ClientId is not null)
        {
            target.ClientId = NullIfWhitespace(update.ClientId);
            changedFields.Add("graph.clientId");
        }
        if (update.ManagedIdentityClientId is not null)
        {
            target.ManagedIdentityClientId = NullIfWhitespace(update.ManagedIdentityClientId);
            changedFields.Add("graph.managedIdentityClientId");
        }
        if (update.SenderUserPrincipalName is not null)
        {
            target.SenderUserPrincipalName = NullIfWhitespace(update.SenderUserPrincipalName);
            changedFields.Add("graph.senderUserPrincipalName");
        }
        if (update.FromAddress is not null)
        {
            target.FromAddress = NullIfWhitespace(update.FromAddress);
            changedFields.Add("graph.fromAddress");
        }
        if (update.FromName is not null)
        {
            target.FromName = NullIfWhitespace(update.FromName);
            changedFields.Add("graph.fromName");
        }
        if (!string.IsNullOrWhiteSpace(update.ClientSecret))
        {
            target.ClientSecret = update.ClientSecret.Trim();
            changedFields.Add("graph.clientSecret");
        }
        return true;
    }

    private static bool ApplySmtpUpdate(
        KeyVaultSmtpConfiguration target,
        EmailSmtpConfigurationUpdate update,
        List<string> changedFields,
        out string message
    )
    {
        message = string.Empty;
        if (
            !TryValidateLength(update.Host, "SMTP host", 253, out message)
            || !TryValidateLength(update.Username, "SMTP username", 320, out message)
            || !TryValidateLength(update.Password, "SMTP password", 4096, out message)
            || !TryValidateLength(update.FromAddress, "SMTP from address", 320, out message)
            || !TryValidateLength(update.FromName, "SMTP from name", 120, out message)
        )
            return false;
        if (update.Port is not null && update.Port is not (465 or 587))
        {
            message = "SMTP must use the TLS-only port 465 or 587.";
            return false;
        }
        if (update.Host is not null && !IsDnsHost(update.Host))
        {
            message = "SMTP host must be a valid DNS host name, not an IP address or local host.";
            return false;
        }
        if (update.Host is not null)
        {
            target.Host = NullIfWhitespace(update.Host);
            changedFields.Add("smtp.host");
        }
        if (update.Port.HasValue)
        {
            target.Port = update.Port.Value;
            changedFields.Add("smtp.port");
        }
        if (update.SecurityMode.HasValue)
        {
            target.SecurityMode = update.SecurityMode.Value;
            changedFields.Add("smtp.securityMode");
        }
        if (update.Authentication.HasValue)
        {
            target.Authentication = update.Authentication.Value;
            changedFields.Add("smtp.authentication");
        }
        if (update.Username is not null)
        {
            target.Username = NullIfWhitespace(update.Username);
            changedFields.Add("smtp.username");
        }
        if (update.FromAddress is not null)
        {
            target.FromAddress = NullIfWhitespace(update.FromAddress);
            changedFields.Add("smtp.fromAddress");
        }
        if (update.FromName is not null)
        {
            target.FromName = NullIfWhitespace(update.FromName);
            changedFields.Add("smtp.fromName");
        }
        if (!string.IsNullOrWhiteSpace(update.Password))
        {
            target.Password = update.Password;
            changedFields.Add("smtp.password");
        }
        if (
            !TryValidateLength(
                update.GoogleOAuthClientId,
                "Google OAuth client ID",
                200,
                out message
            )
            || !TryValidateLength(
                update.GoogleOAuthClientSecret,
                "Google OAuth client secret",
                4096,
                out message
            )
            || !TryValidateLength(
                update.GoogleOAuthRefreshToken,
                "Google OAuth refresh token",
                4096,
                out message
            )
        )
            return false;
        if (update.GoogleOAuthClientId is not null)
        {
            target.GoogleOAuthClientId = NullIfWhitespace(update.GoogleOAuthClientId);
            changedFields.Add("smtp.googleOAuthClientId");
        }
        if (!string.IsNullOrWhiteSpace(update.GoogleOAuthClientSecret))
        {
            target.GoogleOAuthClientSecret = update.GoogleOAuthClientSecret.Trim();
            changedFields.Add("smtp.googleOAuthClientSecret");
        }
        if (!string.IsNullOrWhiteSpace(update.GoogleOAuthRefreshToken))
        {
            target.GoogleOAuthRefreshToken = update.GoogleOAuthRefreshToken.Trim();
            changedFields.Add("smtp.googleOAuthRefreshToken");
        }
        if (
            target.Authentication == EmailSmtpAuthentication.GoogleOAuth2
            && target.Host is not null
            && !target.Host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
        )
        {
            message = "Google OAuth SMTP must use smtp.gmail.com.";
            return false;
        }
        return true;
    }

    private static bool ApplySendGridUpdate(
        KeyVaultSendGridConfiguration target,
        EmailSendGridConfigurationUpdate update,
        List<string> changedFields,
        out string message
    )
    {
        message = string.Empty;
        if (
            !TryValidateLength(update.FromEmail, "SendGrid from email", 320, out message)
            || !TryValidateLength(update.FromName, "SendGrid from name", 120, out message)
            || !TryValidateLength(update.ApiKey, "SendGrid API key", 4096, out message)
        )
            return false;
        if (update.FromEmail is not null)
        {
            target.FromEmail = NullIfWhitespace(update.FromEmail);
            changedFields.Add("sendGrid.fromEmail");
        }
        if (update.FromName is not null)
        {
            target.FromName = NullIfWhitespace(update.FromName);
            changedFields.Add("sendGrid.fromName");
        }
        if (!string.IsNullOrWhiteSpace(update.ApiKey))
        {
            target.ApiKey = update.ApiKey.Trim();
            changedFields.Add("sendGrid.apiKey");
        }
        return true;
    }

    private static List<EmailProvider> NormalizeProviderOrder(IEnumerable<EmailProvider> values) =>
        values.Where(Enum.IsDefined).Distinct().ToList();

    private static IReadOnlyList<EmailProvider> GetProviderOrder(
        IEnumerable<EmailProvider>? values,
        IReadOnlyList<EmailProvider> fallback
    )
    {
        var normalized = values is null ? [] : NormalizeProviderOrder(values);
        return normalized.Count > 0 ? normalized : fallback;
    }

    private static List<EmailProvider> NormalizeProviderOrder(IEnumerable<string> values)
    {
        var result = new List<EmailProvider>();
        foreach (var value in values)
        {
            if (
                Enum.TryParse<EmailProvider>(value, true, out var provider)
                && Enum.IsDefined(provider)
                && !result.Contains(provider)
            )
                result.Add(provider);
        }
        return result;
    }

    private static string? NormalizeGraphEndpoint(string? value)
    {
        var candidate = string.IsNullOrWhiteSpace(value)
            ? "https://graph.microsoft.com/v1.0"
            : value.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return null;
        return
            uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("graph.microsoft.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.TrimEnd('/') == "/v1.0"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment)
            ? uri.ToString().TrimEnd('/')
            : null;
    }

    private static EmailGraphAuthentication ParseGraphAuthentication(string? value) =>
        value?.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "clientsecret" => EmailGraphAuthentication.ClientSecret,
            _ => EmailGraphAuthentication.ManagedIdentity,
        };

    private static EmailSmtpSecurityMode ParseSmtpSecurityMode(string? value) =>
        value?.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "sslonconnect" => EmailSmtpSecurityMode.SslOnConnect,
            _ => EmailSmtpSecurityMode.StartTls,
        };

    private static EmailSmtpAuthentication ParseSmtpAuthentication(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "none" or "relay" => EmailSmtpAuthentication.None,
            "googleoauth2" or "google-oauth2" or "googleoauth" =>
                EmailSmtpAuthentication.GoogleOAuth2,
            _ => EmailSmtpAuthentication.Password,
        };

    private static int ParsePort(string? value) =>
        int.TryParse(value, out var port) && port is 465 or 587 ? port : 587;

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static long ParsePositiveLong(string? value, long fallback) =>
        long.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstConfigured(params string?[] values) =>
        values.Select(NullIfWhitespace).FirstOrDefault(value => value is not null);

    private static bool IsDnsHost(string value)
    {
        var candidate = value.Trim();
        return candidate.Length is > 0 and <= 253
            && Uri.CheckHostName(candidate) == UriHostNameType.Dns
            && candidate.Contains('.', StringComparison.Ordinal);
    }

    private static bool TryValidateLength(
        string? value,
        string field,
        int maximumLength,
        out string message
    )
    {
        if (value?.Length > maximumLength)
        {
            message = $"{field} must not exceed {maximumLength} characters.";
            return false;
        }
        message = string.Empty;
        return true;
    }
}

public sealed record EffectiveEmailDeliveryConfiguration(
    IReadOnlyList<EmailProvider> ProviderOrder,
    EffectiveGraphConfiguration Graph,
    EffectiveSmtpConfiguration Smtp,
    EffectiveSendGridConfiguration SendGrid,
    int TimeoutSeconds,
    long MaxTotalAttachmentBytes,
    bool KeyVaultConfigured,
    bool KeyVaultOperational,
    IReadOnlyDictionary<EmailProvider, int>? ProviderVersions = null,
    IReadOnlyDictionary<EmailProvider, int>? VerifiedProviderVersions = null
)
{
    public bool IsProviderActivated(EmailProvider provider)
    {
        if (ProviderVersions is null || !ProviderVersions.TryGetValue(provider, out var version))
            return true;
        return VerifiedProviderVersions is not null
            && VerifiedProviderVersions.TryGetValue(provider, out var verifiedVersion)
            && verifiedVersion == version;
    }

    public int? GetProviderConfigurationVersion(EmailProvider provider) =>
        ProviderVersions is not null && ProviderVersions.TryGetValue(provider, out var version)
            ? version
            : null;
}

public sealed record EffectiveGraphConfiguration(
    string? Endpoint,
    EmailGraphAuthentication Authentication,
    string? TenantId,
    string? ClientId,
    string? ManagedIdentityClientId,
    string? SenderUserPrincipalName,
    string? FromAddress,
    string FromName,
    string? ClientSecret,
    EmailConfigurationSource Source
);

public sealed record EffectiveSmtpConfiguration(
    string? Host,
    int Port,
    EmailSmtpSecurityMode SecurityMode,
    EmailSmtpAuthentication Authentication,
    string? Username,
    string? Password,
    string? GoogleOAuthClientId,
    string? GoogleOAuthClientSecret,
    string? GoogleOAuthRefreshToken,
    string? FromAddress,
    string FromName,
    EmailConfigurationSource Source
);

public sealed record EffectiveSendGridConfiguration(
    string? FromEmail,
    string FromName,
    string? ApiKey,
    EmailConfigurationSource Source
);

public sealed class KeyVaultEmailDeliveryConfiguration
{
    public List<EmailProvider>? ProviderOrder { get; set; }
    public int? TimeoutSeconds { get; set; }
    public long? MaxTotalAttachmentBytes { get; set; }
    public KeyVaultGraphConfiguration? Graph { get; set; } = new();
    public KeyVaultSmtpConfiguration? Smtp { get; set; } = new();
    public KeyVaultSendGridConfiguration? SendGrid { get; set; } = new();
    public Dictionary<EmailProvider, int>? ProviderVersions { get; set; }
    public Dictionary<EmailProvider, int>? VerifiedProviderVersions { get; set; }
}

public sealed class KeyVaultGraphConfiguration
{
    public string? Endpoint { get; set; }
    public EmailGraphAuthentication? Authentication { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ManagedIdentityClientId { get; set; }
    public string? SenderUserPrincipalName { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? ClientSecret { get; set; }
}

public sealed class KeyVaultSmtpConfiguration
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public EmailSmtpSecurityMode? SecurityMode { get; set; }
    public EmailSmtpAuthentication? Authentication { get; set; }
    public string? Username { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? Password { get; set; }
    public string? GoogleOAuthClientId { get; set; }
    public string? GoogleOAuthClientSecret { get; set; }
    public string? GoogleOAuthRefreshToken { get; set; }
}

public sealed class KeyVaultSendGridConfiguration
{
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public string? ApiKey { get; set; }
}
