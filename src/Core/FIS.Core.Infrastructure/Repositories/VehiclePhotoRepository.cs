using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;

public class VehiclePhotoRepository : IVehiclePhotoRepository
{
    private readonly FisDbContext _context;
    public VehiclePhotoRepository(FisDbContext context) { _context = context; }

    public async Task<VehiclePhoto?> GetByIdAsync(int code) => await _context.Set<VehiclePhoto>().FirstOrDefaultAsync(t => t.VehiclePhotoInfoCode == code);
    public async Task<IEnumerable<VehiclePhoto>> GetAllAsync() => await _context.Set<VehiclePhoto>().ToListAsync();
    public async Task<IEnumerable<VehiclePhoto>> GetByVehicleAsync(int vmfCode) => await _context.Set<VehiclePhoto>().Where(p => p.VehicleMasterCode == vmfCode).ToListAsync();
    public async Task<VehiclePhoto> CreateAsync(VehiclePhoto item) { await _context.Set<VehiclePhoto>().AddAsync(item); await _context.SaveChangesAsync(); return item; }
    public async Task<VehiclePhoto> UpdateAsync(VehiclePhoto item) { _context.Set<VehiclePhoto>().Update(item); await _context.SaveChangesAsync(); return item; }
    public async Task DeleteAsync(int code) { var item = await GetByIdAsync(code); if (item != null) { _context.Set<VehiclePhoto>().Remove(item); await _context.SaveChangesAsync(); } }
}
