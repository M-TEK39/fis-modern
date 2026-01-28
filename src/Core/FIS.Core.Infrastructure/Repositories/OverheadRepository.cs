using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Overhead table.
/// Handles overhead costs for tariff calculations.
/// </summary>
public class OverheadRepository : IOverheadRepository
{
    private readonly FisDbContext _context;

    public OverheadRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get all overheads for a tariff parameter year.
    /// </summary>
    public async Task<List<Overhead>> GetByTariffParameterAsync(int tariffParameterId)
    {
        return await _context.Set<Overhead>()
            .Where(o => o.TariffParameterID == tariffParameterId)
            .OrderBy(o => o.OverheadTypeId)
            .ToListAsync();
    }

    /// <summary>
    /// Get overhead by type for a tariff parameter year.
    /// </summary>
    public async Task<Overhead?> GetByTypeAsync(int tariffParameterId, int overheadTypeId)
    {
        return await _context.Set<Overhead>()
            .FirstOrDefaultAsync(o =>
                o.TariffParameterID == tariffParameterId &&
                o.OverheadTypeId == overheadTypeId);
    }

    /// <summary>
    /// Get overhead by ID.
    /// </summary>
    public async Task<Overhead?> GetByIdAsync(int overheadId)
    {
        return await _context.Set<Overhead>()
            .FirstOrDefaultAsync(o => o.OverheadId == overheadId);
    }

    /// <summary>
    /// Get total overhead amount for a tariff parameter year.
    /// </summary>
    public async Task<decimal> GetTotalAsync(int tariffParameterId)
    {
        return await _context.Set<Overhead>()
            .Where(o => o.TariffParameterID == tariffParameterId)
            .SumAsync(o => o.OverheadAmount);
    }

    /// <summary>
    /// Create new overhead.
    /// </summary>
    public async Task<Overhead> CreateAsync(Overhead overhead, int currentUserId)
    {
        overhead.CaptureDate = DateTime.Now;
        _context.Set<Overhead>().Add(overhead);
        await _context.SaveChangesAsync();
        return overhead;
    }

    /// <summary>
    /// Update existing overhead.
    /// </summary>
    public async Task<Overhead> UpdateAsync(Overhead overhead, int currentUserId)
    {
        if (overhead == null)
            throw new ArgumentNullException(nameof(overhead));

        var existing = await _context.Set<Overhead>().FindAsync(overhead.OverheadId);
        if (existing == null)
            throw new InvalidOperationException($"Overhead with OverheadId {overhead.OverheadId} not found");

        overhead.ModifiedDate = DateTime.Now;
        _context.Entry(existing).CurrentValues.SetValues(overhead);
        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Delete overhead.
    /// </summary>
    public async Task DeleteAsync(int overheadId, int currentUserId)
    {
        var overhead = await GetByIdAsync(overheadId);
        if (overhead != null)
        {
            _context.Set<Overhead>().Remove(overhead);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete all overheads for a tariff parameter year.
    /// </summary>
    public async Task DeleteByTariffParameterAsync(int tariffParameterId)
    {
        var overheads = await GetByTariffParameterAsync(tariffParameterId);
        _context.Set<Overhead>().RemoveRange(overheads);
        await _context.SaveChangesAsync();
    }
}
