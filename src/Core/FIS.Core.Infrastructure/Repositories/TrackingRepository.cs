using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class TrackingRepository : ITrackingRepository
{
    private readonly FisDbContext _context;
    public TrackingRepository(FisDbContext context) { _context = context; }
    public async Task<Tracking?> GetByIdAsync(short trackCode) { return await _context.Set<Tracking>().Include(t => t.Vehicle).FirstOrDefaultAsync(t => t.track_code == trackCode); }
    public async Task<IEnumerable<Tracking>> GetAllAsync() { return await _context.Set<Tracking>().Include(t => t.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Tracking>> GetByVehicleAsync(int vmfCode) { return await _context.Set<Tracking>().Where(t => t.vmf_code == vmfCode).Include(t => t.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Tracking>> GetActiveTrackingAsync() { return await _context.Set<Tracking>().Where(t => t.remove_date == null).Include(t => t.Vehicle).ToListAsync(); }
    public async Task<Tracking> CreateAsync(Tracking tracking, int currentUserId) { _context.Set<Tracking>().Add(tracking); await _context.SaveChangesAsync(); return tracking; }
    public async Task<Tracking> UpdateAsync(Tracking tracking, int currentUserId) { if (tracking == null) throw new ArgumentNullException(nameof(tracking)); var existing = await _context.Set<Tracking>().FindAsync(tracking.track_code); if (existing == null) throw new InvalidOperationException($"Tracking with track_code {tracking.track_code} not found"); _context.Entry(existing).CurrentValues.SetValues(tracking); await _context.SaveChangesAsync(); return existing; }
    public async Task DeleteAsync(short trackCode, int currentUserId) { var tracking = await GetByIdAsync(trackCode); if (tracking != null) { _context.Set<Tracking>().Remove(tracking); await _context.SaveChangesAsync(); } }
}
