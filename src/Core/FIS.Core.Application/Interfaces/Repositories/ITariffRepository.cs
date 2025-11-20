using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for legacy Tariff table (pre-2009 tariff system).
/// Handles tariff lookups by vehicle class, year manufactured, and effective date.
/// </summary>
public interface ITariffRepository
{
    /// <summary>
    /// Get tariff by class code, year manufactured, and effective date.
    /// Finds the tariff where the effective date falls within the start/end date range.
    /// </summary>
    /// <param name="classCode">Vehicle class code</param>
    /// <param name="yearManufactured">Year vehicle was manufactured</param>
    /// <param name="effectiveDate">Date tariff should be effective for</param>
    /// <returns>Tariff or null if not found</returns>
    Task<Tariff?> GetTariffAsync(
        short classCode,
        short yearManufactured,
        DateTime effectiveDate);

    /// <summary>
    /// Get all tariffs for a vehicle class.
    /// </summary>
    /// <param name="classCode">Vehicle class code</param>
    /// <returns>List of tariffs for the class</returns>
    Task<List<Tariff>> GetTariffsByClassAsync(short classCode);

    /// <summary>
    /// Get all tariffs effective on a specific date.
    /// </summary>
    /// <param name="effectiveDate">Effective date</param>
    /// <returns>List of tariffs effective on the date</returns>
    Task<List<Tariff>> GetTariffsByDateAsync(DateTime effectiveDate);

    /// <summary>
    /// Get tariff by ID.
    /// </summary>
    /// <param name="tariffCode">Tariff identifier</param>
    /// <returns>Tariff or null if not found</returns>
    Task<Tariff?> GetByIdAsync(int tariffCode);

    /// <summary>
    /// Get all tariffs.
    /// </summary>
    /// <returns>List of all tariffs</returns>
    Task<List<Tariff>> GetAllAsync();

    /// <summary>
    /// Create new tariff.
    /// </summary>
    /// <param name="tariff">Tariff to create</param>
    /// <returns>Created tariff</returns>
    Task<Tariff> CreateAsync(Tariff tariff);

    /// <summary>
    /// Update existing tariff.
    /// </summary>
    /// <param name="tariff">Tariff to update</param>
    /// <returns>Updated tariff</returns>
    Task<Tariff> UpdateAsync(Tariff tariff);

    /// <summary>
    /// Delete tariff.
    /// </summary>
    /// <param name="tariffCode">Tariff identifier</param>
    Task DeleteAsync(int tariffCode);
}
