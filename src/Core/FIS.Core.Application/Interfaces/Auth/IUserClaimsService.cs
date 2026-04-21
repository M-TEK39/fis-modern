using System.Security.Claims;

namespace FIS.Core.Application.Interfaces.Auth;

/// <summary>
/// Service for enriching Entra ID tokens with legacy FIS user claims.
/// Bridges modern Azure Entra ID authentication with legacy TS_Users permissions.
/// </summary>
public interface IUserClaimsService
{
    /// <summary>
    /// Retrieves FIS-specific claims for an Entra ID user.
    /// Claims are sourced from TS_Users table via EntraId_User_Mapping bridge.
    /// </summary>
    /// <param name="entraObjectId">Azure Entra ID Object ID (GUID from token)</param>
    /// <returns>
    /// Collection of claims including:
    /// - user_access_code (legacy user ID)
    /// - site_code (user's site)
    /// - department_code (user's department)
    /// - admin_level (permission level)
    /// - user_type (user classification)
    /// </returns>
    Task<IEnumerable<Claim>> GetClaimsAsync(string entraObjectId);

    /// <summary>
    /// Creates or updates the mapping between an Entra ID user and a legacy FIS user.
    /// Used during user onboarding to enterprise SSO.
    /// </summary>
    /// <param name="entraObjectId">Azure Entra ID Object ID</param>
    /// <param name="userAccessCode">Legacy TS_Users.user_access_code</param>
    /// <returns>True if mapping was created/updated successfully</returns>
    Task<bool> CreateOrUpdateMappingAsync(string entraObjectId, int userAccessCode);

    /// <summary>
    /// Checks if an Entra ID user has been mapped to a legacy FIS user.
    /// </summary>
    /// <param name="entraObjectId">Azure Entra ID Object ID</param>
    /// <returns>True if mapping exists</returns>
    Task<bool> IsMappedAsync(string entraObjectId);
}
