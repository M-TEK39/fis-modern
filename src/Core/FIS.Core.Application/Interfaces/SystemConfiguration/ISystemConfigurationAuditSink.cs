namespace FIS.Core.Application.Interfaces.SystemConfiguration;

/// <summary>
/// Integration seam for recording safe system configuration audit metadata.
/// Implementations must not persist configuration values or secret material.
/// </summary>
public interface ISystemConfigurationAuditSink
{
    Task RecordAsync(
        SystemConfigurationAuditEntry entry,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Default no-op implementation. The API composition root can register this
/// until a durable audit implementation is supplied.
/// </summary>
public sealed class NoOpSystemConfigurationAuditSink : ISystemConfigurationAuditSink
{
    public Task RecordAsync(
        SystemConfigurationAuditEntry entry,
        CancellationToken cancellationToken = default
    )
    {
        _ = entry;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
