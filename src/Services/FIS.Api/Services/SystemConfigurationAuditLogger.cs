using FIS.Core.Application.Interfaces.SystemConfiguration;

namespace FIS.Api.Services;

/// <summary>
/// Emits value-free configuration audit events for the deployment audit/SIEM
/// pipeline. No secret or configuration value crosses this boundary.
/// </summary>
public sealed class SystemConfigurationAuditLogger : ISystemConfigurationAuditSink
{
    private readonly ILogger<SystemConfigurationAuditLogger> _logger;

    public SystemConfigurationAuditLogger(ILogger<SystemConfigurationAuditLogger> logger)
    {
        _logger = logger;
    }

    public Task RecordAsync(
        SystemConfigurationAuditEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "System configuration audit event {UpdateId}: actor {Actor}; source {Source}; settings {Settings}; updated at {UpdatedAtUtc}",
            entry.Metadata.UpdateId,
            entry.Actor,
            entry.Source,
            string.Join(',', entry.SettingNames),
            entry.Metadata.UpdatedAtUtc
        );
        return Task.CompletedTask;
    }
}
