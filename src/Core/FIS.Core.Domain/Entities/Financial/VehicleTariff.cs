using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Calculated vehicle tariff for modern tariff system (post-2009, fin schema).
/// Contains all calculated tariff components for a specific vehicle.
/// Maps to legacy fin.vehicle_tariff table.
/// </summary>
[Table("vehicle_tariff", Schema = "fin")]
public class VehicleTariff
{
    [Key]
    [Column("vehicle_tariff_code")]
    public int vehicle_tariff_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("end_date")]
    public DateTime? end_date { get; set; }

    [Column("residual_percentage")]
    public decimal residual_percentage { get; set; }

    [Column("parameter_year")]
    public short parameter_year { get; set; }

    [Column("annual_interest_percentage")]
    public decimal? annual_interest_percentage { get; set; }

    [Column("purchase_amount")]
    public decimal? purchase_amount { get; set; }

    [Column("purchase_date")]
    public DateTime? purchase_date { get; set; }

    [Column("purchase_amount_group")]
    public byte? purchase_amount_group { get; set; }

    [Column("overhead_unit_factor")]
    public double? overhead_unit_factor { get; set; }

    [Column("target_replacement_date")]
    public DateTime? target_replacement_date { get; set; }

    [Column("year_manufactured")]
    public int? year_manufactured { get; set; }

    [Column("model_code")]
    public int? model_code { get; set; }

    [Column("class_code")]
    public int? class_code { get; set; }

    [Column("kilometer_life")]
    public int? kilometer_life { get; set; }

    [Column("months_life")]
    public byte? months_life { get; set; }

    // Calculated components
    [Column("residual_amount")]
    public decimal? residual_amount { get; set; }

    [Column("capital_payment")]
    public decimal? capital_payment { get; set; }

    [Column("overhead_payment")]
    public decimal? overhead_payment { get; set; }

    [Column("adjustment_amount")]
    public decimal? adjustment_amount { get; set; }

    // Fixed tariffs
    [Column("vehicle_fixed_tariff")]
    public decimal? vehicle_fixed_tariff { get; set; }

    [Column("vehicle_fixed_daily_tariff")]
    public decimal? vehicle_fixed_daily_tariff { get; set; }

    [Column("vehicle_fixed_tariff_pool")]
    public decimal? vehicle_fixed_tariff_pool { get; set; }

    [Column("class_fixed_tariff")]
    public decimal? class_fixed_tariff { get; set; }

    [Column("class_fixed_pool_tariff")]
    public decimal? class_fixed_pool_tariff { get; set; }

    [Column("lease_fixed_tariff")]
    public decimal? lease_fixed_tariff { get; set; }

    // Variable tariffs
    [Column("overhead_kilometer_amount")]
    public decimal? overhead_kilometer_amount { get; set; }

    [Column("maintenance_kilometer_amount")]
    public decimal? maintenance_kilometer_amount { get; set; }

    [Column("vehicle_kilometer_tariff")]
    public decimal? vehicle_kilometer_tariff { get; set; }

    // Metadata
    [Column("calculation_date")]
    public DateTime calculation_date { get; set; }

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
