using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("Demo_vehicles")]
public class DemoVehicle
{
    [Key]
    [Column("demo_vehicle_code")]
    public short demo_vehicle_code { get; set; }

    [Column("gg_number")]
    [StringLength(50)]
    public string? gg_number { get; set; }

    [Column("reg_number")]
    [StringLength(50)]
    public string? reg_number { get; set; }

    [Column("model_description")]
    [StringLength(255)]
    public string? model_description { get; set; }

    [Column("year_mnf")]
    public int? year_mnf { get; set; }

    [Column("site_code")]
    public short? site_code { get; set; }

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

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }
}
