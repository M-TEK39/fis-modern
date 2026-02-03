using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Maintenance;

/// <summary>
/// MaintenanceTrigger Entity - EXACT legacy schema match
/// Maps to 'maintenance_trigger' table
/// </summary>
[Table("maintenance_trigger")]
public class MaintenanceTrigger
{
    [Key]
    [Column("maint_trigger_code")]
    public short maint_trigger_code { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? description { get; set; }

    [Column("trigger_id")]
    [StringLength(50)]
    public string? trigger_id { get; set; }

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
