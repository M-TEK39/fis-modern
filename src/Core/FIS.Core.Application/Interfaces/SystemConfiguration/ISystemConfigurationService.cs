namespace FIS.Core.Application.Interfaces.SystemConfiguration;

/// <summary>
/// Reads safe system security configuration state and writes allow-listed
/// configuration through Azure Key Vault.
/// </summary>
public interface ISystemConfigurationService
{
    Task<SystemConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    );

    Task<SystemConfigurationUpdateResult> UpdateAsync(
        SystemConfigurationUpdate update,
        string actor,
        CancellationToken cancellationToken = default
    );
}
