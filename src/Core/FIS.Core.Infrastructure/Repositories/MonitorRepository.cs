using FIS.Core.Application.Interfaces;
using MonitorEntity = FIS.Core.Domain.Entities.Monitor;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class MonitorRepository : IMonitorRepository
{
    private readonly FisDbContext _context;

    public MonitorRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MonitorEntity?> GetByIdAsync(short monitorCode)
    {
        return await _context.Set<MonitorEntity>()
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.monitor_code == monitorCode);
    }

    public async Task<IEnumerable<MonitorEntity>> GetAllAsync()
    {
        return await _context.Set<MonitorEntity>()
            .Include(m => m.Vehicle)
            .OrderByDescending(m => m.Capture_dat)
            .ToListAsync();
    }

    public async Task<IEnumerable<MonitorEntity>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<MonitorEntity>()
            .Where(m => m.vmf_code == vmfCode)
            .Include(m => m.Vehicle)
            .OrderByDescending(m => m.Capture_dat)
            .ToListAsync();
    }

    public async Task<IEnumerable<MonitorEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Set<MonitorEntity>()
            .Where(m => m.Capture_dat >= startDate && m.Capture_dat <= endDate)
            .Include(m => m.Vehicle)
            .OrderByDescending(m => m.Capture_dat)
            .ToListAsync();
    }

    public async Task<MonitorEntity> CreateAsync(MonitorEntity monitor)
    {
        await _context.Set<MonitorEntity>().AddAsync(monitor);
        await _context.SaveChangesAsync();
        return monitor;
    }

    public async Task<MonitorEntity> UpdateAsync(MonitorEntity monitor)
    {
        _context.Set<MonitorEntity>().Update(monitor);
        await _context.SaveChangesAsync();
        return monitor;
    }

    public async Task DeleteAsync(short monitorCode)
    {
        var monitor = await GetByIdAsync(monitorCode);
        if (monitor != null)
        {
            _context.Set<MonitorEntity>().Remove(monitor);
            await _context.SaveChangesAsync();
        }
    }
}
