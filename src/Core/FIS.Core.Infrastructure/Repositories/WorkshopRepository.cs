using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;

public class WorkshopRepository : IWorkshopRepository
{
    private readonly FisDbContext _context;
    public WorkshopRepository(FisDbContext context) { _context = context; }

    public async Task<Workshop?> GetByIdAsync(short code) => await _context.Set<Workshop>().Include(w => w.Vehicle).FirstOrDefaultAsync(w => w.ww_code == code);
    public async Task<IEnumerable<Workshop>> GetAllAsync() => await _context.Set<Workshop>().Include(w => w.Vehicle).ToListAsync();
    public async Task<IEnumerable<Workshop>> GetByVehicleAsync(int vmfCode) => await _context.Set<Workshop>().Where(w => w.vmf_code == vmfCode).Include(w => w.Vehicle).ToListAsync();
    public async Task<Workshop> CreateAsync(Workshop item) { await _context.Set<Workshop>().AddAsync(item); await _context.SaveChangesAsync(); return item; }
    public async Task<Workshop> UpdateAsync(Workshop item) { _context.Set<Workshop>().Update(item); await _context.SaveChangesAsync(); return item; }
    public async Task DeleteAsync(short code) { var item = await GetByIdAsync(code); if (item != null) { _context.Set<Workshop>().Remove(item); await _context.SaveChangesAsync(); } }
}
