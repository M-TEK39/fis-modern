using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Monitor Entity - Fleet monitoring and inquiry tracking
/// Maps to legacy 'Monitor' table with exact field names for compatibility
/// </summary>
[Table("Monitor")]
public class Monitor
{
    /// <summary>
    /// Primary key - Monitor code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("monitor_code")]
    public short monitor_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table (nullable)
    /// </summary>
    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    /// <summary>
    /// Capture date
    /// </summary>
    [Column("Capture_dat")]
    public DateTime? Capture_dat { get; set; }

    /// <summary>
    /// User access code (foreign key to user)
    /// </summary>
    [Column("User_access_code")]
    public short? User_access_code { get; set; }

    /// <summary>
    /// Type of inquiry
    /// </summary>
    [StringLength(100)]
    [Column("Inquiry_type")]
    public string? Inquiry_type { get; set; }

    /// <summary>
    /// Inquiry description
    /// </summary>
    [Column("Inquiry_Desc")]
    public string? Inquiry_Desc { get; set; }

    /// <summary>
    /// Driver name
    /// </summary>
    [StringLength(100)]
    [Column("Driver_name")]
    public string? Driver_name { get; set; }

    /// <summary>
    /// Driver personnel number
    /// </summary>
    [StringLength(50)]
    [Column("Driver_persalno")]
    public string? Driver_persalno { get; set; }

    /// <summary>
    /// Driver site code
    /// </summary>
    [Column("Driver_Site")]
    public short? Driver_Site { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated vehicle
    /// </summary>
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

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
