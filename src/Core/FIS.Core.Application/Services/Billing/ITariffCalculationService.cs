using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Services.Billing;

/// <summary>
/// Service for calculating tariffs using both legacy (pre-2009) and modern (post-2009) tariff systems.
/// Replicates the critical GetVehicleTariff SQL function (237 lines) from legacy system.
/// </summary>
public interface ITariffCalculationService
{
    #region Main Tariff Lookup Methods

    /// <summary>
    /// Main tariff lookup method - replicates dbo.GetVehicleTariff function.
    /// Returns tariff amount for a specific contract and date, with special business rules.
    /// </summary>
    /// <param name="contractCode">Contract identifier</param>
    /// <param name="checkDate">Date to check tariff for</param>
    /// <param name="tariffType">Type of tariff (FIXED or KILOS)</param>
    /// <returns>Tariff result with amount and status</returns>
    /// <remarks>
    /// Return codes:
    /// -1 = Year manufactured not found
    /// -2 = No matching tariff for class/year/date
    /// -3 = Incomplete tariff (missing overhead or maintenance components)
    /// 0.00 = Valid zero tariff (relief vehicles, GGMT internal, missing vehicles)
    /// > 0 = Valid tariff amount
    /// </remarks>
    Task<TariffResult> GetVehicleTariffAsync(
        int contractCode,
        DateTime checkDate,
        TariffType tariffType);

    /// <summary>
    /// Overload accepting full contract details for tariff calculation.
    /// Used when contract object is already loaded.
    /// </summary>
    Task<TariffResult> GetVehicleTariffAsync(
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        int vmfCode,
        short siteCode,
        int departmentCode,
        string contractType,
        DateTime checkDate,
        TariffType tariffType);

    #endregion

    #region Legacy Tariff System (Pre-2009)

    /// <summary>
    /// Get fixed tariff from legacy tariff table (pre-2009 system).
    /// Looks up tariff based on vehicle class and year manufactured.
    /// </summary>
    /// <param name="vehicleClassCode">Vehicle class identifier</param>
    /// <param name="yearManufactured">Year vehicle was manufactured</param>
    /// <param name="effectiveDate">Date tariff should be effective for</param>
    /// <param name="contractType">Contract type (A=Permanent, B=Daily, C=Hourly, F=Relief, L=Lease)</param>
    /// <returns>Fixed tariff amount per day/hour/month</returns>
    /// <remarks>
    /// Conversion formulas:
    /// - Permanent (A): monthly_fixed_amount * 12 / 365
    /// - Daily (B): daily_fixed_amount
    /// - Hourly (C): hourly_fixed_amount
    /// - Relief (F): 0.00
    /// - Lease (L): Special rules (see GetLeaseTariff)
    /// </remarks>
    Task<decimal> GetLegacyFixedTariffAsync(
        int vehicleClassCode,
        int yearManufactured,
        DateTime effectiveDate,
        string contractType);

    /// <summary>
    /// Get kilometer tariff from legacy tariff table (pre-2009 system).
    /// </summary>
    /// <param name="vehicleClassCode">Vehicle class identifier</param>
    /// <param name="yearManufactured">Year vehicle was manufactured</param>
    /// <param name="effectiveDate">Date tariff should be effective for</param>
    /// <returns>Tariff per kilometer</returns>
    /// <remarks>
    /// Returns monthly_odo_amount from tariff table.
    /// Lease vehicles (type 'L') return 0.00 (kilometers included in fixed rate).
    /// </remarks>
    Task<decimal> GetLegacyKilometerTariffAsync(
        int vehicleClassCode,
        int yearManufactured,
        DateTime effectiveDate);

    #endregion

    #region Modern Tariff System (Post-2009)

