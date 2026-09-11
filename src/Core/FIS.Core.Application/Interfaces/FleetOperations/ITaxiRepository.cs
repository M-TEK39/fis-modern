using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Taxi entity operations
    /// Provides contract for CRUD operations on taxi requests
    /// </summary>
    public interface ITaxiRepository
    {
        Task<Taxi?> GetByIdAsync(int requestId);
        Task<Taxi?> GetLatestByRequisitionAsync(string rekNum);
        Task<IEnumerable<Taxi>> GetAllAsync();
        Task<TaxiPage> GetPageAsync(TaxiPageQuery query);
        Task<IEnumerable<Taxi>> GetBySiteAsync(short siteCode);
        Task<IEnumerable<Taxi>> GetByDepartmentAsync(short departmentCode);
        Task<IEnumerable<Taxi>> GetByDateAsync(DateTime date);
        Task<Taxi> CreateAsync(Taxi taxi, int currentUserId);
        Task<Taxi> UpdateAsync(Taxi taxi, int currentUserId);
        Task DeleteAsync(int requestId, int currentUserId);
    }

    public sealed record TaxiPageQuery(
        int Page = 1,
        int PageSize = 24,
        bool PendingOnly = false,
        bool JiaPickupOnly = false
    );

    public sealed record TaxiPage(IReadOnlyList<Taxi> Items, int Page, int PageSize, int Total)
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
