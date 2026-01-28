using FIS.Core.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Result model for financial reconciliation operations.
/// Used to identify discrepancies between calculated and recorded tariffs.
/// </summary>
public class FinancialReconciliation
{
    /// <summary>
    /// Start of the reconciliation period
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End of the reconciliation period
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Optional site code filter
    /// </summary>
    public int? SiteCode { get; set; }

    /// <summary>
    /// Optional department code filter
    /// </summary>
    public int? DepartmentCode { get; set; }

    /// <summary>
    /// List of discovered discrepancies
    /// </summary>
    public List<TariffDiscrepancy> Discrepancies { get; set; } = new List<TariffDiscrepancy>();

    /// <summary>
    /// Total number of discrepancies found
    /// </summary>
    public int TotalDiscrepancies { get; set; }

    /// <summary>
    /// Total absolute difference across all discrepancies
    /// </summary>
    public decimal TotalDifference { get; set; }

    /// <summary>
    /// Whether any discrepancies require manual review
    /// </summary>
    public bool RequiresAction { get; set; }

    /// <summary>
    /// When this reconciliation was performed
    /// </summary>
    public DateTime ReconciliationDate { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("created_by_user_code")]
    public int? created_by_user_code { get; set; }

    [Column("modified_by_user_code")]
    public int? modified_by_user_code { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

/// <summary>
/// Represents a discrepancy between calculated and recorded tariff amounts.
/// </summary>
public class TariffDiscrepancy
{
    /// <summary>
    /// Contract code with discrepancy
    /// </summary>
    public int ContractCode { get; set; }

    /// <summary>
    /// Vehicle code
    /// </summary>
    public int VmfCode { get; set; }

    /// <summary>
    /// Amount calculated by tariff engine
    /// </summary>
    public decimal CalculatedAmount { get; set; }

    /// <summary>
    /// Amount recorded in financial system
    /// </summary>
    public decimal RecordedAmount { get; set; }

    /// <summary>
    /// Difference (calculated - recorded)
    /// </summary>
    public decimal Difference { get; set; }

    /// <summary>
    /// Source of the calculated tariff
    /// </summary>
    public TariffSource TariffSource { get; set; }

    /// <summary>
    /// Description of the discrepancy
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this discrepancy requires manual review
    /// </summary>
    public bool RequiresReview { get; set; }

    /// <summary>
    /// Optional resolution notes
    /// </summary>
    public string? ResolutionNotes { get; set; }
}

/// <summary>
/// Represents a financial entry for reconciliation comparison.
/// </summary>
public class FinancialEntry
{
    /// <summary>
    /// Entry date
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Contract code
    /// </summary>
    public int ContractCode { get; set; }

    /// <summary>
    /// Entry amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Entry type or description
    /// </summary>
    public string? EntryType { get; set; }

    /// <summary>
    /// Reference number
    /// </summary>
    public string? Reference { get; set; }
}