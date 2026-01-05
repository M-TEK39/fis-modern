using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for MaintenanceRecord entity operations
/// </summary>
public class MaintenanceRecordRepository : IMaintenanceRecordRepository
{
    private readonly FisDbContext _context;

    public MaintenanceRecordRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<MaintenanceRecord?> GetByIdAsync(int maintenanceId)
    {
        return await _context.MaintenanceRecords
            .FirstOrDefaultAsync(mr => mr.MaintenanceId == maintenanceId);
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.MaintenanceRecords
            .Where(mr => mr.VmfCode == vmfCode)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.MaintenanceRecords
            .Where(mr => mr.MaintenanceDate >= startDate && mr.MaintenanceDate <= endDate)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByServiceTypeAsync(string serviceType)
    {
        return await _context.MaintenanceRecords
            .Where(mr => mr.MaintenanceType == serviceType)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetAllAsync()
    {
        return await _context.MaintenanceRecords
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<MaintenanceRecord> CreateAsync(MaintenanceRecord maintenanceRecord)
    {
        maintenanceRecord.CreatedDate = DateTime.UtcNow;
        _context.MaintenanceRecords.Add(maintenanceRecord);
        await _context.SaveChangesAsync();
        return maintenanceRecord;
    }

    public async Task UpdateAsync(MaintenanceRecord maintenanceRecord)
    {
        maintenanceRecord.ModifiedDate = DateTime.UtcNow;
        _context.Entry(maintenanceRecord).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int maintenanceId)
    {
        var maintenanceRecord = await GetByIdAsync(maintenanceId);
        if (maintenanceRecord != null)
        {
            _context.MaintenanceRecords.Remove(maintenanceRecord);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<MaintenanceRecord>> SearchMaintenanceRecordsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await _context.MaintenanceRecords
                .OrderByDescending(mr => mr.MaintenanceDate)
                .ToListAsync();

        return await _context.MaintenanceRecords
            .Where(mr => mr.MaintenanceType.Contains(searchTerm) ||
                        (mr.Description != null && mr.Description.Contains(searchTerm)) ||
                        (mr.ServiceProvider != null && mr.ServiceProvider.Contains(searchTerm)) ||
                        (mr.WorkOrderNumber != null && mr.WorkOrderNumber.Contains(searchTerm)))
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }
}