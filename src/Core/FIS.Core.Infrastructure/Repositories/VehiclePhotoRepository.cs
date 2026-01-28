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
    public async Task<VehiclePhoto> CreateAsync(VehiclePhoto item, int currentUserId) { await _context.Set<VehiclePhoto>().AddAsync(item); await _context.SaveChangesAsync(); return item; }
    public async Task<VehiclePhoto> UpdateAsync(VehiclePhoto item, int currentUserId) { if (item == null) throw new ArgumentNullException(nameof(item)); var existing = await _context.Set<VehiclePhoto>().FindAsync(item.VehiclePhotoInfoCode); if (existing == null) throw new InvalidOperationException($"VehiclePhoto with VehiclePhotoInfoCode {item.VehiclePhotoInfoCode} not found"); _context.Entry(existing).CurrentValues.SetValues(item); await _context.SaveChangesAsync(); return existing; }
    public async Task DeleteAsync(int code, int currentUserId) { var item = await GetByIdAsync(code); if (item != null) { _context.Set<VehiclePhoto>().Remove(item); await _context.SaveChangesAsync(); } }
}
