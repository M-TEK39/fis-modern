using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// UnitOfMeasure Entity - Units of measurement for vehicle specifications
/// Maps to legacy 'unit_of_measure' table with exact field names for compatibility
/// Defines measurement units used throughout the system (km, miles, liters, etc.)
/// </summary>
[Table("unit_of_measure")]
public class UnitOfMeasure
{
    /// <summary>
    /// Primary key - Unit of measure code (unit identifier)
    /// Auto-generated identity column in legacy database
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("unit_of_measure_code")]
    public short unit_of_measure_code { get; set; }

    /// <summary>
    /// Unit of measure description (Kilometers, Miles, Liters, Gallons, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    [Column("unit_description")]
    public string unit_description { get; set; } = string.Empty;

    /// <summary>
    /// Optional short abbreviation (km, mi, L, gal, etc.)
    /// </summary>
    [StringLength(10)]
    [Column("unit_abbreviation")]
    public string? unit_abbreviation { get; set; }

    /// <summary>
    /// Unit category (Distance, Volume, Weight, etc.)
    /// </summary>
    [StringLength(50)]
    [Column("unit_category")]
    public string? unit_category { get; set; }
}