using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Services.Billing;

/// <summary>
/// Service for automated billing operations, replicating legacy database trigger functionality.
/// Handles creation of journal_detail records for contracts, trips, and fuel transactions.
/// </summary>
public interface IBillingService
{
    #region Auto-Billing Record Creation (Replicates Triggers)

    /// <summary>
    /// Auto-create billing record when contract is created or updated.
    /// Replicates TRG_INS_ContractJournalDetailRecord and TRG_UPD_ContractJournalDetailRecord triggers.
    /// </summary>
    /// <param name="contract">Contract that was created/updated</param>
    /// <param name="isUpdate">True if update, false if insert</param>
    /// <returns>Created journal_detail record</returns>
    /// <remarks>
    /// Creates journal_detail with:
    /// - journal_detail_type_code = 1 (Fixed)
    /// - Quantity = calculated from contract dates/hours
    /// - Tariff = from GetVehicleTariff function
    /// - Amount = ROUND(quantity * tariff, 2)
    /// </remarks>
    Task<JournalDetail> CreateContractBillingRecordAsync(Contract contract, bool isUpdate = false);

    /// <summary>
    /// Auto-create billing record when trip is completed.
    /// Replicates TRG_INS_RouteJournalDetailRecord trigger.
    /// </summary>
    /// <param name="trip">Completed trip authority</param>
    /// <param name="startOdometer">Starting odometer reading</param>
    /// <param name="endOdometer">Ending odometer reading</param>
    /// <returns>Created journal_detail record for kilometers</returns>
    /// <remarks>
    /// Creates journal_detail with:
    /// - journal_detail_type_code = 2 (Kilos)
    /// - Quantity = end_odometer - start_odometer
    /// - Tariff = from GetVehicleTariff(contractCode, date, 'KILOS')
    /// - Amount = ROUND(quantity * tariff, 2)
    /// </remarks>
    Task<JournalDetail> CreateTripBillingRecordAsync(Trip trip, int startOdometer, int endOdometer);

    /// <summary>
    /// Auto-create billing record when fuel is purchased.
    /// Replicates fuel card purchase trigger logic.
    /// </summary>
    /// <param name="fuelPurchase">Fuel card purchase transaction</param>
    /// <returns>Created journal_detail record for fuel</returns>
    /// <remarks>
    /// Creates journal_detail with:
    /// - journal_detail_type_code = 3 (Fuel)
    /// - Quantity = liters purchased
    /// - Tariff = fuel price per liter
    /// - Amount = quantity * tariff
    /// </remarks>
    Task<JournalDetail> CreateFuelBillingRecordAsync(object fuelPurchase);

    #endregion

    #region Amount Calculation Methods

    /// <summary>
    /// Calculate fixed billing amount for a contract period.
    /// Replicates CalculateJournalDetailAmount logic from JournalDetail.cs.
    /// </summary>
    /// <param name="contractCode">Contract identifier</param>
    /// <param name="startDate">Period start date</param>
    /// <param name="endDate">Period end date (charged_until)</param>
    /// <param name="contractType">Contract type (A,B,C,F,L)</param>
    /// <param name="hoursUsed">Hours used (for hourly contracts)</param>
    /// <returns>Calculated amount (quantity * tariff)</returns>
    Task<decimal> CalculateFixedAmountAsync(
        int contractCode,
        DateTime startDate,
        DateTime endDate,
        string contractType,
        int? hoursUsed = null
    );

    /// <summary>
    /// Calculate kilometer billing amount for a trip.
    /// </summary>
    /// <param name="contractCode">Contract identifier</param>
    /// <param name="startOdometer">Starting odometer</param>
    /// <param name="endOdometer">Ending odometer</param>
    /// <param name="date">Date of trip</param>
    /// <returns>Calculated amount (kilometers * tariff)</returns>
    Task<decimal> CalculateKiloAmountAsync(
        int contractCode,
        int startOdometer,
        int endOdometer,
        DateTime date
    );

    /// <summary>
    /// Calculate fuel billing amount for a purchase.
    /// </summary>
    /// <param name="fuelTypeCode">Fuel type identifier</param>
    /// <param name="liters">Liters purchased</param>
    /// <param name="purchaseDate">Date of purchase</param>
    /// <returns>Calculated amount (liters * fuel tariff)</returns>
    Task<decimal> CalculateFuelAmountAsync(int fuelTypeCode, decimal liters, DateTime purchaseDate);

    #endregion

    #region Quantity Calculation Methods

