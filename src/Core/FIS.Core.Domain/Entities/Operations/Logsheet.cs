using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Logsheet Entity - Monthly vehicle usage tracking
/// Maps to legacy 'Logsheets' table with exact field names for compatibility
/// </summary>
[Table("Logsheets")]
public class Logsheet
{
    /// <summary>
    /// Primary key - Log code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("log_code")]
    public int log_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table
    /// </summary>
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Starting odometer reading
    /// </summary>
    [Column("start_odo")]
    public double start_odo { get; set; }

    /// <summary>
    /// Ending odometer reading
    /// </summary>
    [Column("end_odo")]
    public double end_odo { get; set; }

    /// <summary>
    /// Month for this logsheet
    /// </summary>
    [Column("month")]
    public DateTime month { get; set; }

    /// <summary>
    /// Site code
    /// </summary>
    [Column("site_code")]
    public short site_code { get; set; }

    /// <summary>
    /// Requisition number
    /// </summary>
    [StringLength(50)]
    [Column("rek_num")]
    public string? rek_num { get; set; }

    /// <summary>
    /// Number of days vehicle was used
    /// </summary>
    [Column("days_used")]
    public int? days_used { get; set; }

    /// <summary>
    /// Bundle number
    /// </summary>
    [Column("bund_num")]
    public int? bund_num { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated vehicle
    /// </summary>
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    /// <summary>
    /// Associated site
    /// </summary>
    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }

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

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
