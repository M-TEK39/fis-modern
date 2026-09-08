using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Services.Billing;

/// <summary>
/// Service for managing modern tariff system parameters (fin. schema, post-2009).
/// Handles tariff parameter configuration, maintenance values, overhead, and vehicle tariff calculations.
/// </summary>
public interface ITariffParameterService
{
    #region Tariff Parameter Management

    /// <summary>
    /// Get tariff parameters for a specific fiscal year.
    /// </summary>
    /// <param name="year">Fiscal year (e.g., 2024)</param>
    /// <returns>Tariff parameters or null if not found</returns>
    Task<TariffParameter?> GetTariffParameterAsync(int year);

    /// <summary>
    /// Get the current active tariff parameters.
    /// </summary>
    /// <returns>Current tariff parameters or null if none active</returns>
    Task<TariffParameter?> GetCurrentTariffParameterAsync();

    /// <summary>
    /// Create new tariff parameters for a fiscal year.
    /// </summary>
    /// <param name="dto">Tariff parameter data</param>
    /// <returns>Created tariff parameter</returns>
    Task<TariffParameter> CreateTariffParameterAsync(TariffParameterDto dto);

    /// <summary>
    /// Update existing tariff parameters.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="dto">Updated tariff parameter data</param>
    /// <returns>Updated tariff parameter</returns>
    Task<TariffParameter> UpdateTariffParameterAsync(int tariffParameterId, TariffParameterDto dto);

    /// <summary>
    /// Approve tariff parameters for use.
    /// Once approved, tariff parameters are locked and vehicle tariffs can be calculated.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="userId">User approving the parameters</param>
    /// <returns>True if approved successfully</returns>
    Task<bool> ApproveTariffParameterAsync(int tariffParameterId, int userId);

    /// <summary>
    /// Get all tariff parameters (for history/comparison).
    /// </summary>
    /// <returns>List of all tariff parameters</returns>
    Task<List<TariffParameter>> GetAllTariffParametersAsync();

    #endregion

    #region Maintenance Value Management

    /// <summary>
    /// Get maintenance values (cost matrix) for a tariff parameter year.
    /// Returns cost per kilometer by vehicle class and age.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of maintenance values</returns>
    Task<List<MaintenanceValue>> GetMaintenanceValuesAsync(int tariffParameterId);

    /// <summary>
    /// Get maintenance value for specific vehicle class and age.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="classCode">Vehicle class code</param>
    /// <param name="monthsAge">Vehicle age in months</param>
    /// <param name="kilometerAge">Vehicle age in kilometers</param>
    /// <returns>Applicable maintenance value or null</returns>
    Task<MaintenanceValue?> GetMaintenanceValueAsync(
        int tariffParameterId,
        int classCode,
        int monthsAge,
        int kilometerAge
    );

    /// <summary>
    /// Create or update maintenance value.
    /// </summary>
    /// <param name="dto">Maintenance value data</param>
    /// <returns>Created or updated maintenance value</returns>
    Task<MaintenanceValue> SaveMaintenanceValueAsync(MaintenanceValueDto dto);

    /// <summary>
    /// Delete maintenance value.
    /// </summary>
    /// <param name="maintenanceValueId">Maintenance value identifier</param>
    Task DeleteMaintenanceValueAsync(int maintenanceValueId);

    /// <summary>
    /// Calculate maintenance cost per kilometer for a specific vehicle.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Maintenance cost per kilometer</returns>
    Task<decimal> CalculateMaintenanceCostPerKilometerAsync(int vmfCode, int tariffParameterId);

    #endregion

    #region Overhead Management

    /// <summary>
    /// Get overhead costs for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of overhead costs</returns>
    Task<List<Overhead>> GetOverheadsAsync(int tariffParameterId);

    /// <summary>
    /// Get overhead by type.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="overheadTypeId">Overhead type identifier</param>
    /// <returns>Overhead or null if not found</returns>
    Task<Overhead?> GetOverheadAsync(int tariffParameterId, int overheadTypeId);

