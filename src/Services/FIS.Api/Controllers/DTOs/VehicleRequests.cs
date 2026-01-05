using System.ComponentModel.DataAnnotations;

namespace FIS.Api.Controllers.DTOs;

/// <summary>
/// Request for creating a new vehicle with business validation
/// </summary>
public class VehicleCreationRequest
{
    /// <summary>
    /// Vehicle registration number (unique identifier)
    /// </summary>
    [Required]
    [StringLength(20, MinimumLength = 3)]
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Site code where vehicle will be based
    /// </summary>
    [Required]
    public int SiteCode { get; set; }

    /// <summary>
    /// Vehicle type (V=Vehicle, T=Trailer, etc.)
    /// </summary>
    [Required]
    [StringLength(1)]
    public string VehicleType { get; set; } = "V";

    /// <summary>
    /// Vehicle make (manufacturer)
    /// </summary>
    [StringLength(50)]
    public string? Make { get; set; }

    /// <summary>
    /// Vehicle model
    /// </summary>
    [StringLength(50)]
    public string? Model { get; set; }

    /// <summary>
    /// Year of manufacture
    /// </summary>
    [Range(1900, 2030)]
    public int? Year { get; set; }

    /// <summary>
    /// Vehicle color
    /// </summary>
    [StringLength(30)]
    public string? Color { get; set; }

    /// <summary>
    /// Initial mileage/odometer reading
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? InitialMileage { get; set; }

    /// <summary>
    /// Service interval in miles (default 10000)
    /// </summary>
    [Range(1000, 50000)]
    public int ServiceIntervalMiles { get; set; } = 10000;

    /// <summary>
    /// Service interval in months (default 12)
    /// </summary>
    [Range(1, 60)]
    public int ServiceIntervalMonths { get; set; } = 12;

    /// <summary>
    /// Vehicle notes or special instructions
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Request for updating vehicle odometer reading
/// </summary>
public class OdometerUpdateRequest
{
    /// <summary>
    /// New odometer reading
    /// </summary>
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Odometer reading must be positive")]
    public int NewOdometer { get; set; }

    /// <summary>
    /// Optional notes about the odometer update
    /// </summary>
    [StringLength(200)]
    public string? Notes { get; set; }
}
