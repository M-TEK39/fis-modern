using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Clearance Entity - Vehicle clearance management
/// Maps to legacy 'clearance' table with exact field names for compatibility
/// </summary>
[Table("clearance")]
public class Clearance
{
    /// <summary>
    /// Primary key - Clearance code
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("clearance_code")]
    public int clearance_code { get; set; }

    /// <summary>
    /// Foreign key to vehicle table
    /// </summary>
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    /// <summary>
    /// Clearance reference number
    /// </summary>
    [Column("clearance_number")]
    public int? clearance_number { get; set; }

    /// <summary>
    /// Date of clearance
    /// </summary>
    [Column("Clearance_date")]
    public DateTime? Clearance_date { get; set; }

    /// <summary>
    /// Merchant code
    /// </summary>
    [Column("Merchant_code")]
    public int? Merchant_code { get; set; }

    /// <summary>
    /// Clearance amount
    /// </summary>
    [Column("Clearance_amount")]
    public decimal? Clearance_amount { get; set; }

    /// <summary>
    /// Clearance comments/notes
    /// </summary>
    [Column("clearance_comment")]
    public string? clearance_comment { get; set; }

    /// <summary>
    /// Vehicle odometer reading at clearance
    /// </summary>
    [Column("clearance_kilo")]
    public int? clearance_kilo { get; set; }

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
