using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Vehicles;

/// <summary>
/// TempFleetNote Entity - EXACT legacy schema match
/// Maps to 'temp_fleet_note' table
/// </summary>
[Table("temp_fleet_note")]
public class TempFleetNote
{
    [Key]
    [Column("temp_fleet_notes_code")]
    public int temp_fleet_notes_code { get; set; }

    [Column("temp_vmf_code")]
    public int temp_vmf_code { get; set; }

    [Column("notes")]
    public string? notes { get; set; }

    [Column("update_date")]
    public DateTime? update_date { get; set; }

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
