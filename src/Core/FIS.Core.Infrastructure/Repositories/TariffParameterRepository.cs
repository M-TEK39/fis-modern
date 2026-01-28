using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for TariffParameter table (modern tariff system parameters).
/// Handles fiscal year parameters for tariff calculations.
/// </summary>
public class TariffParameterRepository : ITariffParameterRepository
{
    private readonly FisDbContext _context;

    public TariffParameterRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get tariff parameter by year.
    /// </summary>
    public async Task<TariffParameter?> GetByYearAsync(int year)
    {
        return await _context.Set<TariffParameter>()
            .FirstOrDefaultAsync(tp => tp.TariffParameterYear == year);
    }

    /// <summary>
    /// Get current/active tariff parameter.
    /// Returns the parameter for the current fiscal year.
    /// </summary>
    public async Task<TariffParameter?> GetCurrentAsync()
    {
        var currentYear = DateTime.Now.Year;

        // Try current year first
        var parameter = await GetByYearAsync(currentYear);
        if (parameter != null && parameter.Approved)
            return parameter;

        // If not found or not approved, get the most recent approved parameter
        return await _context.Set<TariffParameter>()
            .Where(tp => tp.Approved)
            .OrderByDescending(tp => tp.TariffParameterYear)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get tariff parameter by ID.
    /// </summary>
    public async Task<TariffParameter?> GetByIdAsync(int tariffParameterId)
    {
        return await _context.Set<TariffParameter>()
            .FirstOrDefaultAsync(tp => tp.TariffParameterID == tariffParameterId);
    }

    /// <summary>
    /// Get all tariff parameters ordered by year descending.
    /// </summary>
    public async Task<List<TariffParameter>> GetAllAsync()
    {
        return await _context.Set<TariffParameter>()
            .OrderByDescending(tp => tp.TariffParameterYear)
            .ToListAsync();
    }

    /// <summary>
    /// Get approved tariff parameters only.
    /// </summary>
    public async Task<List<TariffParameter>> GetApprovedAsync()
    {
        return await _context.Set<TariffParameter>()
            .Where(tp => tp.Approved)
            .OrderByDescending(tp => tp.TariffParameterYear)
            .ToListAsync();
    }

    /// <summary>
    /// Create new tariff parameter.
    /// </summary>
    public async Task<TariffParameter> CreateAsync(TariffParameter tariffParameter, int currentUserId)
    {
        tariffParameter.CaptureDate = DateTime.Now;
        tariffParameter.Approved = false; // New parameters start as unapproved
        _context.Set<TariffParameter>().Add(tariffParameter);
        await _context.SaveChangesAsync();
        return tariffParameter;
    }

    /// <summary>
    /// Update existing tariff parameter.
    /// </summary>
    public async Task<TariffParameter> UpdateAsync(TariffParameter tariffParameter, int currentUserId)
    {
        if (tariffParameter == null)
            throw new ArgumentNullException(nameof(tariffParameter));

        var existing = await _context.Set<TariffParameter>().FindAsync(tariffParameter.TariffParameterID);
        if (existing == null)
            throw new InvalidOperationException($"TariffParameter with TariffParameterID {tariffParameter.TariffParameterID} not found");

        tariffParameter.ModifiedDate = DateTime.Now;
        _context.Entry(existing).CurrentValues.SetValues(tariffParameter);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete tariff parameter.
    /// </summary>
    public async Task DeleteAsync(int tariffParameterId, int currentUserId)
    {
        var tariffParameter = await GetByIdAsync(tariffParameterId);
        if (tariffParameter != null)
        {
            _context.Set<TariffParameter>().Remove(tariffParameter);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Check if tariff parameter exists for a year.
    /// </summary>
    public async Task<bool> ExistsForYearAsync(int year)
    {
        return await _context.Set<TariffParameter>()
            .AnyAsync(tp => tp.TariffParameterYear == year);
    }
}
