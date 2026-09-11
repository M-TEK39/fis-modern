using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Fine entity operations
    /// Provides contract for CRUD operations on traffic fines
    /// </summary>
    public interface IFineRepository
    {
        Task<Fine?> GetByIdAsync(int fineCode);
        Task<IEnumerable<Fine>> GetAllAsync();
        Task<FinePage> GetPageAsync(FinePageQuery query);
        Task<IEnumerable<Fine>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Fine>> GetBySiteAsync(short siteCode);
        Task<IEnumerable<Fine>> GetUnpaidFinesAsync();
        Task<Fine> CreateAsync(Fine fine, int currentUserId);
        Task<Fine> UpdateAsync(Fine fine, int currentUserId);
        Task DeleteAsync(int fineCode, int currentUserId);
    }

    public sealed record FinePageQuery(
        int Page = 1,
        int PageSize = 24,
        string SearchType = "GP",
        string? SearchQuery = null
    );

    public sealed record FinePage(IReadOnlyList<Fine> Items, int Page, int PageSize, int Total)
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
