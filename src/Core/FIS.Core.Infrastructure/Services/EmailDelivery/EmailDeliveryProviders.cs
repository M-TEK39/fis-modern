using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using FIS.Core.Application.Interfaces.EmailDelivery;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace FIS.Core.Infrastructure.Services.EmailDelivery;

public interface IEmailDeliveryProvider
{
    EmailProvider Provider { get; }
    bool IsConfigured(EffectiveEmailDeliveryConfiguration configuration);
    bool Supports(EmailDeliveryRequest request, EffectiveEmailDeliveryConfiguration configuration);
    string GetConfigurationDescription(EffectiveEmailDeliveryConfiguration configuration);
    Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration,
        CancellationToken cancellationToken
    );
}

public sealed class GraphEmailDeliveryProvider : IEmailDeliveryProvider
{
    private const int MaxDirectAttachmentBytes = 2_500_000;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GraphEmailDeliveryProvider> _logger;

    public GraphEmailDeliveryProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GraphEmailDeliveryProvider> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public EmailProvider Provider => EmailProvider.Graph;

    public bool IsConfigured(EffectiveEmailDeliveryConfiguration configuration)
    {
        var graph = configuration.Graph;
        return !string.IsNullOrWhiteSpace(graph.Endpoint)
            && !string.IsNullOrWhiteSpace(graph.SenderUserPrincipalName)
            && (
                graph.Authentication == EmailGraphAuthentication.ManagedIdentity
                || (
                    !string.IsNullOrWhiteSpace(graph.TenantId)
                    && !string.IsNullOrWhiteSpace(graph.ClientId)
                    && !string.IsNullOrWhiteSpace(graph.ClientSecret)
                )
            );
    }

    public bool Supports(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration
    ) =>
        request.Attachments?.All(attachment =>
            attachment.Content.Length <= MaxDirectAttachmentBytes
        ) != false;

    public string GetConfigurationDescription(EffectiveEmailDeliveryConfiguration configuration) =>
        IsConfigured(configuration)
            ? "Microsoft Graph is configured for the FIS sender mailbox."
            : "Configure a sender mailbox and managed identity or client-secret credentials.";

    public async Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        if (!IsConfigured(configuration))
            return new(
                EmailDeliveryStatus.Unavailable,
                Provider,
                "Microsoft Graph is not configured.",
                CanFallback: true
            );
        if (!Supports(request, configuration))
            return new(
                EmailDeliveryStatus.Unavailable,
                Provider,
                "Microsoft Graph direct send supports attachments up to 2.5 MB each.",
                CanFallback: true
            );

