using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Loss Entity - Vehicle loss/theft management
/// Maps to legacy 'losses' table with exact field names for compatibility
/// </summary>
[Table("losses")]
public class Loss
{
    /// <summary>
    /// Primary key - Loss code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("loss_code")]
    public short loss_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table
    /// </summary>
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Date of loss
    /// </summary>
    [Column("loss_date")]
    public DateTime loss_date { get; set; }

    /// <summary>
    /// Loss reference number
    /// </summary>
    [StringLength(100)]
    [Column("loss_reference")]
    public string? loss_reference { get; set; }

    /// <summary>
    /// Type of loss (theft, damage, etc.)
    /// </summary>
    [Column("loss_type_code")]
    public short? loss_type_code { get; set; }

    /// <summary>
    /// Site code where loss occurred
    /// </summary>
    [Column("site_code")]
    public short? site_code { get; set; }

    /// <summary>
    /// Department contact person
    /// </summary>
    [StringLength(100)]
    [Column("dept_contact")]
    public string? dept_contact { get; set; }

    /// <summary>
    /// Loss amount (value)
    /// </summary>
    [Column("loss_amount")]
    public decimal? loss_amount { get; set; }

    /// <summary>
    /// Department claim amount
    /// </summary>
    [Column("dept_claim")]
    public decimal? dept_claim { get; set; }

    /// <summary>
    /// SAPD (South African Police Department) reference
    /// </summary>
    [StringLength(100)]
    [Column("sapd")]
    public string? sapd { get; set; }

    /// <summary>
    /// Inspector name
    /// </summary>
    [StringLength(100)]
    [Column("inspector")]
    public string? inspector { get; set; }

    /// <summary>
    /// Case number
    /// </summary>
    [StringLength(100)]
    [Column("case_number")]
    public string? case_number { get; set; }

    /// <summary>
    /// Legacy loss workflow fields. These columns predate the modern API and
    /// must remain available when the client database is used.
    /// </summary>
    [Column("cancelled")]
    public decimal? cancelled { get; set; }

    [Column("cover_forfeit")]
    public decimal? cover_forfeit { get; set; }

    [Column("prosecute")]
    public decimal? prosecute { get; set; }

    [Column("compensation_order")]
    public decimal? compensation_order { get; set; }

    [Column("remarks")]
    public string? remarks { get; set; }

    [Column("hq_reference")]
    public string? hq_reference { get; set; }

    [Column("place_of_loss")]
    public string? place_of_loss { get; set; }

    [Column("garaging_authority")]
    public decimal? garaging_authority { get; set; }

    [Column("driver_name")]
    public string? driver_name { get; set; }

    [Column("report_from_dept")]
    public decimal? report_from_dept { get; set; }

    [Column("date_reported_ggmt")]
    public DateTime? date_reported_ggmt { get; set; }

    [Column("date_reported_sapd")]
    public DateTime? date_reported_sapd { get; set; }

    [Column("Call_Refer")]
    public decimal? Call_Refer { get; set; }

    [Column("Tow_need")]
    public string? Tow_need { get; set; }

    /// <summary>
    /// Modern status value when the expanded losses table provides it.
    /// </summary>
    [Column("loss_status")]
    public string? loss_status { get; set; }

    [NotMapped]
    public string? vehicle_identifier { get; set; }

    [NotMapped]
    public string? loss_type_description { get; set; }

    [NotMapped]
    public string? site_description { get; set; }

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
