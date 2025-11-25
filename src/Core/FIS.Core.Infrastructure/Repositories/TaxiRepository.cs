using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class TaxiRepository : ITaxiRepository
{
    private readonly FisDbContext _context;

    public TaxiRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Taxi?> GetByIdAsync(int requestId)
    {
        return await _context.Set<Taxi>()
            .Include(t => t.Department)
            .Include(t => t.Site)
            .FirstOrDefaultAsync(t => t.request_id == requestId);
    }

    public async Task<IEnumerable<Taxi>> GetAllAsync()
    {
        return await _context.Set<Taxi>()
            .Include(t => t.Department)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Taxi>> GetBySiteAsync(short siteCode)
    {
        return await _context.Set<Taxi>()
            .Where(t => t.site_code == siteCode)
            .Include(t => t.Department)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Taxi>> GetByDepartmentAsync(short departmentCode)
    {
        return await _context.Set<Taxi>()
            .Where(t => t.department_code == departmentCode)
            .Include(t => t.Department)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Taxi>> GetByDateAsync(DateTime date)
    {
        return await _context.Set<Taxi>()
            .Where(t => t.date_required.Date == date.Date)
            .Include(t => t.Department)
            .Include(t => t.Site)
            .ToListAsync();
    }

    public async Task<Taxi> CreateAsync(Taxi taxi)
    {
        await _context.Set<Taxi>().AddAsync(taxi);
        await _context.SaveChangesAsync();
        return taxi;
    }

    public async Task<Taxi> UpdateAsync(Taxi taxi)
    {
        _context.Set<Taxi>().Update(taxi);
        await _context.SaveChangesAsync();
        return taxi;
    }

    public async Task DeleteAsync(int requestId)
    {
        var taxi = await GetByIdAsync(requestId);
        if (taxi != null)
        {
            _context.Set<Taxi>().Remove(taxi);
            await _context.SaveChangesAsync();
        }
    }
}
