using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for access level operations (bitwise permissions)
/// Manages module-based permissions using bit flags
/// </summary>
public class AccessLevelRepository : IAccessLevelRepository
{
    private readonly FisDbContext _context;

    public AccessLevelRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get access level by ID
    /// </summary>
    public async Task<AccessLevel?> GetByIdAsync(short accessLevelId)
    {
        return await _context.AccessLevels
            .Where(a => !a.is_deleted)
            .FirstOrDefaultAsync(a => a.AccessLevelID == accessLevelId);
    }

    /// <summary>
    /// Get access level by name
    /// </summary>
    public async Task<AccessLevel?> GetByNameAsync(string accessLevelName)
    {
        if (string.IsNullOrWhiteSpace(accessLevelName))
            return null;

        return await _context.AccessLevels
            .Where(a => !a.is_deleted)
            .FirstOrDefaultAsync(a => a.AccessLevelName != null &&
                                     a.AccessLevelName.ToLower() == accessLevelName.ToLower().Trim());
    }

    /// <summary>
    /// Get all access levels
    /// </summary>
    public async Task<IEnumerable<AccessLevel>> GetAllAsync()
    {
        return await _context.AccessLevels
            .Where(a => !a.is_deleted)
            .OrderBy(a => a.AccessLevelValue)
            .ToListAsync();
    }

    /// <summary>
    /// Create new access level
    /// </summary>
    public async Task<AccessLevel> CreateAsync(AccessLevel accessLevel, int currentUserId)
    {
        accessLevel.date_created = DateTime.Now;
        accessLevel.created_by_user_code = currentUserId;
        accessLevel.is_deleted = false;

        _context.AccessLevels.Add(accessLevel);
        await _context.SaveChangesAsync();
        return accessLevel;
    }

    /// <summary>
    /// Update access level
    /// </summary>
    public async Task UpdateAsync(AccessLevel accessLevel, int currentUserId)
    {
        var existing = await _context.AccessLevels
            .FirstOrDefaultAsync(a => a.AccessLevelID == accessLevel.AccessLevelID);

        if (existing == null)
            throw new KeyNotFoundException($"Access level with ID {accessLevel.AccessLevelID} not found");

        existing.date_updated = DateTime.Now;
        existing.modified_by_user_code = currentUserId;

        // Use CurrentValues.SetValues for tracking-safe updates
        _context.Entry(existing).CurrentValues.SetValues(accessLevel);
        _context.Entry(existing).Property(x => x.date_created).IsModified = false;
        _context.Entry(existing).Property(x => x.created_by_user_code).IsModified = false;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Soft delete access level
    /// </summary>
    public async Task DeleteAsync(short accessLevelId, int currentUserId)
    {
        var accessLevel = await _context.AccessLevels
            .FirstOrDefaultAsync(a => a.AccessLevelID == accessLevelId);

        if (accessLevel == null)
            throw new KeyNotFoundException($"Access level with ID {accessLevelId} not found");

        accessLevel.is_deleted = true;
        accessLevel.date_updated = DateTime.Now;
        accessLevel.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Check if user has a specific permission using bitwise AND
    /// </summary>
    public async Task<bool> UserHasPermissionAsync(long userAccessLevel, string permissionName)
    {
        var permission = await GetByNameAsync(permissionName);
        if (permission == null)
            return false;

        // Bitwise AND: Check if the permission bit is set in user's access level
        return (userAccessLevel & permission.AccessLevelValue) == permission.AccessLevelValue;
    }

    /// <summary>
    /// Get list of all permissions a user has based on their access level
    /// </summary>
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(long userAccessLevel)
    {
        var allPermissions = await GetAllAsync();
        var userPermissions = new List<string>();

        foreach (var permission in allPermissions)
        {
            // Bitwise AND: Check if each permission bit is set
            if ((userAccessLevel & permission.AccessLevelValue) == permission.AccessLevelValue)
            {
                if (!string.IsNullOrEmpty(permission.AccessLevelName))
                {
                    userPermissions.Add(permission.AccessLevelName);
                }
            }
        }

        return userPermissions;
    }
}
