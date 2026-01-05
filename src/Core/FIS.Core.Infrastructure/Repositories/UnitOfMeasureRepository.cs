using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// UnitOfMeasure repository implementation for Entity Framework data access
/// Handles all database operations for UnitOfMeasure entities
/// </summary>
public class UnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly FisDbContext _context;

    public UnitOfMeasureRepository(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get unit of measure by unit code
    /// </summary>
    /// <param name="unitCode">The unit code to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    public async Task<UnitOfMeasure?> GetByIdAsync(short unitCode)
    {
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.unit_of_measure_code == unitCode);
    }

    /// <summary>
    /// Get unit of measure by description
    /// </summary>
    /// <param name="description">The unit description to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    public async Task<UnitOfMeasure?> GetByDescriptionAsync(string description)
    {
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.unit_description == description);
    }

    /// <summary>
    /// Get unit of measure by abbreviation
    /// </summary>
    /// <param name="abbreviation">The unit abbreviation to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    public async Task<UnitOfMeasure?> GetByAbbreviationAsync(string abbreviation)
    {
        return await _context.UnitsOfMeasure
            .FirstOrDefaultAsync(u => u.unit_abbreviation == abbreviation);
    }

    /// <summary>
    /// Get all units of measure
    /// </summary>
    /// <returns>List of all unit of measure entities</returns>
    public async Task<IEnumerable<UnitOfMeasure>> GetAllUnitsAsync()
    {
        return await _context.UnitsOfMeasure
            .OrderBy(u => u.unit_category)
            .ThenBy(u => u.unit_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get units of measure by category
    /// </summary>
    /// <param name="category">The unit category to filter by</param>
    /// <returns>List of units in the specified category</returns>
    public async Task<IEnumerable<UnitOfMeasure>> GetByCategoryAsync(string category)
    {
        return await _context.UnitsOfMeasure
            .Where(u => u.unit_category == category)
            .OrderBy(u => u.unit_description)
            .ToListAsync();
    }

    /// <summary>
    /// Search units of measure by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching unit of measure entities</returns>
    public async Task<IEnumerable<UnitOfMeasure>> SearchUnitsAsync(string searchTerm)
    {
        return await _context.UnitsOfMeasure
            .Where(u => u.unit_description.Contains(searchTerm) ||
                       (u.unit_abbreviation != null && u.unit_abbreviation.Contains(searchTerm)) ||
                       (u.unit_category != null && u.unit_category.Contains(searchTerm)))
            .OrderBy(u => u.unit_description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new unit of measure
    /// </summary>
    /// <param name="unit">The unit of measure entity to create</param>
    /// <returns>The created unit of measure with generated ID</returns>
    public async Task<UnitOfMeasure> CreateAsync(UnitOfMeasure unit)
    {
        _context.UnitsOfMeasure.Add(unit);
        await _context.SaveChangesAsync();
        return unit;
    }

    /// <summary>
    /// Update an existing unit of measure
    /// </summary>
    /// <param name="unit">The unit of measure entity to update</param>
    /// <returns>The updated unit of measure entity</returns>
    public async Task<UnitOfMeasure> UpdateAsync(UnitOfMeasure unit)
    {
        _context.UnitsOfMeasure.Update(unit);
        await _context.SaveChangesAsync();
        return unit;
    }

    /// <summary>
    /// Delete a unit of measure by unit code
    /// </summary>
    /// <param name="unitCode">The unit code to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(short unitCode)
    {
        var unit = await GetByIdAsync(unitCode);
        if (unit == null)
            return false;

        _context.UnitsOfMeasure.Remove(unit);
        await _context.SaveChangesAsync();
        return true;
    }
}