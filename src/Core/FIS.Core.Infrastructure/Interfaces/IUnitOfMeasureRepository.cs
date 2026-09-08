using FIS.Core.Domain.Entities;

namespace FIS.Core.Infrastructure.Interfaces;

/// <summary>
/// Repository interface for UnitOfMeasure entity operations
/// Provides data access abstraction for unit of measure management
/// </summary>
public interface IUnitOfMeasureRepository
{
    /// <summary>
    /// Get unit of measure by unit code
    /// </summary>
    /// <param name="unitCode">The unit code to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    Task<UnitOfMeasure?> GetByIdAsync(short unitCode);

    /// <summary>
    /// Get unit of measure by description
    /// </summary>
    /// <param name="description">The unit description to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    Task<UnitOfMeasure?> GetByDescriptionAsync(string description);

    /// <summary>
    /// Get unit of measure by abbreviation
    /// </summary>
    /// <param name="abbreviation">The unit abbreviation to search for</param>
    /// <returns>UnitOfMeasure entity if found, null otherwise</returns>
    Task<UnitOfMeasure?> GetByAbbreviationAsync(string abbreviation);

    /// <summary>
    /// Get all units of measure
    /// </summary>
    /// <returns>List of all unit of measure entities</returns>
    Task<IEnumerable<UnitOfMeasure>> GetAllUnitsAsync();

    /// <summary>
    /// Get units of measure by category
    /// </summary>
    /// <param name="category">The unit category to filter by</param>
    /// <returns>List of units in the specified category</returns>
    Task<IEnumerable<UnitOfMeasure>> GetByCategoryAsync(string category);

    /// <summary>
    /// Search units of measure by partial description match
    /// </summary>
    /// <param name="searchTerm">The search term to match against descriptions</param>
    /// <returns>List of matching unit of measure entities</returns>
    Task<IEnumerable<UnitOfMeasure>> SearchUnitsAsync(string searchTerm);

    /// <summary>
    /// Create a new unit of measure
    /// </summary>
    /// <param name="unit">The unit of measure entity to create</param>
    /// <returns>The created unit of measure with generated ID</returns>
    Task<UnitOfMeasure> CreateAsync(UnitOfMeasure unit, int currentUserId);

    /// <summary>
    /// Update an existing unit of measure
    /// </summary>
    /// <param name="unit">The unit of measure entity to update</param>
    /// <returns>The updated unit of measure entity</returns>
    Task<UnitOfMeasure> UpdateAsync(UnitOfMeasure unit, int currentUserId);

    /// <summary>
    /// Delete a unit of measure by unit code
    /// </summary>
    /// <param name="unitCode">The unit code to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(short unitCode, int currentUserId);
}
