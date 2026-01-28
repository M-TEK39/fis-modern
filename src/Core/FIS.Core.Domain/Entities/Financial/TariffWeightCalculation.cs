using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Tariff weight calculation for overhead distribution (fin schema).
/// Distributes overhead across vehicle categories using weighted factors.
/// Maps to legacy fin.TariffWeightCalculation table.
/// </summary>
[Table("TariffWeightCalculation", Schema = "fin")]
public class TariffWeightCalculation
{
    [Key]
    [Column("TariffWeightCalculation_Code")]
    public int TariffWeightCalculation_Code { get; set; }

    [Column("TariffParameterID")]
    public int TariffParameterID { get; set; }

    [Column("category")]
    public decimal category { get; set; }

    [Column("number")]
    public int? number { get; set; }

    [Column("WeightFactorPerUnit")]
    public double? WeightFactorPerUnit { get; set; }

    [Column("calculation_date_time")]
    public DateTime? calculation_date_time { get; set; }

    // Navigation properties
    public virtual TariffParameter? TariffParameter { get; set; }

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
