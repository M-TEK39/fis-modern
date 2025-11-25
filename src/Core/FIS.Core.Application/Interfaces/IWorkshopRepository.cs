using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces
{
    public interface IWorkshopRepository
    {
        Task<Workshop?> GetByIdAsync(short code);
        Task<IEnumerable<Workshop>> GetAllAsync();
        Task<IEnumerable<Workshop>> GetByVehicleAsync(int vmfCode);
        Task<Workshop> CreateAsync(Workshop item);
        Task<Workshop> UpdateAsync(Workshop item);
        Task DeleteAsync(short code);
    }
}
