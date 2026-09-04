using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for user profile operations (user_access_old1 table)
/// </summary>
public class UserProfileRepository : IUserProfileRepository
{
    private readonly FisDbContext _context;

    public UserProfileRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get user profile by user access code
    /// </summary>
    public async Task<UserAccessOld?> GetByIdAsync(short userAccessCode)
    {
        return await _context.UserAccessOlds
            .Where(u => u.user_active)
            .FirstOrDefaultAsync(u => u.user_access_code == userAccessCode);
    }

    /// <summary>
    /// Get user profile by first name (for login)
    /// </summary>
    public async Task<UserAccessOld?> GetByFirstNameAsync(string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return null;

        return await _context.UserAccessOlds
            .Where(u => u.user_active)
            .FirstOrDefaultAsync(u => u.FirstName != null &&
                                     u.FirstName.ToLower() == firstName.ToLower().Trim());
    }

    /// <summary>
    /// Get user profile by email
    /// </summary>
    public async Task<UserAccessOld?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _context.UserAccessOlds
            .Where(u => u.user_active)
            .FirstOrDefaultAsync(u => u.E_Mail != null &&
                                     u.E_Mail.ToLower() == email.ToLower().Trim());
    }

    /// <summary>
    /// Get all active user profiles
    /// </summary>
    public async Task<IEnumerable<UserAccessOld>> GetAllActiveAsync()
    {
        return await _context.UserAccessOlds
            .Where(u => u.user_active)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    /// <summary>
    /// Get user profiles by site code
    /// </summary>
    public async Task<IEnumerable<UserAccessOld>> GetBySiteAsync(short siteCode)
    {
        return await _context.UserAccessOlds
            .Where(u => u.user_active && u.Site_code == siteCode)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    /// <summary>
    /// Search user profiles by name, email, or telephone
    /// </summary>
    public async Task<IEnumerable<UserAccessOld>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<UserAccessOld>();

        var term = searchTerm.ToLower().Trim();

        return await _context.UserAccessOlds
            .Where(u => u.user_active &&
                       ((u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                        (u.LastName != null && u.LastName.ToLower().Contains(term)) ||
                        (u.E_Mail != null && u.E_Mail.ToLower().Contains(term)) ||
                        (u.telephone != null && u.telephone.Contains(term))))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();
    }

    /// <summary>
    /// Create new user profile
    /// </summary>
    public async Task<UserAccessOld> CreateAsync(UserAccessOld userProfile, int currentUserId)
    {
        userProfile.date_created = DateTime.Now;
        userProfile.user_active = true;

        _context.UserAccessOlds.Add(userProfile);
        await _context.SaveChangesAsync();
        return userProfile;
    }

    /// <summary>
    /// Update user profile
    /// </summary>
    public async Task UpdateAsync(UserAccessOld userProfile, int currentUserId)
    {
        var existing = await _context.UserAccessOlds
            .FirstOrDefaultAsync(u => u.user_access_code == userProfile.user_access_code);

        if (existing == null)
            throw new KeyNotFoundException($"User profile with code {userProfile.user_access_code} not found");

        existing.date_updated = DateTime.Now;

        // Use CurrentValues.SetValues for tracking-safe updates
        _context.Entry(existing).CurrentValues.SetValues(userProfile);
        _context.Entry(existing).Property(x => x.date_created).IsModified = false;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Soft delete user profile
    /// </summary>
    public async Task DeleteAsync(short userAccessCode, int currentUserId)
    {
        var userProfile = await _context.UserAccessOlds
            .FirstOrDefaultAsync(u => u.user_access_code == userAccessCode);

        if (userProfile == null)
            throw new KeyNotFoundException($"User profile with code {userAccessCode} not found");

        userProfile.user_active = false;
        userProfile.date_updated = DateTime.Now;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Validate user credentials (FirstName + password)
    /// </summary>
    public async Task<bool> ValidateCredentialsAsync(string firstName, string password)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(password))
            return false;

        var user = await GetByFirstNameAsync(firstName);
        if (user == null)
            return false;

        if (string.IsNullOrWhiteSpace(user.password))
            return false;

        var storedPassword = user.password.Trim();

        // The expanded path uses BCrypt. The original user_access_old1 table
        // stores an uppercase MD5 digest in char(32), so support both formats
        // without writing the modern hash back into the legacy column.
        if (LooksLikeBcryptHash(storedPassword))
        {
            return BCrypt.Net.BCrypt.Verify(password, storedPassword);
        }

        if (LooksLikeLegacyMd5Hash(storedPassword))
        {
            var suppliedHash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password)));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedHash),
                Encoding.UTF8.GetBytes(storedPassword.ToUpperInvariant()));
        }

        return user.password.TrimEnd() == password;
    }

    private static bool LooksLikeBcryptHash(string value)
    {
        return value.StartsWith("$2a$", StringComparison.Ordinal)
               || value.StartsWith("$2b$", StringComparison.Ordinal)
               || value.StartsWith("$2y$", StringComparison.Ordinal);
    }

    private static bool LooksLikeLegacyMd5Hash(string value)
    {
        return value.Length == 32 && value.All(Uri.IsHexDigit);
    }
}
