using System.Security.Cryptography;
using System.Text;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using FIS.Data.SqlServer.Compatibility;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for user profile operations (user_access_old1 table)
/// </summary>
public class UserProfileRepository : IUserProfileRepository
{
    private readonly FisDbContext _context;
    private readonly LegacyUserProfileOptionalFieldsService _optionalFields;

    public UserProfileRepository(
        FisDbContext context,
        LegacyUserProfileOptionalFieldsService optionalFields
    )
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _optionalFields = optionalFields ?? throw new ArgumentNullException(nameof(optionalFields));
    }

    /// <summary>
    /// Get user profile by user access code
    /// </summary>
    public async Task<UserAccessOld?> GetByIdAsync(short userAccessCode)
    {
        var user = await _context
            .UserAccessOlds.Where(u => u.user_active)
            .FirstOrDefaultAsync(u => u.user_access_code == userAccessCode);

        if (user is not null)
        {
            await _optionalFields.HydrateAsync(user);
        }

        return user;
    }

    /// <summary>
    /// Get user profile by first name (for login)
    /// </summary>
    public async Task<UserAccessOld?> GetByFirstNameAsync(string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return null;

        var user = await _context
            .UserAccessOlds.Where(u => u.user_active)
            .FirstOrDefaultAsync(u =>
                u.FirstName != null && u.FirstName.ToLower() == firstName.ToLower().Trim()
            );

        if (user is not null)
        {
            await _optionalFields.HydrateAsync(user);
        }

        return user;
    }

    /// <summary>
    /// Get user profile by email
    /// </summary>
    public async Task<UserAccessOld?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var user = await _context
            .UserAccessOlds.Where(u => u.user_active)
            .FirstOrDefaultAsync(u =>
                u.E_Mail != null && u.E_Mail.ToLower() == email.ToLower().Trim()
            );

        if (user is not null)
        {
            await _optionalFields.HydrateAsync(user);
        }

        return user;
    }

    /// <summary>
    /// Get all active user profiles
    /// </summary>
    public async Task<IEnumerable<UserAccessOld>> GetAllActiveAsync()
    {
        var users = await _context
            .UserAccessOlds.Where(u => u.user_active)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        await _optionalFields.HydrateManyAsync(users);
        return users;
    }

    public async Task<UserProfileAdministrationPage> GetAdministrationPageAsync(
        string alphabet,
        int page,
        int pageSize
    )
    {
        var selectedAlphabet = string.IsNullOrWhiteSpace(alphabet)
            ? "A"
            : alphabet.Trim().ToUpperInvariant();
        var selectedAlphabetLower = selectedAlphabet.ToLowerInvariant();
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        var filteredUsers = _context
            .UserAccessOlds.AsNoTracking()
            .Where(user =>
                user.user_active
                && (
                    user.LastName == null
                    || user.LastName.Trim() == string.Empty
                    || user.LastName.StartsWith(selectedAlphabet)
                    || user.LastName.StartsWith(selectedAlphabetLower)
                )
            );

        var total = await filteredUsers.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedPageSize));
        normalizedPage = Math.Min(normalizedPage, totalPages);

        var users = await filteredUsers
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.user_access_code)
            .Skip(checked((normalizedPage - 1) * normalizedPageSize))
            .Take(normalizedPageSize)
            .ToListAsync();

        await _optionalFields.HydrateManyAsync(users);
        return new UserProfileAdministrationPage(users, normalizedPage, normalizedPageSize, total);
    }

    /// <summary>
    /// Get user profiles by site code
    /// </summary>
    public async Task<IEnumerable<UserAccessOld>> GetBySiteAsync(short siteCode)
    {
        var users = await _context
            .UserAccessOlds.Where(u => u.user_active && u.Site_code == siteCode)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        await _optionalFields.HydrateManyAsync(users);
        return users;
    }

    /// <summary>
    /// Search user profiles by name, email, or telephone
    /// </summary>
    public async Task<IEnumerable<UserAccessOld>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Enumerable.Empty<UserAccessOld>();

        var term = searchTerm.ToLower().Trim();

        var users = await _context
            .UserAccessOlds.Where(u =>
                u.user_active
                && (
                    (u.FirstName != null && u.FirstName.ToLower().Contains(term))
                    || (u.LastName != null && u.LastName.ToLower().Contains(term))
                    || (u.E_Mail != null && u.E_Mail.ToLower().Contains(term))
                    || (u.telephone != null && u.telephone.Contains(term))
                )
            )
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync();

        await _optionalFields.HydrateManyAsync(users);
        return users;
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
        await _optionalFields.PersistAsync(userProfile);
        return userProfile;
    }

    /// <summary>
    /// Update user profile
    /// </summary>
    public async Task UpdateAsync(UserAccessOld userProfile, int currentUserId)
    {
        var existing = await _context.UserAccessOlds.FirstOrDefaultAsync(u =>
            u.user_access_code == userProfile.user_access_code
        );

        if (existing == null)
            throw new KeyNotFoundException(
                $"User profile with code {userProfile.user_access_code} not found"
            );

        existing.date_updated = DateTime.Now;

        // Use CurrentValues.SetValues for tracking-safe updates
        _context.Entry(existing).CurrentValues.SetValues(userProfile);
        _context.Entry(existing).Property(x => x.date_created).IsModified = false;

        await _context.SaveChangesAsync();
        existing.approver_code_at_gfleet = userProfile.approver_code_at_gfleet;
        await _optionalFields.PersistAsync(existing);
    }

    /// <summary>
    /// Soft delete user profile
    /// </summary>
    public async Task DeleteAsync(short userAccessCode, int currentUserId)
    {
        var userProfile = await _context.UserAccessOlds.FirstOrDefaultAsync(u =>
            u.user_access_code == userAccessCode
        );

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
                Encoding.UTF8.GetBytes(storedPassword.ToUpperInvariant())
            );
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
