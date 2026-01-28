using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for make operations against legacy make table
/// Handles vehicle manufacturer/brand data with legacy schema compatibility
/// </summary>
public class MakeRepository : IMakeRepository
{
    private readonly FisDbContext _context;

    public MakeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get make by make code (primary key)
    /// </summary>
    public async Task<Make?> GetByIdAsync(short makeCode)
    {
        return await _context.Makes
            .Include(m => m.Models)
            .FirstOrDefaultAsync(m => m.make_code == makeCode);
    }

    /// <summary>
    /// Get make by name/description
    /// </summary>
    public async Task<Make?> GetByNameAsync(string makeName)
    {
        return await _context.Makes
            .Include(m => m.Models)
            .FirstOrDefaultAsync(m => m.make_description.ToLower() == makeName.ToLower());
    }

    /// <summary>
    /// Get all makes with their models
    /// </summary>
    public async Task<IEnumerable<Make>> GetAllMakesAsync()
    {
        return await _context.Makes
            .Include(m => m.Models)
            .OrderBy(m => m.make_description)
            .ToListAsync();
    }

    /// <summary>
    /// Search makes by name/description
    /// </summary>
    public async Task<IEnumerable<Make>> SearchMakesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllMakesAsync();

        return await _context.Makes
            .Include(m => m.Models)
            .Where(m => m.make_description.ToLower().Contains(searchTerm.ToLower()))
            .OrderBy(m => m.make_description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new make
    /// </summary>
    public async Task<Make> CreateAsync(Make make, int currentUserId)
    {
        if (make == null)
            throw new ArgumentNullException(nameof(make));

        // Auto-populate audit fields
            make.date_created = DateTime.UtcNow;
            make.is_deleted = false;
            
            _context.Makes.Add(make);
        await _context.SaveChangesAsync();
        return make;
    }

    /// <summary>
    /// Update an existing make
    /// </summary>
    public async Task<Make> UpdateAsync(Make make, int currentUserId)
    {
        if (make == null)
            throw new ArgumentNullException(nameof(make));

        // Find the tracked entity (if any) and update its properties
        var existingMake = await _context.Makes.FindAsync(make.make_code);
        if (existingMake == null)
            throw new InvalidOperationException($"Make with code {make.make_code} not found");

        // Update properties of the tracked entity
        existingMake.make_description = make.make_description;

        await _context.SaveChangesAsync();
        return existingMake;
    }

    /// <summary>
    /// Delete a make
    /// </summary>
    public async Task DeleteAsync(short makeCode, int currentUserId)
    {
        var make = await GetByIdAsync(makeCode);
        if (make != null)
        {
            // Soft delete instead of hard delete
                make.is_deleted = true;
                make.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}