using FIS.Core.Domain.Entities.ReferenceData;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for FuelTariff entity operations
    /// Provides contract for managing fuel price rates over time
    /// </summary>
    public interface IFuelTariffRepository
    {
        /// <summary>
        /// Gets a fuel tariff by its unique code
        /// </summary>
        Task<FuelTariff?> GetByIdAsync(short fuelTariffCode);

        /// <summary>
        /// Gets the current active tariff for a specific fuel type
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type code</param>
        /// <returns>Current tariff if found, null otherwise</returns>
        Task<FuelTariff?> GetCurrentTariffAsync(short fuelTypeCode);

        /// <summary>
        /// Gets all tariff history for a specific fuel type
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type code</param>
        /// <returns>Collection of all tariffs for the fuel type</returns>
        Task<IEnumerable<FuelTariff>> GetTariffHistoryAsync(short fuelTypeCode);

        /// <summary>
        /// Gets all current active tariffs (end_date is null)
        /// </summary>
        /// <returns>Collection of current tariffs</returns>
        Task<IEnumerable<FuelTariff>> GetAllCurrentTariffsAsync();

        /// <summary>
        /// Creates a new fuel tariff
        /// </summary>
        Task<FuelTariff> CreateAsync(FuelTariff fuelTariff, int currentUserId);

        /// <summary>
        /// Updates an existing fuel tariff
        /// </summary>
        Task<FuelTariff> UpdateAsync(FuelTariff fuelTariff, int currentUserId);

        /// <summary>
        /// Closes the current tariff by setting end_date and creates a new one
        /// </summary>
        /// <param name="fuelTypeCode">The fuel type to update</param>
        /// <param name="newRate">The new rate per litre</param>
        /// <param name="notes">Optional notes about the rate change</param>
        /// <param name="currentUserId">User making the change</param>
        /// <returns>The newly created tariff</returns>
        Task<FuelTariff> CreateNewRateAsync(
            short fuelTypeCode,
            decimal newRate,
            string? notes,
            int currentUserId
        );

        /// <summary>
        /// Deletes a fuel tariff (soft delete)
        /// </summary>
        Task DeleteAsync(short fuelTariffCode, int currentUserId);
    }
}
