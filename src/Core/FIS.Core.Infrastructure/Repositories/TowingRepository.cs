using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class TowingRepository : ITowingRepository
{
    private readonly FisDbContext _context;

    public TowingRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Towing?> GetByIdAsync(short towingCode)
    {
        return await _context.Set<Towing>()
            .Include(t => t.Vehicle)
            .Include(t => t.Site)
            .FirstOrDefaultAsync(t => t.Towing_code == towingCode);
    }

    public async Task<IEnumerable<Towing>> GetAllAsync()
    {
        return await _context.Set<Towing>()
            .Include(t => t.Vehicle)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Towing>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Towing>()
            .Where(t => t.vmf_code == vmfCode)
            .Include(t => t.Vehicle)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Towing>> GetBySiteAsync(short siteCode)
    {
        return await _context.Set<Towing>()
            .Where(t => t.Site_code == siteCode)
            .Include(t => t.Vehicle)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<Towing> CreateAsync(Towing towing)
    {
        await _context.Set<Towing>().AddAsync(towing);
        await _context.SaveChangesAsync();
        return towing;
    }

    public async Task<Towing> UpdateAsync(Towing towing)
    {
        _context.Set<Towing>().Update(towing);
        await _context.SaveChangesAsync();
        return towing;
    }

    public async Task DeleteAsync(short towingCode)
    {
        var towing = await GetByIdAsync(towingCode);
        if (towing != null)
        {
            _context.Set<Towing>().Remove(towing);
            await _context.SaveChangesAsync();
        }
    }
}
