using MaintenanceTriggerEntity = FIS.Core.Domain.Entities.Maintenance.MaintenanceTrigger;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for MaintenanceTrigger entity operations
    /// Provides contract for CRUD operations on vehicle maintenance triggers
    /// </summary>
    public interface IMaintenanceTriggerRepository
    {
        /// <summary>
        /// Gets a maintenance trigger by its unique code
        /// </summary>
        /// <param name="triggerCode">The maintenance trigger code to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        Task<MaintenanceTriggerEntity?> GetByIdAsync(short triggerCode);

        /// <summary>
        /// Gets a maintenance trigger by its description
        /// </summary>
        /// <param name="description">The maintenance trigger description to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        Task<MaintenanceTriggerEntity?> GetByDescriptionAsync(string description);

        /// <summary>
        /// Gets a maintenance trigger by its trigger ID
        /// </summary>
        /// <param name="triggerId">The trigger ID to search for</param>
        /// <returns>MaintenanceTrigger entity if found, null otherwise</returns>
        Task<MaintenanceTriggerEntity?> GetByTriggerIdAsync(string triggerId);

        /// <summary>
        /// Gets all maintenance triggers
        /// </summary>
        /// <returns>Collection of all maintenance trigger entities</returns>
        Task<IEnumerable<MaintenanceTriggerEntity>> GetAllMaintenanceTriggersAsync();

        /// <summary>
        /// Searches maintenance triggers by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching maintenance trigger entities</returns>
        Task<IEnumerable<MaintenanceTriggerEntity>> SearchMaintenanceTriggersAsync(
            string searchTerm
        );

        /// <summary>
        /// Creates a new maintenance trigger
        /// </summary>
        /// <param name="maintenanceTrigger">The maintenance trigger entity to create</param>
        /// <returns>The created maintenance trigger entity</returns>
        Task<MaintenanceTriggerEntity> CreateAsync(
            MaintenanceTriggerEntity maintenanceTrigger,
            int currentUserId
        );

        /// <summary>
        /// Updates an existing maintenance trigger
        /// </summary>
        /// <param name="maintenanceTrigger">The maintenance trigger entity to update</param>
        /// <returns>The updated maintenance trigger entity</returns>
        Task<MaintenanceTriggerEntity> UpdateAsync(
            MaintenanceTriggerEntity maintenanceTrigger,
            int currentUserId
        );

        /// <summary>
        /// Deletes a maintenance trigger by its code
        /// </summary>
        /// <param name="triggerCode">The maintenance trigger code to delete</param>
        Task DeleteAsync(short triggerCode, int currentUserId);
    }
}
