namespace FIS.Api.Services.SessionManagement;

internal static class SessionTokenConventions
{
    public static bool IsRememberedSession(
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        TimeSpan standardRefreshLifetime
    ) => expiresAtUtc - createdAtUtc > standardRefreshLifetime;
}
