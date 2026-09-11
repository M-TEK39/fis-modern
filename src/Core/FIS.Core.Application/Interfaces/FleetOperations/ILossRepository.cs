using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Loss entity operations
    /// Provides contract for CRUD operations on vehicle losses
    /// </summary>
    public interface ILossRepository
    {
        Task<Loss?> GetByIdAsync(short lossCode);
        Task<IEnumerable<Loss>> GetAllAsync();
        Task<LossPage> GetPageAsync(LossPageQuery query);
        Task<IEnumerable<Loss>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Loss>> GetBySiteAsync(short siteCode);
        Task<Loss> CreateAsync(Loss loss, int currentUserId);
        Task<Loss> UpdateAsync(Loss loss, int currentUserId);
        Task DeleteAsync(short lossCode, int currentUserId);
    }

    public sealed record LossPageQuery(int Page = 1, int PageSize = 24, int? VmfCode = null);

    public sealed record LossPage(IReadOnlyList<Loss> Items, int Page, int PageSize, int Total)
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }
}
