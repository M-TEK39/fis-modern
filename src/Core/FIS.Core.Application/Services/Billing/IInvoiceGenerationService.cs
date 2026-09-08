using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Services.Billing;

/// <summary>
/// Service for generating invoices and financial reports.
/// Replicates legacy invoice generation stored procedures (DEV_REP_StatementDetailedInvoice, etc.).
/// </summary>
public interface IInvoiceGenerationService
{
    #region Invoice Creation

    /// <summary>
    /// Generate monthly invoice for a department.
    /// Creates invoice header with summary totals.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch/invoice date (typically month-end)</param>
    /// <returns>Created invoice</returns>
    Task<Invoice> GenerateMonthlyInvoiceAsync(int departmentCode, DateTime batchDate);

    /// <summary>
    /// Generate invoice items (line items) for an invoice.
    /// Creates detailed breakdown of fixed and kilometer charges.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <param name="batchDate">Batch date for filtering transactions</param>
    /// <returns>List of created invoice items</returns>
    Task<List<InvoiceItem>> GenerateInvoiceItemsAsync(int invoiceCode, DateTime batchDate);

    /// <summary>
    /// Generate invoice with items in single operation.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch/invoice date</param>
    /// <returns>Complete invoice with items</returns>
    Task<InvoiceWithItems> GenerateCompleteInvoiceAsync(int departmentCode, DateTime batchDate);

    /// <summary>
    /// Generate invoices for all departments for a month.
    /// Batch operation for month-end processing.
    /// </summary>
    /// <param name="batchDate">Month-end date</param>
    /// <returns>List of generated invoices</returns>
    Task<List<Invoice>> GenerateAllDepartmentInvoicesAsync(DateTime batchDate);

    #endregion

    #region Detailed Invoice Reports

    /// <summary>
    /// Get detailed invoice report for a department and batch date.
    /// Replicates DEV_REP_StatementDetailedInvoice stored procedure.
    /// Shows fixed charges and kilometer details with odometer readings.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>Detailed invoice report</returns>
    /// <remarks>
    /// Report includes:
    /// - Kilometer details from route_details with odometer readings
    /// - Fixed charges from contract_bas_records
    /// - Original odometer readings if modified (from audit_trips)
    /// - Tariff rates and calculated amounts
    /// - Excludes: Suspense (type 5), Revenue (type 6), renumbered journals
    /// </remarks>
    Task<StatementDetailedInvoiceReport> GetDetailedInvoiceAsync(
        int departmentCode,
        DateTime batchDate
    );

    /// <summary>
    /// Get vehicle billing history report.
    /// Replicates DEV_REP_VehicleBillingHistory stored procedure.
    /// Shows all charges for a specific vehicle in a financial year.
    /// </summary>
    /// <param name="vmfCode">Vehicle identifier</param>
    /// <param name="financialYear">Financial year (e.g., "2024")</param>
    /// <returns>Vehicle billing history report</returns>
    Task<VehicleBillingHistoryReport> GetVehicleBillingHistoryAsync(
        int vmfCode,
        string financialYear
    );

    /// <summary>
    /// Get fuel detailed invoice report.
    /// Replicates DEV_REP_FuelDetailedInvoicedReport stored procedure.
    /// Shows fuel consumption billing with card details.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="startDate">Period start date</param>
    /// <param name="endDate">Period end date</param>
    /// <returns>Fuel invoice report</returns>
    Task<FuelDetailedInvoiceReport> GetFuelDetailedInvoiceAsync(
        int departmentCode,
        DateTime startDate,
        DateTime endDate
    );

    #endregion

    #region Summary Reports

    /// <summary>
    /// Get invoiced amounts by department for a date range.
    /// Replicates DEV_REP_AllInvoicedAmountsPerDepartment stored procedure.
    /// </summary>
    /// <param name="startDate">Period start date</param>
    /// <param name="endDate">Period end date</param>
    /// <returns>List of department invoice summaries</returns>
    Task<List<DepartmentInvoiceSummary>> GetInvoicedAmountsByDepartmentAsync(
        DateTime startDate,
        DateTime endDate
    );

