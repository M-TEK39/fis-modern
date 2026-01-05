using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for Overhead table.
/// Handles overhead costs for tariff calculations.
/// </summary>
public interface IOverheadRepository
{
    /// <summary>
    /// Get all overheads for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of overheads</returns>
    Task<List<Overhead>> GetByTariffParameterAsync(int tariffParameterId);

    /// <summary>
    /// Get overhead by type for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="overheadTypeId">Overhead type identifier</param>
    /// <returns>Overhead or null if not found</returns>
    Task<Overhead?> GetByTypeAsync(int tariffParameterId, int overheadTypeId);

    /// <summary>
    /// Get overhead by ID.
    /// </summary>
    /// <param name="overheadId">Overhead identifier</param>
    /// <returns>Overhead or null if not found</returns>
    Task<Overhead?> GetByIdAsync(int overheadId);

    /// <summary>
    /// Get total overhead amount for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Total overhead amount</returns>
    Task<decimal> GetTotalAsync(int tariffParameterId);

    /// <summary>
    /// Create new overhead.
    /// </summary>
    /// <param name="overhead">Overhead to create</param>
    /// <returns>Created overhead</returns>
    Task<Overhead> CreateAsync(Overhead overhead);

    /// <summary>
    /// Update existing overhead.
    /// </summary>
    /// <param name="overhead">Overhead to update</param>
    /// <returns>Updated overhead</returns>
    Task<Overhead> UpdateAsync(Overhead overhead);

    /// <summary>
    /// Delete overhead.
    /// </summary>
    /// <param name="overheadId">Overhead identifier</param>
    Task DeleteAsync(int overheadId);

    /// <summary>
    /// Delete all overheads for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    Task DeleteByTariffParameterAsync(int tariffParameterId);
}
