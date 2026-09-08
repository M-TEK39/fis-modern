using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class VehicleDocumentRepository : IVehicleDocumentRepository
{
    private readonly FisDbContext _context;

    public VehicleDocumentRepository(FisDbContext context) => _context = context;

    public async Task<IEnumerable<VehicleDocument>> GetByVehicleAsync(
        int vmfCode,
        string? category = null
    )
    {
        var query = _context.VehicleDocuments.Where(d => d.vmf_code == vmfCode && !d.is_deleted);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.document_category == category);

        return await query.OrderByDescending(d => d.date_created).ToListAsync();
    }

    public async Task<IEnumerable<VehicleDocument>> GetByReferenceAsync(
        string referenceType,
        int referenceId
    ) =>
        await _context
            .VehicleDocuments.Where(d =>
                d.reference_type == referenceType && d.reference_id == referenceId && !d.is_deleted
            )
            .OrderByDescending(d => d.date_created)
            .ToListAsync();

    public async Task<VehicleDocument?> GetByIdAsync(int documentId) =>
        await _context.VehicleDocuments.FirstOrDefaultAsync(d =>
            d.document_id == documentId && !d.is_deleted
        );

    public async Task<VehicleDocument> CreateAsync(VehicleDocument document)
    {
        document.date_created = DateTime.UtcNow;
        _context.VehicleDocuments.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task DeleteAsync(int documentId)
    {
        var doc = await _context.VehicleDocuments.FindAsync(documentId);
        if (doc != null)
        {
            doc.is_deleted = true;
            doc.date_updated = DateTime.UtcNow;
            _context.VehicleDocuments.Update(doc);
            await _context.SaveChangesAsync();
        }
    }
}
