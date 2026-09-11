using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    public interface IWorkshopRepository
    {
        Task<Workshop?> GetByIdAsync(short code);
        Task<IEnumerable<Workshop>> GetAllAsync();
        Task<WorkshopPage> GetPageAsync(WorkshopPageQuery query);
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
}
