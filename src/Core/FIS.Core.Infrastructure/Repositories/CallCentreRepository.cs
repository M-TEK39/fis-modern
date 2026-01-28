using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class CallCentreRepository : ICallCentreRepository
{
    private readonly FisDbContext _context;

    public CallCentreRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CallCentre?> GetByIdAsync(short callCentreCode)
    {
        return await _context.Set<CallCentre>()
            .Include(c => c.Vehicle)
            .FirstOrDefaultAsync(c => c.Call_centre_code == callCentreCode);
    }

    public async Task<IEnumerable<CallCentre>> GetAllAsync()
    {
        return await _context.Set<CallCentre>()
            .Include(c => c.Vehicle)
            .OrderByDescending(c => c.Call_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<CallCentre>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<CallCentre>()
            .Where(c => c.vmf_code == vmfCode)
            .Include(c => c.Vehicle)
            .OrderByDescending(c => c.Call_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<CallCentre>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Set<CallCentre>()
            .Where(c => c.Call_date >= startDate && c.Call_date <= endDate)
            .Include(c => c.Vehicle)
            .OrderByDescending(c => c.Call_date)
            .ToListAsync();
    }

    public async Task<CallCentre> CreateAsync(CallCentre callCentre, int currentUserId)
    {
        await _context.Set<CallCentre>().AddAsync(callCentre);
        await _context.SaveChangesAsync();
        return callCentre;
    }

    public async Task<CallCentre> UpdateAsync(CallCentre callCentre, int currentUserId)
    {
        if (callCentre == null)
            throw new ArgumentNullException(nameof(callCentre));

        var existing = await _context.Set<CallCentre>().FindAsync(callCentre.Call_centre_code);
        if (existing == null)
            throw new InvalidOperationException($"CallCentre with Call_centre_code {callCentre.Call_centre_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(callCentre);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(short callCentreCode, int currentUserId)
    {
        var callCentre = await GetByIdAsync(callCentreCode);
        if (callCentre != null)
        {
            _context.Set<CallCentre>().Remove(callCentre);
            await _context.SaveChangesAsync();
        }
    }
}
