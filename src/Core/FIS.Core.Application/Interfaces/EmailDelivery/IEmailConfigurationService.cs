namespace FIS.Core.Application.Interfaces.EmailDelivery;

/// <summary>
/// Reads safe delivery status and manages configuration through Key Vault only.
/// </summary>
public interface IEmailConfigurationService
{
    Task<EmailDeliveryConfigurationStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    );

    Task<EmailConfigurationUpdateResult> UpdateAsync(
        EmailDeliveryConfigurationUpdate update,
        string actorUserId,
        CancellationToken cancellationToken = default
    );

    Task<EmailProviderTestResult> TestProviderAsync(
        EmailProvider provider,
        EmailProviderTestRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default
    );
}
