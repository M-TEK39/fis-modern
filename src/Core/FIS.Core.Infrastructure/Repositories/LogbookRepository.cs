using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Uses the negotiated raw compatibility projection for every logbook
/// operation. The logbook table itself may contain the modern audit columns,
/// but related vehicle/site entities can still be on the client shape; an EF
/// Include would project optional columns from those related tables and fail
/// before the fallback could run.
/// </summary>
public sealed class LogbookRepository : ILogbookRepository
{
    private readonly LegacyLogbookRepository _compatibilityRepository;

    public LogbookRepository(FisDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _compatibilityRepository = new LegacyLogbookRepository(context);
    }

    public Task<Logbook?> GetByIdAsync(short logbookCode) =>
        _compatibilityRepository.GetByIdAsync(logbookCode);

    public Task<IEnumerable<Logbook>> GetAllAsync() =>
        _compatibilityRepository.GetAllAsync();

    public Task<IEnumerable<Logbook>> GetByVehicleAsync(int vmfCode) =>
        _compatibilityRepository.GetByVehicleAsync(vmfCode);

    public Task<IEnumerable<Logbook>> GetBySiteAsync(short siteCode) =>
        _compatibilityRepository.GetBySiteAsync(siteCode);

    public Task<Logbook> CreateAsync(Logbook logbook, int currentUserId) =>
        _compatibilityRepository.CreateAsync(logbook, currentUserId);

    public Task<Logbook> UpdateAsync(Logbook logbook, int currentUserId) =>
        _compatibilityRepository.UpdateAsync(logbook, currentUserId);

    public Task DeleteAsync(short logbookCode, int currentUserId) =>
        _compatibilityRepository.DeleteAsync(logbookCode, currentUserId);
}
