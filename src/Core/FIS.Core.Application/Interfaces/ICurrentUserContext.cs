namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Resolves current authenticated user context for application-layer auditing.
/// </summary>
public interface ICurrentUserContext
{
    int GetCurrentUserIdOrDefault(int fallbackUserId = 1);

    string GetCurrentUserDisplayName(int? fallbackUserId = null);
}
