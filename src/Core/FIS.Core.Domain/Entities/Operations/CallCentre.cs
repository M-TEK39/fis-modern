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
    [StringLength(20)]
    [Column("Incident_type")]
    public string? Incident_type { get; set; }

    /// <summary>
    /// Incident description/details (legacy field, used for cross-module references)
    /// </summary>
    [StringLength(60)]
    [Column("Incident_Desc")]
    public string? Incident_Desc { get; set; }

    /// <summary>
    /// Name of person who captured the call
    /// </summary>
    [StringLength(30)]
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
    [StringLength(60)]
    [Column("Caller_name")]
    public string? Caller_name { get; set; }

    /// <summary>
    /// Driver name
    /// </summary>
    [StringLength(60)]
    [Column("Driver_name")]
    public string? Driver_name { get; set; }

    /// <summary>
    /// Driver personnel number
    /// </summary>
    [StringLength(15)]
    [Column("Driver_persalno")]
    public string? Driver_persalno { get; set; }

    /// <summary>
    /// Driver license number
    /// </summary>
    [StringLength(15)]
    [Column("Driver_Licno")]
    public string? Driver_Licno { get; set; }

    /// <summary>
    /// GG number (fleet identifier)
    /// </summary>
    [StringLength(8)]
    [Column("GG_number")]
    public string? GG_number { get; set; }

    /// <summary>
    /// Driver base station
    /// </summary>
    [StringLength(30)]
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
    [StringLength(30)]
    [Column("Driver_tel")]
    public string? Driver_tel { get; set; }

    /// <summary>
    /// Driver cell phone number
    /// </summary>
    [StringLength(10)]
    [Column("Driver_cell")]
    public string? Driver_cell { get; set; }

    [StringLength(15)]
    [Column("Driver_fax")]
    public string? Driver_fax { get; set; }

    [StringLength(25)]
    [Column("Driver_email")]
    public string? Driver_email { get; set; }

    [Column("Incident_date")]
    public DateTime? Incident_date { get; set; }

    [Column("Incident_time")]
    public DateTime? Incident_time { get; set; }

    [StringLength(30)]
    [Column("Caller_tel")]
    public string? Caller_tel { get; set; }

    [StringLength(60)]
    [Column("TrOfficer_name")]
    public string? TrOfficer_name { get; set; }

    [StringLength(15)]
    [Column("TrOfficer_tel")]
    public string? TrOfficer_tel { get; set; }

    [Column("TrOfficer_Site")]
    public short? TrOfficer_Site { get; set; }

    [StringLength(50)]
    [Column("Incident_town")]
    public string? Incident_town { get; set; }

    [StringLength(30)]
    [Column("Incident_street")]
    public string? Incident_street { get; set; }

    [Column("Counter")]
    public short? Counter { get; set; }

    [StringLength(15)]
    [Column("Caller_fax")]
    public string? Caller_fax { get; set; }

    [StringLength(15)]
    [Column("TrOfficer_fax")]
    public string? TrOfficer_fax { get; set; }

    [StringLength(40)]
    [Column("Caller_email")]
    public string? Caller_email { get; set; }

    [StringLength(60)]
    [Column("TrOfficer_email")]
    public string? TrOfficer_email { get; set; }

    [StringLength(1)]
    [Column("Inform_CRO")]
    public string? Inform_CRO { get; set; }

    [StringLength(60)]
    [Column("CRO_Remarks")]
    public string? CRO_Remarks { get; set; }

    [StringLength(80)]
    [Column("Incident_Remarks")]
    public string? Incident_Remarks { get; set; }

    [Column("Notify_list_code")]
    public int? Notify_list_code { get; set; }

    [StringLength(1)]
    [Column("call_closed")]
    public string? call_closed { get; set; }

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
