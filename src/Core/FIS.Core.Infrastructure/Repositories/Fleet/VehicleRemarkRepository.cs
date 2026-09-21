using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// vehicle_remarks is an expanded table. Archive vehicle notes live in
/// dbo.fleet_notes. is_deleted is optional; leftover EF must not SELECT it.
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

        return await QueryByIdAsync(remarkId, track: false);
    }

    public async Task<IEnumerable<VehicleRemark>> GetByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [vmf_code] = {vmfCode} AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [vmf_code] = {vmfCode} ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync();
    }

    public async Task<IEnumerable<VehicleRemark>> GetActiveByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [vmf_code] = {vmfCode} AND [is_resolved] = 0 AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [vmf_code] = {vmfCode} AND [is_resolved] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync();
    }

    public async Task<VehicleRemark?> GetLatestActiveByVehicleAsync(int vmfCode)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [vmf_code] = {vmfCode} AND [is_resolved] = 0 AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .FirstOrDefaultAsync()
            : await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [vmf_code] = {vmfCode} AND [is_resolved] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<VehicleRemark>> GetAllActiveAsync()
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [is_resolved] = 0 AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [is_resolved] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync();
    }

    public async Task<IEnumerable<VehicleRemark>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleRemarks.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleRemarks.FromSql($"SELECT * FROM [dbo].[vehicle_remarks] ORDER BY [date_created] DESC")
                .AsNoTracking()
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
            await QueryByIdAsync(remarkId, track: true)
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
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        if (await HasDeletedColumnAsync())
        {
            await _context.Database.ExecuteSqlAsync(
                $"UPDATE [dbo].[vehicle_remarks] SET [is_deleted] = 1 WHERE [remark_id] = {remarkId}"
            );
            return;
        }

        var remark =
            await _context.VehicleRemarks.FirstOrDefaultAsync(r => r.remark_id == remarkId)
            ?? throw new KeyNotFoundException($"Vehicle remark not found with ID: {remarkId}");

        _context.VehicleRemarks.Remove(remark);
        await _context.SaveChangesAsync();
    }

    private async Task<VehicleRemark?> QueryByIdAsync(int remarkId, bool track)
    {
        var query = await HasDeletedColumnAsync()
            ? _context.VehicleRemarks.FromSql(
                $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [remark_id] = {remarkId} AND [is_deleted] = 0"
            )
            : _context.VehicleRemarks.FromSql(
                $"SELECT * FROM [dbo].[vehicle_remarks] WHERE [remark_id] = {remarkId}"
            );

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync();
    }

    private Task<bool> HasDeletedColumnAsync() =>
        WorkflowOptionalTable.ColumnExistsAsync(_context, "dbo", TableName, "is_deleted");
}
