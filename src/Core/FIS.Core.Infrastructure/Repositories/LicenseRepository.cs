using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// License repository implementation for Entity Framework data access
/// Handles all database operations for License entities
/// </summary>
public class LicenseRepository : ILicenseRepository
{
    private readonly FisDbContext _context;

    public LicenseRepository(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get license by license code
    /// </summary>
    /// <param name="licenceCode">The license code to search for</param>
    /// <returns>License entity if found, null otherwise</returns>
    public async Task<License?> GetByIdAsync(short licenceCode)
    {
        return await _context.Licenses
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(l => l.licence_code == licenceCode);
    }

    /// <summary>
    /// Get license by description
    /// </summary>
    /// <param name="description">The license description to search for</param>
    /// <returns>License entity if found, null otherwise</returns>
    public async Task<License?> GetByDescriptionAsync(string description)
    {
        return await _context.Licenses
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(l => l.licence_description == description);
    }

    /// <summary>
    /// Get all licenses
    /// </summary>
    /// <returns>List of all license entities</returns>
    public async Task<IEnumerable<License>> GetAllLicensesAsync()
    {
        return await _context.Licenses
                .Where(x => !x.is_deleted)
            .OrderBy(l => l.licence_description)
            .ToListAsync();
    }

    /// <summary>
    /// Search licenses by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching license entities</returns>
    public async Task<IEnumerable<License>> SearchLicensesAsync(string searchTerm)
    {
        return await _context.Licenses
            .Where(l => l.licence_description.Contains(searchTerm) ||
                       (l.licence_category != null && l.licence_category.Contains(searchTerm)))
            .OrderBy(l => l.licence_description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new license
    /// </summary>
    /// <param name="license">The license entity to create</param>
    /// <param name="currentUserId">The ID of the user performing the action</param>
    /// <returns>The created license with generated ID</returns>
    public async Task<License> CreateAsync(License license, int currentUserId)
    {
        // Auto-populate audit fields
            license.date_created = DateTime.UtcNow;
            license.created_by_user_code = currentUserId;
            license.is_deleted = false;
            
            _context.Licenses.Add(license);
        await _context.SaveChangesAsync();
        return license;
    }

    /// <summary>
    /// Update an existing license
    /// </summary>
    /// <param name="license">The license entity to update</param>
    /// <param name="currentUserId">The ID of the user performing the action</param>
    /// <returns>The updated license entity</returns>
    public async Task<License> UpdateAsync(License license, int currentUserId)
    {
        if (license == null)
            throw new ArgumentNullException(nameof(license));

        var existing = await _context.Licenses.FindAsync(license.licence_code);
        if (existing == null)
            throw new InvalidOperationException($"License with licence_code {license.licence_code} not found");

        // Preserve creation audit fields
        license.date_created = existing.date_created;
        license.created_by_user_code = existing.created_by_user_code;
        // Set update audit fields
        license.date_updated = DateTime.UtcNow;
        license.modified_by_user_code = currentUserId;
        
        _context.Entry(existing).CurrentValues.SetValues(license);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete a license by license code
    /// </summary>
    /// <param name="licenceCode">The license code to delete</param>
    /// <param name="currentUserId">The ID of the user performing the action</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(short licenceCode, int currentUserId)
    {
        var license = await GetByIdAsync(licenceCode);
        if (license == null)
            return false;

        // Soft delete instead of hard delete
                license.is_deleted = true;
                license.modified_by_user_code = currentUserId;
                license.date_updated = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}