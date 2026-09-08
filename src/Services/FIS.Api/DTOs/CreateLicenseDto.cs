using System.ComponentModel.DataAnnotations;

namespace FIS.Api.DTOs;

/// <summary>
/// Data Transfer Object for creating new License entities
/// Used in API endpoints to transfer license creation data
/// </summary>
public class CreateLicenseDto
{
    /// <summary>
    /// License type description (Driver's License, Commercial License, etc.)
    /// </summary>
    [Required]
    [StringLength(255, ErrorMessage = "License description cannot exceed 255 characters")]
    public string licence_description { get; set; } = string.Empty;

    /// <summary>
    /// Optional license category code
    /// </summary>
    [StringLength(20, ErrorMessage = "License category cannot exceed 20 characters")]
    public string? licence_category { get; set; }
}
