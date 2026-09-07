using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Contracts;

[Table("Contractor_taxi_class")]
public class ContractorTaxiClass
{
    [Key]
    [Column("class_id")]
    public short class_id { get; set; }

    [Column("contractor_id")]
    public short contractor_id { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? description { get; set; }

    [Column("km_tariff")]
    public decimal? km_tariff { get; set; }

    [Column("driver_per_hour")]
    public decimal? driver_per_hour { get; set; }

    [Column("daily_tariff")]
    public decimal? daily_tariff { get; set; }

    // Legacy tariff fields. The reference repository negotiates these columns
    // because the expanded table only carries the three active tariff values.
    [NotMapped]
    public decimal? half_day_tariff { get; set; }

    [NotMapped]
    public string? tariff_type { get; set; }

    [NotMapped]
    public DateTime? tariff_date { get; set; }

    [NotMapped]
    public short? model_code { get; set; }

    [NotMapped]
    public decimal? km_tariff_bus { get; set; }

    [NotMapped]
    public bool? active { get; set; }

    [NotMapped]
    public DateTime? tariff_end_date { get; set; }

    [NotMapped]
    public TimeSpan? normalhours_start_time { get; set; }

    [NotMapped]
    public TimeSpan? normalhours_end_time { get; set; }

    [NotMapped]
    public TimeSpan? midweekovertime_start_time { get; set; }

    [NotMapped]
    public TimeSpan? midweekovertime_end_time { get; set; }

    [NotMapped]
    public TimeSpan? holidayhours_start_time { get; set; }

    [NotMapped]
    public TimeSpan? holidayhours_end_time { get; set; }

    // Global audit fields
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

    // Navigation properties
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }

    [ForeignKey("contractor_id")]
    public virtual Contractor? Contractor { get; set; }
}
