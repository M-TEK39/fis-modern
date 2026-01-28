using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class LogbookRepository : ILogbookRepository
{
    private readonly FisDbContext _context;

    public LogbookRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Logbook?> GetByIdAsync(short logbookCode)
    {
        return await _context.Set<Logbook>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .FirstOrDefaultAsync(l => l.logbookcode == logbookCode);
    }

    public async Task<IEnumerable<Logbook>> GetAllAsync()
    {
        return await _context.Set<Logbook>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logbook>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Logbook>()
            .Where(l => l.vmf_code == vmfCode)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logbook>> GetBySiteAsync(short siteCode)
    {
        return await _context.Set<Logbook>()
            .Where(l => l.site_code == siteCode)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<Logbook> CreateAsync(Logbook logbook, int currentUserId)
    {
        await _context.Set<Logbook>().AddAsync(logbook);
        await _context.SaveChangesAsync();
        return logbook;
    }

    public async Task<Logbook> UpdateAsync(Logbook logbook, int currentUserId)
    {
        if (logbook == null)
            throw new ArgumentNullException(nameof(logbook));

        var existing = await _context.Set<Logbook>().FindAsync(logbook.logbookcode);
        if (existing == null)
            throw new InvalidOperationException($"Logbook with logbookcode {logbook.logbookcode} not found");

        _context.Entry(existing).CurrentValues.SetValues(logbook);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(short logbookCode, int currentUserId)
    {
        var logbook = await GetByIdAsync(logbookCode);
        if (logbook != null)
        {
            _context.Set<Logbook>().Remove(logbook);
            await _context.SaveChangesAsync();
        }
    }
}