    /// <summary>
    /// Create or update overhead cost.
    /// </summary>
    /// <param name="dto">Overhead data</param>
    /// <returns>Created or updated overhead</returns>
    Task<Overhead> SaveOverheadAsync(OverheadDto dto);

    /// <summary>
    /// Delete overhead cost.
    /// </summary>
    /// <param name="overheadId">Overhead identifier</param>
    Task DeleteOverheadAsync(int overheadId);

    /// <summary>
    /// Calculate total overhead for a tariff parameter year.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Total overhead amount</returns>
    Task<decimal> GetTotalOverheadAsync(int tariffParameterId);

    /// <summary>
    /// Calculate average overhead tariffs by category.
    /// Distributes overhead across vehicle categories using weight calculations.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of average overhead tariffs by category</returns>
    Task<List<OverheadAverageTariff>> CalculateAverageOverheadTariffsAsync(int tariffParameterId);

    #endregion

    #region Weight Calculation Management

    /// <summary>
    /// Get weight calculations for overhead distribution.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>List of weight calculations</returns>
    Task<List<TariffWeightCalculation>> GetWeightCalculationsAsync(int tariffParameterId);

    /// <summary>
    /// Create or update weight calculation.
    /// </summary>
    /// <param name="dto">Weight calculation data</param>
    /// <returns>Created or updated weight calculation</returns>
    Task<TariffWeightCalculation> SaveWeightCalculationAsync(TariffWeightCalculationDto dto);

    /// <summary>
    /// Delete weight calculation.
    /// </summary>
    /// <param name="weightCalculationId">Weight calculation identifier</param>
    Task DeleteWeightCalculationAsync(int weightCalculationId);

    /// <summary>
    /// Recalculate all weights based on current vehicle distribution.
    /// Updates vehicle counts and weight factors.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    Task RecalculateWeightsAsync(int tariffParameterId);

    /// <summary>
    /// Calculate total weight for overhead distribution.
    /// Formula: Sum(number * WeightFactorPerUnit) across all categories.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Total weighted units</returns>
    Task<decimal> CalculateTotalWeightAsync(int tariffParameterId);

    #endregion

    #region Vehicle Tariff Calculation

    /// <summary>
    /// Calculate complete vehicle tariff for a specific vehicle and parameter year.
    /// Replicates fin.GetVehicleConfiguredTariff stored procedure logic.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>Calculated vehicle tariff with all components</returns>
    /// <remarks>
    /// Calculates:
    /// - Capital payment (depreciation)
    /// - Overhead payment (fixed + per-km)
    /// - Maintenance cost per kilometer
    /// - Total fixed tariff (monthly, daily, pool)
    /// - Total kilometer tariff
    /// </remarks>
    Task<VehicleTariff> CalculateVehicleTariffAsync(int vmfCode, int parameterYear);

    /// <summary>
    /// Recalculate tariffs for all vehicles in a parameter year.
    /// Triggered when tariff parameters are updated or approved.
    /// </summary>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>Number of vehicle tariffs calculated</returns>
    Task<int> RecalculateAllVehicleTariffsAsync(int parameterYear);

    /// <summary>
    /// Recalculate tariff for a specific vehicle.
    /// Triggered when vehicle attributes change (class, purchase amount, etc.).
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <returns>Updated vehicle tariff</returns>
    Task<VehicleTariff> RecalculateVehicleTariffAsync(int vmfCode);

    /// <summary>
    /// Get existing vehicle tariff (already calculated).
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>Vehicle tariff or null if not calculated</returns>
    Task<VehicleTariff?> GetVehicleTariffAsync(int vmfCode, int parameterYear);

    #endregion

    #region Tariff Calculation Components

    /// <summary>
    /// Calculate capital payment (depreciation component).
    /// Formula: (purchase_amount - residual_amount) / months_life
    /// </summary>
    /// <param name="purchaseAmount">Vehicle purchase amount</param>
    /// <param name="residualPercentage">Residual value percentage</param>
    /// <param name="monthsLife">Expected life in months</param>
    /// <returns>Monthly capital payment</returns>
    decimal CalculateCapitalPayment(
        decimal purchaseAmount,
        decimal residualPercentage,
        int monthsLife
    );

