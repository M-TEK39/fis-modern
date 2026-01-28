using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class ClearanceRepository : IClearanceRepository
{
    private readonly FisDbContext _context;

    public ClearanceRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Clearance?> GetByIdAsync(int clearanceCode)
    {
        return await _context.Set<Clearance>()
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.clearance_code == clearanceCode);
    }

    public async Task<IEnumerable<Clearance>> GetAllAsync()
    {
        return await _context.Set<Clearance>()
            .Include(c => c.Vehicle)
            .ToListAsync();
    }

    public async Task<IEnumerable<Clearance>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Clearance>()
            .Where(c => c.vmf_code == vmfCode)
            .Include(c => c.Vehicle)
            .ToListAsync();
    }

    public async Task<Clearance> CreateAsync(Clearance clearance, int currentUserId)
    {
        await _context.Set<Clearance>().AddAsync(clearance);
        await _context.SaveChangesAsync();
        return clearance;
    }

    public async Task<Clearance> UpdateAsync(Clearance clearance, int currentUserId)
    {
        if (clearance == null)
            throw new ArgumentNullException(nameof(clearance));

        var existing = await _context.Set<Clearance>().FindAsync(clearance.clearance_code);
        if (existing == null)
            throw new InvalidOperationException($"Clearance with clearance_code {clearance.clearance_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(clearance);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int clearanceCode, int currentUserId)
    {
        var clearance = await GetByIdAsync(clearanceCode);
        if (clearance != null)
        {
            _context.Set<Clearance>().Remove(clearance);
            await _context.SaveChangesAsync();
        }
    }
}
