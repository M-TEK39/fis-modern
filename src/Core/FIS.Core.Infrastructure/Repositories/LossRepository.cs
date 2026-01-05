using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class LossRepository : ILossRepository
{
    private readonly FisDbContext _context;

    public LossRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Loss?> GetByIdAsync(short lossCode)
    {
        return await _context.Set<Loss>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .FirstOrDefaultAsync(l => l.loss_code == lossCode);
    }

    public async Task<IEnumerable<Loss>> GetAllAsync()
    {
        return await _context.Set<Loss>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Loss>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Loss>()
            .Where(l => l.vmf_code == vmfCode)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Loss>> GetBySiteAsync(short siteCode)
    {
        return await _context.Set<Loss>()
            .Where(l => l.site_code == siteCode)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<Loss> CreateAsync(Loss loss)
    {
        await _context.Set<Loss>().AddAsync(loss);
        await _context.SaveChangesAsync();
        return loss;
    }

    public async Task<Loss> UpdateAsync(Loss loss)
    {
        _context.Set<Loss>().Update(loss);
        await _context.SaveChangesAsync();
        return loss;
    }

    public async Task DeleteAsync(short lossCode)
    {
        var loss = await GetByIdAsync(lossCode);
        if (loss != null)
        {
            _context.Set<Loss>().Remove(loss);
            await _context.SaveChangesAsync();
        }
    }
}
