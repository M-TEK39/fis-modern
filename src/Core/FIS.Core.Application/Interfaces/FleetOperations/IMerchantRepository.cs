using FIS.Core.Domain.Entities.ReferenceData;

namespace FIS.Core.Application.Interfaces;

public interface IMerchantRepository
{
    Task<MerchantReference?> GetByIdAsync(int merchantCode);
    Task<IEnumerable<MerchantReference>> GetAllAsync();
    Task<MerchantReference> CreateAsync(MerchantReference merchant, int currentUserId);
    Task<MerchantReference> UpdateAsync(MerchantReference merchant, int currentUserId);
    Task<int> CountClearancesAsync(int merchantCode);
    Task DeleteAsync(int merchantCode, int currentUserId);
}
