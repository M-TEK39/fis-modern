using FIS.Core.Domain.Entities.WorkshopEntities;

namespace FIS.Core.Application.Interfaces;

public interface IWorkshopMerchantRepository
{
    System.Threading.Tasks.Task<WwMerchant?> GetByIdAsync(int merchantCode);
    System.Threading.Tasks.Task<IEnumerable<WwMerchant>> GetAllAsync();
    System.Threading.Tasks.Task<WorkshopMerchantPage> GetPageAsync(WorkshopMerchantPageQuery query);
    System.Threading.Tasks.Task<WwMerchant> CreateAsync(WwMerchant merchant, int currentUserId);
    System.Threading.Tasks.Task<WwMerchant> UpdateAsync(WwMerchant merchant, int currentUserId);
    System.Threading.Tasks.Task DeleteAsync(int merchantCode, int currentUserId);
}

public sealed record WorkshopMerchantPageQuery(
    int Page = 1,
    int PageSize = 24,
    string? Search = null
);

public sealed record WorkshopMerchantPage(
    IReadOnlyList<WwMerchant> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
