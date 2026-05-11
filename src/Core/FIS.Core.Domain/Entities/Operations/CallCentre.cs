using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// CallCentre Entity - Call centre incident management
/// Maps to legacy 'Call_centre' table with exact field names for compatibility
/// </summary>
[Table("Call_centre")]
public class CallCentre
{
    /// <summary>
    /// Primary key - Call centre code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Call_centre_code")]
    public short Call_centre_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table (nullable)
    /// </summary>
    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    /// <summary>
    /// Time of the call
    /// </summary>
    [Column("Call_time")]
    public DateTime? Call_time { get; set; }

    /// <summary>
    /// Date of the call
    /// </summary>
    [Column("Call_date")]
    public DateTime? Call_date { get; set; }

    /// <summary>
    /// Incident type/category (legacy field)
    /// </summary>
    [StringLength(100)]
    [Column("Incident_type")]
    public string? Incident_type { get; set; }

    /// <summary>
    /// Incident description/details (legacy field, used for cross-module references)
    /// </summary>
    [StringLength(500)]
    [Column("Incident_Desc")]
    public string? Incident_Desc { get; set; }

    /// <summary>
    /// Name of person who captured the call
    /// </summary>
    [StringLength(100)]
    [Column("Capture_name")]
    public string? Capture_name { get; set; }

    /// <summary>
    /// User access code (foreign key to user)
    /// </summary>
    [Column("User_access_code")]
    public short? User_access_code { get; set; }

    /// <summary>
    /// Name of the caller
    /// </summary>
    [StringLength(100)]
    [Column("Caller_name")]
    public string? Caller_name { get; set; }

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
    /// Driver license number
    /// </summary>
    [StringLength(50)]
    [Column("Driver_Licno")]
    public string? Driver_Licno { get; set; }

    /// <summary>
    /// GG number (fleet identifier)
    /// </summary>
    [StringLength(50)]
    [Column("GG_number")]
    public string? GG_number { get; set; }

    /// <summary>
    /// Driver base station
    /// </summary>
    [StringLength(100)]
    [Column("Driver_base_station")]
    public string? Driver_base_station { get; set; }

    /// <summary>
    /// Driver site code
    /// </summary>
    [Column("Driver_Site")]
    public short? Driver_Site { get; set; }

    /// <summary>
    /// Driver telephone number
    /// </summary>
    [StringLength(50)]
    [Column("Driver_tel")]
    public string? Driver_tel { get; set; }

    /// <summary>
    /// Driver cell phone number
    /// </summary>
    [StringLength(50)]
    [Column("Driver_cell")]
    public string? Driver_cell { get; set; }

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
