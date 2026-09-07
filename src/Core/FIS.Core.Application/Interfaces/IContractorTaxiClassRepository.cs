using FIS.Core.Domain.Entities.Contracts;

namespace FIS.Core.Application.Interfaces;

public interface IContractorTaxiClassRepository
{
    Task<IEnumerable<ContractorTaxiClass>> GetAllAsync();
}
