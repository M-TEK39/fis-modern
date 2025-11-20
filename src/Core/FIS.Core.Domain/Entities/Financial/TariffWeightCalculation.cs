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
}
