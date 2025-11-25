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
    [StringLength(50)]
    [Column("begin_num")]
    public string? begin_num { get; set; }

    /// <summary>
    /// Ending logbook number
    /// </summary>
    [StringLength(50)]
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
    [StringLength(100)]
    [Column("lb_receiver_name")]
    public string? lb_receiver_name { get; set; }

    /// <summary>
    /// Telephone number of receiver
    /// </summary>
    [StringLength(50)]
    [Column("lb_tel_num")]
    public string? lb_tel_num { get; set; }

    /// <summary>
    /// Logbook comments/notes
    /// </summary>
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
}
