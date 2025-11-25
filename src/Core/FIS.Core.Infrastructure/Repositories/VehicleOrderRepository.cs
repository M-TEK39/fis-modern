using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;

public class VehicleOrderRepository : IVehicleOrderRepository
{
    private readonly FisDbContext _context;
    public VehicleOrderRepository(FisDbContext context) { _context = context; }

    public async Task<VehicleOrder?> GetByIdAsync(int code) => await _context.Set<VehicleOrder>().FirstOrDefaultAsync(t => t.order_id == code);
    public async Task<IEnumerable<VehicleOrder>> GetAllAsync() => await _context.Set<VehicleOrder>().ToListAsync();
    public async Task<VehicleOrder> CreateAsync(VehicleOrder item) { await _context.Set<VehicleOrder>().AddAsync(item); await _context.SaveChangesAsync(); return item; }
    public async Task<VehicleOrder> UpdateAsync(VehicleOrder item) { _context.Set<VehicleOrder>().Update(item); await _context.SaveChangesAsync(); return item; }
    public async Task DeleteAsync(int code) { var item = await GetByIdAsync(code); if (item != null) { _context.Set<VehicleOrder>().Remove(item); await _context.SaveChangesAsync(); } }
}
