using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Logsheet entity operations
    /// Provides contract for CRUD operations on vehicle logsheets
    /// </summary>
    public interface ILogsheetRepository
    {
        Task<Logsheet?> GetByIdAsync(int logCode);
        Task<IEnumerable<Logsheet>> GetAllAsync();
        Task<LogsheetPage> GetPageAsync(LogsheetPageQuery query);
        Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Logsheet>> GetByMonthAsync(DateTime month);
        Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId);
        Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId);
        Task DeleteAsync(int logCode, int currentUserId);
    }

    public sealed record LogsheetPageQuery(
        int Page = 1,
        int PageSize = 24,
        int? VmfCode = null,
        string? RequisitionNumber = null
    );

    public sealed record LogsheetPage(
        IReadOnlyList<Logsheet> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
