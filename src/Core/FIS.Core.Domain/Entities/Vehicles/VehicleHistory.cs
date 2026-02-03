using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("vehicle_history")]
public class VehicleHistory
{
    [Key]
    [Column("hist_code")]
    public int hist_code { get; set; }

    [Column("hist_vmf_code")]
    public int hist_vmf_code { get; set; }

    [Column("hist_vehicle_status_code")]
    public short? hist_vehicle_status_code { get; set; }

    [Column("hist_date_changed")]
    public DateTime? hist_date_changed { get; set; }

    [Column("hist_user_access_code")]
    public short? hist_user_access_code { get; set; }

    [Column("hist_fleet_number")]
    [StringLength(50)]
    public string? hist_fleet_number { get; set; }

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

    [ForeignKey("hist_vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
