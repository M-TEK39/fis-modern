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
            .FirstOrDefaultAsync(l => l.licence_description == description);
    }

    /// <summary>
    /// Get all licenses
    /// </summary>
    /// <returns>List of all license entities</returns>
    public async Task<IEnumerable<License>> GetAllLicensesAsync()
    {
        return await _context.Licenses
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
    /// <returns>The created license with generated ID</returns>
    public async Task<License> CreateAsync(License license)
    {
        _context.Licenses.Add(license);
        await _context.SaveChangesAsync();
        return license;
    }

    /// <summary>
    /// Update an existing license
    /// </summary>
    /// <param name="license">The license entity to update</param>
    /// <returns>The updated license entity</returns>
    public async Task<License> UpdateAsync(License license)
    {
        _context.Licenses.Update(license);
        await _context.SaveChangesAsync();
        return license;
    }

    /// <summary>
    /// Delete a license by license code
    /// </summary>
    /// <param name="licenceCode">The license code to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(short licenceCode)
    {
        var license = await GetByIdAsync(licenceCode);
        if (license == null)
            return false;

        _context.Licenses.Remove(license);
        await _context.SaveChangesAsync();
        return true;
    }
}