using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Towing entity operations
    /// Provides contract for CRUD operations on towing requests
    /// </summary>
    public interface ITowingRepository
    {
        Task<Towing?> GetByIdAsync(short towingCode);
        Task<IEnumerable<Towing>> GetAllAsync();
        Task<TowingPage> GetPageAsync(TowingPageQuery query);
        Task<IEnumerable<Towing>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Towing>> GetBySiteAsync(short siteCode);
        Task<Towing> CreateAsync(Towing towing, int currentUserId);
        Task<Towing> UpdateAsync(Towing towing, int currentUserId);
        Task DeleteAsync(short towingCode, int currentUserId);
    }

    public sealed record TowingPageQuery(
        int Page = 1,
        int PageSize = 24,
        IReadOnlyCollection<int>? VmfCodes = null
    );

    public sealed record TowingPage(IReadOnlyList<Towing> Items, int Page, int PageSize, int Total)
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
