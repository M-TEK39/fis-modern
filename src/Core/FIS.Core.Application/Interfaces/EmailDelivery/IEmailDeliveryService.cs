namespace FIS.Core.Application.Interfaces.EmailDelivery;

/// <summary>
/// Provider-neutral email delivery entry point.
/// </summary>
public interface IEmailDeliveryService
{
    Task<EmailDeliveryResult> SendAsync(
        EmailDeliveryRequest request,
        CancellationToken cancellationToken = default
    );
}
