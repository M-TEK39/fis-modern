using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Taxi entity operations
    /// Provides contract for CRUD operations on taxi requests
    /// </summary>
    public interface ITaxiRepository
    {
        Task<Taxi?> GetByIdAsync(int requestId, IReadOnlySet<short>? allowedSiteCodes = null);
        Task<Taxi?> GetLatestByRequisitionAsync(string rekNum, IReadOnlySet<short>? allowedSiteCodes = null);
        Task<IEnumerable<Taxi>> GetAllAsync(IReadOnlySet<short>? allowedSiteCodes = null);
        Task<TaxiPage> GetPageAsync(TaxiPageQuery query);
        Task<TaxiReportPage> GetReportPageAsync(TaxiReportPageQuery query);
        Task<IEnumerable<Taxi>> GetBySiteAsync(short siteCode, IReadOnlySet<short>? allowedSiteCodes = null);
        Task<IEnumerable<Taxi>> GetByDepartmentAsync(short departmentCode, IReadOnlySet<short>? allowedSiteCodes = null);
        Task<IEnumerable<Taxi>> GetByDateAsync(DateTime date, IReadOnlySet<short>? allowedSiteCodes = null);
        Task<Taxi> CreateAsync(Taxi taxi, int currentUserId);
        Task<IReadOnlyList<Taxi>> CreateRecurringAsync(
            Taxi taxi,
            DateTime startDate,
            DateTime endDate,
            int currentUserId
        );
        Task<Taxi> UpdateAsync(Taxi taxi, int currentUserId);
        Task DeleteAsync(int requestId, int currentUserId);
    }

    public sealed record TaxiPageQuery(
        int Page = 1,
        int PageSize = 24,
        bool PendingOnly = false,
        bool JiaPickupOnly = false,
        string? Search = null,
        IReadOnlySet<short>? AllowedSiteCodes = null
    );

    public sealed record TaxiPage(IReadOnlyList<Taxi> Items, int Page, int PageSize, int Total)
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }

    public enum TaxiReportKind
    {
        PreviousFinYearVipTaxi,
        ListPerDepartment,
        ListInServicePerDepartment,
        Financial,
    }

    public sealed record TaxiReportPageQuery(
        TaxiReportKind ReportKind,
        int Page = 1,
        int PageSize = 24,
        string? Search = null,
        DateTime? AsOfDate = null,
        IReadOnlySet<short>? AllowedSiteCodes = null
    );

    public sealed record TaxiReportPage(
        IReadOnlyList<Taxi> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
