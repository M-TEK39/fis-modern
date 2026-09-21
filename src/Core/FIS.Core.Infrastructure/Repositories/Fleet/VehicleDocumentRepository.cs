using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// vehicle_documents is an expanded table with no 2012 archive equivalent.
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

        var query = _context.VehicleDocuments.Where(d => d.vmf_code == vmfCode && !d.is_deleted);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.document_category == category);

        return await query.OrderByDescending(d => d.date_created).ToListAsync();
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

        return await _context
            .VehicleDocuments.Where(d =>
                d.reference_type == referenceType && d.reference_id == referenceId && !d.is_deleted
            )
            .OrderByDescending(d => d.date_created)
            .ToListAsync();
    }

    public async Task<VehicleDocument?> GetByIdAsync(int documentId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await _context.VehicleDocuments.FirstOrDefaultAsync(d =>
            d.document_id == documentId && !d.is_deleted
        );
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

        var doc = await _context.VehicleDocuments.FindAsync(documentId);
        if (doc != null)
        {
            doc.is_deleted = true;
            doc.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
