using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// ContractStatus Entity - Contract status types and descriptions
/// Maps to legacy 'contract_status' table with exact field names for compatibility
/// Defines contract status values used throughout the contract lifecycle
/// </summary>
[Table("contract_status")]
public class ContractStatus
{
    /// <summary>
    /// Primary key - Contract status code (status identifier)
    /// Auto-generated identity column in legacy database
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("contract_status_code")]
    public short contract_status_code { get; set; }

    /// <summary>
    /// Contract status description (Active, Completed, Cancelled, etc.)
    /// </summary>
    [Required]
    [StringLength(100)]
    [Column("status_description")]
    public string status_description { get; set; } = string.Empty;

    /// <summary>
    /// Optional status abbreviation
    /// </summary>
    [StringLength(10)]
    [Column("status_abbreviation")]
    public string? status_abbreviation { get; set; }

    /// <summary>
    /// Indicates if this status means the contract is active
    /// </summary>
    [Column("is_active")]
    public bool is_active { get; set; }

    /// <summary>
    /// Indicates if this status means the contract is finalized/closed
    /// </summary>
    [Column("is_final")]
    public bool is_final { get; set; }

    // Legacy properties for compatibility with existing code
    public string StatusCode => contract_status_code.ToString();
    public string StatusDescription => status_description;
    public bool IsActive => is_active;
    public int DisplayOrder => contract_status_code;

    // Navigation properties
    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

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
}

/// <summary>
/// Represents contract terms and conditions
/// </summary>
public class ContractTerms
{
    public int Id { get; set; }
    public int ContractCode { get; set; }
    public string TermType { get; set; } = string.Empty;
    public string TermDescription { get; set; } = string.Empty;
    public decimal? TermValue { get; set; }
    public string? TermUnits { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    // Navigation properties
    public virtual Contract Contract { get; set; } = null!;
}

/// <summary>
/// Represents vehicle maintenance records
/// </summary>
public class VehicleMaintenanceRecord
{
    public int Id { get; set; }
    public int VmfCode { get; set; }
    public DateTime MaintenanceDate { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Cost { get; set; }
    public int? Odometer { get; set; }
    public string? ServiceProvider { get; set; }
    public DateTime CreatedDate { get; set; }

    // Navigation properties
    public virtual Vehicle Vehicle { get; set; } = null!;
}
