using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Vehicle_Damages")]
public class VehicleDamage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("damage_id")]
    public short damage_id { get; set; }

    [Column("temp_vmf_code")]
    public int? temp_vmf_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("damage_status")]
    public string? damage_status { get; set; }

    [Column("damages_comment")]
    public string? damages_comment { get; set; }

    [Column("status_date")]
    public DateTime? status_date { get; set; }

    [Column("modified_by_user_access_code")]
    public int? modified_by_user_access_code { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("modified_by_user_access_code")]
    public virtual User? ModifiedByUser { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    
}
