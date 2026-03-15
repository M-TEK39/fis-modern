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

    public async Task<Taxi?> GetLatestByRequisitionAsync(string rekNum)
    {
        var normalized = (rekNum ?? string.Empty).Trim().ToUpperInvariant();

        return await _context.Set<Taxi>()
            .Include(t => t.Department)
            .Include(t => t.Site)
            .Where(t => t.rek_num != null && t.rek_num.ToUpper() == normalized)
            .Where(t => !_context.Set<Taxi>().Any(child => child.parent_taxi_code == t.request_id))
            .OrderByDescending(t => t.request_id)
            .FirstOrDefaultAsync();
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

    public async Task<Taxi> CreateAsync(Taxi taxi, int currentUserId)
    {
        await _context.Set<Taxi>().AddAsync(taxi);
        await _context.SaveChangesAsync();
        return taxi;
    }

    public async Task<Taxi> UpdateAsync(Taxi taxi, int currentUserId)
    {
        if (taxi == null)
            throw new ArgumentNullException(nameof(taxi));

        var existing = await _context.Set<Taxi>().FindAsync(taxi.request_id);
        if (existing == null)
            throw new InvalidOperationException($"Taxi with request_id {taxi.request_id} not found");

        _context.Entry(existing).CurrentValues.SetValues(taxi);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int requestId, int currentUserId)
    {
        var taxi = await GetByIdAsync(requestId);
        if (taxi != null)
        {
            _context.Set<Taxi>().Remove(taxi);
            await _context.SaveChangesAsync();
        }
    }
}
