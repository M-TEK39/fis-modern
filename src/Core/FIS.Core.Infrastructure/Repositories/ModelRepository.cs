using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for model operations against legacy model table
/// Handles vehicle model data with detailed specifications and legacy schema compatibility
/// </summary>
public class ModelRepository : IModelRepository
{
    private readonly FisDbContext _context;

    public ModelRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get model by model code (primary key)
    /// </summary>
    public async Task<Model?> GetByIdAsync(short modelCode)
    {
        return await _context.Models
            .Include(m => m.Make)
            .FirstOrDefaultAsync(m => m.model_code == modelCode);
    }

    /// <summary>
    /// Get model by name/description
    /// </summary>
    public async Task<Model?> GetByNameAsync(string modelName)
    {
        return await _context.Models
            .Include(m => m.Make)
            .FirstOrDefaultAsync(m => m.model_description.ToLower() == modelName.ToLower());
    }

    /// <summary>
    /// Get all models with their makes
    /// </summary>
    public async Task<IEnumerable<Model>> GetAllModelsAsync()
    {
        return await _context.Models
            .Include(m => m.Make)
            .OrderBy(m => m.Make!.make_description)
            .ThenBy(m => m.model_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get models by make
    /// </summary>
    public async Task<IEnumerable<Model>> GetModelsByMakeAsync(short makeCode)
    {
        return await _context.Models
            .Include(m => m.Make)
            .Where(m => m.make_code == makeCode)
            .OrderBy(m => m.model_description)
            .ToListAsync();
    }

    /// <summary>
    /// Search models by name/description
    /// </summary>
    public async Task<IEnumerable<Model>> SearchModelsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllModelsAsync();

        return await _context.Models
            .Include(m => m.Make)
            .Where(m => m.model_description.ToLower().Contains(searchTerm.ToLower()) ||
                       m.Make!.make_description.ToLower().Contains(searchTerm.ToLower()))
            .OrderBy(m => m.Make!.make_description)
            .ThenBy(m => m.model_description)
            .ToListAsync();
    }

    /// <summary>
    /// Get models by engine type
    /// </summary>
    public async Task<IEnumerable<Model>> GetModelsByEngineTypeAsync(string engineType)
    {
        if (string.IsNullOrWhiteSpace(engineType))
            return await GetAllModelsAsync();

        return await _context.Models
            .Include(m => m.Make)
            .Where(m => !string.IsNullOrEmpty(m.engine_type) && 
                       m.engine_type.ToLower().Contains(engineType.ToLower()))
            .OrderBy(m => m.Make!.make_description)
            .ThenBy(m => m.model_description)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new model
    /// </summary>
    public async Task<Model> CreateAsync(Model model, int currentUserId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        // Auto-populate audit fields
            model.date_created = DateTime.UtcNow;
            model.is_deleted = false;
            
            _context.Models.Add(model);
        await _context.SaveChangesAsync();
        return model;
    }

    /// <summary>
    /// Update an existing model
    /// </summary>
    public async Task<Model> UpdateAsync(Model model, int currentUserId)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var existing = await _context.Models.FindAsync(model.model_code);
        if (existing == null)
            throw new InvalidOperationException($"Model with code {model.model_code} not found");

        // Update properties using EF Core's SetValues (handles all properties automatically)
        _context.Entry(existing).CurrentValues.SetValues(model);

        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete a model
    /// </summary>
    public async Task DeleteAsync(short modelCode, int currentUserId)
    {
        var model = await GetByIdAsync(modelCode);
        if (model != null)
        {
            // Soft delete instead of hard delete
                model.is_deleted = true;
                model.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}