    /// <summary>
    /// Calculate residual amount (end-of-life value).
    /// Formula: purchase_amount * (residual_percentage / 100)
    /// </summary>
    /// <param name="purchaseAmount">Vehicle purchase amount</param>
    /// <param name="residualPercentage">Residual value percentage</param>
    /// <returns>Residual amount</returns>
    decimal CalculateResidualAmount(decimal purchaseAmount, decimal residualPercentage);

    /// <summary>
    /// Calculate overhead payment (fixed monthly component).
    /// Distributes total overhead across vehicles using weight calculations.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <param name="vehicleCategory">Vehicle category code</param>
    /// <param name="overheadUnitFactor">Vehicle's overhead weight factor</param>
    /// <returns>Monthly overhead payment</returns>
    Task<decimal> CalculateOverheadPaymentAsync(
        int tariffParameterId,
        string vehicleCategory,
        decimal overheadUnitFactor
    );

    /// <summary>
    /// Calculate overhead per kilometer.
    /// </summary>
    /// <param name="monthlyOverhead">Monthly overhead payment</param>
    /// <param name="annualRecoveredKilos">Expected annual kilometers</param>
    /// <returns>Overhead cost per kilometer</returns>
    decimal CalculateOverheadPerKilometer(decimal monthlyOverhead, int annualRecoveredKilos);

    /// <summary>
    /// Calculate effective interest rate from annual rate.
    /// Formula: (1 + annual_rate)^(1/payments_per_year) - 1
    /// </summary>
    /// <param name="annualInterestRate">Annual interest rate percentage</param>
    /// <param name="paymentsPerYear">Number of payments per year</param>
    /// <returns>Effective periodic interest rate</returns>
    decimal CalculateEffectiveInterestRate(decimal annualInterestRate, int paymentsPerYear);

    /// <summary>
    /// Calculate fixed daily tariff from monthly tariff.
    /// Formula: monthly_tariff * 12 / 365
    /// </summary>
    /// <param name="monthlyTariff">Monthly fixed tariff</param>
    /// <returns>Daily fixed tariff</returns>
    decimal CalculateDailyTariff(decimal monthlyTariff);

    /// <summary>
    /// Calculate pool vehicle daily tariff.
    /// Uses pool charged days per month from parameters.
    /// </summary>
    /// <param name="monthlyTariff">Monthly fixed tariff</param>
    /// <param name="poolChargedDaysPerMonth">Chargeable days per month</param>
    /// <returns>Pool vehicle daily tariff</returns>
    decimal CalculatePoolTariff(decimal monthlyTariff, int poolChargedDaysPerMonth);

    #endregion

    #region Validation and Reports

    /// <summary>
    /// Validate that tariff parameters are complete and can be approved.
    /// </summary>
    /// <param name="tariffParameterId">Tariff parameter identifier</param>
    /// <returns>Validation result with any errors</returns>
    Task<TariffParameterValidationResult> ValidateTariffParameterAsync(int tariffParameterId);

    /// <summary>
    /// Get tariff calculation summary report for a parameter year.
    /// Shows totals, averages, and distributions.
    /// </summary>
    /// <param name="parameterYear">Tariff parameter year</param>
    /// <returns>Summary report data</returns>
    Task<TariffCalculationSummary> GetTariffCalculationSummaryAsync(int parameterYear);

    /// <summary>
    /// Compare tariff parameters across years.
    /// Shows year-over-year changes.
    /// </summary>
    /// <param name="fromYear">From year</param>
    /// <param name="toYear">To year</param>
    /// <returns>Comparison report data</returns>
    Task<TariffParameterComparison> CompareTariffParametersAsync(int fromYear, int toYear);

    #endregion
}

#region Supporting Types

