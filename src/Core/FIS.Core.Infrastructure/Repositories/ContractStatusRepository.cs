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
        return await _context.ContractStatuses
            .FirstOrDefaultAsync(cs => cs.contract_status_code == statusCode);
    }

    /// <summary>
    /// Get contract status by description
    /// </summary>
    /// <param name="description">The status description to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    public async Task<ContractStatus?> GetByDescriptionAsync(string description)
    {
        return await _context.ContractStatuses
            .FirstOrDefaultAsync(cs => cs.status_description == description);
    }

    /// <summary>
    /// Get contract status by abbreviation
    /// </summary>
    /// <param name="abbreviation">The status abbreviation to search for</param>
    /// <returns>ContractStatus entity if found, null otherwise</returns>
    public async Task<ContractStatus?> GetByAbbreviationAsync(string abbreviation)
    {
        return await _context.ContractStatuses
            .FirstOrDefaultAsync(cs => cs.status_abbreviation == abbreviation);
    }

    /// <summary>
    /// Get all contract statuses
    /// </summary>
    /// <returns>List of all contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> GetAllStatusesAsync()
    {
        return await _context.ContractStatuses
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get active contract statuses only
    /// </summary>
    /// <returns>List of active contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> GetActiveStatusesAsync()
    {
        return await _context.ContractStatuses
            .Where(cs => cs.is_active)
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get final contract statuses only
    /// </summary>
    /// <returns>List of final contract status entities</returns>
    public async Task<IEnumerable<ContractStatus>> GetFinalStatusesAsync()
    {
        return await _context.ContractStatuses
            .Where(cs => cs.is_final)
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
        return await _context.ContractStatuses
            .Where(cs => cs.status_description.Contains(searchTerm) ||
                        (cs.status_abbreviation != null && cs.status_abbreviation.Contains(searchTerm)))
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new contract status
    /// </summary>
    /// <param name="status">The contract status entity to create</param>
    /// <returns>The created contract status with generated ID</returns>
    public async Task<ContractStatus> CreateAsync(ContractStatus status)
    {
        _context.ContractStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    /// <summary>
    /// Update an existing contract status
    /// </summary>
    /// <param name="status">The contract status entity to update</param>
    /// <returns>The updated contract status entity</returns>
    public async Task<ContractStatus> UpdateAsync(ContractStatus status)
    {
        _context.ContractStatuses.Update(status);
        await _context.SaveChangesAsync();
        return status;
    }

    /// <summary>
    /// Delete a contract status by status code
    /// </summary>
    /// <param name="statusCode">The status code to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(short statusCode)
    {
        var status = await GetByIdAsync(statusCode);
        if (status == null)
            return false;

        _context.ContractStatuses.Remove(status);
        await _context.SaveChangesAsync();
        return true;
    }
}