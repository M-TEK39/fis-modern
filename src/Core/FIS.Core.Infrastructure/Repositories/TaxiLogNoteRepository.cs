using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class TaxiLogNoteRepository : ITaxiLogNoteRepository
{
    private readonly FisDbContext _context;

    public TaxiLogNoteRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<TaxiLogNote>> GetAllAsync()
    {
        return await _context.TaxiLogNotes
            .Where(note => !note.is_deleted)
            .OrderBy(note => note.taxi_log_note_description)
            .ToListAsync();
    }
}
