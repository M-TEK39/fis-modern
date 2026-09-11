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
        return await _context
            .UnitsOfMeasure.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(u => u.unit_of_measure_code == unitCode);
    }

    /// <summary>
    /// Get unit of measure by description
    /// </summary>
    /// <param name="description">The unit description to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    public async Task<UnitOfMeasure?> GetByDescriptionAsync(string description)
    {
        return await _context
            .UnitsOfMeasure.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(u => u.unit_description == description);
    }

    /// <summary>
    /// Get unit of measure by abbreviation
    /// </summary>
    /// <param name="abbreviation">The unit abbreviation to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    public async Task<UnitOfMeasure?> GetByAbbreviationAsync(string abbreviation)
    {
        return await _context
            .UnitsOfMeasure.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(u => u.unit_abbreviation == abbreviation);
    }

    /// <summary>
    /// Get all units of measure
    /// </summary>
    /// <returns>List of all unit of measure entities</returns>
    public async Task<IEnumerable<UnitOfMeasure>> GetAllUnitsAsync()
    {
        return await _context
            .UnitsOfMeasure.Where(x => !x.is_deleted)
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
        return await _context
            .UnitsOfMeasure.Where(u => u.unit_category == category)
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
        return await _context
            .UnitsOfMeasure.Where(u =>
                u.unit_description.Contains(searchTerm)
                || (u.unit_abbreviation != null && u.unit_abbreviation.Contains(searchTerm))
                || (u.unit_category != null && u.unit_category.Contains(searchTerm))
            )
            .OrderBy(u => u.unit_description)
            .ToListAsync();
    }

    public async Task<UnitOfMeasurePage> GetPageAsync(int page, int pageSize)
    {
        var resolvedPage = Math.Max(1, page);
        var resolvedPageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.UnitsOfMeasure.AsNoTracking().Where(unit => !unit.is_deleted);
        var total = await query.CountAsync();
        var items = await query
            .OrderBy(unit => unit.unit_category)
            .ThenBy(unit => unit.unit_description)
            .ThenBy(unit => unit.unit_of_measure_code)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync();
        return new UnitOfMeasurePage(items, resolvedPage, resolvedPageSize, total);
    }

    /// <summary>
    /// Create a new unit of measure
    /// </summary>
    /// <param name="unit">The unit of measure entity to create</param>
    /// <param name="currentUserId">The user creating the unit</param>
    /// <returns>The created unit of measure with generated ID</returns>
    public async Task<UnitOfMeasure> CreateAsync(UnitOfMeasure unit, int currentUserId)
    {
        // Auto-populate audit fields
        unit.date_created = DateTime.UtcNow;
        unit.created_by_user_code = currentUserId;
        unit.is_deleted = false;

        _context.UnitsOfMeasure.Add(unit);
        await _context.SaveChangesAsync();
        return unit;
    }

    /// <summary>
    /// Update an existing unit of measure
    /// </summary>
    /// <param name="unit">The unit of measure entity to update</param>
    /// <param name="currentUserId">The user updating the unit</param>
    /// <returns>The updated unit of measure entity</returns>
    public async Task<UnitOfMeasure> UpdateAsync(UnitOfMeasure unit, int currentUserId)
    {
        if (unit == null)
            throw new ArgumentNullException(nameof(unit));

        var existing = await _context.UnitsOfMeasure.FindAsync(unit.unit_of_measure_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"UnitOfMeasure with unit_of_measure_code {unit.unit_of_measure_code} not found"
            );

        // Preserve creation audit fields
        unit.date_created = existing.date_created;
        unit.created_by_user_code = existing.created_by_user_code;
        // Set update audit fields
        unit.date_updated = DateTime.UtcNow;
        unit.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(unit);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete a unit of measure by unit code
    /// </summary>
    /// <param name="unitCode">The unit code to delete</param>
    /// <param name="currentUserId">The user deleting the unit</param>
    /// <returns>True if deleted, false if not found</returns>
    public async Task<bool> DeleteAsync(short unitCode, int currentUserId)
    {
        var unit = await GetByIdAsync(unitCode);
        if (unit == null)
            return false;

        // Soft delete instead of hard delete
        unit.is_deleted = true;
        unit.date_updated = DateTime.UtcNow;
        unit.modified_by_user_code = currentUserId;
        await _context.SaveChangesAsync();
        return true;
    }
}
