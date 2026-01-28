using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for MaintenanceValue table (maintenance cost matrix).
/// Handles maintenance cost per kilometer by class and vehicle age.
/// </summary>
public interface IMaintenanceValueRepository
{
    /// <summary>
    /// Get maintenance value for specific class and age criteria.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="classCode">Vehicle class code</param>
    /// <param name="monthsAge">Vehicle age in months</param>
    /// <param name="kilometerAge">Vehicle age in kilometers</param>
    /// <returns>Maintenance value or null if not found</returns>
    Task<MaintenanceValue?> GetByClassAndAgeAsync(
        int tariffParameterId,
        int classCode,
        int monthsAge,
        int kilometerAge);

    /// <summary>
    /// Get all maintenance values for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of maintenance values</returns>
    Task<List<MaintenanceValue>> GetByTariffParameterAsync(int tariffParameterId);

    /// <summary>
    /// Get maintenance values for a specific class.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="classCode">Vehicle class code</param>
    /// <returns>List of maintenance values for the class</returns>
    Task<List<MaintenanceValue>> GetByClassAsync(int tariffParameterId, int classCode);

    /// <summary>
    /// Get maintenance value by ID.
    /// </summary>
    /// <param name="maintenanceValueId">Maintenance value identifier</param>
    /// <returns>Maintenance value or null if not found</returns>
    Task<MaintenanceValue?> GetByIdAsync(int maintenanceValueId);

    /// <summary>
    /// Create new maintenance value.
    /// </summary>
    /// <param name="maintenanceValue">Maintenance value to create</param>
    /// <returns>Created maintenance value</returns>
    Task<MaintenanceValue> CreateAsync(MaintenanceValue maintenanceValue, int currentUserId);

    /// <summary>
    /// Update existing maintenance value.
    /// </summary>
    /// <param name="maintenanceValue">Maintenance value to update</param>
    /// <returns>Updated maintenance value</returns>
    Task<MaintenanceValue> UpdateAsync(MaintenanceValue maintenanceValue, int currentUserId);

    /// <summary>
    /// Delete maintenance value.
    /// </summary>
    /// <param name="maintenanceValueId">Maintenance value identifier</param>
    Task DeleteAsync(int maintenanceValueId, int currentUserId);

    /// <summary>
    /// Delete all maintenance values for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    Task DeleteByTariffParameterAsync(int tariffParameterId);
}
