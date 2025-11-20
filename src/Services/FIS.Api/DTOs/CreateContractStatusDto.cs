using System.ComponentModel.DataAnnotations;

namespace FIS.Api.DTOs;

/// <summary>
/// Data Transfer Object for creating new ContractStatus entities
/// Used in API endpoints to transfer contract status creation data
/// </summary>
public class CreateContractStatusDto
{
    /// <summary>
    /// Contract status description (Active, Completed, Cancelled, etc.)
    /// </summary>
    [Required]
    [StringLength(100, ErrorMessage = "Status description cannot exceed 100 characters")]
    public string status_description { get; set; } = string.Empty;

    /// <summary>
    /// Optional status abbreviation
    /// </summary>
    [StringLength(10, ErrorMessage = "Status abbreviation cannot exceed 10 characters")]
    public string? status_abbreviation { get; set; }

    /// <summary>
    /// Indicates if this status means the contract is active
    /// </summary>
    public bool is_active { get; set; }

    /// <summary>
    /// Indicates if this status means the contract is finalized/closed
    /// </summary>
    public bool is_final { get; set; }
}