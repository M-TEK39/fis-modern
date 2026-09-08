using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    public interface IWorkshopRepository
    {
        Task<Workshop?> GetByIdAsync(short code);
        Task<IEnumerable<Workshop>> GetAllAsync();
        Task<IEnumerable<Workshop>> GetByVehicleAsync(int vmfCode);
        Task<Workshop> CreateAsync(Workshop item, int currentUserId);
        Task<Workshop> UpdateAsync(Workshop item, int currentUserId);
        Task DeleteAsync(short code, int currentUserId);
    }
}
