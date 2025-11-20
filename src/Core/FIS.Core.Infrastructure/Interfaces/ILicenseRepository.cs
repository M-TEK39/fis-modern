using FIS.Core.Domain.Entities;

namespace FIS.Core.Infrastructure.Interfaces;

/// <summary>
/// Repository interface for License entity operations
/// Provides data access abstraction for license management
/// </summary>
public interface ILicenseRepository
{
    /// <summary>
    /// Get license by license code
    /// </summary>
    /// <param name="licenceCode">The license code to search for</param>
    /// <returns>License entity if found, null otherwise</returns>
    Task<License?> GetByIdAsync(short licenceCode);

    /// <summary>
    /// Get license by description
    /// </summary>
    /// <param name="description">The license description to search for</param>
    /// <returns>License entity if found, null otherwise</returns>
    Task<License?> GetByDescriptionAsync(string description);

    /// <summary>
    /// Get all licenses
    /// </summary>
    /// <returns>List of all license entities</returns>
    Task<IEnumerable<License>> GetAllLicensesAsync();

    /// <summary>
    /// Search licenses by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching license entities</returns>
    Task<IEnumerable<License>> SearchLicensesAsync(string searchTerm);

    /// <summary>
    /// Create a new license
    /// </summary>
    /// <param name="license">The license entity to create</param>
    /// <returns>The created license with generated ID</returns>
    Task<License> CreateAsync(License license);

    /// <summary>
    /// Update an existing license
    /// </summary>
    /// <param name="license">The license entity to update</param>
    /// <returns>The updated license entity</returns>
    Task<License> UpdateAsync(License license);

    /// <summary>
    /// Delete a license by license code
    /// </summary>
    /// <param name="licenceCode">The license code to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(short licenceCode);
}