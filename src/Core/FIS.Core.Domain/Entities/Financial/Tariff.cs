using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Legacy tariff table (pre-2009 tariff system).
/// Contains tariff rates by vehicle class and year manufactured.
/// Maps to legacy dbo.tariff table.
/// </summary>
[Table("tariff")]
public class Tariff
{
    [Key]
    [Column("tariff_code")]
    public int tariff_code { get; set; }

    [Column("class_code")]
    public short class_code { get; set; }

    [Column("year_manufactured")]
    public short? year_manufactured { get; set; }

    // Fixed rate components
    [Column("monthly_fixed_amount")]
    public decimal monthly_fixed_amount { get; set; }

    [Column("monthly_odo_amount")]
    public decimal monthly_odo_amount { get; set; }

    [Column("daily_fixed_amount")]
    public decimal? daily_fixed_amount { get; set; }

    [Column("hourly_fixed_amount")]
    public decimal? hourly_fixed_amount { get; set; }

    // Effective date range
    [Column("effective_start_date")]
    public DateTime effective_start_date { get; set; }

    [Column("effective_end_date")]
    public DateTime? effective_end_date { get; set; }

    // Provision percentages
    [Column("replacement_percent")]
    public short? replacement_percent { get; set; }

    [Column("loss_percent")]
    public short? loss_percent { get; set; }

    [Column("profit_percent")]
    public short? profit_percent { get; set; }

    [Column("overhead_percent")]
    public short? overhead_percent { get; set; }

    [Column("accident_percent")]
    public short? accident_percent { get; set; }

    // Fuel component
    [Column("fuel_kilo_tariff")]
    public decimal? fuel_kilo_tariff { get; set; }

    // Audit fields
    [Column("date_created")]
    public DateTime? date_created { get; set; }

    [Column("created_by")]
    public int? created_by { get; set; }

    [Column("date_modified")]
    public DateTime? date_modified { get; set; }

    [Column("modified_by")]
    public int? modified_by { get; set; }
}
