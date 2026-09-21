using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// vehicle_documents is an expanded table with no 2012 archive equivalent.
/// is_deleted is optional on that table; leftover EF must not SELECT it.
/// </summary>
public class VehicleDocumentRepository : IVehicleDocumentRepository
{
    private const string TableName = "vehicle_documents";

    private readonly FisDbContext _context;

    public VehicleDocumentRepository(FisDbContext context) => _context = context;

    public async Task<IEnumerable<VehicleDocument>> GetByVehicleAsync(
        int vmfCode,
        string? category = null
    )
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        var hideDeleted = await HasDeletedColumnAsync();
        if (!string.IsNullOrWhiteSpace(category))
        {
            return hideDeleted
                ? await _context
                    .VehicleDocuments.FromSql(
                        $"SELECT * FROM [dbo].[vehicle_documents] WHERE [vmf_code] = {vmfCode} AND [document_category] = {category} AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                    )
                    .AsNoTracking()
                    .ToListAsync()
                : await _context
                    .VehicleDocuments.FromSql(
                        $"SELECT * FROM [dbo].[vehicle_documents] WHERE [vmf_code] = {vmfCode} AND [document_category] = {category} ORDER BY [date_created] DESC"
                    )
                    .AsNoTracking()
                    .ToListAsync();
        }

        return hideDeleted
            ? await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [vmf_code] = {vmfCode} AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [vmf_code] = {vmfCode} ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync();
    }

    public async Task<IEnumerable<VehicleDocument>> GetByReferenceAsync(
        string referenceType,
        int referenceId
    )
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [reference_type] = {referenceType} AND [reference_id] = {referenceId} AND [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [reference_type] = {referenceType} AND [reference_id] = {referenceId} ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync();
    }

    public async Task<VehicleDocument?> GetByIdAsync(int documentId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [document_id] = {documentId} AND [is_deleted] = 0"
                )
                .AsNoTracking()
                .FirstOrDefaultAsync()
            : await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [document_id] = {documentId}"
                )
                .AsNoTracking()
                .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<VehicleDocument>> GetAllAsync()
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await HasDeletedColumnAsync()
            ? await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] WHERE [is_deleted] = 0 ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync()
            : await _context
                .VehicleDocuments.FromSql(
                    $"SELECT * FROM [dbo].[vehicle_documents] ORDER BY [date_created] DESC"
                )
                .AsNoTracking()
                .ToListAsync();
    }

    public async Task<VehicleDocument> CreateAsync(VehicleDocument document)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        document.date_created = DateTime.UtcNow;
        _context.VehicleDocuments.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task DeleteAsync(int documentId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        if (await HasDeletedColumnAsync())
        {
            if (await HasUpdatedColumnAsync())
            {
                var updatedAt = DateTime.UtcNow;
                await _context.Database.ExecuteSqlAsync(
                    $"UPDATE [dbo].[vehicle_documents] SET [is_deleted] = 1, [date_updated] = {updatedAt} WHERE [document_id] = {documentId}"
                );
                return;
            }

            await _context.Database.ExecuteSqlAsync(
                $"UPDATE [dbo].[vehicle_documents] SET [is_deleted] = 1 WHERE [document_id] = {documentId}"
            );
            return;
        }

        var doc = await _context.VehicleDocuments.FindAsync(documentId);
        if (doc is null)
        {
            return;
        }

        _context.VehicleDocuments.Remove(doc);
        await _context.SaveChangesAsync();
    }

    private Task<bool> HasDeletedColumnAsync() =>
        WorkflowOptionalTable.ColumnExistsAsync(_context, "dbo", TableName, "is_deleted");

    private Task<bool> HasUpdatedColumnAsync() =>
        WorkflowOptionalTable.ColumnExistsAsync(_context, "dbo", TableName, "date_updated");
}
