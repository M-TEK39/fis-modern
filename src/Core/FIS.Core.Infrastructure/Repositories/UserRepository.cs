using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for user operations against legacy TS_Users table
/// Simple CRUD operations for the 3-field user table
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly FisDbContext _context;

    public UserRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get user by access code (primary key)
    /// </summary>
    public async Task<User?> GetByIdAsync(int userAccessCode)
    {
        return await _context.Users
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(u => u.user_access_code == userAccessCode);
    }

    /// <summary>
    /// Get user by email address
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _context.Users
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(u => u.email == email);
    }

    /// <summary>
    /// Get user by telephone number
    /// </summary>
    public async Task<User?> GetByTelephoneAsync(string telephone)
    {
        if (string.IsNullOrWhiteSpace(telephone))
            return null;

        return await _context.Users
                .Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(u => u.tel_no == telephone);
    }

    /// <summary>
    /// Get all users
    /// </summary>
    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users
                .Where(x => !x.is_deleted)
            .OrderBy(u => u.user_access_code)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    public async Task<User> CreateAsync(User user, int currentUserId)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        // Auto-populate audit fields
            user.date_created = DateTime.UtcNow;
            user.is_deleted = false;
            
            _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    public async Task UpdateAsync(User user, int currentUserId)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        var existing = await _context.Users.FindAsync(user.user_access_code);
        if (existing == null)
            throw new InvalidOperationException($"User with user_access_code {user.user_access_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(user);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete a user by access code
    /// </summary>
    public async Task DeleteAsync(int userAccessCode, int currentUserId)
    {
        var user = await GetByIdAsync(userAccessCode);
        if (user != null)
        {
            // Soft delete instead of hard delete
                user.is_deleted = true;
                user.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}