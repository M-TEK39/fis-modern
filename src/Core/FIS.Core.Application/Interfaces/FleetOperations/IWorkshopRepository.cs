using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    public interface IWorkshopRepository
    {
        Task<Workshop?> GetByIdAsync(short code);
        Task<IEnumerable<Workshop>> GetAllAsync();
        Task<WorkshopPage> GetPageAsync(WorkshopPageQuery query);
        Task<WorkshopReportPage> GetReportPageAsync(WorkshopReportPageQuery query);
        Task<IEnumerable<Workshop>> GetByVehicleAsync(int vmfCode);
        Task<Workshop> CreateAsync(Workshop item, int currentUserId);
        Task<Workshop> UpdateAsync(Workshop item, int currentUserId);
        Task DeleteAsync(short code, int currentUserId);
    }

    public sealed record WorkshopPageQuery(
        int Page = 1,
        int PageSize = 24,
        string? SearchTerm = null,
        string? Status = null,
        string? SearchField = null
    );

    public sealed record WorkshopPageItem(
        short WwCode,
        int? VmfCode,
        DateTime? ReceiveDate,
        TimeSpan? CompleteTime,
        DateTime? CompleteDate,
        string? FleetNumber,
        string? RegistrationNumber,
        short? LocationCode,
        string Status
    );

    public sealed record WorkshopPage(
        IReadOnlyList<WorkshopPageItem> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }

    public enum WorkshopReportKind
    {
        Period,
        OneVehicle,
        PrintJobCard,
        InShop,
    }

    public enum WorkshopReportVehicleField
    {
        FleetNumber,
        RegistrationNumber,
    }

    public sealed record WorkshopReportPageQuery(
        WorkshopReportKind ReportKind,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        string? VehicleSearch = null,
        WorkshopReportVehicleField VehicleSearchField = WorkshopReportVehicleField.FleetNumber,
        string? Garage = null,
        string? Category = null,
        bool OpenOnly = false,
        short? WorkshopCode = null,
        int Page = 1,
        int PageSize = 24
    );

    public sealed record WorkshopReportPageItem(
        short WorkshopCode,
        int? VmfCode,
        string? FleetNumber,
        string? RegistrationNumber,
        string? ModelDescription,
        int? CurrentOdo,
        DateTime? ReceiveDate,
        TimeSpan? ReceiveTime,
        DateTime? CompleteDate,
        TimeSpan? CompleteTime,
        string? ContactName,
        string? ContactTel,
        string? ContactFax,
        string? ContactEmail,
        string? AccidMech,
        string? Garage,
        string? DriverName,
        decimal? CallRefer,
        decimal? WorkshopKm,
        string? WorkshopRemarks,
        string? WorkshopReason,
        int? MerchantCode,
        decimal? RepairCost,
        DateTime? DateFromWorkshop,
        string? JobClose
    );

    public sealed record WorkshopReportPage(
        IReadOnlyList<WorkshopReportPageItem> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
