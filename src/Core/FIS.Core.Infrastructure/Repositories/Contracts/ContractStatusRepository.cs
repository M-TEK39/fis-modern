using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// ContractStatus repository implementation for Entity Framework data access
/// Handles all database operations for ContractStatus entities
/// </summary>
public class ContractStatusRepository : IContractStatusRepository
{
    private readonly FisDbContext _context;

    public ContractStatusRepository(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get contract status by status code
    /// </summary>
    /// <param name="statusCode">The status code to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    public async Task<ContractStatus?> GetByIdAsync(short statusCode)
    {
        return await _context
            .ContractStatuses.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(cs => cs.contract_status_code == statusCode);
    }

    /// <summary>
    /// Get contract status by description
    /// </summary>
    /// <param name="description">The status description to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    public async Task<ContractStatus?> GetByDescriptionAsync(string description)
    {
        return await _context
            .ContractStatuses.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(cs => cs.status_description == description);
    }

    /// <summary>
    /// Get contract status by abbreviation
    /// </summary>
    /// <param name="abbreviation">The status abbreviation to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    public async Task<ContractStatus?> GetByAbbreviationAsync(string abbreviation)
    {
        return await _context
            .ContractStatuses.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(cs => cs.status_abbreviation == abbreviation);
    }

    /// <summary>
    /// Get all contract statuses
    /// </summary>
    /// <returns>List of all contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> GetAllStatusesAsync()
    {
        return await _context
            .ContractStatuses.Where(x => !x.is_deleted)
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get active contract statuses only
    /// </summary>
    /// <returns>List of active contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> GetActiveStatusesAsync()
    {
        return await _context
            .ContractStatuses.Where(cs => cs.is_active)
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get final contract statuses only
    /// </summary>
    /// <returns>List of final contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> GetFinalStatusesAsync()
    {
        return await _context
            .ContractStatuses.Where(cs => cs.is_final)
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Search contract statuses by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> SearchStatusesAsync(string searchTerm)
    {
        return await _context
            .ContractStatuses.Where(cs =>
                cs.status_description.Contains(searchTerm)
                || (cs.status_abbreviation != null && cs.status_abbreviation.Contains(searchTerm))
            )
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new contract status
    /// </summary>
    /// <param name="status">The contract status entity to create</param>
    /// <param name="currentUserId">The user creating the status</param>
    /// <returns>The created contract status with generated ID</returns>
    public async Task<ContractStatus> CreateAsync(ContractStatus status, int currentUserId)
    {
        // Auto-populate audit fields
        status.date_created = DateTime.UtcNow;
        status.created_by_user_code = currentUserId;
        status.is_deleted = false;

        _context.ContractStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    /// <summary>
    /// Update an existing contract status
    /// </summary>
    /// <param name="status">The contract status entity to update</param>
    /// <param name="currentUserId">The user updating the status</param>
    /// <returns>The updated contract status entity</returns>
    public async Task<ContractStatus> UpdateAsync(ContractStatus status, int currentUserId)
    {
        if (status == null)
            throw new ArgumentNullException(nameof(status));

        var existing = await _context.ContractStatuses.FindAsync(status.contract_status_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"ContractStatus with contract_status_code {status.contract_status_code} not found"
            );

        // Preserve creation audit fields
        status.date_created = existing.date_created;
        status.created_by_user_code = existing.created_by_user_code;
        // Set update audit fields
        status.date_updated = DateTime.UtcNow;
        status.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(status);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete a contract status by status code
    /// </summary>
    /// <param name="statusCode">The status code to delete</param>
    /// <param name="currentUserId">The user deleting the status</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(short statusCode, int currentUserId)
    {
        var status = await GetByIdAsync(statusCode);
        if (status == null)
            return false;

        // Soft delete instead of hard delete
        status.is_deleted = true;
        status.date_updated = DateTime.UtcNow;
        status.modified_by_user_code = currentUserId;
        await _context.SaveChangesAsync();
        return true;
    }
}
