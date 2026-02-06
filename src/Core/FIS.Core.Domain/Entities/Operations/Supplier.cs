using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Operations;

/// <summary>
/// Supplier Entity - Third party suppliers and vendors
/// Maps to legacy 'Suppliers' table with exact field names for compatibility
/// </summary>
[Table("Suppliers")]
public class Supplier
{
    /// <summary>
    /// Primary key - Supplier ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_id")]
    public short supplier_id { get; set; }

    /// <summary>
    /// Supplier name
    /// </summary>
    [Required]
    [StringLength(200)]
    [Column("supplier_name")]
    public string supplier_name { get; set; } = string.Empty;

    /// <summary>
    /// Supplier contact person
    /// </summary>
    [StringLength(200)]
    [Column("contact_person")]
    public string? contact_person { get; set; }

    /// <summary>
    /// Supplier phone number
    /// </summary>
    [StringLength(50)]
    [Column("phone_number")]
    public string? phone_number { get; set; }

    /// <summary>
    /// Supplier email address
    /// </summary>
    [StringLength(200)]
    [Column("email")]
    public string? email { get; set; }

    /// <summary>
    /// Supplier physical address
    /// </summary>
    [StringLength(500)]
    [Column("address")]
    public string? address { get; set; }

    /// <summary>
    /// Supplier tax number or registration number
    /// </summary>
    [StringLength(50)]
    [Column("tax_number")]
    public string? tax_number { get; set; }

    /// <summary>
    /// Whether supplier is active
    /// </summary>
    [Column("is_active")]
    public bool is_active { get; set; } = true;

    /// <summary>
    /// Additional notes or comments
    /// </summary>
    [Column("notes")]
    public string? notes { get; set; }

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
