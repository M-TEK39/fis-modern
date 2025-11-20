using FIS.Core.Domain.Enums;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Result model for monthly billing calculations.
/// Contains all contract billing details for a specific period and site/department.
/// </summary>
public class MonthlyBillingResult
{
    /// <summary>
    /// Site code for the billing
    /// </summary>
    public int SiteCode { get; set; }

    /// <summary>
    /// Department code for the billing
    /// </summary>
    public int DepartmentCode { get; set; }

    /// <summary>
    /// Start of the billing period
    /// </summary>
    public DateTime BillingPeriodStart { get; set; }

    /// <summary>
    /// End of the billing period
    /// </summary>
    public DateTime BillingPeriodEnd { get; set; }

    /// <summary>
    /// Individual contract billing details
    /// </summary>
    public List<ContractBilling> ContractBillings { get; set; } = new List<ContractBilling>();

    /// <summary>
    /// Total fixed charges across all contracts
    /// </summary>
    public decimal TotalFixedCharges { get; set; }

    /// <summary>
    /// Total kilometer charges across all contracts
    /// </summary>
    public decimal TotalKilometerCharges { get; set; }

    /// <summary>
    /// Total excess charges across all contracts
    /// </summary>
    public decimal TotalExcessCharges { get; set; }

    /// <summary>
    /// Grand total of all charges
    /// </summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// When this billing was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional reference number for this billing run
    /// </summary>
    public string? BillingReference { get; set; }
}

/// <summary>
/// Billing details for a single contract within a monthly billing period.
/// </summary>
public class ContractBilling
{
    /// <summary>
    /// Contract code
    /// </summary>
    public int ContractCode { get; set; }

    /// <summary>
    /// Vehicle code
    /// </summary>
    public int VmfCode { get; set; }

    /// <summary>
    /// Contract type (A = Assigned, L = Lease, etc.)
    /// </summary>
    public string ContractType { get; set; } = string.Empty;

    /// <summary>
    /// Individual billing items for this contract
    /// </summary>
    public List<BillingItem> BillingItems { get; set; } = new List<BillingItem>();

    /// <summary>
    /// Total fixed charges for this contract
    /// </summary>
    public decimal TotalFixedCharges { get; set; }

    /// <summary>
    /// Total kilometer charges for this contract
    /// </summary>
    public decimal TotalKilometerCharges { get; set; }

    /// <summary>
    /// Total excess charges for this contract
    /// </summary>
    public decimal TotalExcessCharges { get; set; }

    /// <summary>
    /// Total amount for this contract
    /// </summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Individual billing item within a contract billing.
/// </summary>
public class BillingItem
{
    /// <summary>
    /// Date this charge applies to
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Description of the charge
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount of the charge
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Type of tariff this represents
    /// </summary>
    public TariffType TariffType { get; set; }

    /// <summary>
    /// Source of the tariff
    /// </summary>
    public TariffSource Source { get; set; }

    /// <summary>
    /// Optional quantity (e.g., kilometers)
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// Optional rate applied
    /// </summary>
    public decimal? Rate { get; set; }
}