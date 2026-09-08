using FIS.Core.Domain.Entities.WorkshopEntities;

namespace FIS.Core.Application.Interfaces;

public interface IWorkshopMerchantRepository
{
    System.Threading.Tasks.Task<WwMerchant?> GetByIdAsync(int merchantCode);
    System.Threading.Tasks.Task<IEnumerable<WwMerchant>> GetAllAsync();
    System.Threading.Tasks.Task<WwMerchant> CreateAsync(WwMerchant merchant, int currentUserId);
    System.Threading.Tasks.Task<WwMerchant> UpdateAsync(WwMerchant merchant, int currentUserId);
    System.Threading.Tasks.Task DeleteAsync(int merchantCode, int currentUserId);
}
