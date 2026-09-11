using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Journal Detail Service interface for financial transaction operations
/// Maps legacy GGFleet.Provider.JournalDetailProvider functionality
/// Handles journal entry creation, updates, reversals, and amount calculations
/// </summary>
public interface IJournalDetailService
{
    /// <summary>
    /// Get all journal details through the legacy/expanded schema compatibility projection.
    /// </summary>
    Task<IEnumerable<JournalDetail>> GetAllJournalDetailsAsync();

    /// <summary>
    /// Gets one database-backed page of un-invoiced journal details through the
    /// same legacy/expanded schema compatibility projection.
    /// </summary>
    Task<JournalDetailPage> GetUninvoicedJournalDetailsPageAsync(
        int? departmentCode,
        int page,
        int pageSize
    );

    /// <summary>
    /// Create a new journal detail entry
    /// Legacy: AddJournalDetail method
    /// </summary>
    Task<JournalDetail> CreateJournalDetailAsync(JournalDetail journalDetail);

    /// <summary>
    /// Update an existing journal detail entry
    /// Legacy: UpdateJournalDetail method
    /// </summary>
    Task UpdateJournalDetailAsync(JournalDetail journalDetail);

    /// <summary>
    /// Get journal detail by ID
    /// </summary>
    Task<JournalDetail?> GetJournalDetailByIdAsync(int journalDetailId);

    /// <summary>
    /// Get journal detail by code (GUID)
    /// Legacy: ListJournalDetail method
    /// </summary>
    Task<JournalDetail?> GetJournalDetailByCodeAsync(Guid journalDetailCode);

    /// <summary>
    /// Generate reversal entries for a journal detail
    /// Legacy: GenerateReversals method (stored procedure: NEW_DEV_UPD_JournalDetailReversal)
    /// Creates offsetting journal entries to reverse a transaction
    /// </summary>
    Task<JournalDetail> GenerateReversalAsync(Guid journalDetailCode);

    /// <summary>
    /// Calculate journal detail amount for a contract period
    /// Legacy: CalculateJournalDetailAmount method
    /// Formula: Quantity (days) * Tariff = Amount
    /// </summary>
    Task<decimal> CalculateJournalAmountAsync(
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        int vmfCode,
        short siteCode,
        int departmentCode,
        string contractType,
        DateTime checkDate
    );

    /// <summary>
    /// Get vehicle tariff (rate) for billing calculation
    /// Legacy: GetVehicleTariff method
    /// </summary>
    Task<decimal> GetVehicleTariffAsync(
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        int vmfCode,
        short siteCode,
        int departmentCode,
        string contractType,
        DateTime checkDate,
        string tariffType = "FIXED"
    );

    /// <summary>
    /// Get journal details for a vehicle
    /// </summary>
    Task<IEnumerable<JournalDetail>> GetJournalDetailsByVehicleAsync(int vmfCode);

    /// <summary>
    /// Get journal details for a site
    /// </summary>
    Task<IEnumerable<JournalDetail>> GetJournalDetailsBySiteAsync(short siteCode);

    /// <summary>
    /// Get journal details by financial year
    /// </summary>
    Task<IEnumerable<JournalDetail>> GetJournalDetailsByFinancialYearAsync(string financialYear);

    /// <summary>
    /// Calculate financial year from a date
    /// Legacy: Helper.CurrentFinancialYear method
    /// Financial year starts in March
    /// </summary>
    string CalculateFinancialYear(DateTime date);

    /// <summary>
    /// Delete a journal detail entry
    /// </summary>
    Task DeleteJournalDetailAsync(int journalDetailId);
}
