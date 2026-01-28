using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class LogsheetRepository : ILogsheetRepository
{
    private readonly FisDbContext _context;

    public LogsheetRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Logsheet?> GetByIdAsync(int logCode)
    {
        return await _context.Set<Logsheet>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .FirstOrDefaultAsync(l => l.log_code == logCode);
    }

    public async Task<IEnumerable<Logsheet>> GetAllAsync()
    {
        return await _context.Set<Logsheet>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Logsheet>()
            .Where(l => l.vmf_code == vmfCode)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logsheet>> GetByMonthAsync(DateTime month)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        return await _context.Set<Logsheet>()
            .Where(l => l.month >= startOfMonth && l.month <= endOfMonth)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId)
    {
        await _context.Set<Logsheet>().AddAsync(logsheet);
        await _context.SaveChangesAsync();
        return logsheet;
    }

    public async Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId)
    {
        if (logsheet == null)
            throw new ArgumentNullException(nameof(logsheet));

        var existing = await _context.Set<Logsheet>().FindAsync(logsheet.log_code);
        if (existing == null)
            throw new InvalidOperationException($"Logsheet with log_code {logsheet.log_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(logsheet);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int logCode, int currentUserId)
    {
        var logsheet = await GetByIdAsync(logCode);
        if (logsheet != null)
        {
            _context.Set<Logsheet>().Remove(logsheet);
            await _context.SaveChangesAsync();
        }
    }
}
