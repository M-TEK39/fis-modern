using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Application.Interfaces;

public interface ILegacyCredentialRepository
{
    Task<LegacyUserCredential?> GetByUserAccessCodeAsync(int userAccessCode);
    Task<List<LegacyUserCredential>> GetAllAsync();
    Task<LegacyUserCredential> CreateAsync(LegacyUserCredential credential, int currentUserId);
    Task<LegacyUserCredential> UpdateAsync(LegacyUserCredential credential, int currentUserId);
    Task DeleteAsync(int userAccessCode, int currentUserId);
    Task<bool> ExistsAsync(int userAccessCode);
}
