using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("vehicle_type_history")]
public class VehicleTypeHistory
{
    [Key]
    [Column("vehicle_type_history_code")]
    public int vehicle_type_history_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("type_code")]
    public short type_code { get; set; }

    [Column("type_start_date")]
    public DateTime type_start_date { get; set; }

    [Column("type_end_date")]
    public DateTime? type_end_date { get; set; }

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
