using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Records operational remarks against a vehicle — e.g. "missing", "under investigation".
/// Multiple remarks can be open simultaneously; each can be resolved independently.
/// Table: vehicle_remarks (new — no legacy equivalent)
/// </summary>
[Table("vehicle_remarks")]
public class VehicleRemark
{
    [Key]
    [Column("remark_id")]
    public int remark_id { get; set; }

    [Required]
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Category of the remark.
    /// Known values: General | Missing | UnderInvestigation | AccidentHold | Other
    /// </summary>
    [Required]
    [Column("remark_category")]
    [StringLength(50)]
    public string remark_category { get; set; } = "General";

    /// <summary>
    /// Free-text description of the remark.
    /// </summary>
    [Required]
    [Column("remark_text")]
    [StringLength(2000)]
    public string remark_text { get; set; } = string.Empty;

    /// <summary>
    /// True while the remark is active; false once resolved/cleared.
    /// </summary>
    [Column("is_resolved")]
    public bool is_resolved { get; set; } = false;

    [Column("resolved_date")]
    public DateTime? resolved_date { get; set; }

    [Column("resolved_by_user_code")]
    public int? resolved_by_user_code { get; set; }

    [Column("resolution_notes")]
    [StringLength(2000)]
    public string? resolution_notes { get; set; }

    // Standard audit fields
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
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("resolved_by_user_code")]
    public virtual User? ResolvedByUser { get; set; }
}