    /// <summary>
    /// Get invoiced amounts by site for a date range.
    /// </summary>
    /// <param name="siteCode">Site identifier (null = all sites)</param>
    /// <param name="startDate">Period start date</param>
    /// <param name="endDate">Period end date</param>
    /// <returns>List of site invoice summaries</returns>
    Task<List<SiteInvoiceSummary>> GetInvoicedAmountsBySiteAsync(
        int? siteCode,
        DateTime startDate,
        DateTime endDate
    );

    /// <summary>
    /// Get invoiced amounts by vehicle for a department.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="financialYear">Financial year (e.g., "2024")</param>
    /// <returns>List of vehicle invoice summaries</returns>
    Task<List<VehicleInvoiceSummary>> GetInvoicedAmountsByVehicleAsync(
        int departmentCode,
        string financialYear
    );

    /// <summary>
    /// Get invoiced amounts by journal detail type.
    /// Shows breakdown by Fixed, Kilos, Fuel, etc.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="startDate">Period start date</param>
    /// <param name="endDate">Period end date</param>
    /// <returns>List of invoice summaries by type</returns>
    Task<List<TypeInvoiceSummary>> GetInvoicedAmountsByTypeAsync(
        int departmentCode,
        DateTime startDate,
        DateTime endDate
    );

    #endregion

    #region Invoice Item Details

    /// <summary>
    /// Get fixed charge items for an invoice.
    /// Includes contract-based daily/monthly/hourly charges.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>List of fixed charge items</returns>
    Task<List<FixedChargeItem>> GetFixedChargeItemsAsync(int departmentCode, DateTime batchDate);

    /// <summary>
    /// Get kilometer charge items for an invoice.
    /// Includes trip-based distance charges with odometer readings.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>List of kilometer charge items</returns>
    Task<List<KilometerChargeItem>> GetKilometerChargeItemsAsync(
        int departmentCode,
        DateTime batchDate
    );

    /// <summary>
    /// Get fuel charge items for an invoice.
    /// Includes fuel card purchases.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>List of fuel charge items</returns>
    Task<List<FuelChargeItem>> GetFuelChargeItemsAsync(int departmentCode, DateTime batchDate);

    #endregion

    #region Invoice Status and Workflow

    /// <summary>
    /// Get invoice by identifier.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <returns>Invoice or null if not found</returns>
    Task<Invoice?> GetInvoiceAsync(int invoiceCode);

    /// <summary>
    /// Get invoices for a department.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="financialYear">Financial year filter (null = all years)</param>
    /// <returns>List of invoices</returns>
    Task<List<Invoice>> GetDepartmentInvoicesAsync(
        int departmentCode,
        string? financialYear = null
    );

    /// <summary>
    /// Mark invoice as sent to department.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <param name="sentDate">Date invoice was sent</param>
    Task MarkInvoiceSentAsync(int invoiceCode, DateTime sentDate);

    /// <summary>
    /// Mark invoice as paid.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <param name="paidDate">Date invoice was paid</param>
    /// <param name="paymentReference">Payment reference number</param>
    Task MarkInvoicePaidAsync(int invoiceCode, DateTime paidDate, string? paymentReference = null);

    /// <summary>
    /// Cancel/void an invoice.
    /// </summary>
    /// <param name="invoiceCode">Invoice identifier</param>
    /// <param name="reason">Cancellation reason</param>
    Task CancelInvoiceAsync(int invoiceCode, string reason);

    #endregion

    #region Invoice Calculations

    /// <summary>
    /// Calculate total invoice amount from journal details.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>Total invoice amount</returns>
    Task<decimal> CalculateInvoiceTotalAsync(int departmentCode, DateTime batchDate);

