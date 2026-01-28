using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Lease vehicle tariff rates.
/// Contains lease-specific monthly rates and excess kilometer charges.
/// Maps to legacy dbo.LeaseTariff table.
/// </summary>
[Table("LeaseTariff")]
public class LeaseTariff
{
    [Key]
    [Column("lease_tariff_code")]
    public int lease_tariff_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("end_date")]
    public DateTime end_date { get; set; }

    [Column("fixed_tariff")]
    public decimal fixed_tariff { get; set; }

    [Column("active")]
    public bool active { get; set; }

    [Column("excess_kilo_tariff")]
    public decimal? excess_kilo_tariff { get; set; }

    [Column("date_created")]
    public DateTime? date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    

    

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
