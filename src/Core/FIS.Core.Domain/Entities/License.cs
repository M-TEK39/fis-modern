using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// License Entity - License types required for vehicle operation
/// Maps to legacy 'license' table with exact field names for compatibility
/// Defines license requirements for different vehicle models
/// </summary>
[Table("license")]
public class License
{
    /// <summary>
    /// Primary key - License code (license type identifier)
    /// Auto-generated identity column in legacy database
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("licence_code")]
    public short licence_code { get; set; }

    /// <summary>
    /// License type description (Driver's License, Commercial License, etc.)
    /// </summary>
    [Required]
    [StringLength(255)]
    [Column("licence_description")]
    public string licence_description { get; set; } = string.Empty;

    /// <summary>
    /// Optional license category code
    /// </summary>
    [StringLength(20)]
    [Column("licence_category")]
    public string? licence_category { get; set; }

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