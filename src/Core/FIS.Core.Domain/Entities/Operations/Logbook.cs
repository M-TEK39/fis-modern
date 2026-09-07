using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Logbook Entity - Vehicle logbook management
/// Maps to legacy 'logbook' table with exact field names for compatibility
/// </summary>
[Table("logbook")]
public class Logbook
{
    /// <summary>
    /// Primary key - Logbook code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("logbookcode")]
    public short logbookcode { get; set; }

    /// <summary>
    /// Foreign key to vehicle table (nullable)
    /// </summary>
    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    /// <summary>
    /// Beginning logbook number
    /// </summary>
    [StringLength(8)]
    [Column("begin_num")]
    public string? begin_num { get; set; }

    /// <summary>
    /// Ending logbook number
    /// </summary>
    [StringLength(8)]
    [Column("end_num")]
    public string? end_num { get; set; }

    /// <summary>
    /// Date logbook was handed out
    /// </summary>
    [Column("handout_date")]
    public DateTime? handout_date { get; set; }

    /// <summary>
    /// Site code where logbook was issued
    /// </summary>
    [Column("site_code")]
    public short? site_code { get; set; }

    /// <summary>
    /// Name of logbook receiver
    /// </summary>
    [StringLength(25)]
    [Column("lb_receiver_name")]
    public string? lb_receiver_name { get; set; }

    /// <summary>
    /// Telephone number of receiver
    /// </summary>
    [StringLength(20)]
    [Column("lb_tel_num")]
    public string? lb_tel_num { get; set; }

    /// <summary>
    /// Logbook comments/notes
    /// </summary>
    [StringLength(60)]
    [Column("lb_comment")]
    public string? lb_comment { get; set; }

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
