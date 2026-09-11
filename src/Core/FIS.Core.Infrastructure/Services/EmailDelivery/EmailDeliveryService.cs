using System.Collections.Concurrent;
using System.Net.Mail;
using FIS.Core.Application.Interfaces.EmailDelivery;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services.EmailDelivery;

/// <summary>
/// Provider-neutral email dispatcher. It keeps a short in-process circuit for
/// unavailable providers and only falls back before a message can have been accepted.
/// </summary>
public sealed class EmailDeliveryService : IEmailDeliveryService, IEmailConfigurationService
{
    private const int FailuresBeforeCircuitOpen = 3;
    private const int MaximumAttemptsPerProvider = 2;
    private static readonly TimeSpan CircuitDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecentTestDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(5);
    private readonly EmailDeliveryConfigurationStore _configurationStore;
    private readonly IReadOnlyDictionary<EmailProvider, IEmailDeliveryProvider> _providers;
    private readonly ConcurrentDictionary<EmailProvider, ProviderRuntimeState> _runtime = new();
    private readonly ILogger<EmailDeliveryService> _logger;

    public EmailDeliveryService(
        EmailDeliveryConfigurationStore configurationStore,
        IEnumerable<IEmailDeliveryProvider> providers,
        ILogger<EmailDeliveryService> logger
    )
    {
        _configurationStore = configurationStore;
        _providers = providers.ToDictionary(provider => provider.Provider);
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (!TryValidateRequest(request, out var message))
            return new EmailDeliveryResult(EmailDeliveryStatus.Rejected, null, message);

        var configuration = await _configurationStore.GetEffectiveAsync(cancellationToken);
        var attemptedProviders = new List<EmailProvider>();
        var attachmentBytes =
            request.Attachments?.Sum(attachment => (long)attachment.Content.Length) ?? 0;
        if (attachmentBytes > configuration.MaxTotalAttachmentBytes)
        {
            return new EmailDeliveryResult(
                EmailDeliveryStatus.Rejected,
                null,
                "Total email attachment size exceeds the configured limit."
            );
        }
        foreach (var provider in configuration.ProviderOrder)
        {
            if (!_providers.TryGetValue(provider, out var deliveryProvider))
                continue;

            var state = _runtime.GetOrAdd(provider, _ => new ProviderRuntimeState());
            if (state.CircuitOpenUntilUtc is { } openUntil && openUntil > DateTimeOffset.UtcNow)
                continue;

            if (
                !deliveryProvider.IsConfigured(configuration)
                || !configuration.IsProviderActivated(provider)
                || !deliveryProvider.Supports(request, configuration)
            )
                continue;

            var result = await SendWithRetriesAsync(
                deliveryProvider,
                request,
                configuration,
                cancellationToken
            );
            attemptedProviders.Add(provider);
            result = result with { AttemptedProviders = attemptedProviders.ToArray() };

            if (result.IsAccepted)
            {
                RecordSuccess(state, isTest: false);
                _logger.LogInformation(
                    "Email accepted by {Provider} for category {Category}; correlation {CorrelationId}",
                    provider,
                    request.Category ?? "operational",
                    request.CorrelationId ?? "none"
                );
                return result;
            }

            if (
                result.Status == EmailDeliveryStatus.Unavailable
                || result.CanFallback
                || result.CanRetry
            )
                RecordFailure(state);
            _logger.LogWarning(
                "Email delivery did not complete through {Provider}; outcome {Outcome}; fallback {CanFallback}; correlation {CorrelationId}",
                provider,
                result.Status,
                result.CanFallback,
                request.CorrelationId ?? "none"
            );

            // An unknown outcome can mean that a provider accepted the message just
            // before the connection ended. Never duplicate that message by falling back.
            if (!result.CanFallback)
                return result;
        }

        EmailProvider? lastAttemptedProvider =
            attemptedProviders.Count > 0 ? attemptedProviders[^1] : null;
        return new EmailDeliveryResult(
            EmailDeliveryStatus.Unavailable,
            lastAttemptedProvider,
            "No configured email provider is currently available.",
            CanFallback: false,
            AttemptedProviders: attemptedProviders
        );
    }

