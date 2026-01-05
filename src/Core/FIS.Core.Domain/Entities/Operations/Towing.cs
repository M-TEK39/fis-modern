using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Towing Entity - Vehicle towing request management
/// Maps to legacy 'Towing' table with exact field names for compatibility
/// </summary>
[Table("Towing")]
public class Towing
{
    /// <summary>
    /// Primary key - Towing code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Towing_code")]
    public short Towing_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table
    /// </summary>
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Call reference number
    /// </summary>
    [Column("Call_refer")]
    public decimal? Call_refer { get; set; }

    /// <summary>
    /// Date towing was requested
    /// </summary>
    [Column("Tow_request_date")]
    public DateTime? Tow_request_date { get; set; }

    /// <summary>
    /// Time towing was requested
    /// </summary>
    [Column("Tow_request_time")]
    public DateTime? Tow_request_time { get; set; }

    /// <summary>
    /// Location where vehicle needs to be towed from
    /// </summary>
    [Column("Tow_location_start")]
    public string? Tow_location_start { get; set; }

    /// <summary>
    /// Description of vehicle problem
    /// </summary>
    [Column("Vehicle_problem")]
    public string? Vehicle_problem { get; set; }

    /// <summary>
    /// Key availability/location
    /// </summary>
    [StringLength(200)]
    [Column("Keys")]
    public string? Keys { get; set; }

    /// <summary>
    /// Site code
    /// </summary>
    [Column("Site_code")]
    public short? Site_code { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated vehicle
    /// </summary>
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    /// <summary>
    /// Associated site
    /// </summary>
    [ForeignKey("Site_code")]
    public virtual Site? Site { get; set; }
}
