using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("vehicle_status")]
public class VehicleStatus
{
    [Key]
    [Column("vehicle_status_code")]
    public short vehicle_status_code { get; set; }

    [Column("status_description")]
    [StringLength(255)]
    public string? status_description { get; set; }

    [Column("status_predecessors")]
    [StringLength(255)]
    public string? status_predecessors { get; set; }

    [Column("user_roles")]
    [StringLength(255)]
    public string? user_roles { get; set; }

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
