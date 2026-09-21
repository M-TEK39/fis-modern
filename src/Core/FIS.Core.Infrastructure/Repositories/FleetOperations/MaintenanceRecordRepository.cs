using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// maintenance_records is not in the 2012 archive. Legacy maintenance history
/// is reported through DEV_REP_*VehicleMaintenanceHistory procedures.
/// </summary>
public class MaintenanceRecordRepository : IMaintenanceRecordRepository
{
    private const string TableName = "maintenance_records";

    private readonly FisDbContext _context;

    public MaintenanceRecordRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<MaintenanceRecord?> GetByIdAsync(int maintenanceId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await _context.MaintenanceRecords.FirstOrDefaultAsync(mr =>
            mr.MaintenanceId == maintenanceId
        );
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

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
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .MaintenanceRecords.Where(mr =>
                mr.MaintenanceDate >= startDate && mr.MaintenanceDate <= endDate
            )
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByServiceTypeAsync(string serviceType)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .MaintenanceRecords.Where(mr => mr.MaintenanceType == serviceType)
            .OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .MaintenanceRecords.OrderByDescending(mr => mr.MaintenanceDate)
            .ToListAsync();
    }

    public async Task<MaintenanceRecord> CreateAsync(
        MaintenanceRecord maintenanceRecord,
        int currentUserId
    )
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        _context.MaintenanceRecords.Add(maintenanceRecord);
        await _context.SaveChangesAsync();
        return maintenanceRecord;
    }

    public async Task UpdateAsync(MaintenanceRecord maintenanceRecord, int currentUserId)
    {
        _ = currentUserId;
        if (maintenanceRecord == null)
            throw new ArgumentNullException(nameof(maintenanceRecord));
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var existing = await _context.MaintenanceRecords.FindAsync(maintenanceRecord.MaintenanceId);
        if (existing == null)
            throw new InvalidOperationException(
                $"MaintenanceRecord with MaintenanceId {maintenanceRecord.MaintenanceId} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(maintenanceRecord);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int maintenanceId, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var maintenanceRecord = await GetByIdAsync(maintenanceId);
        if (maintenanceRecord != null)
        {
            _context.MaintenanceRecords.Remove(maintenanceRecord);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<MaintenanceRecord>> SearchMaintenanceRecordsAsync(
        string searchTerm
    )
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
            return await _context
                .MaintenanceRecords.OrderByDescending(mr => mr.MaintenanceDate)
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
