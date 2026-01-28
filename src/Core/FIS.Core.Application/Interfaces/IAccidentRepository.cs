using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface IAccidentRepository
{
    Task<Accident?> GetByIdAsync(int accidentCode);
    Task<IEnumerable<Accident>> GetAllAsync();
    Task<IEnumerable<Accident>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<Accident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<Accident> CreateAsync(Accident accident, int currentUserId);
    Task<Accident> UpdateAsync(Accident accident, int currentUserId);
    Task DeleteAsync(int accidentCode, int currentUserId);
}
