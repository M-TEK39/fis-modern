using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Universal report request for flexible reporting
/// Maps to legacy RPT_Universal_Report functionality
/// </summary>
public class UniversalReportRequest
{
    public string ReportType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? VmfCode { get; set; }
    public List<string> Filters { get; set; } = new();
}

/// <summary>
/// Vehicle report model matching legacy vehicle reporting
/// </summary>
public class VehicleReport
{
    public int VmfCode { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string FleetNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public int YearManufactured { get; set; }
    public int? CurrentOdo { get; set; }
    public int CurrentOdometer { get; set; } // Separate property for compatibility
    public DateTime? TakeOnDate { get; set; }
    public DateTime? NextServiceDate { get; set; }
    public DateTime? LicenceDueDate { get; set; }
    public DateTime? CofDueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal TotalCosts { get; set; }
    public decimal MonthlyOverhead { get; set; }
    public List<string> Alerts { get; set; } = new();
    public DateTime GeneratedDate { get; set; }
}

/// <summary>
/// Master file report for all vehicles
/// Maps to legacy RPT_Master_Report functionality
/// </summary>
public class MasterFileReport
{
    public DateTime GeneratedDate { get; set; }
    public List<VehicleReport> Vehicles { get; set; } = new();
    public int TotalVehicles { get; set; }
    public decimal TotalFleetValue { get; set; }
    public int VehiclesDueService { get; set; }
    public int VehiclesDueLicence { get; set; }
    public Dictionary<string, int> VehiclesByStatus { get; set; } = new();
    public Dictionary<string, int> VehiclesByDepartment { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Universal report result with flexible data structure
/// </summary>
public class UniversalReport
{
    public string ReportType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public Dictionary<string, object> ReportData { get; set; } = new();
    public List<Dictionary<string, object>> DataRows { get; set; } = new();
    public Dictionary<string, object> Summary { get; set; } = new();

    /// <summary>
    /// True when every DataRow contains "Month" (month name) and "Year" (int)
    /// fields so the frontend can reliably filter by posting month.
    /// False for vehicle-master types (summary, detailed, maintenance) which
    /// carry no per-row posting date.
    /// </summary>
    public bool SupportsDateFilter { get; set; }
}

/// <summary>
/// Financial income summary report
/// Maps to legacy SummaryIncomeSplit functionality
/// </summary>
public class SummaryIncomeReport
{
    public int FinancialYear { get; set; }
    public DateTime GeneratedDate { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetIncome { get; set; }
    public Dictionary<string, decimal> IncomeByDepartment { get; set; } = new();
    public Dictionary<string, decimal> IncomeByMonth { get; set; } = new();
    public Dictionary<string, decimal> IncomeByVehicleType { get; set; } = new();
    public decimal BudgetedIncome { get; set; }
    public decimal VarianceAmount { get; set; }
    public decimal VariancePercentage { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Detailed income report with line items
/// </summary>
public class DetailedIncomeReport
{
    public int FinancialYear { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<IncomeItem> IncomeItems { get; set; } = new();
    public List<IncomeDetailLine> IncomeDetails { get; set; } = new();
    public Dictionary<string, decimal> Totals { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Income item for detailed reports
/// </summary>
public class IncomeItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Income detail line for detailed income reports
/// </summary>
public class IncomeDetailLine
{
    public string VmfCode { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

/// <summary>
/// Tariff list report
/// </summary>
public class TariffListReport
{
    public int FinancialYear { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<TariffItem> Tariffs { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Tariff item for tariff reports
/// </summary>
public class TariffItem
{
    public string TariffCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Vehicle billing history report
/// </summary>
public class VehicleBillingHistoryReport
{
    public int VmfCode { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int FinancialYear { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<BillingItem> BillingItems { get; set; } = new();
    public List<BillingHistoryLine> BillingHistory { get; set; } = new();
    public decimal TotalBilled { get; set; }
    public decimal AverageMonthlyBilling { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Billing item for billing reports
/// </summary>
public class BillingItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
}

/// <summary>
/// Billing history line for detailed billing reports
/// </summary>
public class BillingHistoryLine
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Kilometer gaps report
/// </summary>
public class KiloGapsReport
{
    public int FinancialYear { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<KiloGap> Gaps { get; set; } = new();
    public int TotalGaps { get; set; }
    public int VehiclesAffected { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Kilometer gap item
/// </summary>
public class KiloGap
{
    public int VmfCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int MissingDays { get; set; }
}

/// <summary>
/// Service history report
/// </summary>
public class ServiceHistoryReport
{
    public int VehicleCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public List<MaintenanceRecord> MaintenanceRecords { get; set; } = new();
    public decimal TotalCost { get; set; }
    public decimal AverageCostPerService { get; set; }
    public int TotalServices { get; set; }
    public DateTime? FirstServiceDate { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public int ServiceIntervalDays { get; set; }
    public int ServiceIntervalKm { get; set; }
    public string? PreferredServiceProvider { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<ServiceRecord> ServiceRecords { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Service record item
/// </summary>
public class ServiceRecord
{
    public DateTime ServiceDate { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public int Odometer { get; set; }
    public decimal Cost { get; set; }
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// Maintenance schedule report
/// </summary>
/// <summary>
/// Maintenance schedule report
/// </summary>
public class MaintenanceScheduleReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<ScheduledMaintenanceItem> ScheduledItems { get; set; } = new();
    public List<OverdueMaintenanceItem> OverdueItems { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Scheduled maintenance item
/// </summary>
public class ScheduledMaintenanceItem
{
    public int VmfCode { get; set; }
    public DateTime DueDate { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Overdue maintenance item
/// </summary>
public class OverdueMaintenanceItem
{
    public int VmfCode { get; set; }
    public DateTime DueDate { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public int DaysOverdue { get; set; }
}

/// <summary>
/// Maintenance cost report
/// </summary>
public class MaintenanceCostReport
{
    public int? VmfCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<MaintenanceCostItem> CostItems { get; set; } = new();
    public List<MaintenanceCostLine> CostLines { get; set; } = new(); // Separate property for detailed lines
    public Dictionary<string, decimal> CostsByType { get; set; } = new();
    public Dictionary<string, decimal> CostsByMonth { get; set; } = new();
    public decimal TotalCost { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Maintenance cost item
/// </summary>
public class MaintenanceCostItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Maintenance cost line for detailed cost reports
/// </summary>
public class MaintenanceCostLine
{
    public int VmfCode { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime ServiceDate { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string ServiceProvider { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public int Odometer { get; set; }
    public string WorkDescription { get; set; } = string.Empty;
}

/// <summary>
/// Trip summary report
/// </summary>
public class TripSummaryReport
{
    public int? VmfCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<TripSummaryItem> TripSummaries { get; set; } = new();
    public List<TripSummaryLine> TripLines { get; set; } = new(); // For detailed line summaries
    public Dictionary<string, int> TripsByDepartment { get; set; } = new();
    public decimal TotalDistance { get; set; }
    public decimal TotalKilometers { get; set; } // Separate property for assignability
    public int TotalTrips { get; set; }
    public decimal TotalRevenue { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Trip summary item
/// </summary>
public class TripSummaryItem
{
    public DateTime Date { get; set; }
    public string Destination { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public string Driver { get; set; } = string.Empty;
}

/// <summary>
/// Trip summary line for detailed trip reports
/// </summary>
public class TripSummaryLine
{
    public int TripId { get; set; }
    public int VmfCode { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int StartOdometer { get; set; }
    public int EndOdometer { get; set; }
    public int Distance { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string VehicleRegistration { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public int TripCount { get; set; }
    public decimal TotalKilometers { get; set; }
    public decimal TotalRevenue { get; set; }
    public string Department { get; set; } = string.Empty;
    public DateTime FirstTrip { get; set; }
    public DateTime LastTrip { get; set; }
}

public sealed record TripSummaryPageQuery(
    int Page = 1,
    int PageSize = 24,
    int? VmfCode = null,
    DateTime StartDate = default,
    DateTime EndDate = default,
    string? Search = null,
    string? Filter = null
);

public sealed record TripSummaryPage(
    IReadOnlyList<TripSummaryLine> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Trip detail report
/// </summary>
public class TripDetailReport
{
    public int TripId { get; set; }
    public int ContractCode { get; set; }
    public DateTime TripDate { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string ApproverRank { get; set; } = string.Empty;
    public string TripReason { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public int EndOdometer { get; set; }
    public decimal TripKilometers { get; set; }
    public decimal TripRevenue { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string AuthorityNumber { get; set; } = string.Empty;
    public List<TripExpense> Expenses { get; set; } = new();
    public DateTime GeneratedDate { get; set; }
    public TripDetailInfo TripDetails { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Trip detail information
/// </summary>
public class TripDetailInfo
{
    public int TripId { get; set; }
    public int VmfCode { get; set; }
    public DateTime TripDate { get; set; }
    public string Driver { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public int StartOdometer { get; set; }
    public int EndOdometer { get; set; }
    public int Distance { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string AuthorityNumber { get; set; } = string.Empty;
}

/// <summary>
/// Trip expense item
/// </summary>
public class TripExpense
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

/// <summary>
/// Authority report
/// </summary>
public class AuthorityReport
{
    public int ContractId { get; set; }
    public int ContractCode { get; set; }
    public string AuthorityNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int AuthorizedKilometers { get; set; }
    public int UsedKilometers { get; set; }
    public int RemainingKilometers { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; }
    public List<AuthorityDetail> AuthorityDetails { get; set; } = new();
    public List<TripSummaryLine> RelatedTrips { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Authority detail item
/// </summary>
public class AuthorityDetail
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public int AllocatedKm { get; set; }
    public int UsedKm { get; set; }
    public int RemainingKm { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Contract summary report
/// </summary>
public class ContractSummaryReport
{
    public int? ContractId { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<ContractSummaryItem> ContractSummaries { get; set; } = new();
    public List<ContractSummaryLine> Contracts { get; set; } = new();
    public int TotalContracts { get; set; }
    public int ActiveContracts { get; set; }
    public int ExpiredContracts { get; set; }
    public decimal TotalValue { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Contract summary item
/// </summary>
public class ContractSummaryItem
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public int AllocatedKm { get; set; }
}

/// <summary>
/// Contract summary line item
/// </summary>
public class ContractSummaryLine
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal ContractValue { get; set; }
    public int VehicleCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int UsedKilometers { get; set; }
    public int AuthorizedKilometers { get; set; }
}

/// <summary>
/// Contract billing report
/// </summary>
public class ContractBillingReport
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedDate { get; set; }
    public List<ContractBillingItem> BillingItems { get; set; } = new();
    public List<ContractBillingLine> BillingLines { get; set; } = new();
    public decimal TotalBilling { get; set; }
    public Dictionary<string, decimal> BillingByVehicle { get; set; } = new();
    public decimal TotalBilled { get; set; }
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Contract billing item
/// </summary>
public class ContractBillingItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Kilometers { get; set; }
}

/// <summary>
/// Contract billing line item
/// </summary>
public class ContractBillingLine
{
    public int ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime BillingDate { get; set; }
    public decimal Amount { get; set; }
    public int Kilometers { get; set; }
    public string VehicleRegistration { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Report definition for available reports
/// </summary>
public class ReportDefinition
{
    public string ReportName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool RequiresVehicleSelection { get; set; }
    public bool RequiresDateRange { get; set; }
    public bool SupportsPdfExport { get; set; }
    public bool SupportsCsvExport { get; set; }
    public bool SupportsExcelExport { get; set; }
    public List<ReportParameter> Parameters { get; set; } = new();
}

/// <summary>
/// Report parameter definition
/// </summary>
public class ReportParameter
{
    public string ParameterName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public List<string>? ValidValues { get; set; }
}
