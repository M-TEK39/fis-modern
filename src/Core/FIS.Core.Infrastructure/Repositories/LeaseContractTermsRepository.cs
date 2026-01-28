using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class LeaseContractTermsRepository : ILeaseContractTermsRepository
{
    private readonly FisDbContext _context;
    public LeaseContractTermsRepository(FisDbContext context) { _context = context; }
    public async Task<LeaseContractTerms?> GetByIdAsync(int termId) { return await _context.Set<LeaseContractTerms>().Include(l => l.Vehicle).Include(l => l.CreatedByUser).Include(l => l.ModifiedByUser).FirstOrDefaultAsync(l => l.VehicleContractTermID == termId); }
    public async Task<IEnumerable<LeaseContractTerms>> GetAllAsync() { return await _context.Set<LeaseContractTerms>().Include(l => l.Vehicle).ToListAsync(); }
    public async Task<LeaseContractTerms?> GetByVehicleAsync(int vmfCode) { return await _context.Set<LeaseContractTerms>().Where(l => l.vmf_Code == vmfCode).Include(l => l.Vehicle).OrderByDescending(l => l.CreatedDate).FirstOrDefaultAsync(); }
    public async Task<IEnumerable<LeaseContractTerms>> GetActiveTermsAsync() { var now = DateTime.Now; return await _context.Set<LeaseContractTerms>().Where(l => l.StartDate <= now && (l.EndDate == null || l.EndDate >= now)).Include(l => l.Vehicle).ToListAsync(); }
    public async Task<LeaseContractTerms> CreateAsync(LeaseContractTerms terms, int currentUserId) { _context.Set<LeaseContractTerms>().Add(terms); await _context.SaveChangesAsync(); return terms; }
    public async Task<LeaseContractTerms> UpdateAsync(LeaseContractTerms terms, int currentUserId) { if (terms == null) throw new ArgumentNullException(nameof(terms)); var existing = await _context.Set<LeaseContractTerms>().FindAsync(terms.VehicleContractTermID); if (existing == null) throw new InvalidOperationException($"LeaseContractTerms with VehicleContractTermID {terms.VehicleContractTermID} not found"); _context.Entry(existing).CurrentValues.SetValues(terms); await _context.SaveChangesAsync(); return existing; }
    public async Task DeleteAsync(int termId, int currentUserId) { var terms = await GetByIdAsync(termId); if (terms != null) { _context.Set<LeaseContractTerms>().Remove(terms); await _context.SaveChangesAsync(); } }
}
