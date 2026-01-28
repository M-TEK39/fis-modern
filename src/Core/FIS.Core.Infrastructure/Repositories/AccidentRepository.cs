using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class AccidentRepository : IAccidentRepository
{
    private readonly FisDbContext _context;
    public AccidentRepository(FisDbContext context) { _context = context; }
    public async Task<Accident?> GetByIdAsync(int accidentCode) { return await _context.Set<Accident>().Include(a => a.Vehicle).FirstOrDefaultAsync(a => a.accident_code == accidentCode); }
    public async Task<IEnumerable<Accident>> GetAllAsync() { return await _context.Set<Accident>().Include(a => a.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Accident>> GetByVehicleAsync(int vmfCode) { return await _context.Set<Accident>().Where(a => a.vmf_code == vmfCode).Include(a => a.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Accident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) { return await _context.Set<Accident>().Where(a => a.occurence_date >= startDate && a.occurence_date <= endDate).Include(a => a.Vehicle).ToListAsync(); }
    public async Task<Accident> CreateAsync(Accident accident, int currentUserId) { _context.Set<Accident>().Add(accident); await _context.SaveChangesAsync(); return accident; }
    public async Task<Accident> UpdateAsync(Accident accident, int currentUserId) { if (accident == null) throw new ArgumentNullException(nameof(accident)); var existing = await _context.Set<Accident>().FindAsync(accident.accident_code); if (existing == null) throw new InvalidOperationException($"Accident with accident_code {accident.accident_code} not found"); _context.Entry(existing).CurrentValues.SetValues(accident); await _context.SaveChangesAsync(); return existing; }
    public async Task DeleteAsync(int accidentCode, int currentUserId) { var accident = await GetByIdAsync(accidentCode); if (accident != null) { _context.Set<Accident>().Remove(accident); await _context.SaveChangesAsync(); } }
}
