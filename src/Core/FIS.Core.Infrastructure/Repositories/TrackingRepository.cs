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
    public async Task<Tracking> CreateAsync(Tracking tracking) { _context.Set<Tracking>().Add(tracking); await _context.SaveChangesAsync(); return tracking; }
    public async Task<Tracking> UpdateAsync(Tracking tracking) { _context.Set<Tracking>().Update(tracking); await _context.SaveChangesAsync(); return tracking; }
    public async Task DeleteAsync(short trackCode) { var tracking = await GetByIdAsync(trackCode); if (tracking != null) { _context.Set<Tracking>().Remove(tracking); await _context.SaveChangesAsync(); } }
}
