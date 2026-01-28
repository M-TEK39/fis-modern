using FIS.Core.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Request model for batch tariff calculations.
/// Used when calculating multiple contract tariffs in a single operation.
/// </summary>
public class BatchTariffRequest
{
    /// <summary>
    /// Contract code to calculate tariff for
    /// </summary>
    public int ContractCode { get; set; }

    /// <summary>
    /// Date to calculate tariff as of
    /// </summary>
    public DateTime CheckDate { get; set; }

    /// <summary>
    /// Type of tariff to calculate (Fixed, Kilos, etc.)
    /// </summary>
    public TariffType TariffType { get; set; }

    /// <summary>
    /// Optional vehicle code (if different from contract default)
    /// </summary>
    public int? VmfCode { get; set; }

    /// <summary>
    /// Optional reference for tracking this request
    /// </summary>
    public string? Reference { get; set; }

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