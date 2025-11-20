using System.ComponentModel.DataAnnotations;

namespace FIS.Api.DTOs;

/// <summary>
/// Data Transfer Object for creating new UnitOfMeasure entities
/// Used in API endpoints to transfer unit of measure creation data
/// </summary>
public class CreateUnitOfMeasureDto
{
    /// <summary>
    /// Unit of measure description (Kilometers, Miles, Liters, Gallons, etc.)
    /// </summary>
    [Required]
    [StringLength(100, ErrorMessage = "Unit description cannot exceed 100 characters")]
    public string unit_description { get; set; } = string.Empty;

    /// <summary>
    /// Optional short abbreviation (km, mi, L, gal, etc.)
    /// </summary>
    [StringLength(10, ErrorMessage = "Unit abbreviation cannot exceed 10 characters")]
    public string? unit_abbreviation { get; set; }

    /// <summary>
    /// Unit category (Distance, Volume, Weight, etc.)
    /// </summary>
    [StringLength(50, ErrorMessage = "Unit category cannot exceed 50 characters")]
    public string? unit_category { get; set; }
}