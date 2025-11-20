using FuelTypeEntity = FIS.Data.Entities.FuelType;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for FuelType entity operations
    /// Provides contract for CRUD operations on vehicle fuel types
    /// </summary>
    public interface IFuelTypeRepository
    {
        /// <summary>
        /// Gets a fuel type by its unique code
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type code to search for</param>
        /// <returns>FuelType entity if found, null otherwise</returns>
        Task<FuelTypeEntity?> GetByIdAsync(short fuelTypeCode);

        /// <summary>
        /// Gets a fuel type by its description/name
        /// </summary>
        /// <param name="fuelDescription">The fuel description to search for</param>
        /// <returns>FuelType entity if found, null otherwise</returns>
        Task<FuelTypeEntity?> GetByDescriptionAsync(string fuelDescription);

        /// <summary>
        /// Gets all fuel types
        /// </summary>
        /// <returns>Collection of all fuel type entities</returns>
        Task<IEnumerable<FuelTypeEntity>> GetAllFuelTypesAsync();

        /// <summary>
        /// Searches fuel types by description containing the search term
        /// </summary>
        /// <param name="searchTerm">The search term to filter by</param>
        /// <returns>Collection of matching fuel type entities</returns>
        Task<IEnumerable<FuelTypeEntity>> SearchFuelTypesAsync(string searchTerm);

        /// <summary>
        /// Creates a new fuel type
        /// </summary>
        /// <param name="fuelType">The fuel type entity to create</param>
        /// <returns>The created fuel type entity</returns>
        Task<FuelTypeEntity> CreateAsync(FuelTypeEntity fuelType);

        /// <summary>
        /// Updates an existing fuel type
        /// </summary>
        /// <param name="fuelType">The fuel type entity to update</param>
        /// <returns>The updated fuel type entity</returns>
        Task<FuelTypeEntity> UpdateAsync(FuelTypeEntity fuelType);

        /// <summary>
        /// Deletes a fuel type by its code
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type code to delete</param>
        Task DeleteAsync(short fuelTypeCode);
    }
}