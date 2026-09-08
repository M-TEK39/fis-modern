using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class VehicleOrderRepository : IVehicleOrderRepository
{
    private readonly FisDbContext _context;

    public VehicleOrderRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<VehicleOrder?> GetByIdAsync(int code) =>
        await _context.Set<VehicleOrder>().FirstOrDefaultAsync(t => t.order_id == code);

    public async Task<IEnumerable<VehicleOrder>> GetAllAsync() =>
        await _context.Set<VehicleOrder>().ToListAsync();

    public async Task<VehicleOrder> CreateAsync(VehicleOrder item, int currentUserId)
    {
        await _context.Set<VehicleOrder>().AddAsync(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<VehicleOrder> UpdateAsync(VehicleOrder item, int currentUserId)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        var existing = await _context.Set<VehicleOrder>().FindAsync(item.order_id);
        if (existing == null)
            throw new InvalidOperationException(
                $"VehicleOrder with order_id {item.order_id} not found"
            );
        _context.Entry(existing).CurrentValues.SetValues(item);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int code, int currentUserId)
    {
        var item = await GetByIdAsync(code);
        if (item != null)
        {
            _context.Set<VehicleOrder>().Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
