using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for managing EntraId_User_Mapping table.
/// Handles the bridge between Azure Entra ID users and legacy TS_Users.
/// </summary>
public interface IEntraIdUserMappingRepository
{
    /// <summary>
    /// Retrieves mapping by Entra ID Object ID.
    /// </summary>
    /// <param name="entraObjectId">Azure Entra ID Object ID (GUID)</param>
    /// <returns>Mapping if found, null otherwise</returns>
    Task<EntraIdUserMapping?> GetByEntraObjectIdAsync(string entraObjectId);

    /// <summary>
    /// Retrieves mapping by legacy user access code.
    /// </summary>
    /// <param name="userAccessCode">TS_Users.user_access_code</param>
    /// <returns>Mapping if found, null otherwise</returns>
    Task<EntraIdUserMapping?> GetByUserAccessCodeAsync(int userAccessCode);

    /// <summary>
    /// Creates a new mapping between Entra ID user and legacy user.
    /// </summary>
    /// <param name="mapping">Mapping to create</param>
    /// <returns>Created mapping with generated ID</returns>
    Task<EntraIdUserMapping> CreateAsync(EntraIdUserMapping mapping);

    /// <summary>
    /// Updates an existing mapping.
    /// </summary>
    /// <param name="mapping">Mapping to update</param>
    Task UpdateAsync(EntraIdUserMapping mapping);

    /// <summary>
    /// Deletes a mapping (for unmapping users).
    /// </summary>
    /// <param name="mappingId">Mapping ID to delete</param>
    Task DeleteAsync(int mappingId);

    /// <summary>
    /// Checks if a mapping exists for an Entra ID Object ID.
    /// </summary>
    /// <param name="entraObjectId">Azure Entra ID Object ID</param>
    /// <returns>True if mapping exists</returns>
    Task<bool> ExistsAsync(string entraObjectId);

    /// <summary>
    /// Retrieves all mappings (for admin purposes).
    /// </summary>
    /// <returns>All EntraId-User mappings</returns>
    Task<IEnumerable<EntraIdUserMapping>> GetAllAsync();
}