    /// <summary>
    /// Calculate billing quantity based on contract type.
    /// Replicates quantity calculation logic from triggers.
    /// </summary>
    /// <param name="contractType">Contract type (A,B,C,F,L)</param>
    /// <param name="startDate">Contract/period start date</param>
    /// <param name="endDate">Contract/period end date (charged_until)</param>
    /// <param name="hoursUsed">Hours used (for hourly contracts)</param>
    /// <returns>Calculated quantity (days/hours/months)</returns>
    /// <remarks>
    /// Calculation rules:
    /// - Hourly (C): hours_used
    /// - Lease (L) starting 16th-31st: 1 (full month, but 0.00 tariff)
    /// - Lease (L) starting 1st-15th: 1 (full month charge)
    /// - All others: DATEDIFF(start_date, charged_until) + 1 days
    /// </remarks>
    int CalculateQuantity(
        string contractType,
        DateTime startDate,
        DateTime endDate,
        int? hoursUsed = null
    );

    /// <summary>
    /// Calculate number of days between two dates (legacy DateDiff helper).
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <returns>Number of days (inclusive)</returns>
    int CalculateDays(DateTime startDate, DateTime endDate);

    #endregion

    #region Financial Year Handling

    /// <summary>
    /// Get financial year for a transaction date.
    /// Legacy system uses April 1 fiscal year start.
    /// </summary>
    /// <param name="transactionDate">Transaction date</param>
    /// <returns>Financial year string (e.g., "2024")</returns>
    /// <remarks>
    /// Rules:
    /// - Transactions Jan 1 - Mar 31: Prior year (e.g., 2023 for Jan 2024)
    /// - Transactions Apr 1 - Dec 31: Current year (e.g., 2024 for Apr 2024)
    /// </remarks>
    string GetFinancialYear(DateTime transactionDate);

    /// <summary>
    /// Check if a specific year is the current financial year.
    /// </summary>
    /// <param name="year">Year to check (e.g., "2024")</param>
    /// <param name="referenceDate">Reference date (typically current date)</param>
    /// <returns>True if year is current financial year, false otherwise</returns>
    bool IsInCurrentFinancialYear(string year, DateTime referenceDate);

    /// <summary>
    /// Get the current financial year based on today's date.
    /// </summary>
    /// <returns>Current financial year string</returns>
    string GetCurrentFinancialYear();

    #endregion

    #region Rebilling and Reversal Logic

    /// <summary>
    /// Create rebill record for a reversed transaction.
    /// Required when reversing transactions from prior financial year.
    /// </summary>
    /// <param name="originalJournalDetailCode">Original journal_detail GUID to reverse</param>
    /// <param name="newFinancialYear">Financial year for rebill</param>
    /// <param name="reason">Reason for rebill</param>
    /// <returns>Created rebill journal_detail record</returns>
    /// <remarks>
    /// Rebilling rules:
    /// - If reversing prior year transaction in new year: create rebill in new year
    /// - Rebill has: journal_detail_rebill_code = original GUID
    /// - Original has: journal_detail_reversalof = reversal GUID
    /// </remarks>
    Task<JournalDetail> CreateRebillRecordAsync(
        Guid originalJournalDetailCode,
        string newFinancialYear,
        string reason
    );

    /// <summary>
    /// Reverse a journal detail transaction.
    /// Creates negative entry to offset original.
    /// </summary>
    /// <param name="journalDetailCode">Journal detail GUID to reverse</param>
    /// <param name="reason">Reason for reversal</param>
    /// <returns>Created reversal journal_detail record</returns>
    /// <remarks>
    /// Reversal creates negative journal_detail with:
    /// - Same department, site, vehicle, type, tariff
    /// - Negative quantity and amount
    /// - journal_detail_reversalof = original GUID
    /// </remarks>
    Task<JournalDetail> ReverseJournalDetailAsync(Guid journalDetailCode, string reason);

    /// <summary>
    /// Check if reversal requires rebill (cross-fiscal-year).
    /// </summary>
    /// <param name="originalJournalDetail">Original transaction to reverse</param>
    /// <param name="reversalDate">Date of reversal</param>
    /// <returns>True if rebill required, false otherwise</returns>
    bool RequiresRebill(JournalDetail originalJournalDetail, DateTime reversalDate);

    #endregion

    #region Contract Billing Updates

