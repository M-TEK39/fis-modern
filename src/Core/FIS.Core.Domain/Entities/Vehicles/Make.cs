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

    // Computed properties for modern API compatibility
    public short MakeCode => make_code;
    public string MakeDescription => make_description;
    public string Name => make_description;
}