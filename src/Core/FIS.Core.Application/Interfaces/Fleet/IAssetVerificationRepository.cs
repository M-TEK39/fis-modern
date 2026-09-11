using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface IAssetVerificationRepository
{
    Task<AssetVerification?> GetByIdAsync(int verificationCode);
    Task<IEnumerable<AssetVerification>> GetAllAsync();
    Task<AssetVerificationPage> GetPageAsync(AssetVerificationPageQuery query);
    Task<IEnumerable<AssetVerification>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<AssetVerification>> GetBySiteAsync(int siteCode);
    Task<IEnumerable<AssetVerification>> GetByStatusAsync(string status);
    Task<AssetVerification> CreateAsync(AssetVerification verification, int currentUserId);
    Task<AssetVerification> UpdateAsync(AssetVerification verification, int currentUserId);
    Task DeleteAsync(int verificationCode, int currentUserId);
}

public sealed record AssetVerificationPageQuery(int Page = 1, int PageSize = 24);

public sealed record AssetVerificationPage(
    IReadOnlyList<AssetVerification> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