        var graph = configuration.Graph;
        AccessToken token;
        try
        {
            TokenCredential credential =
                graph.Authentication == EmailGraphAuthentication.ManagedIdentity
                    ? string.IsNullOrWhiteSpace(graph.ManagedIdentityClientId)
                        ? new ManagedIdentityCredential(new ManagedIdentityCredentialOptions())
                        : new ManagedIdentityCredential(
                            ManagedIdentityId.FromUserAssignedClientId(
                                graph.ManagedIdentityClientId
                            )
                        )
                    : new ClientSecretCredential(
                        graph.TenantId!,
                        graph.ClientId!,
                        graph.ClientSecret!
                    );
            token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://graph.microsoft.com/.default"]),
                cancellationToken
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Microsoft Graph token acquisition failed for email correlation {CorrelationId}",
                request.CorrelationId
            );
            return new(
                EmailDeliveryStatus.Unavailable,
                Provider,
                "Microsoft Graph authentication is unavailable.",
                CanFallback: true
            );
        }

        var payload = new Dictionary<string, object?>
        {
            ["message"] = new Dictionary<string, object?>
            {
                ["subject"] = request.Subject,
                ["body"] = new Dictionary<string, string>
                {
                    ["contentType"] = request.IsHtml ? "HTML" : "Text",
                    ["content"] = request.Body,
                },
                ["toRecipients"] = request
                    .Recipients.Select(recipient => new Dictionary<string, object?>
                    {
                        ["emailAddress"] = new Dictionary<string, string>
                        {
                            ["address"] = recipient.Address,
                            ["name"] = recipient.DisplayName ?? string.Empty,
                        },
                    })
                    .ToList(),
                ["attachments"] =
                    request
                        .Attachments?.Select(attachment => new Dictionary<string, object?>
                        {
                            ["@odata.type"] = "#microsoft.graph.fileAttachment",
                            ["name"] = attachment.FileName,
                            ["contentType"] = attachment.ContentType,
                            ["contentBytes"] = Convert.ToBase64String(attachment.Content),
                        })
                        .ToList() ?? [],
            },
            ["saveToSentItems"] = true,
        };

        try
        {
            using var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                $"{graph.Endpoint}/users/{Uri.EscapeDataString(graph.SenderUserPrincipalName!)}/sendMail"
            );
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token.Token
            );
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            using var response = await _httpClientFactory
                .CreateClient(nameof(GraphEmailDeliveryProvider))
                .SendAsync(requestMessage, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Accepted)
                return new(
                    EmailDeliveryStatus.Accepted,
                    Provider,
                    "Microsoft Graph accepted the message."
                );

            if (
                response.StatusCode is HttpStatusCode.RequestTimeout
                || (int)response.StatusCode >= 500
            )
            {
                return new(
                    EmailDeliveryStatus.Unknown,
                    Provider,
                    $"Microsoft Graph returned HTTP {(int)response.StatusCode} after receiving the request. Delivery cannot be confirmed."
                );
            }
            // This is a definite pre-acceptance rejection. It is safe to try
            // the next configured provider; timeouts and 5xx results above
            // deliberately remain Unknown because acceptance is uncertain.
            const bool canFallback = true;
            var canRetry = response.StatusCode is (HttpStatusCode)429;
            return new(
                EmailDeliveryStatus.Rejected,
                Provider,
                $"Microsoft Graph rejected the message with HTTP {(int)response.StatusCode}.",
                CanFallback: canFallback,
                CanRetry: canRetry,
                RetryAfter: canRetry
                    ? EmailDeliveryRetryAfter.GetRetryAfter(response.Headers)
                    : null
            );
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(
                EmailDeliveryStatus.Unknown,
                Provider,
                "Microsoft Graph delivery timed out after submission started."
            );
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Microsoft Graph request outcome is unknown for email correlation {CorrelationId}",
                request.CorrelationId
            );
            return new(
                EmailDeliveryStatus.Unknown,
                Provider,
                "Microsoft Graph connection ended before delivery could be confirmed."
            );
        }
    }
}

public sealed class SmtpEmailDeliveryProvider : IEmailDeliveryProvider
{
    private const string GoogleOAuthTokenEndpoint = "https://oauth2.googleapis.com/token";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmtpEmailDeliveryProvider> _logger;

    public SmtpEmailDeliveryProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<SmtpEmailDeliveryProvider> logger
    )
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public EmailProvider Provider => EmailProvider.Smtp;

    public bool IsConfigured(EffectiveEmailDeliveryConfiguration configuration)
    {
        var smtp = configuration.Smtp;
        return !string.IsNullOrWhiteSpace(smtp.Host)
            && !string.IsNullOrWhiteSpace(smtp.FromAddress)
            && smtp.Port is 465 or 587
            && (
                smtp.Authentication switch
                {
                    EmailSmtpAuthentication.None => true,
                    EmailSmtpAuthentication.Password => !string.IsNullOrWhiteSpace(smtp.Username)
                        && !string.IsNullOrWhiteSpace(smtp.Password),
                    EmailSmtpAuthentication.GoogleOAuth2 => smtp.Host.Equals(
                        "smtp.gmail.com",
                        StringComparison.OrdinalIgnoreCase
                    )
                        && !string.IsNullOrWhiteSpace(smtp.Username)
                        && !string.IsNullOrWhiteSpace(smtp.GoogleOAuthClientId)
                        && !string.IsNullOrWhiteSpace(smtp.GoogleOAuthClientSecret)
                        && !string.IsNullOrWhiteSpace(smtp.GoogleOAuthRefreshToken),
                    _ => false,
                }
            );
    }

    public bool Supports(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration
    ) => true;

    public string GetConfigurationDescription(EffectiveEmailDeliveryConfiguration configuration) =>
        IsConfigured(configuration)
            ? configuration.Smtp.Authentication == EmailSmtpAuthentication.GoogleOAuth2
                ? "Google SMTP is configured with server-side OAuth."
                : "TLS-protected SMTP is configured."
            : "Configure a TLS SMTP host, sender address, and approved relay, password, or Google OAuth credentials.";

    public async Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        if (!IsConfigured(configuration))
            return new(
                EmailDeliveryStatus.Unavailable,
                Provider,
                "SMTP is not configured.",
                CanFallback: true
            );

        var smtp = configuration.Smtp;
        var message = new MimeMessage();
        try
        {
            message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress!));
            foreach (var recipient in request.Recipients)
                message.To.Add(new MailboxAddress(recipient.DisplayName, recipient.Address));
            message.Subject = request.Subject;
            var body = new BodyBuilder
            {
                HtmlBody = request.IsHtml ? request.Body : null,
                TextBody = request.IsHtml ? null : request.Body,
            };
            foreach (var attachment in request.Attachments ?? [])
                body.Attachments.Add(
                    attachment.FileName,
                    attachment.Content,
                    MimeKit.ContentType.Parse(attachment.ContentType)
                );
            message.Body = body.ToMessageBody();
        }
        catch (Exception)
        {
            return new(EmailDeliveryStatus.Rejected, Provider, "The email message is invalid.");
        }

        using var client = new SmtpClient();
        try
        {
            var secureSocketOptions =
                smtp.SecurityMode == EmailSmtpSecurityMode.SslOnConnect
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;
            await client.ConnectAsync(
                smtp.Host!,
                smtp.Port,
                secureSocketOptions,
                cancellationToken
            );
            if (smtp.Authentication == EmailSmtpAuthentication.Password)
                await client.AuthenticateAsync(smtp.Username!, smtp.Password!, cancellationToken);
            else if (smtp.Authentication == EmailSmtpAuthentication.GoogleOAuth2)
            {
                var accessToken = await GetGoogleAccessTokenAsync(smtp, cancellationToken);
                await client.AuthenticateAsync(
                    new SaslMechanismOAuth2(smtp.Username!, accessToken),
                    cancellationToken
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "SMTP connection or authentication failed for email correlation {CorrelationId}",
                request.CorrelationId
            );
            return new(
                EmailDeliveryStatus.Unavailable,
                Provider,
                "SMTP connection or authentication is unavailable.",
                CanFallback: true
            );
        }

        try
        {
            var messageId = await client.SendAsync(message, cancellationToken);
            try
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "SMTP accepted email correlation {CorrelationId}, but the graceful disconnect failed",
                    request.CorrelationId
                );
            }
            return new(
                EmailDeliveryStatus.Accepted,
                Provider,
                "SMTP accepted the message.",
                messageId
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "SMTP send outcome is unknown for email correlation {CorrelationId}",
                request.CorrelationId
            );
            return new(
                EmailDeliveryStatus.Unknown,
                Provider,
                "SMTP delivery could not be confirmed after submission started."
            );
        }
    }

    private async Task<string> GetGoogleAccessTokenAsync(
        EffectiveSmtpConfiguration smtp,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GoogleOAuthTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = smtp.GoogleOAuthClientId!,
                    ["client_secret"] = smtp.GoogleOAuthClientSecret!,
                    ["refresh_token"] = smtp.GoogleOAuthRefreshToken!,
                    ["grant_type"] = "refresh_token",
                }
            ),
        };
        using var response = await _httpClientFactory
            .CreateClient(nameof(SmtpEmailDeliveryProvider))
            .SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Google OAuth token refresh was rejected.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken
        );
        if (
            !payload.RootElement.TryGetProperty("access_token", out var token)
            || string.IsNullOrWhiteSpace(token.GetString())
        )
            throw new InvalidOperationException(
                "Google OAuth token response did not contain an access token."
            );
        return token.GetString()!;
    }
}

