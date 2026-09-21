using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// vehicle_remarks is an expanded table. Archive vehicle notes live in
/// dbo.fleet_notes.
/// </summary>
public class VehicleRemarkRepository : IVehicleRemarkRepository
{
    private const string TableName = "vehicle_remarks";

    private readonly FisDbContext _context;

    public VehicleRemarkRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<VehicleRemark?> GetByIdAsync(int remarkId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await _context.VehicleRemarks.FirstOrDefaultAsync(r =>
            r.remark_id == remarkId && !r.is_deleted
        );
    }

    public async Task<IEnumerable<VehicleRemark>> GetByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .VehicleRemarks.Where(r => r.vmf_code == vmfCode && !r.is_deleted)
            .OrderByDescending(r => r.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<VehicleRemark>> GetActiveByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .VehicleRemarks.Where(r => r.vmf_code == vmfCode && !r.is_deleted && !r.is_resolved)
            .OrderByDescending(r => r.date_created)
            .ToListAsync();
    }

    public async Task<VehicleRemark?> GetLatestActiveByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await _context
            .VehicleRemarks.Where(r => r.vmf_code == vmfCode && !r.is_deleted && !r.is_resolved)
            .OrderByDescending(r => r.date_created)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<VehicleRemark>> GetAllActiveAsync()
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .VehicleRemarks.Where(r => !r.is_deleted && !r.is_resolved)
            .OrderByDescending(r => r.date_created)
            .ToListAsync();
    }

    public async Task<VehicleRemark> CreateAsync(VehicleRemark remark, int currentUserId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        remark.date_created = DateTime.UtcNow;
        remark.created_by_user_code = currentUserId;
        remark.is_resolved = false;
        remark.is_deleted = false;

        _context.VehicleRemarks.Add(remark);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(remark.remark_id)
            ?? throw new InvalidOperationException("Failed to retrieve created vehicle remark");
    }

    public async Task<VehicleRemark> ResolveAsync(
        int remarkId,
        int resolvedByUserId,
        string? resolutionNotes
    )
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var remark =
            await _context.VehicleRemarks.FirstOrDefaultAsync(r =>
                r.remark_id == remarkId && !r.is_deleted
            )
            ?? throw new KeyNotFoundException($"Vehicle remark not found with ID: {remarkId}");

        if (remark.is_resolved)
            throw new InvalidOperationException($"Remark {remarkId} is already resolved.");

        remark.is_resolved = true;
        remark.resolved_date = DateTime.UtcNow;
        remark.resolved_by_user_code = resolvedByUserId;
        remark.resolution_notes = resolutionNotes;
        remark.date_updated = DateTime.UtcNow;
        remark.modified_by_user_code = resolvedByUserId;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(remarkId)
            ?? throw new InvalidOperationException("Failed to retrieve resolved vehicle remark");
    }

    public async Task DeleteAsync(int remarkId, int currentUserId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var remark =
            await _context.VehicleRemarks.FirstOrDefaultAsync(r =>
                r.remark_id == remarkId && !r.is_deleted
            )
            ?? throw new KeyNotFoundException($"Vehicle remark not found with ID: {remarkId}");

        remark.is_deleted = true;
        remark.date_updated = DateTime.UtcNow;
        remark.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }
}
