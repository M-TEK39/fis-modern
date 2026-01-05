using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for VehicleTariff table (modern fin.vehicle_tariff, post-2009 system).
/// Handles calculated vehicle-specific tariffs with all cost components.
/// </summary>
public interface IVehicleTariffRepository
{
    /// <summary>
    /// Get vehicle tariff for a specific vehicle and parameter year.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>Vehicle tariff or null if not found</returns>
    Task<VehicleTariff?> GetByVehicleAndYearAsync(int vmfCode, short parameterYear);

    /// <summary>
    /// Get vehicle tariff for a specific vehicle and effective date.
    /// Finds the tariff where the date falls within the start/end date range.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="effectiveDate">Effective date</param>
    /// <returns>Vehicle tariff or null if not found</returns>
    Task<VehicleTariff?> GetByVehicleAndDateAsync(int vmfCode, DateTime effectiveDate);

    /// <summary>
    /// Get all vehicle tariffs for a parameter year.
    /// </summary>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>List of vehicle tariffs for the year</returns>
    Task<List<VehicleTariff>> GetByParameterYearAsync(short parameterYear);

    /// <summary>
    /// Get vehicle tariff by ID.
    /// </summary>
    /// <param name="vehicleTariffCode">Vehicle tariff identifier</param>
    /// <returns>Vehicle tariff or null if not found</returns>
    Task<VehicleTariff?> GetByIdAsync(int vehicleTariffCode);

    /// <summary>
    /// Get all vehicle tariffs for a specific vehicle.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <returns>List of vehicle tariffs</returns>
    Task<List<VehicleTariff>> GetByVehicleAsync(int vmfCode);

    /// <summary>
    /// Create new vehicle tariff.
    /// </summary>
    /// <param name="vehicleTariff">Vehicle tariff to create</param>
    /// <returns>Created vehicle tariff</returns>
    Task<VehicleTariff> CreateAsync(VehicleTariff vehicleTariff);

    /// <summary>
    /// Update existing vehicle tariff.
    /// </summary>
    /// <param name="vehicleTariff">Vehicle tariff to update</param>
    /// <returns>Updated vehicle tariff</returns>
    Task<VehicleTariff> UpdateAsync(VehicleTariff vehicleTariff);

    /// <summary>
    /// Delete vehicle tariff.
    /// </summary>
    /// <param name="vehicleTariffCode">Vehicle tariff identifier</param>
    Task DeleteAsync(int vehicleTariffCode);

    /// <summary>
    /// Delete all vehicle tariffs for a parameter year.
    /// Used when recalculating tariffs for a year.
    /// </summary>
    /// <param name="parameterYear">Tariff parameter year</param>
    Task DeleteByParameterYearAsync(short parameterYear);

    /// <summary>
    /// Check if vehicle has tariff for a parameter year.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>True if tariff exists, false otherwise</returns>
    Task<bool> ExistsAsync(int vmfCode, short parameterYear);
}
