using FIS.Core.Domain.Entities;

namespace FIS.Core.Infrastructure.Interfaces;

/// <summary>
/// Repository interface for ContractStatus entity operations
/// Provides data access abstraction for contract status management
/// </summary>
public interface IContractStatusRepository
{
    /// <summary>
    /// Get contract status by status code
    /// </summary>
    /// <param name="statusCode">The status code to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    Task<ContractStatus?> GetByIdAsync(short statusCode);

    /// <summary>
    /// Get contract status by description
    /// </summary>
    /// <param name="description">The status description to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    Task<ContractStatus?> GetByDescriptionAsync(string description);

    /// <summary>
    /// Get contract status by abbreviation
    /// </summary>
    /// <param name="abbreviation">The status abbreviation to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    Task<ContractStatus?> GetByAbbreviationAsync(string abbreviation);

    /// <summary>
    /// Get all contract statuses
    /// </summary>
    /// <returns>List of all contract status entities</returns>
    Task<IEnumerable<ContractStatus>> GetAllStatusesAsync();

    /// <summary>
    /// Get active contract statuses only
    /// </summary>
    /// <returns>List of active contract status entities</returns>
    Task<IEnumerable<ContractStatus>> GetActiveStatusesAsync();

    /// <summary>
    /// Get final contract statuses only
    /// </summary>
    /// <returns>List of final contract status entities</returns>
    Task<IEnumerable<ContractStatus>> GetFinalStatusesAsync();

    /// <summary>
    /// Search contract statuses by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching contract status entities</returns>
    Task<IEnumerable<ContractStatus>> SearchStatusesAsync(string searchTerm);

    /// <summary>
    /// Create a new contract status
    /// </summary>
    /// <param name="status">The contract status entity to create</param>
    /// <returns>The created contract status with generated ID</returns>
    Task<ContractStatus> CreateAsync(ContractStatus status);

    /// <summary>
    /// Update an existing contract status
    /// </summary>
    /// <param name="status">The contract status entity to update</param>
    /// <returns>The updated contract status entity</returns>
    Task<ContractStatus> UpdateAsync(ContractStatus status);

    /// <summary>
    /// Delete a contract status by status code
    /// </summary>
    /// <param name="statusCode">The status code to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(short statusCode);
}