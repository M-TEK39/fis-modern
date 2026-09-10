using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

public interface ITaxiLogNoteRepository
{
    Task<IEnumerable<TaxiLogNote>> GetAllAsync();
}
