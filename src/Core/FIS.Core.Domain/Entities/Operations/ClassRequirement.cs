using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Operations;

/// <summary>
/// ClassRequirement Entity - Vehicle class requirements for third party projects
/// Maps to legacy 'ClassRequirements' table with exact field names for compatibility
/// Links projects to required vehicle classes with quantity requirements
/// </summary>
[Table("ClassRequirements")]
public class ClassRequirement
{
    /// <summary>
    /// Primary key - Class Requirement ID
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("class_requirement_id")]
    public int class_requirement_id { get; set; }

    /// <summary>
    /// Foreign key to ThirdPartyProject (or Project) table
    /// Nullable until ThirdPartyProject entity is created
    /// </summary>
    [Column("project_id")]
    public int? project_id { get; set; }

    /// <summary>
    /// Foreign key to Class table (vehicle class)
    /// </summary>
    [Column("class_id")]
    public short class_id { get; set; }

    /// <summary>
    /// Number of vehicles required for this class
    /// </summary>
    [Column("required_count")]
    public int? required_count { get; set; }

    /// <summary>
    /// Start date for the requirement
    /// </summary>
    [Column("start_date")]
    public DateTime? start_date { get; set; }

    /// <summary>
    /// End date for the requirement
    /// </summary>
    [Column("end_date")]
    public DateTime? end_date { get; set; }

    /// <summary>
    /// Additional requirement notes
    /// </summary>
    [Column("notes")]
    public string? notes { get; set; }

    // Navigation properties
    /// <summary>
    /// Associated vehicle class
    /// </summary>
    [ForeignKey("class_id")]
    public virtual Class? Class { get; set; }

    // Note: ThirdPartyProject navigation property will be added when that entity is created
    // public virtual ThirdPartyProject? Project { get; set; }

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
