using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository for vehicle licence history snapshots.
/// </summary>
public class VehicleLicenceHistoryRepository : IVehicleLicenceHistoryRepository
{
    private readonly FisDbContext _context;

    public VehicleLicenceHistoryRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<VehicleLicenceHistory>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.VehicleLicenceHistories
            .Include(h => h.CapturedByUser)
            .Where(h => h.vmf_code == vmfCode)
            .OrderByDescending(h => h.captured_at)
            .ToListAsync();
    }

    public async Task<VehicleLicenceHistory?> GetLatestByVehicleAsync(int vmfCode)
    {
        return await _context.VehicleLicenceHistories
            .Include(h => h.CapturedByUser)
            .Where(h => h.vmf_code == vmfCode)
            .OrderByDescending(h => h.captured_at)
            .FirstOrDefaultAsync();
    }

    public async Task<VehicleLicenceHistory> CreateAsync(VehicleLicenceHistory history)
    {
        history.captured_at = DateTime.UtcNow;
        _context.VehicleLicenceHistories.Add(history);
        await _context.SaveChangesAsync();
        return history;
    }
}
