using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(short bookingId);
    Task<IEnumerable<Booking>> GetAllAsync();
    Task<IEnumerable<Booking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<Booking>> GetByStatusAsync(string status);
    Task<IEnumerable<Booking>> GetByVehicleAsync(int vmfCode);
    Task<Booking> CreateAsync(Booking booking);
    Task<Booking> UpdateAsync(Booking booking);
    Task DeleteAsync(short bookingId);
}
