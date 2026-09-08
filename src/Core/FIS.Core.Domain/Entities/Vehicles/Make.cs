using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Make Entity - Vehicle manufacturer/brand definitions
/// Maps to legacy 'make' table with exact field names for compatibility
/// </summary>
[Table("make")]
public class Make
{
    /// <summary>
    /// Primary key - Make code (manufacturer identifier)
    /// Auto-generated identity column in legacy database
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("make_code")]
    public short make_code { get; set; }

    /// <summary>
    /// Make description/name (Toyota, Ford, BMW, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    [Column("make_description")]
    public string make_description { get; set; } = string.Empty;

    // Navigation properties
    /// <summary>
    /// Models associated with this make
    /// </summary>
    public virtual ICollection<Model> Models { get; set; } = new List<Model>();

    /// <summary>
    /// Vehicles of this make
    /// </summary>
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

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

    // Computed properties for modern API compatibility
    public short MakeCode => make_code;
    public string MakeDescription => make_description;
    public string Name => make_description;
}