    /// <summary>
    /// Get configured vehicle tariff from modern fin.vehicle_tariff table (post-2009 system).
    /// Used for vehicles manufactured 2008+ after 2009-04-01.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="parameterYear">Tariff parameter year (fiscal year)</param>
    /// <param name="effectiveDate">Date tariff should be effective for</param>
    /// <returns>Complete vehicle tariff with all cost components</returns>
    Task<VehicleTariff?> GetConfiguredVehicleTariffAsync(
        int vmfCode,
        int parameterYear,
        DateTime effectiveDate);

    /// <summary>
    /// Get fixed tariff from modern tariff system based on contract type.
    /// </summary>
    /// <param name="vehicleTariff">Vehicle tariff record</param>
    /// <param name="contractType">Contract type (A=Permanent, B=Daily, C=Hourly, L=Lease)</param>
    /// <returns>Fixed tariff amount</returns>
    /// <remarks>
    /// Formulas:
    /// - Permanent (A): vehicle_fixed_daily_tariff (= vehicle_fixed_tariff * 12 / 365)
    /// - Daily (B): vehicle_fixed_tariff_pool
    /// - Hourly (C): vehicle_fixed_tariff_pool / 8
    /// - Lease (L): vehicle_fixed_tariff (monthly)
    /// </remarks>
    decimal GetModernFixedTariff(VehicleTariff vehicleTariff, string contractType);

    /// <summary>
    /// Get kilometer tariff from modern tariff system.
    /// </summary>
    /// <param name="vehicleTariff">Vehicle tariff record</param>
    /// <param name="contractType">Contract type</param>
    /// <returns>Tariff per kilometer</returns>
    /// <remarks>
    /// Returns vehicle_kilometer_tariff for most contracts.
    /// Lease contracts (L) return 0.00 (kilometers included in fixed rate).
    /// </remarks>
    decimal GetModernKilometerTariff(VehicleTariff vehicleTariff, string contractType);

    #endregion

    #region Lease Tariff Handling

    /// <summary>
    /// Get lease-specific tariff from LeaseTariff table.
    /// Used for contract type 'L' (Lease).
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="effectiveDate">Date tariff should be effective for</param>
    /// <returns>Lease tariff with fixed monthly amount and excess kilometer rate</returns>
    Task<LeaseTariff?> GetLeaseTariffAsync(int vmfCode, DateTime effectiveDate);

    /// <summary>
    /// Determine if lease contract should be pro-rated to 0.00 based on start date.
    /// Legacy business rule: Lease contracts starting between 16th and end of month = 0.00 tariff.
    /// </summary>
    /// <param name="contractStartDate">Contract start date</param>
    /// <returns>True if tariff should be 0.00, false otherwise</returns>
    /// <remarks>
    /// Pro-rating rule:
    /// - Starts 16th-31st: 0.00 tariff (pro-rated away)
    /// - Starts 1st-15th: Full monthly charge
    /// - Duration greater than or equal to 18 days: Full monthly charge
    /// - Duration less than 18 days: Pro-rata daily (fixed_tariff * 12 / 365)
    /// </remarks>
    bool IsLeaseProRated(DateTime contractStartDate);

    /// <summary>
    /// Calculate excess kilometer charges for lease vehicles.
    /// </summary>
    /// <param name="leaseTariff">Lease tariff record</param>
    /// <param name="actualKilometers">Actual kilometers driven</param>
    /// <param name="contractedKilometers">Contracted kilometer limit</param>
    /// <returns>Excess charge amount</returns>
    decimal CalculateLeaseExcessCharge(
        LeaseTariff leaseTariff,
        int actualKilometers,
        int contractedKilometers);

    #endregion

    #region Fuel Tariff

    /// <summary>
    /// Get fuel tariff (price per liter) for a specific fuel type and date.
    /// </summary>
    /// <param name="fuelTypeCode">Fuel type identifier</param>
    /// <param name="effectiveDate">Date tariff should be effective for</param>
    /// <returns>Fuel price per liter</returns>
    Task<decimal> GetFuelTariffAsync(int fuelTypeCode, DateTime effectiveDate);

    #endregion

    #region Special Business Rules

