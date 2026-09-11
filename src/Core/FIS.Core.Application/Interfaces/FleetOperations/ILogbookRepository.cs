using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Logbook entity operations
    /// Provides contract for CRUD operations on vehicle logbooks
    /// </summary>
    public interface ILogbookRepository
    {
        Task<Logbook?> GetByIdAsync(short logbookCode);
        Task<IEnumerable<Logbook>> GetAllAsync();
        Task<LogbookPage> GetPageAsync(LogbookPageQuery query);
        Task<IEnumerable<Logbook>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Logbook>> GetBySiteAsync(short siteCode);
        Task<Logbook> CreateAsync(Logbook logbook, int currentUserId);
        Task<Logbook> UpdateAsync(Logbook logbook, int currentUserId);
        Task DeleteAsync(short logbookCode, int currentUserId);
    }

    public sealed record LogbookPageQuery(
        int Page = 1,
        int PageSize = 24,
        string? SearchTerm = null,
        int? VmfCode = null
    );

    public sealed record LogbookPage(
        IReadOnlyList<Logbook> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
