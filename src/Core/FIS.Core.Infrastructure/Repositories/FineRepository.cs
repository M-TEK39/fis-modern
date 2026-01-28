using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class FineRepository : IFineRepository
{
    private readonly FisDbContext _context;

    public FineRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Fine?> GetByIdAsync(int fineCode)
    {
        return await _context.Set<Fine>()
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .FirstOrDefaultAsync(f => f.Fine_code == fineCode);
    }

    public async Task<IEnumerable<Fine>> GetAllAsync()
    {
        return await _context.Set<Fine>()
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Fine>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Fine>()
            .Where(f => f.vmf_code == vmfCode)
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Fine>> GetBySiteAsync(short siteCode)
    {
        return await _context.Set<Fine>()
            .Where(f => f.Site_code == siteCode)
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Fine>> GetUnpaidFinesAsync()
    {
        return await _context.Set<Fine>()
            .Where(f => f.Fine_pay_date == null)
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .ToListAsync();
    }

    public async Task<Fine> CreateAsync(Fine fine, int currentUserId)
    {
        await _context.Set<Fine>().AddAsync(fine);
        await _context.SaveChangesAsync();
        return fine;
    }

    public async Task<Fine> UpdateAsync(Fine fine, int currentUserId)
    {
        if (fine == null)
            throw new ArgumentNullException(nameof(fine));

        var existing = await _context.Set<Fine>().FindAsync(fine.Fine_code);
        if (existing == null)
            throw new InvalidOperationException($"Fine with Fine_code {fine.Fine_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(fine);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int fineCode, int currentUserId)
    {
        var fine = await GetByIdAsync(fineCode);
        if (fine != null)
        {
            _context.Set<Fine>().Remove(fine);
            await _context.SaveChangesAsync();
        }
    }
}
