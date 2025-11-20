using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using MaintenanceTriggerEntity = FIS.Data.Entities.MaintenanceTrigger;

namespace FIS.Core.Infrastructure.Repositories
{
    /// <summary>
    /// Repository implementation for MaintenanceTrigger entity operations
    /// Provides CRUD operations for vehicle maintenance triggers
    /// </summary>
    public class MaintenanceTriggerRepository : IMaintenanceTriggerRepository
    {
        private readonly FisDbContext _context;

        public MaintenanceTriggerRepository(FisDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets a maintenance trigger by its unique code
        /// </summary>
        /// <param name="triggerCode">The maintenance trigger code to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        public async Task<MaintenanceTriggerEntity?> GetByIdAsync(short triggerCode)
        {
            return await _context.MaintenanceTriggers
                .FirstOrDefaultAsync(mt => mt.maint_trigger_code == triggerCode);
        }

        /// <summary>
        /// Gets a maintenance trigger by its description
        /// </summary>
        /// <param name="description">The maintenance trigger description to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        public async Task<MaintenanceTriggerEntity?> GetByDescriptionAsync(string description)
        {
            return await _context.MaintenanceTriggers
                .FirstOrDefaultAsync(mt => mt.description.ToLower() == description.ToLower());
        }

        /// <summary>
        /// Gets a maintenance trigger by its trigger ID
        /// </summary>
        /// <param name="triggerId">The trigger ID to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        public async Task<MaintenanceTriggerEntity?> GetByTriggerIdAsync(string triggerId)
        {
            return await _context.MaintenanceTriggers
                .FirstOrDefaultAsync(mt => mt.trigger_id == triggerId);
        }

        /// <summary>
        /// Gets all maintenance triggers
        /// </summary>
        /// <returns>Collection of all maintenance trigger entities</returns>
        public async Task<IEnumerable<MaintenanceTriggerEntity>> GetAllMaintenanceTriggersAsync()
        {
            return await _context.MaintenanceTriggers
                .OrderBy(mt => mt.description)
                .ToListAsync();
        }

        /// <summary>
        /// Searches maintenance triggers by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching maintenance trigger entities</returns>
        public async Task<IEnumerable<MaintenanceTriggerEntity>> SearchMaintenanceTriggersAsync(string searchTerm)
        {
            return await _context.MaintenanceTriggers
                .Where(mt => mt.description.ToLower().Contains(searchTerm.ToLower()))
                .OrderBy(mt => mt.description)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new maintenance trigger
        /// </summary>
        /// <param name="maintenanceTrigger">The maintenance trigger entity to create</param>
        /// <returns>The created maintenance trigger entity</returns>
        public async Task<MaintenanceTriggerEntity> CreateAsync(MaintenanceTriggerEntity maintenanceTrigger)
        {
            _context.MaintenanceTriggers.Add(maintenanceTrigger);
            await _context.SaveChangesAsync();
            return maintenanceTrigger;
        }

        /// <summary>
        /// Updates an existing maintenance trigger
        /// </summary>
        /// <param name="maintenanceTrigger">The maintenance trigger entity to update</param>
        /// <returns>The updated maintenance trigger entity</returns>
        public async Task<MaintenanceTriggerEntity> UpdateAsync(MaintenanceTriggerEntity maintenanceTrigger)
        {
            _context.Entry(maintenanceTrigger).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return maintenanceTrigger;
        }

        /// <summary>
        /// Deletes a maintenance trigger by its code
        /// </summary>
        /// <param name="triggerCode">The maintenance trigger code to delete</param>
        public async Task DeleteAsync(short triggerCode)
        {
            var maintenanceTrigger = await _context.MaintenanceTriggers.FindAsync(triggerCode);
            if (maintenanceTrigger != null)
            {
                _context.MaintenanceTriggers.Remove(maintenanceTrigger);
                await _context.SaveChangesAsync();
            }
        }
    }
}