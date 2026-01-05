using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface ILeaseContractTermsRepository
{
    Task<LeaseContractTerms?> GetByIdAsync(int termId);
    Task<IEnumerable<LeaseContractTerms>> GetAllAsync();
    Task<LeaseContractTerms?> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<LeaseContractTerms>> GetActiveTermsAsync();
    Task<LeaseContractTerms> CreateAsync(LeaseContractTerms terms);
    Task<LeaseContractTerms> UpdateAsync(LeaseContractTerms terms);
    Task DeleteAsync(int termId);
}