    /// <summary>
    /// Update contract billing when contract is modified.
    /// Handles rebilling, reversals, and updates based on change type.
    /// </summary>
    /// <param name="contract">Modified contract</param>
    /// <param name="changeType">Type of change (INSERT, UPDATE, REBILL, REVERSAL)</param>
    /// <returns>Updated or created journal_detail record</returns>
    /// <remarks>
    /// Replicates ContractService change tracking:
    /// - DoInsert: Create new billing record
    /// - DoUpdate: Update existing billing record
    /// - DoRebill: Create rebill for prior year change
    /// - DoReversal: Reverse existing billing, create new
    /// </remarks>
    Task<JournalDetail> UpdateContractBillingAsync(
        Contract contract,
        ContractChangeType changeType
    );

    /// <summary>
    /// Update charged_until date on contract when billing is created.
    /// </summary>
    /// <param name="contractCode">Contract identifier</param>
    /// <param name="chargedUntilDate">New charged_until date</param>
    Task UpdateChargedUntilAsync(int contractCode, DateTime chargedUntilDate);

    #endregion

    #region Batch Operations

    /// <summary>
    /// Get all unposted journal details for a department.
    /// Used for monthly billing review before posting.
    /// </summary>
    /// <param name="departmentCode">Department identifier (null = all departments)</param>
    /// <param name="startDate">Start date filter (null = no filter)</param>
    /// <param name="endDate">End date filter (null = no filter)</param>
    /// <returns>List of unposted journal details</returns>
    /// <remarks>
    /// Returns journal_detail records where:
    /// - journal_code IS NULL (not assigned to batch)
    /// - journal_detail_isaccepted = 0 (not posted)
    /// - journal_detail_type_code NOT IN (5, 6) (exclude Suspense, Revenue)
    /// </remarks>
    Task<List<JournalDetail>> GetUnpostedJournalDetailsAsync(
        int? departmentCode = null,
        DateTime? startDate = null,
        DateTime? endDate = null
    );

    /// <summary>
    /// Mark journal details as posted.
    /// Sets journal_detail_isaccepted = 1 and journal_detail_date_posted = current date.
    /// </summary>
    /// <param name="journalDetailCodes">List of journal detail GUIDs to post</param>
    /// <param name="journalCode">Journal code to assign (batch grouping)</param>
    Task PostJournalDetailsAsync(List<Guid> journalDetailCodes, int journalCode);

    #endregion

    #region Validation

    /// <summary>
    /// Validate that a contract can be billed.
    /// Checks for required fields, valid tariffs, etc.
    /// </summary>
    /// <param name="contract">Contract to validate</param>
    /// <returns>Validation result with any errors</returns>
    Task<BillingValidationResult> ValidateContractForBillingAsync(Contract contract);

    /// <summary>
    /// Validate that a trip can be billed.
    /// Checks for valid contract, odometer readings, etc.
    /// </summary>
    /// <param name="trip">Trip to validate</param>
    /// <param name="startOdometer">Starting odometer</param>
    /// <param name="endOdometer">Ending odometer</param>
    /// <returns>Validation result with any errors</returns>
    Task<BillingValidationResult> ValidateTripForBillingAsync(
        Trip trip,
        int startOdometer,
        int endOdometer
    );

    #endregion
}

#region Supporting Types

/// <summary>
/// Type of contract change for billing purposes.
/// Matches legacy ContractService DoInsert, DoUpdate, DoRebill, DoReversal operations.
/// </summary>
public enum ContractChangeType
{
    /// <summary>
    /// New contract created - create billing record.
    /// </summary>
    Insert,

    /// <summary>
    /// Contract updated in same period - update billing record.
    /// </summary>
    Update,

    /// <summary>
    /// Contract changed requiring rebill (cross-fiscal-year or major change).
    /// </summary>
    Rebill,

    /// <summary>
    /// Contract changed requiring reversal and new billing record.
    /// </summary>
    Reversal,
}

/// <summary>
/// Result of billing validation.
/// </summary>
public class BillingValidationResult
{
    /// <summary>
    /// Is billing valid?
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation error messages.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Validation warning messages.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Create a valid result.
    /// </summary>
    public static BillingValidationResult Valid()
    {
        return new BillingValidationResult { IsValid = true };
    }

    /// <summary>
    /// Create an invalid result with errors.
    /// </summary>
    public static BillingValidationResult Invalid(params string[] errors)
    {
        return new BillingValidationResult { IsValid = false, Errors = errors.ToList() };
    }

    /// <summary>
    /// Add an error message.
    /// </summary>
    public void AddError(string error)
    {
        Errors.Add(error);
        IsValid = false;
    }

    /// <summary>
    /// Add a warning message.
    /// </summary>
    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }
}

#endregion
