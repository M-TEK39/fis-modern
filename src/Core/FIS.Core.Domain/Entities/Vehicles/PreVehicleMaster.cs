using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Vehicles;

/// <summary>
/// Pre-Vehicle Master Entity - Tracks vehicle inception authorization workflow
/// Maps to legacy 'pre_vehicle_master' table
/// Manages approve/reject flow for new vehicle captures before they become active
/// </summary>
[Table("pre_vehicle_master")]
public class PreVehicleMaster
{
    /// <summary>
    /// Primary key - Temporary VMF code for pre-capture
    /// </summary>
    [Key]
    [Column("temp_vmf_code")]
    public int temp_vmf_code { get; set; }

    /// <summary>
    /// Vehicle chassis number (VIN) - unique identifier for pre-capture
    /// </summary>
    [StringLength(50)]
    [Column("chassis_number")]
    public string? chassis_number { get; set; }

    /// <summary>
    /// Engine number
    /// </summary>
    [StringLength(50)]
    [Column("engine_number")]
    public string? engine_number { get; set; }

    /// <summary>
    /// Model code - FK to model table
    /// </summary>
    [Column("model_code")]
    public short model_code { get; set; }

    /// <summary>
    /// Registration number
    /// </summary>
    [Column("registration_number")]
    [StringLength(50)]
    public string? registration_number { get; set; }

    /// <summary>
    /// Color of the vehicle
    /// </summary>
    [StringLength(50)]
    [Column("colour")]
    public string? colour { get; set; }

    /// <summary>
    /// Purchase amount
    /// </summary>
    [Column("purchase_amount")]
    public decimal? purchase_amount { get; set; }

    /// <summary>
    /// Purchase date
    /// </summary>
    [Column("purchase_date")]
    public DateTime? purchase_date { get; set; }

    /// <summary>
    /// Purchased from (vendor/supplier)
    /// </summary>
    [StringLength(200)]
    [Column("purchase_from")]
    public string? purchase_from { get; set; }

    /// <summary>
    /// Take on date - when vehicle enters fleet
    /// </summary>
    [Column("take_on_date")]
    public DateTime? take_on_date { get; set; }

    /// <summary>
    /// Take on odometer reading
    /// </summary>
    [Column("take_on_odo")]
    public int? take_on_odo { get; set; }

    /// <summary>
    /// Fleet number being replaced (if applicable)
    /// </summary>
    [StringLength(20)]
    [Column("replaced_gg_number")]
    public string? replaced_gg_number { get; set; }

    /// <summary>
    /// Fleet management comments/notes
    /// </summary>
    [Column("Fleet_Notes")]
    public string? Fleet_Notes { get; set; }

    /// <summary>
    /// Damage status flag (Y/N)
    /// </summary>
    [StringLength(1)]
    [Column("damage_status")]
    public string? damage_status { get; set; }

    /// <summary>
    /// Damage description/comments
    /// </summary>
    [Column("damages_comment")]
    public string? damages_comment { get; set; }

    /// <summary>
    /// Authorization status: "Awaiting Authorization", "Authorized", "Rejected"
    /// </summary>
    [StringLength(50)]
    [Column("Authority_Status")]
    public string? Authority_Status { get; set; }

    /// <summary>
    /// User code who authorized/rejected the vehicle
    /// </summary>
    [Column("authorized_by_user_code")]
    public int? authorized_by_user_code { get; set; }

    /// <summary>
    /// Date/time when authorization decision was made
    /// </summary>
    [Column("authorization_date")]
    public DateTime? authorization_date { get; set; }

    /// <summary>
    /// Rejection reason (if rejected)
    /// </summary>
    [Column("rejection_reason")]
    public string? rejection_reason { get; set; }

    /// <summary>
    /// Authorization comments
    /// </summary>
    [Column("authorization_comment")]
    public string? authorization_comment { get; set; }

    /// <summary>
    /// Vehicle VMF code (if already authorized and moved to vehicle_master)
    /// </summary>
    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    // Global audit fields
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
    [ForeignKey("model_code")]
    public virtual Model? Model { get; set; }

    [ForeignKey("authorized_by_user_code")]
    public virtual User? AuthorizedByUser { get; set; }

    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