/// <summary>
/// Data transfer object for tariff parameters.
/// </summary>
public class TariffParameterDto
{
    public int TariffParameterYear { get; set; }
    public decimal AnnualInterestRatePercentage { get; set; }
    public int AnnualPayments { get; set; }
    public int PoolVehicleChargedDaysPerMonth { get; set; }
    public int AnnualRecoveredKilos { get; set; }
    public decimal AverageFuelPrice { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Data transfer object for maintenance values.
/// </summary>
public class MaintenanceValueDto
{
    public int? MaintenanceValueId { get; set; }
    public int TariffParameterId { get; set; }
    public int ClassCode { get; set; }
    public int MonthsAge { get; set; }
    public int KilometerAge { get; set; }
    public decimal Amount { get; set; }
    public decimal RandPerKilometer { get; set; }
}

/// <summary>
/// Data transfer object for overhead costs.
/// </summary>
public class OverheadDto
{
    public int? OverheadId { get; set; }
    public int TariffParameterId { get; set; }
    public int OverheadTypeId { get; set; }
    public string? OverheadDescription { get; set; }
    public decimal OverheadAmount { get; set; }
}

/// <summary>
/// Data transfer object for weight calculations.
/// </summary>
public class TariffWeightCalculationDto
{
    public int? TariffWeightCalculationId { get; set; }
    public int TariffParameterId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Number { get; set; }
    public decimal WeightFactorPerUnit { get; set; }
}

/// <summary>
/// Average overhead tariff by category.
/// </summary>
public class OverheadAverageTariff
{
    public string Category { get; set; } = string.Empty;
    public int VehicleCount { get; set; }
    public decimal WeightFactor { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal AverageOverheadPerMonth { get; set; }
    public decimal AverageOverheadPerKilometer { get; set; }
}

/// <summary>
/// Validation result for tariff parameters.
/// </summary>
public class TariffParameterValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static TariffParameterValidationResult Valid()
    {
        return new TariffParameterValidationResult { IsValid = true };
    }

    public static TariffParameterValidationResult Invalid(params string[] errors)
    {
        return new TariffParameterValidationResult { IsValid = false, Errors = errors.ToList() };
    }
}

/// <summary>
/// Summary of tariff calculations for a parameter year.
/// </summary>
public class TariffCalculationSummary
{
    public int ParameterYear { get; set; }
    public int TotalVehicles { get; set; }
    public int VehiclesWithTariffs { get; set; }
    public decimal TotalOverhead { get; set; }
    public decimal AverageMonthlyTariff { get; set; }
    public decimal AverageDailyTariff { get; set; }
    public decimal AverageKilometerTariff { get; set; }
    public decimal MinimumMonthlyTariff { get; set; }
    public decimal MaximumMonthlyTariff { get; set; }
    public List<TariffByClass> TariffsByClass { get; set; } = new();
}

/// <summary>
/// Tariff statistics by vehicle class.
/// </summary>
public class TariffByClass
{
    public int ClassCode { get; set; }
    public string? ClassName { get; set; }
    public int VehicleCount { get; set; }
    public decimal AverageMonthlyTariff { get; set; }
    public decimal AverageKilometerTariff { get; set; }
}

/// <summary>
/// Comparison of tariff parameters between years.
/// </summary>
public class TariffParameterComparison
{
    public int FromYear { get; set; }
    public int ToYear { get; set; }
    public decimal InterestRateChange { get; set; }
    public decimal AverageFuelPriceChange { get; set; }
    public decimal AverageTariffChange { get; set; }
    public decimal AverageTariffChangePercentage { get; set; }
    public List<TariffClassComparison> ClassComparisons { get; set; } = new();
}

/// <summary>
/// Comparison of tariffs by class between years.
/// </summary>
public class TariffClassComparison
{
    public int ClassCode { get; set; }
    public string? ClassName { get; set; }
    public decimal FromYearAverage { get; set; }
    public decimal ToYearAverage { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercentage { get; set; }
}

#endregion
