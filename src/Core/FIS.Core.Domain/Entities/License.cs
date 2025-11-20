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
}