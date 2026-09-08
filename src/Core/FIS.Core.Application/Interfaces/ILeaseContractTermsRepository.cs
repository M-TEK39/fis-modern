using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface ILeaseContractTermsRepository
{
    Task<LeaseContractTerms?> GetByIdAsync(int termId);
    Task<IEnumerable<LeaseContractTerms>> GetAllAsync();
    Task<LeaseContractTerms?> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<LeaseContractTerms>> GetActiveTermsAsync();
    Task<LeaseContractTerms> CreateAsync(LeaseContractTerms terms, int currentUserId);
    Task<LeaseContractTerms> UpdateAsync(LeaseContractTerms terms, int currentUserId);
    Task DeleteAsync(int termId, int currentUserId);
}
