using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for site operations against legacy site table
/// Handles organizational site/location management
/// </summary>
public class SiteRepository : ISiteRepository
{
    private readonly FisDbContext _context;

    public SiteRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get site by site code (primary key)
    /// </summary>
    public async Task<Site?> GetByIdAsync(int siteCode)
    {
        return await _context.Sites
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(s => s.Site_code == siteCode);
    }

    /// <summary>
    /// Get site by description/name
    /// </summary>
    public async Task<Site?> GetByNameAsync(string siteName)
    {
        if (string.IsNullOrWhiteSpace(siteName))
            return null;

        return await _context.Sites
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(s => s.description == siteName);
    }

    /// <summary>
    /// Get all active sites
    /// For legacy compatibility, we'll return all sites since there's no active flag
    /// </summary>
    public async Task<IEnumerable<Site>> GetActiveSitesAsync()
    {
        return await _context.Sites
                .Where(x => !x.is_deleted).OrderBy(s => s.description).ToListAsync();
    }

    /// <summary>
    /// Search sites by multiple criteria
    /// </summary>
    public async Task<IEnumerable<Site>> SearchSitesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetActiveSitesAsync();

        var term = searchTerm.ToLower().Trim();

        return await _context
            .Sites.Where(s =>
                (s.description != null && s.description.ToLower().Contains(term))
                || (s.res_person != null && s.res_person.ToLower().Contains(term))
                || (s.address1 != null && s.address1.ToLower().Contains(term))
                || (s.address2 != null && s.address2.ToLower().Contains(term))
            )
            .OrderBy(s => s.description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new site
    /// </summary>
    public async Task<Site> CreateAsync(Site site, int currentUserId)
    {
        if (site == null)
            throw new ArgumentNullException(nameof(site));

        // Auto-populate audit fields
            site.date_created = DateTime.UtcNow;
            site.is_deleted = false;
            
            _context.Sites.Add(site);
        await _context.SaveChangesAsync();
        return site;
    }

    /// <summary>
    /// Update an existing site
    /// </summary>
    public async Task UpdateAsync(Site site, int currentUserId)
    {
        if (site == null)
            throw new ArgumentNullException(nameof(site));

        var existing = await _context.Sites.FindAsync(site.Site_code);
        if (existing == null)
            throw new InvalidOperationException($"Site with Site_code {site.Site_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(site);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete a site by site code
    /// </summary>
    public async Task DeleteAsync(int siteCode, int currentUserId)
    {
        var site = await GetByIdAsync(siteCode);
        if (site != null)
        {
            // Soft delete instead of hard delete
                site.is_deleted = true;
                site.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