    public async Task<EmailDeliveryConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var configuration = await _configurationStore.GetEffectiveAsync(cancellationToken);
        var statuses = _providers
            .Values.OrderBy(provider =>
                Array.IndexOf(configuration.ProviderOrder.ToArray(), provider.Provider)
            )
            .Select(provider => CreateProviderStatus(provider, configuration))
            .ToArray();

        var managementAvailable =
            configuration.KeyVaultConfigured && configuration.KeyVaultOperational;
        return new EmailDeliveryConfigurationStatus(
            configuration.ProviderOrder,
            statuses,
            managementAvailable,
            managementAvailable
                    ? "Configuration is stored in Azure Key Vault. Secret values remain write-only."
                : configuration.KeyVaultConfigured
                    ? "Azure Key Vault cannot currently be reached. Existing deployment configuration remains active."
                : "Set SystemSettings:KeyVaultUri to enable secure runtime configuration management.",
            configuration.TimeoutSeconds,
            configuration.MaxTotalAttachmentBytes,
            DateTimeOffset.UtcNow,
            new EmailGraphConfigurationSummary(
                configuration.Graph.Endpoint ?? "https://graph.microsoft.com/v1.0",
                configuration.Graph.Authentication,
                configuration.Graph.TenantId,
                configuration.Graph.ClientId,
                configuration.Graph.ManagedIdentityClientId,
                configuration.Graph.SenderUserPrincipalName,
                configuration.Graph.FromAddress,
                configuration.Graph.FromName,
                !string.IsNullOrWhiteSpace(configuration.Graph.ClientSecret)
            ),
            new EmailSmtpConfigurationSummary(
                configuration.Smtp.Host,
                configuration.Smtp.Port,
                configuration.Smtp.SecurityMode,
                configuration.Smtp.Authentication,
                configuration.Smtp.Username,
                configuration.Smtp.FromAddress,
                configuration.Smtp.FromName,
                !string.IsNullOrWhiteSpace(configuration.Smtp.Password),
                configuration.Smtp.GoogleOAuthClientId,
                !string.IsNullOrWhiteSpace(configuration.Smtp.GoogleOAuthClientSecret),
                !string.IsNullOrWhiteSpace(configuration.Smtp.GoogleOAuthRefreshToken)
            ),
            new EmailSendGridConfigurationSummary(
                configuration.SendGrid.FromEmail,
                configuration.SendGrid.FromName,
                !string.IsNullOrWhiteSpace(configuration.SendGrid.ApiKey)
            )
        );
    }

    public Task<EmailConfigurationUpdateResult> UpdateAsync(
        EmailDeliveryConfigurationUpdate update,
        string actorUserId,
        CancellationToken cancellationToken = default
    ) => _configurationStore.UpdateAsync(update, actorUserId, cancellationToken);

    public async Task<EmailProviderTestResult> TestProviderAsync(
        EmailProvider provider,
        EmailProviderTestRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsEmailAddress(request.RecipientAddress))
        {
            return new EmailProviderTestResult(
                provider,
                EmailDeliveryStatus.Rejected,
                "Provide a valid test recipient email address.",
                null
            );
        }

        if (!_providers.TryGetValue(provider, out var deliveryProvider))
        {
            return new EmailProviderTestResult(
                provider,
                EmailDeliveryStatus.Unavailable,
                "The selected email provider is unavailable.",
                null
            );
        }

        var configuration = await _configurationStore.GetEffectiveAsync(cancellationToken);
        var state = _runtime.GetOrAdd(provider, _ => new ProviderRuntimeState());
        if (
            !deliveryProvider.IsConfigured(configuration)
            || !deliveryProvider.Supports(TestRequest(request), configuration)
        )
        {
            return new EmailProviderTestResult(
                provider,
                EmailDeliveryStatus.Unavailable,
                deliveryProvider.GetConfigurationDescription(configuration),
                state.RecentSuccessfulTestUntilUtc
            );
        }

        var result = await SendWithRetriesAsync(
            deliveryProvider,
            TestRequest(request),
            configuration,
            cancellationToken
        );
        if (result.IsAccepted)
        {
            var activated = await _configurationStore.MarkProviderTestedAsync(
                provider,
                configuration.GetProviderConfigurationVersion(provider),
                actorUserId,
                cancellationToken
            );
            if (!activated)
            {
                return new EmailProviderTestResult(
                    provider,
                    EmailDeliveryStatus.Unavailable,
                    "The test message was accepted, but the provider could not be activated because its secure configuration changed or Azure Key Vault was unavailable. Refresh and test again.",
                    state.RecentSuccessfulTestUntilUtc,
                    result.MessageId
                );
            }

            RecordSuccess(state, isTest: true);
            _logger.LogInformation(
                "Email provider {Provider} test was accepted for administrator {ActorUserId}; correlation {CorrelationId}",
                provider,
                actorUserId,
                request.CorrelationId ?? "none"
            );
        }
        else
        {
            RecordFailure(state);
        }

        return new EmailProviderTestResult(
            provider,
            result.Status,
            result.Description,
            state.RecentSuccessfulTestUntilUtc,
            result.MessageId
        );
    }

    private EmailProviderHealthStatus CreateProviderStatus(
        IEmailDeliveryProvider provider,
        EffectiveEmailDeliveryConfiguration configuration
    )
    {
        var state = _runtime.GetOrAdd(provider.Provider, _ => new ProviderRuntimeState());
        var configured = provider.IsConfigured(configuration);
        var requiresActivation =
            configured && !configuration.IsProviderActivated(provider.Provider);
        var circuitOpen =
            state.CircuitOpenUntilUtc is { } openUntil && openUntil > DateTimeOffset.UtcNow;
        var enabled = configuration.ProviderOrder.Contains(provider.Provider);
        var health =
            !configured ? EmailProviderHealth.Unavailable
            : requiresActivation ? EmailProviderHealth.Unknown
            : circuitOpen ? EmailProviderHealth.Degraded
            : state.LastSuccessfulSendUtc.HasValue || state.LastSuccessfulTestUtc.HasValue
                ? EmailProviderHealth.Healthy
            : EmailProviderHealth.Unknown;

        return new EmailProviderHealthStatus(
            provider.Provider,
            enabled,
            configured,
            configured && !requiresActivation && !circuitOpen,
            requiresActivation,
            health,
            GetConfigurationSource(provider.Provider, configuration),
            provider.Supports(
                new EmailDeliveryRequest([], "support check", "support check", Attachments: []),
                configuration
            ),
            provider.GetConfigurationDescription(configuration),
            state.ConsecutiveFailureCount,
            circuitOpen ? state.CircuitOpenUntilUtc : null,
            state.LastSuccessfulSendUtc,
            state.LastSuccessfulTestUtc,
            state.RecentSuccessfulTestUntilUtc
        );
    }

    private static EmailConfigurationSource GetConfigurationSource(
        EmailProvider provider,
        EffectiveEmailDeliveryConfiguration configuration
    ) =>
        provider switch
        {
            EmailProvider.Graph => configuration.Graph.Source,
            EmailProvider.Smtp => configuration.Smtp.Source,
            EmailProvider.SendGrid => configuration.SendGrid.Source,
            _ => EmailConfigurationSource.Unavailable,
        };

    private static EmailDeliveryRequest TestRequest(EmailProviderTestRequest request) =>
        new(
            [new EmailRecipient(request.RecipientAddress.Trim())],
            "FIS email delivery test",
            "<p>This confirms the selected Fleet Information System email provider accepted a test message.</p>",
            IsHtml: true,
            Category: "configuration-test",
            CorrelationId: request.CorrelationId ?? Guid.NewGuid().ToString("N")
        );

    private static bool TryValidateRequest(EmailDeliveryRequest request, out string message)
    {
        if (
            request.Recipients.Count == 0
            || request.Recipients.Any(recipient => !IsEmailAddress(recipient.Address))
        )
        {
            message = "Provide at least one valid recipient email address.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 998)
        {
            message = "Provide an email subject no longer than 998 characters.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(request.Body))
        {
            message = "Provide an email body.";
            return false;
        }
        if (
            request.Attachments?.Any(attachment =>
                string.IsNullOrWhiteSpace(attachment.FileName) || attachment.Content.Length == 0
            ) == true
        )
        {
            message = "Each email attachment needs a file name and content.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool IsEmailAddress(string? value)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(value)
                && new MailAddress(value.Trim()).Address == value.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<EmailDeliveryResult> SendWithTimeoutAsync(
        IEmailDeliveryProvider provider,
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(configuration.TimeoutSeconds)
        );
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token
        );
        try
        {
            return await provider.SendAsync(request, configuration, linked.Token);
        }
        catch (OperationCanceledException)
            when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new EmailDeliveryResult(
                EmailDeliveryStatus.Unknown,
                provider.Provider,
                "Email delivery timed out after submission started."
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Email provider {Provider} failed unexpectedly for correlation {CorrelationId}",
                provider.Provider,
                request.CorrelationId ?? "none"
            );
            return new EmailDeliveryResult(
                EmailDeliveryStatus.Unknown,
                provider.Provider,
                "Email delivery could not be confirmed after submission started."
            );
        }
    }

    private async Task<EmailDeliveryResult> SendWithRetriesAsync(
        IEmailDeliveryProvider provider,
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        EmailDeliveryResult? lastResult = null;
        for (var attempt = 1; attempt <= MaximumAttemptsPerProvider; attempt++)
        {
            var result = await SendWithTimeoutAsync(
                provider,
                request,
                configuration,
                cancellationToken
            );
            lastResult = result;
            if (result.IsAccepted || !result.CanRetry || attempt == MaximumAttemptsPerProvider)
                return result;

            var retryDelay = result.RetryAfter ?? DefaultRetryDelay;
            retryDelay = retryDelay > MaximumRetryDelay ? MaximumRetryDelay : retryDelay;
            if (retryDelay < TimeSpan.Zero)
                retryDelay = TimeSpan.Zero;
            _logger.LogInformation(
                "Email provider {Provider} returned a safe pre-acceptance failure. Retrying once after {DelayMilliseconds} ms for correlation {CorrelationId}",
                provider.Provider,
                retryDelay.TotalMilliseconds,
                request.CorrelationId ?? "none"
            );
            await Task.Delay(retryDelay, cancellationToken);
        }

        return lastResult!;
    }

    private static void RecordSuccess(ProviderRuntimeState state, bool isTest)
    {
        state.ConsecutiveFailureCount = 0;
        state.CircuitOpenUntilUtc = null;
        var now = DateTimeOffset.UtcNow;
        if (isTest)
        {
            state.LastSuccessfulTestUtc = now;
            state.RecentSuccessfulTestUntilUtc = now.Add(RecentTestDuration);
        }
        else
        {
            state.LastSuccessfulSendUtc = now;
        }
    }

    private static void RecordFailure(ProviderRuntimeState state)
    {
        state.ConsecutiveFailureCount++;
        if (state.ConsecutiveFailureCount >= FailuresBeforeCircuitOpen)
            state.CircuitOpenUntilUtc = DateTimeOffset.UtcNow.Add(CircuitDuration);
    }

    private sealed class ProviderRuntimeState
    {
        public int ConsecutiveFailureCount { get; set; }
        public DateTimeOffset? CircuitOpenUntilUtc { get; set; }
        public DateTimeOffset? LastSuccessfulSendUtc { get; set; }
        public DateTimeOffset? LastSuccessfulTestUtc { get; set; }
        public DateTimeOffset? RecentSuccessfulTestUntilUtc { get; set; }
    }
}
