using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for TariffWeightCalculation table.
/// Handles weight calculations for overhead distribution across vehicle categories.
/// </summary>
public class TariffWeightCalculationRepository : ITariffWeightCalculationRepository
{
    private readonly FisDbContext _context;

    public TariffWeightCalculationRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get all weight calculations for a tariff parameter year.
    /// </summary>
    public async Task<List<TariffWeightCalculation>> GetByTariffParameterAsync(int tariffParameterId)
    {
        return await _context.Set<TariffWeightCalculation>()
            .Where(twc => twc.TariffParameterID == tariffParameterId)
            .OrderBy(twc => twc.category)
            .ToListAsync();
    }

    /// <summary>
    /// Get weight calculation for a specific category.
    /// </summary>
    public async Task<TariffWeightCalculation?> GetByCategoryAsync(int tariffParameterId, string category)
    {
        // Note: The entity has category as decimal, which seems unusual
        // Attempting to parse the string to decimal for comparison
        if (decimal.TryParse(category, out decimal categoryValue))
        {
            return await _context.Set<TariffWeightCalculation>()
                .FirstOrDefaultAsync(twc =>
                    twc.TariffParameterID == tariffParameterId &&
                    twc.category == categoryValue);
        }
        return null;
    }

    /// <summary>
    /// Get weight calculation by ID.
    /// </summary>
    public async Task<TariffWeightCalculation?> GetByIdAsync(int tariffWeightCalculationId)
    {
        return await _context.Set<TariffWeightCalculation>()
            .FirstOrDefaultAsync(twc => twc.TariffWeightCalculation_Code == tariffWeightCalculationId);
    }

    /// <summary>
    /// Get total weight for a tariff parameter year.
    /// Formula: Sum(number * WeightFactorPerUnit)
    /// </summary>
    public async Task<decimal> GetTotalWeightAsync(int tariffParameterId)
    {
        var calculations = await GetByTariffParameterAsync(tariffParameterId);

        return calculations
            .Where(twc => twc.number.HasValue && twc.WeightFactorPerUnit.HasValue)
            .Sum(twc => (decimal)(twc.number!.Value * twc.WeightFactorPerUnit!.Value));
    }

    /// <summary>
    /// Create new weight calculation.
    /// </summary>
    public async Task<TariffWeightCalculation> CreateAsync(TariffWeightCalculation weightCalculation, int currentUserId)
    {
        weightCalculation.calculation_date_time = DateTime.Now;
        _context.Set<TariffWeightCalculation>().Add(weightCalculation);
        await _context.SaveChangesAsync();
        return weightCalculation;
    }

    /// <summary>
    /// Update existing weight calculation.
    /// </summary>
    public async Task<TariffWeightCalculation> UpdateAsync(TariffWeightCalculation weightCalculation, int currentUserId)
    {
        if (weightCalculation == null)
            throw new ArgumentNullException(nameof(weightCalculation));

        var existing = await _context.Set<TariffWeightCalculation>().FindAsync(weightCalculation.TariffWeightCalculation_Code);
        if (existing == null)
            throw new InvalidOperationException($"TariffWeightCalculation with TariffWeightCalculation_Code {weightCalculation.TariffWeightCalculation_Code} not found");

        weightCalculation.calculation_date_time = DateTime.Now;
        _context.Entry(existing).CurrentValues.SetValues(weightCalculation);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete weight calculation.
    /// </summary>
    public async Task DeleteAsync(int tariffWeightCalculationId, int currentUserId)
    {
        var weightCalculation = await GetByIdAsync(tariffWeightCalculationId);
        if (weightCalculation != null)
        {
            _context.Set<TariffWeightCalculation>().Remove(weightCalculation);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete all weight calculations for a tariff parameter year.
    /// </summary>
    public async Task DeleteByTariffParameterAsync(int tariffParameterId)
    {
        var calculations = await GetByTariffParameterAsync(tariffParameterId);
        _context.Set<TariffWeightCalculation>().RemoveRange(calculations);
        await _context.SaveChangesAsync();
    }
}
