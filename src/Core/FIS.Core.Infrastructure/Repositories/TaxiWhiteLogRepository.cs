using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class TaxiWhiteLogRepository : ITaxiWhiteLogRepository
{
    private readonly FisDbContext _context;

    public TaxiWhiteLogRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TaxiWhiteLog?> GetByIdAsync(int logId)
    {
        return await _context.TaxiWhiteLogs
            .FirstOrDefaultAsync(l => l.Log_id == logId && !l.is_deleted);
    }

    public async Task<IEnumerable<TaxiWhiteLog>> GetAllAsync()
    {
        return await _context.TaxiWhiteLogs
            .Where(l => !l.is_deleted)
            .OrderByDescending(l => l.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<TaxiWhiteLog>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.TaxiWhiteLogs
            .Where(l => l.vmf_code == vmfCode && !l.is_deleted)
            .OrderByDescending(l => l.start_date)
            .ToListAsync();
    }

    public async Task<TaxiWhiteLog> CreateAsync(TaxiWhiteLog log, int currentUserId)
    {
        log.date_created = DateTime.Now;
        log.created_by_user_code = currentUserId;
        log.is_deleted = false;

        await _context.TaxiWhiteLogs.AddAsync(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task DeleteAsync(int logId, int currentUserId)
    {
        var log = await _context.TaxiWhiteLogs.FindAsync(logId);
        if (log != null)
        {
            log.is_deleted = true;
            log.date_updated = DateTime.Now;
            log.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}
