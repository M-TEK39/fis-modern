using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class TaxiLogRepository : ITaxiLogRepository
{
    private readonly FisDbContext _context;

    public TaxiLogRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TaxiLog?> GetByIdAsync(int logId)
    {
        return await _context.TaxiLogs
            .FirstOrDefaultAsync(log => log.log_id == logId && !log.is_deleted);
    }

    public async Task<TaxiLog?> GetLatestByRequisitionAsync(string rekNum)
    {
        var normalized = (rekNum ?? string.Empty).Trim().ToUpperInvariant();

        return await _context.TaxiLogs
            .Where(log => !log.is_deleted && log.rek_num != null && log.rek_num.ToUpper() == normalized)
            .Where(log => !_context.TaxiLogs.Any(child => child.parent_taxi_log_code == log.log_id && !child.is_deleted))
            .OrderByDescending(log => log.log_id)
            .FirstOrDefaultAsync();
    }

    public async Task<TaxiLog> CreateAsync(TaxiLog log, int currentUserId)
    {
        log.date_created = DateTime.Now;
        log.created_by_user_code = currentUserId;
        log.is_deleted = false;

        await _context.TaxiLogs.AddAsync(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<TaxiLog> UpdateAsync(TaxiLog log, int currentUserId)
    {
        var existing = await _context.TaxiLogs.FindAsync(log.log_id)
            ?? throw new InvalidOperationException($"Taxi log with log_id {log.log_id} not found");

        var createdDate = existing.date_created;
        var createdByUserCode = existing.created_by_user_code;

        _context.Entry(existing).CurrentValues.SetValues(log);
        existing.date_created = createdDate;
        existing.created_by_user_code = createdByUserCode;
        existing.date_updated = DateTime.Now;
        existing.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
        return existing;
    }
}