    /// <summary>
    /// Check if department/site is GGMT internal (should not be billed).
    /// </summary>
    /// <param name="siteCode">Site identifier</param>
    /// <param name="departmentCode">Department identifier</param>
    /// <returns>True if internal (0.00 tariff), false otherwise</returns>
    /// <remarks>
    /// GGMT Internal rules:
    /// - Site codes 1621, 1622 (GGMT hire pools): 0.00 tariff
    /// - Department 147 (GGMT itself): 0.00 tariff
    /// </remarks>
    bool IsGGMTInternal(short siteCode, int departmentCode);

    /// <summary>
    /// Check if vehicle is in "Missing" status (should not be billed).
    /// </summary>
    /// <param name="vehicleStatusCode">Vehicle status identifier</param>
    /// <returns>True if missing (0.00 tariff), false otherwise</returns>
    /// <remarks>
    /// Vehicle status 11 = Missing, should return 0.00 tariff.
    /// </remarks>
    bool IsVehicleMissing(int vehicleStatusCode);

    /// <summary>
    /// Determine which tariff system to use (legacy vs modern) based on vehicle and date.
    /// </summary>
    /// <param name="yearManufactured">Year vehicle was manufactured</param>
    /// <param name="contractStartDate">Contract start date</param>
    /// <returns>Tariff system to use</returns>
    /// <remarks>
    /// Rules:
    /// - Vehicles manufactured 2008+ starting after 2009-04-01: Modern (fin.vehicle_tariff)
    /// - Third-party rentals: Always modern
    /// - All others: Legacy (tariff table)
    /// </remarks>
    TariffSystem DetermineTariffSystem(int yearManufactured, DateTime contractStartDate);

    #endregion

    #region Batch Tariff Calculations

    /// <summary>
    /// Calculate tariffs for multiple contracts in batch for efficiency.
    /// Used for bulk billing operations and monthly invoicing.
    /// </summary>
    /// <param name="requests">List of tariff calculation requests</param>
    /// <returns>List of tariff calculation results</returns>
    Task<List<BatchTariffResult>> CalculateBatchTariffsAsync(List<BatchTariffRequest> requests);

    /// <summary>
    /// Calculate monthly billing amounts for a range of contracts.
    /// Includes fixed charges, kilometer charges, and special adjustments.
    /// </summary>
    /// <param name="siteCode">Site code to bill</param>
    /// <param name="departmentCode">Department code to bill</param>
    /// <param name="billingPeriodStart">Start of billing period</param>
    /// <param name="billingPeriodEnd">End of billing period</param>
    /// <returns>Complete monthly billing result</returns>
    Task<MonthlyBillingResult> CalculateMonthlyBillingAsync(
        int siteCode,
        int departmentCode,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd);

    #endregion

    #region Financial Reconciliation

    /// <summary>
    /// Validate tariff calculations against financial records for reconciliation.
    /// Used for monthly financial close and audit purposes.
    /// </summary>
    /// <param name="periodStart">Start of reconciliation period</param>
    /// <param name="periodEnd">End of reconciliation period</param>
    /// <param name="siteCode">Optional site code filter</param>
    /// <param name="departmentCode">Optional department code filter</param>
    /// <returns>Reconciliation result with discrepancies</returns>
    Task<FinancialReconciliation> ReconcileTariffsAsync(
        DateTime periodStart,
        DateTime periodEnd,
        int? siteCode = null,
        int? departmentCode = null);

    #endregion

    #region Validation

    /// <summary>
    /// Validate that a vehicle tariff has all required components.
    /// </summary>
    /// <param name="vehicleTariff">Vehicle tariff to validate</param>
    /// <returns>Validation result with status and messages</returns>
    /// <remarks>
    /// Returns status -3 if overhead or maintenance components are NULL (incomplete tariff).
    /// </remarks>
    TariffValidationResult ValidateTariff(VehicleTariff vehicleTariff);

    #endregion
}

#region Supporting Types

