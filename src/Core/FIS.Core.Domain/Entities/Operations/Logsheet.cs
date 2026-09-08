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
    [StringLength(10)]
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

    // Legacy transaction columns. They remain part of dbo.Logsheets even
    // though the modern entry workflow only edits the fields above.
    [Column("trans_date")]
    public DateTime trans_date { get; set; }

    [Column("driver_time")]
    public double? driver_time { get; set; }

    [Column("FBS_comp")]
    public DateTime? FBS_comp { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("trans_time")]
    public TimeSpan trans_time { get; set; }

    [Column("department_code")]
    public short department_code { get; set; }

    [Column("contract_code")]
    public int? contract_code { get; set; }

    [Column("journal_detail_code")]
    public Guid journal_detail_code { get; set; }

    [Column("parent_log_code")]
    public int? parent_log_code { get; set; }

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
