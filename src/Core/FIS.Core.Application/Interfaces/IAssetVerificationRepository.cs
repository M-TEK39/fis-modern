using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface IAssetVerificationRepository
{
    Task<AssetVerification?> GetByIdAsync(int verificationCode);
    Task<IEnumerable<AssetVerification>> GetAllAsync();
    Task<IEnumerable<AssetVerification>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<AssetVerification>> GetBySiteAsync(int siteCode);
    Task<IEnumerable<AssetVerification>> GetByStatusAsync(string status);
    Task<AssetVerification> CreateAsync(AssetVerification verification);
    Task<AssetVerification> UpdateAsync(AssetVerification verification);
    Task DeleteAsync(int verificationCode);
}
