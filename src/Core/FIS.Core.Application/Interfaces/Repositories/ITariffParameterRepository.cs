using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository for TariffParameter table (modern tariff system parameters).
/// Handles fiscal year parameters for tariff calculations.
/// </summary>
public interface ITariffParameterRepository
{
    /// <summary>
    /// Get tariff parameter by year.
    /// </summary>
    /// <param name="year">Tariff parameter year</param>
    /// <returns>Tariff parameter or null if not found</returns>
    Task<TariffParameter?> GetByYearAsync(int year);

    /// <summary>
    /// Get current/active tariff parameter.
    /// Returns the parameter for the current fiscal year.
    /// </summary>
    /// <returns>Current tariff parameter or null if not found</returns>
    Task<TariffParameter?> GetCurrentAsync();

    /// <summary>
    /// Get tariff parameter by ID.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Tariff parameter or null if not found</returns>
    Task<TariffParameter?> GetByIdAsync(int tariffParameterId);

    /// <summary>
    /// Get all tariff parameters ordered by year descending.
    /// </summary>
    /// <returns>List of all tariff parameters</returns>
    Task<List<TariffParameter>> GetAllAsync();

    /// <summary>
    /// Archive Fiscal_TariffParameter_Management years dropdown uses
    /// fin.DEV_SEL_TariffParameter_Lookup with DataTextField DropdownText
    /// and DataValueField TariffParameterID.
    /// </summary>
    Task<IReadOnlyList<TariffParameterLookup>?> GetLookupAsync();

    /// <summary>
    /// Archive gvOverhead_General_Bind uses
    /// fin.DEV_SEL_TariffParameter_ByTariffParameterID @TariffParameterID.
    /// Null means the procedure is absent; empty means it returned no row.
    /// </summary>
    Task<IReadOnlyList<TariffParameter>?> GetBySelectorIdAsync(int tariffParameterId);

    /// <summary>
    /// Get approved tariff parameters only.
    /// </summary>
    /// <returns>List of approved tariff parameters</returns>
    Task<List<TariffParameter>> GetApprovedAsync();

    /// <summary>
    /// Create new tariff parameter.
    /// </summary>
    /// <param name="tariffParameter">Tariff parameter to create</param>
    /// <returns>Created tariff parameter</returns>
    Task<TariffParameter> CreateAsync(TariffParameter tariffParameter, int currentUserId);

    /// <summary>
    /// Update existing tariff parameter.
    /// </summary>
    /// <param name="tariffParameter">Tariff parameter to update</param>
    /// <returns>Updated tariff parameter</returns>
    Task<TariffParameter> UpdateAsync(TariffParameter tariffParameter, int currentUserId);

    /// <summary>
    /// Delete tariff parameter.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    Task DeleteAsync(int tariffParameterId, int currentUserId);

    /// <summary>
    /// Check if tariff parameter exists for a year.
    /// </summary>
    /// <param name="year">Tariff parameter year</param>
    /// <returns>True if exists, false otherwise</returns>
    Task<bool> ExistsForYearAsync(int year);
}

public sealed record TariffParameterLookup(int TariffParameterId, string? DropdownText);
