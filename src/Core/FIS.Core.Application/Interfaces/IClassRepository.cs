using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for Class entity operations
    /// Provides contract for CRUD operations on vehicle classes
    /// </summary>
    public interface IClassRepository
    {
        /// <summary>
        /// Gets a class by its unique code
        /// </summary>
        /// <param name="classCode">The class code to search for</param>
        /// <returns>Class entity if found, null otherwise</returns>
        Task<Class?> GetByIdAsync(short classCode);

        /// <summary>
        /// Gets all non-deleted classes
        /// </summary>
        /// <returns>Collection of all active class entities</returns>
        Task<IEnumerable<Class>> GetAllAsync();

        /// <summary>
        /// Searches classes by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching class entities</returns>
        Task<IEnumerable<Class>> SearchAsync(string searchTerm);

        /// <summary>
        /// Checks whether models or vehicles still depend on a class.
        /// </summary>
        Task<ClassDeleteCheck> GetDeleteCheckAsync(short classCode);

        /// <summary>
        /// Creates a new class with audit fields auto-populated
        /// </summary>
        /// <param name="classEntity">The class entity to create</param>
        /// <param name="currentUserId">User ID for audit trail</param>
        /// <returns>The created class entity</returns>
        Task<Class> CreateAsync(Class classEntity, int currentUserId);

        /// <summary>
        /// Updates an existing class with audit fields auto-populated
        /// </summary>
        /// <param name="classEntity">The class entity to update</param>
        /// <param name="currentUserId">User ID for audit trail</param>
        /// <returns>The updated class entity</returns>
        Task<Class> UpdateAsync(Class classEntity, int currentUserId);

        /// <summary>
        /// Soft deletes a class by setting is_deleted flag
        /// </summary>
        /// <param name="classCode">The class code to delete</param>
        /// <param name="currentUserId">User ID for audit trail</param>
        Task DeleteAsync(short classCode, int currentUserId);
    }

    public sealed record ClassDeleteCheck(int ModelCount, int VehicleCount)
    {
        public bool CanDelete => ModelCount == 0 && VehicleCount == 0;
    }
}