    /// <summary>
    /// Calculate invoice totals by type (Fixed, Kilos, Fuel).
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>Totals breakdown by type</returns>
    Task<InvoiceTotalsByType> CalculateInvoiceTotalsByTypeAsync(
        int departmentCode,
        DateTime batchDate
    );

    /// <summary>
    /// Calculate invoice item cost components.
    /// Includes replacement, overhead, profit provisions, etc.
    /// </summary>
    /// <param name="journalDetailCode">Journal detail identifier</param>
    /// <param name="contractCode">Contract identifier</param>
    /// <returns>Cost component breakdown</returns>
    Task<InvoiceItemCostComponents> CalculateCostComponentsAsync(
        Guid journalDetailCode,
        int contractCode
    );

    #endregion

    #region Validation

    /// <summary>
    /// Validate that invoice can be generated for a department/date.
    /// Checks for posted journal details, no conflicts, etc.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>Validation result</returns>
    Task<InvoiceValidationResult> ValidateInvoiceGenerationAsync(
        int departmentCode,
        DateTime batchDate
    );

    /// <summary>
    /// Get unposted journal details that will prevent invoice generation.
    /// </summary>
    /// <param name="departmentCode">Department identifier</param>
    /// <param name="batchDate">Batch date</param>
    /// <returns>List of unposted journal details</returns>
    Task<List<JournalDetail>> GetUnpostedTransactionsAsync(int departmentCode, DateTime batchDate);

    #endregion
}

#region Supporting Types

/// <summary>
/// Invoice with all related items.
/// </summary>
public class InvoiceWithItems
{
    public Invoice Invoice { get; set; } = null!;
    public List<InvoiceItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalItems { get; set; }
}

/// <summary>
/// Detailed invoice report (replicates DEV_REP_StatementDetailedInvoice).
/// </summary>
public class StatementDetailedInvoiceReport
{
    public int DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime BatchDate { get; set; }
    public string FinancialYear { get; set; } = string.Empty;

    public List<KilometerDetailLine> KilometerDetails { get; set; } = new();
    public List<FixedDetailLine> FixedDetails { get; set; } = new();

    public decimal TotalKilometerCharges { get; set; }
    public decimal TotalFixedCharges { get; set; }
    public decimal GrandTotal { get; set; }
}

/// <summary>
/// Kilometer detail line in invoice report.
/// </summary>
public class KilometerDetailLine
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime TripDate { get; set; }
    public int StartOdometer { get; set; }
    public int EndOdometer { get; set; }
    public int Kilometers { get; set; }
    public decimal Tariff { get; set; }
    public decimal Amount { get; set; }
    public bool OdometerModified { get; set; }
    public int? OriginalStartOdometer { get; set; }
    public int? OriginalEndOdometer { get; set; }
}

/// <summary>
/// Fixed charge detail line in invoice report.
/// </summary>
public class FixedDetailLine
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty; // "Daily", "Monthly", "Hourly"
    public decimal Tariff { get; set; }
    public decimal Amount { get; set; }
    public string ContractType { get; set; } = string.Empty;
}

/// <summary>
/// Vehicle billing history report (replicates DEV_REP_VehicleBillingHistory).
/// </summary>
public class VehicleBillingHistoryReport
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public string FinancialYear { get; set; } = string.Empty;

    public List<BillingHistoryLine> Transactions { get; set; } = new();

    public decimal TotalFixed { get; set; }
    public decimal TotalKilometers { get; set; }
    public decimal TotalFuel { get; set; }
    public decimal GrandTotal { get; set; }
}

/// <summary>
/// Billing history transaction line.
/// </summary>
public class BillingHistoryLine
{
    public DateTime TransactionDate { get; set; }
    public string Type { get; set; } = string.Empty; // "Fixed", "Kilos", "Fuel"
    public int DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Tariff { get; set; }
    public decimal Amount { get; set; }
    public Guid JournalDetailCode { get; set; }
}

