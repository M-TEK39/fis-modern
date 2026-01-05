using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class BookingRepository : IBookingRepository
{
    private readonly FisDbContext _context;
    public BookingRepository(FisDbContext context) { _context = context; }
    public async Task<Booking?> GetByIdAsync(short bookingId) { return await _context.Set<Booking>().Include(b => b.Vehicle).Include(b => b.Location).FirstOrDefaultAsync(b => b.booking_id == bookingId); }
    public async Task<IEnumerable<Booking>> GetAllAsync() { return await _context.Set<Booking>().Include(b => b.Vehicle).Include(b => b.Location).ToListAsync(); }
    public async Task<IEnumerable<Booking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate) { return await _context.Set<Booking>().Where(b => b.start_date >= startDate && b.start_date <= endDate).Include(b => b.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Booking>> GetByStatusAsync(string status) { return await _context.Set<Booking>().Where(b => b.booking_status == status).Include(b => b.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<Booking>> GetByVehicleAsync(int vmfCode) { return await _context.Set<Booking>().Where(b => b.vmf_code == vmfCode).Include(b => b.Vehicle).ToListAsync(); }
    public async Task<Booking> CreateAsync(Booking booking) { _context.Set<Booking>().Add(booking); await _context.SaveChangesAsync(); return booking; }
    public async Task<Booking> UpdateAsync(Booking booking) { _context.Set<Booking>().Update(booking); await _context.SaveChangesAsync(); return booking; }
    public async Task DeleteAsync(short bookingId) { var booking = await GetByIdAsync(bookingId); if (booking != null) { _context.Set<Booking>().Remove(booking); await _context.SaveChangesAsync(); } }
}
