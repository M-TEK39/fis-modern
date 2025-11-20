using System.ComponentModel.DataAnnotations;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Location Entity - Fixed version with proper validation
/// Represents physical locations where vehicles can be stationed
/// </summary>
public class Location
{
    [Key]
    public int LocationId { get; set; }

    [Required]
    [StringLength(100)]
    public string LocationName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    // Contact Information
    [StringLength(100)]
    public string? ContactPerson { get; set; }

    [StringLength(15)]
    public string? PhoneNumber { get; set; }

    [StringLength(100)]
    public string? Email { get; set; }

    // Address Information
    [StringLength(100)]
    public string? AddressLine1 { get; set; }

    [StringLength(100)]
    public string? AddressLine2 { get; set; }

    [StringLength(50)]
    public string? City { get; set; }

    [StringLength(10)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? Province { get; set; }

    [StringLength(50)]
    public string? Country { get; set; } = "South Africa";

    // GPS Coordinates for tracking
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Business rules
    [Required]
    public bool IsActive { get; set; } = true;

    // Audit trail
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedDate { get; set; }

    // Navigation properties - vehicles at this location
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