/// <summary>
/// Fuel detailed invoice report (replicates DEV_REP_FuelDetailedInvoicedReport).
/// </summary>
public class FuelDetailedInvoiceReport
{
    public int DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public List<FuelTransactionLine> Transactions { get; set; } = new();

    public decimal TotalLiters { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Fuel transaction line in invoice report.
/// </summary>
public class FuelTransactionLine
{
    public DateTime PurchaseDate { get; set; }
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? FuelCardNumber { get; set; }
    public string FuelType { get; set; } = string.Empty;
    public decimal Liters { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal Amount { get; set; }
    public int Odometer { get; set; }
}

/// <summary>
/// Department invoice summary.
/// </summary>
public class DepartmentInvoiceSummary
{
    public int DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public decimal TotalFixed { get; set; }
    public decimal TotalKilometers { get; set; }
    public decimal TotalFuel { get; set; }
    public decimal TotalOther { get; set; }
    public decimal GrandTotal { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Site invoice summary.
/// </summary>
public class SiteInvoiceSummary
{
    public int SiteCode { get; set; }
    public string? SiteName { get; set; }
    public decimal TotalAmount { get; set; }
    public int DepartmentCount { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Vehicle invoice summary.
/// </summary>
public class VehicleInvoiceSummary
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public decimal TotalFixed { get; set; }
    public decimal TotalKilometers { get; set; }
    public decimal TotalFuel { get; set; }
    public decimal GrandTotal { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Invoice summary by journal detail type.
/// </summary>
public class TypeInvoiceSummary
{
    public int JournalDetailTypeCode { get; set; }
    public string TypeName { get; set; } = string.Empty; // "Fixed", "Kilos", "Fuel"
    public decimal TotalAmount { get; set; }
    public decimal TotalQuantity { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Fixed charge invoice item.
/// </summary>
public class FixedChargeItem
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public int ContractCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal Tariff { get; set; }
    public decimal Amount { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public Guid JournalDetailCode { get; set; }
}

/// <summary>
/// Kilometer charge invoice item.
/// </summary>
public class KilometerChargeItem
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public int TripAuthorityCode { get; set; }
    public DateTime TripDate { get; set; }
    public int StartOdometer { get; set; }
    public int EndOdometer { get; set; }
    public int Kilometers { get; set; }
    public decimal Tariff { get; set; }
    public decimal Amount { get; set; }
    public Guid JournalDetailCode { get; set; }
}

/// <summary>
/// Fuel charge invoice item.
/// </summary>
public class FuelChargeItem
{
    public int VmfCode { get; set; }
    public string? RegistrationNumber { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? FuelCardNumber { get; set; }
    public decimal Liters { get; set; }
    public decimal PricePerLiter { get; set; }
    public decimal Amount { get; set; }
    public Guid JournalDetailCode { get; set; }
}

/// <summary>
/// Invoice totals breakdown by type.
/// </summary>
public class InvoiceTotalsByType
{
    public decimal FixedCharges { get; set; }
    public decimal KilometerCharges { get; set; }
    public decimal FuelCharges { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal TotalCharges { get; set; }
}

/// <summary>
/// Invoice item cost component breakdown.
/// </summary>
public class InvoiceItemCostComponents
{
    public decimal CostReplacement { get; set; }
    public decimal ProvisionOverhead { get; set; }
    public decimal CostOverhead { get; set; }
    public decimal ProvisionLoss { get; set; }
    public decimal ProvisionAccident { get; set; }
    public decimal ProvisionProfit { get; set; }
    public decimal ProvisionReplacement { get; set; }
    public decimal VariableCost { get; set; }
    public decimal TotalCost { get; set; }
}

/// <summary>
/// Validation result for invoice generation.
/// </summary>
public class InvoiceValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static InvoiceValidationResult Valid()
    {
        return new InvoiceValidationResult { IsValid = true };
    }

    public static InvoiceValidationResult Invalid(params string[] errors)
    {
        return new InvoiceValidationResult { IsValid = false, Errors = errors.ToList() };
    }
}

#endregion
