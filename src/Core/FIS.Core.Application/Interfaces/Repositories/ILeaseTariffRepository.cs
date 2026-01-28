using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for LeaseTariff table.
/// Handles lease-specific monthly rates and excess kilometer charges.
/// </summary>
public interface ILeaseTariffRepository
{
    /// <summary>
    /// Get lease tariff for a vehicle effective on a specific date.
    /// Finds the active tariff where the date falls within the start/end date range.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="effectiveDate">Effective date</param>
    /// <returns>Lease tariff or null if not found</returns>
    Task<LeaseTariff?> GetByVehicleAndDateAsync(int vmfCode, DateTime effectiveDate);

    /// <summary>
    /// Get all lease tariffs for a vehicle.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <returns>List of lease tariffs</returns>
    Task<List<LeaseTariff>> GetByVehicleAsync(int vmfCode);

    /// <summary>
    /// Get currently active lease tariff for a vehicle.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <returns>Active lease tariff or null if not found</returns>
    Task<LeaseTariff?> GetActiveByVehicleAsync(int vmfCode);

    /// <summary>
    /// Get lease tariff by ID.
    /// </summary>
    /// <param name="leaseTariffCode">Lease tariff identifier</param>
    /// <returns>Lease tariff or null if not found</returns>
    Task<LeaseTariff?> GetByIdAsync(int leaseTariffCode);

    /// <summary>
    /// Get all lease tariffs.
    /// </summary>
    /// <returns>List of all lease tariffs</returns>
    Task<List<LeaseTariff>> GetAllAsync();

    /// <summary>
    /// Get all active lease tariffs.
    /// </summary>
    /// <returns>List of active lease tariffs</returns>
    Task<List<LeaseTariff>> GetAllActiveAsync();

    /// <summary>
    /// Create new lease tariff.
    /// </summary>
    /// <param name="leaseTariff">Lease tariff to create</param>
    /// <returns>Created lease tariff</returns>
    Task<LeaseTariff> CreateAsync(LeaseTariff leaseTariff, int currentUserId);

    /// <summary>
    /// Update existing lease tariff.
    /// </summary>
    /// <param name="leaseTariff">Lease tariff to update</param>
    /// <returns>Updated lease tariff</returns>
    Task<LeaseTariff> UpdateAsync(LeaseTariff leaseTariff, int currentUserId);

    /// <summary>
    /// Delete lease tariff.
    /// </summary>
    /// <param name="leaseTariffCode">Lease tariff identifier</param>
    Task DeleteAsync(int leaseTariffCode, int currentUserId);

    /// <summary>
    /// Deactivate a lease tariff.
    /// </summary>
    /// <param name="leaseTariffCode">Lease tariff identifier</param>
    Task DeactivateAsync(int leaseTariffCode);
}
