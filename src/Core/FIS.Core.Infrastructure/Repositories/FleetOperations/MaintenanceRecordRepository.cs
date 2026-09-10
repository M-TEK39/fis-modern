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
        return await _context
            .MaintenanceRecords.Where(x => !x.is_deleted)
            .FirstOrDefaultAsync(mr => mr.MaintenanceId == maintenanceId);
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByVehicleAsync(int vmfCode)
    {
        return await _context
            .MaintenanceRecords.Where(mr => mr.VmfCode == vmfCode)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        return await _context
            .MaintenanceRecords.Where(mr =>
                mr.MaintenanceDate >= startDate && mr.MaintenanceDate <= endDate
            )
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByServiceTypeAsync(string serviceType)
    {
        return await _context
            .MaintenanceRecords.Where(mr => mr.MaintenanceType == serviceType)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetAllAsync()
    {
        return await _context
            .MaintenanceRecords.Where(x => !x.is_deleted)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<MaintenanceRecord> CreateAsync(
        MaintenanceRecord maintenanceRecord,
        int currentUserId
    )
    {
        maintenanceRecord.CreatedDate = DateTime.UtcNow;
        // Auto-populate audit fields
        maintenanceRecord.date_created = DateTime.UtcNow;
        maintenanceRecord.is_deleted = false;

        _context.MaintenanceRecords.Add(maintenanceRecord);
        await _context.SaveChangesAsync();
        return maintenanceRecord;
    }

    public async Task UpdateAsync(MaintenanceRecord maintenanceRecord, int currentUserId)
    {
        if (maintenanceRecord == null)
            throw new ArgumentNullException(nameof(maintenanceRecord));

        var existing = await _context.MaintenanceRecords.FindAsync(maintenanceRecord.MaintenanceId);
        if (existing == null)
            throw new InvalidOperationException(
                $"MaintenanceRecord with MaintenanceId {maintenanceRecord.MaintenanceId} not found"
            );

        maintenanceRecord.ModifiedDate = DateTime.UtcNow;
        _context.Entry(existing).CurrentValues.SetValues(maintenanceRecord);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int maintenanceId, int currentUserId)
    {
        var maintenanceRecord = await GetByIdAsync(maintenanceId);
        if (maintenanceRecord != null)
        {
            // Soft delete instead of hard delete
            maintenanceRecord.is_deleted = true;
            maintenanceRecord.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<MaintenanceRecord>> SearchMaintenanceRecordsAsync(
        string searchTerm
    )
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await _context
                .MaintenanceRecords.Where(x => !x.is_deleted)
                .OrderByDescending(mr => mr.MaintenanceDate)
                .ToListAsync();

        return await _context
            .MaintenanceRecords.Where(mr =>
                mr.MaintenanceType.Contains(searchTerm)
                || (mr.Description != null && mr.Description.Contains(searchTerm))
                || (mr.ServiceProvider != null && mr.ServiceProvider.Contains(searchTerm))
                || (mr.WorkOrderNumber != null && mr.WorkOrderNumber.Contains(searchTerm))
            )
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }
}
