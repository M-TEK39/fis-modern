using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using MaintenanceTriggerEntity = FIS.Core.Domain.Entities.Maintenance.MaintenanceTrigger;

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
            return await _context
                .MaintenanceTriggers.Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(mt => mt.maint_trigger_code == triggerCode);
        }

        /// <summary>
        /// Gets a maintenance trigger by its description
        /// </summary>
        /// <param name="description">The maintenance trigger description to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        public async Task<MaintenanceTriggerEntity?> GetByDescriptionAsync(string description)
        {
            return await _context
                .MaintenanceTriggers.Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(mt =>
                    mt.description != null && mt.description.ToLower() == description.ToLower()
                );
        }

        /// <summary>
        /// Gets a maintenance trigger by its trigger ID
        /// </summary>
        /// <param name="triggerId">The trigger ID to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        public async Task<MaintenanceTriggerEntity?> GetByTriggerIdAsync(string triggerId)
        {
            return await _context
                .MaintenanceTriggers.Where(x => !x.is_deleted)
                .FirstOrDefaultAsync(mt => mt.trigger_id == triggerId);
        }

        /// <summary>
        /// Gets all maintenance triggers
        /// </summary>
        /// <returns>Collection of all maintenance trigger entities</returns>
        public async Task<IEnumerable<MaintenanceTriggerEntity>> GetAllMaintenanceTriggersAsync()
        {
            return await _context
                .MaintenanceTriggers.Where(x => !x.is_deleted)
                .OrderBy(mt => mt.description)
                .ToListAsync();
        }

        /// <summary>
        /// Searches maintenance triggers by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching maintenance trigger entities</returns>
        public async Task<IEnumerable<MaintenanceTriggerEntity>> SearchMaintenanceTriggersAsync(
            string searchTerm
        )
        {
            return await _context
                .MaintenanceTriggers.Where(mt =>
                    mt.description != null
                    && mt.description.ToLower().Contains(searchTerm.ToLower())
                )
                .OrderBy(mt => mt.description)
                .ToListAsync();
        }

        /// <summary>
        /// Creates a new maintenance trigger
        /// </summary>
        /// <param name="maintenanceTrigger">The maintenance trigger entity to create</param>
        /// <param name="currentUserId">The ID of the user performing the action</param>
        /// <returns>The created maintenance trigger entity</returns>
        public async Task<MaintenanceTriggerEntity> CreateAsync(
            MaintenanceTriggerEntity maintenanceTrigger,
            int currentUserId
        )
        {
            // Auto-populate audit fields
            maintenanceTrigger.date_created = DateTime.UtcNow;
            maintenanceTrigger.created_by_user_code = currentUserId;
            maintenanceTrigger.is_deleted = false;

            _context.MaintenanceTriggers.Add(maintenanceTrigger);
            await _context.SaveChangesAsync();
            return maintenanceTrigger;
        }

        /// <summary>
        /// Updates an existing maintenance trigger
        /// </summary>
        /// <param name="maintenanceTrigger">The maintenance trigger entity to update</param>
        /// <param name="currentUserId">The ID of the user performing the action</param>
        /// <returns>The updated maintenance trigger entity</returns>
        public async Task<MaintenanceTriggerEntity> UpdateAsync(
            MaintenanceTriggerEntity maintenanceTrigger,
            int currentUserId
        )
        {
            if (maintenanceTrigger == null)
                throw new ArgumentNullException(nameof(maintenanceTrigger));

            var existing = await _context.MaintenanceTriggers.FindAsync(
                maintenanceTrigger.maint_trigger_code
            );
            if (existing == null)
                throw new InvalidOperationException(
                    $"MaintenanceTrigger with maint_trigger_code {maintenanceTrigger.maint_trigger_code} not found"
                );

            // Preserve creation audit fields
            maintenanceTrigger.date_created = existing.date_created;
            maintenanceTrigger.created_by_user_code = existing.created_by_user_code;
            // Set update audit fields
            maintenanceTrigger.date_updated = DateTime.UtcNow;
            maintenanceTrigger.modified_by_user_code = currentUserId;

            _context.Entry(existing).CurrentValues.SetValues(maintenanceTrigger);
            await _context.SaveChangesAsync();
            return existing;
        }

        /// <summary>
        /// Deletes a maintenance trigger by its code
        /// </summary>
        /// <param name="triggerCode">The maintenance trigger code to delete</param>
        /// <param name="currentUserId">The ID of the user performing the action</param>
        public async Task DeleteAsync(short triggerCode, int currentUserId)
        {
            var maintenanceTrigger = await _context.MaintenanceTriggers.FindAsync(triggerCode);
            if (maintenanceTrigger != null)
            {
                // Soft delete instead of hard delete
                maintenanceTrigger.is_deleted = true;
                maintenanceTrigger.date_updated = DateTime.UtcNow;
                maintenanceTrigger.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();
            }
        }
    }
}
