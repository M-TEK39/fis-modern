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
    public async Task<Make> CreateAsync(Make make)
    {
        if (make == null)
            throw new ArgumentNullException(nameof(make));

        _context.Makes.Add(make);
        await _context.SaveChangesAsync();
        return make;
    }

    /// <summary>
    /// Update an existing make
    /// </summary>
    public async Task<Make> UpdateAsync(Make make)
    {
        if (make == null)
            throw new ArgumentNullException(nameof(make));

        _context.Entry(make).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return make;
    }

    /// <summary>
    /// Delete a make
    /// </summary>
    public async Task DeleteAsync(short makeCode)
    {
        var make = await GetByIdAsync(makeCode);
        if (make != null)
        {
            _context.Makes.Remove(make);
            await _context.SaveChangesAsync();
        }
    }
}