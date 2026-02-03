using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("New_Vehicles_received")]
public class NewVehicleReceived
{
    [Key]
    [Column("new_vehicle_code")]
    public short new_vehicle_code { get; set; }

    [Column("order_id")]
    public short order_id { get; set; }

    [Column("gg_number")]
    [StringLength(50)]
    public string? gg_number { get; set; }

    [Column("engine_num")]
    [StringLength(50)]
    public string? engine_num { get; set; }

    [Column("chassis_num")]
    [StringLength(50)]
    public string? chassis_num { get; set; }

    [Column("extras")]
    public string? extras { get; set; }

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
