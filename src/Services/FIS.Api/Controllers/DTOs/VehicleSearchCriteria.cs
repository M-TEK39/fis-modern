using System.ComponentModel.DataAnnotations;

namespace FIS.Api.Controllers.DTOs;

/// <summary>
/// Search criteria for advanced vehicle filtering
/// </summary>
public class VehicleSearchCriteria
{
    /// <summary>
    /// Filter by vehicle status (A=Active, I=Inactive)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Filter by availability status (true for available, false for hired)
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    /// Search term for registration number (partial match)
    /// </summary>
    public string? RegistrationNumberSearch { get; set; }

    /// <summary>
    /// Filter by vehicle type (V=Vehicle, T=Trailer, etc.)
    /// </summary>
    public string? VehicleType { get; set; }

    /// <summary>
    /// Filter by site code
    /// </summary>
    public int? SiteCode { get; set; }

    /// <summary>
    /// Filter by minimum vehicle year
    /// </summary>
    public int? MinYear { get; set; }

    /// <summary>
    /// Filter by maximum vehicle year
    /// </summary>
    public int? MaxYear { get; set; }

    /// <summary>
    /// Filter by mileage range - minimum
    /// </summary>
    public int? MinMileage { get; set; }

    /// <summary>
    /// Filter by mileage range - maximum
    /// </summary>
    public int? MaxMileage { get; set; }

    /// <summary>
    /// Filter vehicles that need service based on business rules
    /// </summary>
    public bool? NeedsService { get; set; }

    /// <summary>
    /// Sort by field (Registration, Year, Mileage, Status)
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction (ASC or DESC)
    /// </summary>
    public string? SortDirection { get; set; } = "ASC";

    /// <summary>
    /// Maximum number of results to return (default 100, max 1000)
    /// </summary>
    [Range(1, 1000)]
    public int MaxResults { get; set; } = 100;
}