public sealed class SendGridEmailDeliveryProvider : IEmailDeliveryProvider
{
    private readonly ILogger<SendGridEmailDeliveryProvider> _logger;

    public SendGridEmailDeliveryProvider(ILogger<SendGridEmailDeliveryProvider> logger)
    {
        _logger = logger;
    }

    public EmailProvider Provider => EmailProvider.SendGrid;

    public bool IsConfigured(EffectiveEmailDeliveryConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.SendGrid.ApiKey)
        && !string.IsNullOrWhiteSpace(configuration.SendGrid.FromEmail);

    public bool Supports(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration
    ) => true;

    public string GetConfigurationDescription(EffectiveEmailDeliveryConfiguration configuration) =>
        IsConfigured(configuration)
            ? "SendGrid is configured as an optional FIS delivery provider."
            : "Configure a SendGrid API key and sender address.";

    public async Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        EffectiveEmailDeliveryConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        if (!IsConfigured(configuration))
            return new(
                EmailDeliveryStatus.Unavailable,
                Provider,
                "SendGrid is not configured.",
                CanFallback: true
            );

        var sendGrid = configuration.SendGrid;
        var message = new SendGridMessage
        {
            From = new EmailAddress(sendGrid.FromEmail, sendGrid.FromName),
            Subject = request.Subject,
            PlainTextContent = request.IsHtml ? null : request.Body,
            HtmlContent = request.IsHtml ? request.Body : null,
        };
        message.AddTos(
            request
                .Recipients.Select(recipient => new EmailAddress(
                    recipient.Address,
                    recipient.DisplayName
                ))
                .ToList()
        );
        foreach (var attachment in request.Attachments ?? [])
            message.AddAttachment(
                attachment.FileName,
                Convert.ToBase64String(attachment.Content),
                attachment.ContentType
            );

        try
        {
            var client = new SendGridClient(sendGrid.ApiKey);
            var response = await client.SendEmailAsync(
                message,
                cancellationToken: cancellationToken
            );
            var code = (int)response.StatusCode;
            if (code is >= 200 and < 300)
            {
                var messageId = response.Headers.TryGetValues("X-Message-Id", out var values)
                    ? values.FirstOrDefault()
                    : null;
                return new(
                    EmailDeliveryStatus.Accepted,
                    Provider,
                    "SendGrid accepted the message.",
                    messageId
                );
            }

            if (code == 408 || code >= 500)
            {
                return new(
                    EmailDeliveryStatus.Unknown,
                    Provider,
                    $"SendGrid returned HTTP {code} after receiving the request. Delivery cannot be confirmed."
                );
            }
            // This is a definite pre-acceptance rejection. Network failures,
            // timeouts, and 5xx results above remain Unknown and never fall back.
            const bool canFallback = true;
            var canRetry = code == 429;
            return new(
                EmailDeliveryStatus.Rejected,
                Provider,
                $"SendGrid rejected the message with HTTP {code}.",
                CanFallback: canFallback,
                CanRetry: canRetry,
                RetryAfter: canRetry
                    ? EmailDeliveryRetryAfter.GetRetryAfter(response.Headers)
                    : null
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "SendGrid request outcome is unknown for email correlation {CorrelationId}",
                request.CorrelationId
            );
            return new(
                EmailDeliveryStatus.Unknown,
                Provider,
                "SendGrid delivery could not be confirmed after submission started."
            );
        }
    }
}

internal static class EmailDeliveryRetryAfter
{
    public static TimeSpan? GetRetryAfter(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Retry-After", out var values))
            return null;
        var rawValue = values.FirstOrDefault();
        if (int.TryParse(rawValue, out var seconds) && seconds >= 0)
            return TimeSpan.FromSeconds(seconds);
        return DateTimeOffset.TryParse(rawValue, out var retryAt)
            ? retryAt - DateTimeOffset.UtcNow
            : null;
    }
}
