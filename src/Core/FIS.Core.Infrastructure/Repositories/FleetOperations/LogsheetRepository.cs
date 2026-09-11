using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Routes logsheet operations through the negotiated compatibility repository.
/// A table can contain the modern audit columns while still missing optional
/// transaction fields from the deployed client schema, so an EF entity graph
/// cannot safely be selected based on a partial column check.
/// </summary>
public sealed class LogsheetRepository : ILogsheetRepository
{
    private readonly LegacyLogsheetRepository _compatibilityRepository;

    public LogsheetRepository(FisDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _compatibilityRepository = new LegacyLogsheetRepository(context);
    }

    public Task<Logsheet?> GetByIdAsync(int logCode) =>
        _compatibilityRepository.GetByIdAsync(logCode);

    public Task<IEnumerable<Logsheet>> GetAllAsync() => _compatibilityRepository.GetAllAsync();

    public Task<LogsheetPage> GetPageAsync(LogsheetPageQuery query) =>
        _compatibilityRepository.GetPageAsync(query);

    public Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode) =>
        _compatibilityRepository.GetByVehicleAsync(vmfCode);

    public Task<IEnumerable<Logsheet>> GetByMonthAsync(DateTime month) =>
        _compatibilityRepository.GetByMonthAsync(month);

    public Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId) =>
        _compatibilityRepository.CreateAsync(logsheet, currentUserId);

    public Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId) =>
        _compatibilityRepository.UpdateAsync(logsheet, currentUserId);

    public Task DeleteAsync(int logCode, int currentUserId) =>
        _compatibilityRepository.DeleteAsync(logCode, currentUserId);
}
