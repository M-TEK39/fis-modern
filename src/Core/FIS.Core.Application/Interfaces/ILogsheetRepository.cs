using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Logsheet entity operations
    /// Provides contract for CRUD operations on vehicle logsheets
    /// </summary>
    public interface ILogsheetRepository
    {
        Task<Logsheet?> GetByIdAsync(int logCode);
        Task<IEnumerable<Logsheet>> GetAllAsync();
        Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<Logsheet>> GetByMonthAsync(DateTime month);
        Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId);
        Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId);
        Task DeleteAsync(int logCode, int currentUserId);
    }
}