/// <summary>
/// Result of tariff calculation with status and amount.
/// </summary>
public class TariffResult
{
    /// <summary>
    /// Calculated tariff amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Status of tariff calculation.
    /// </summary>
    public TariffStatus Status { get; set; }

    /// <summary>
    /// Human-readable message explaining result.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Source of tariff (which system was used).
    /// </summary>
    public TariffSource Source { get; set; }

    /// <summary>
    /// Create a successful tariff result.
    /// </summary>
    public static TariffResult Success(decimal amount, TariffSource source)
    {
        return new TariffResult
        {
            Amount = amount,
            Status = TariffStatus.Valid,
            Source = source,
            Message = $"Tariff calculated successfully from {source} system"
        };
    }

    /// <summary>
    /// Create an error tariff result.
    /// </summary>
    public static TariffResult Error(TariffStatus status, string message)
    {
        return new TariffResult
        {
            Amount = status == TariffStatus.Valid ? 0m : (decimal)status,
            Status = status,
            Source = TariffSource.None,
            Message = message
        };
    }
}

/// <summary>
/// Status codes matching legacy GetVehicleTariff return values.
/// </summary>
public enum TariffStatus
{
    /// <summary>
    /// Valid tariff amount (> 0 or = 0 with valid reason).
    /// </summary>
    Valid = 0,

    /// <summary>
    /// Year manufactured not found (returns -1).
    /// </summary>
    YearNotFound = -1,

    /// <summary>
    /// No matching tariff for class/year/date combination (returns -2).
    /// </summary>
    NoMatch = -2,

    /// <summary>
    /// Incomplete tariff - missing overhead or maintenance components (returns -3).
    /// </summary>
    Incomplete = -3
}

/// <summary>
/// Source of tariff calculation.
/// </summary>
public enum TariffSource
{
    /// <summary>
    /// No tariff found.
    /// </summary>
    None,

    /// <summary>
    /// Legacy tariff table (pre-2009).
    /// </summary>
    Legacy,

    /// <summary>
    /// Modern fin.vehicle_tariff table (post-2009).
    /// </summary>
    Modern,

    /// <summary>
    /// LeaseTariff table (lease-specific rates).
    /// </summary>
    Lease,

    /// <summary>
    /// Special business rule (0.00 tariff for GGMT internal, relief vehicles, etc.).
    /// </summary>
    SpecialRule
}

/// <summary>
/// Type of tariff requested.
/// </summary>
public enum TariffType
{
    /// <summary>
    /// Fixed tariff (daily/monthly/hourly charges).
    /// </summary>
    Fixed,

    /// <summary>
    /// Kilometer tariff (per-kilometer charges).
    /// </summary>
    Kilos
}

/// <summary>
/// Tariff system to use for calculation.
/// </summary>
public enum TariffSystem
{
    /// <summary>
    /// Legacy tariff table (pre-2009).
    /// </summary>
    Legacy,

    /// <summary>
    /// Modern fin.vehicle_tariff table (post-2009).
    /// </summary>
    Modern,

    /// <summary>
    /// LeaseTariff table (lease-specific).
    /// </summary>
    Lease
}

/// <summary>
/// Validation result for tariff completeness.
/// </summary>
public class TariffValidationResult
{
    /// <summary>
    /// Is tariff valid and complete?
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Status code.
    /// </summary>
    public TariffStatus Status { get; set; }

    /// <summary>
    /// Validation messages.
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Create a valid result.
    /// </summary>
    public static TariffValidationResult Valid()
    {
        return new TariffValidationResult
        {
            IsValid = true,
            Status = TariffStatus.Valid,
            Messages = new List<string> { "Tariff is complete and valid" }
        };
    }

    /// <summary>
    /// Create an invalid result.
    /// </summary>
    public static TariffValidationResult Invalid(TariffStatus status, params string[] messages)
    {
        return new TariffValidationResult
        {
            IsValid = false,
            Status = status,
            Messages = messages.ToList()
        };
    }
}

#endregion
