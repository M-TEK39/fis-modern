using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for TariffWeightCalculation table.
/// Handles weight calculations for overhead distribution across vehicle categories.
/// </summary>
public interface ITariffWeightCalculationRepository
{
    /// <summary>
    /// Get all weight calculations for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of weight calculations</returns>
    Task<List<TariffWeightCalculation>> GetByTariffParameterAsync(int tariffParameterId);

    /// <summary>
    /// Get weight calculation for a specific category.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="category">Vehicle category</param>
    /// <returns>Weight calculation or null if not found</returns>
    Task<TariffWeightCalculation?> GetByCategoryAsync(int tariffParameterId, string category);

    /// <summary>
    /// Get weight calculation by ID.
    /// </summary>
    /// <param name="tariffWeightCalculationId">Weight calculation identifier</param>
    /// <returns>Weight calculation or null if not found</returns>
    Task<TariffWeightCalculation?> GetByIdAsync(int tariffWeightCalculationId);

    /// <summary>
    /// Get total weight for a tariff parameter year.
    /// Formula: Sum(number * WeightFactorPerUnit)
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Total weight</returns>
    Task<decimal> GetTotalWeightAsync(int tariffParameterId);

    /// <summary>
    /// Create new weight calculation.
    /// </summary>
    /// <param name="weightCalculation">Weight calculation to create</param>
    /// <returns>Created weight calculation</returns>
    Task<TariffWeightCalculation> CreateAsync(TariffWeightCalculation weightCalculation, int currentUserId);

    /// <summary>
    /// Update existing weight calculation.
    /// </summary>
    /// <param name="weightCalculation">Weight calculation to update</param>
    /// <returns>Updated weight calculation</returns>
    Task<TariffWeightCalculation> UpdateAsync(TariffWeightCalculation weightCalculation, int currentUserId);

    /// <summary>
    /// Delete weight calculation.
    /// </summary>
    /// <param name="tariffWeightCalculationId">Weight calculation identifier</param>
    Task DeleteAsync(int tariffWeightCalculationId, int currentUserId);

    /// <summary>
    /// Delete all weight calculations for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    Task DeleteByTariffParameterAsync(int tariffParameterId);
}
