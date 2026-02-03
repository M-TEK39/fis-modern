using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("vehicle_status_history")]
public class VehicleStatusHistory
{
    [Key]
    [Column("vehicle_status_history_code")]
    public int vehicle_status_history_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("vehicle_status_code")]
    public short vehicle_status_code { get; set; }

    [Column("vehicle_status_description")]
    [StringLength(255)]
    public string? vehicle_status_description { get; set; }

    [Column("status_start_date")]
    public DateTime status_start_date { get; set; }

    [Column("status_end_date")]
    public DateTime status_end_date { get; set; }

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

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
