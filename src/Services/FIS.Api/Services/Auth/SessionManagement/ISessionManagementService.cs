namespace FIS.Api.Services.SessionManagement;

public interface ISessionManagementService
{
    Task<SessionStoreStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<SessionListResult> ListActiveSessionsAsync(
        int? userAccessCode,
        int currentUserAccessCode,
        string? currentAccessToken,
        int limit,
        CancellationToken cancellationToken = default
    );

    Task<SessionOperationResult> RevokeCurrentSessionAsync(
        int userAccessCode,
        string accessToken,
        CancellationToken cancellationToken = default
    );

    Task<SessionOperationResult> RevokeUserSessionsAsync(
        int userAccessCode,
        CancellationToken cancellationToken = default
    );

    Task<SessionOperationResult> RevokeAllSessionsAsync(
        CancellationToken cancellationToken = default
    );
}
