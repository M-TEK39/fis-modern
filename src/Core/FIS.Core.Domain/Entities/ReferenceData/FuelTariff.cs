using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("fuel_tariff")]
public class FuelTariff
{
    [Key]
    [Column("fuel_tariff_code")]
    public short fuel_tariff_code { get; set; }

    [Column("fuel_type_code")]
    public short fuel_type_code { get; set; }

    [Column("fuel_tariff")]
    public decimal fuel_tariff { get; set; }

    [Column("fuel_tariff_notes")]
    [StringLength(1000)]
    public string? fuel_tariff_notes { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("end_date")]
    public DateTime? end_date { get; set; }

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
}
