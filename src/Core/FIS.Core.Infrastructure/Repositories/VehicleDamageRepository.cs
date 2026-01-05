using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class VehicleDamageRepository : IVehicleDamageRepository
{
    private readonly FisDbContext _context;
    public VehicleDamageRepository(FisDbContext context) { _context = context; }
    public async Task<VehicleDamage?> GetByIdAsync(short damageId) { return await _context.Set<VehicleDamage>().Include(d => d.Vehicle).Include(d => d.ModifiedByUser).FirstOrDefaultAsync(d => d.damage_id == damageId); }
    public async Task<IEnumerable<VehicleDamage>> GetAllAsync() { return await _context.Set<VehicleDamage>().Include(d => d.Vehicle).Include(d => d.ModifiedByUser).ToListAsync(); }
    public async Task<IEnumerable<VehicleDamage>> GetByVehicleAsync(int vmfCode) { return await _context.Set<VehicleDamage>().Where(d => d.vmf_code == vmfCode).Include(d => d.Vehicle).Include(d => d.ModifiedByUser).ToListAsync(); }
    public async Task<IEnumerable<VehicleDamage>> GetByStatusAsync(string status) { return await _context.Set<VehicleDamage>().Where(d => d.damage_status == status).Include(d => d.Vehicle).Include(d => d.ModifiedByUser).ToListAsync(); }
    public async Task<VehicleDamage> CreateAsync(VehicleDamage damage) { _context.Set<VehicleDamage>().Add(damage); await _context.SaveChangesAsync(); return damage; }
    public async Task<VehicleDamage> UpdateAsync(VehicleDamage damage) { _context.Set<VehicleDamage>().Update(damage); await _context.SaveChangesAsync(); return damage; }
    public async Task DeleteAsync(short damageId) { var damage = await GetByIdAsync(damageId); if (damage != null) { _context.Set<VehicleDamage>().Remove(damage); await _context.SaveChangesAsync(); } }
}
