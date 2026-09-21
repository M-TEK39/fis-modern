using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Operations;

/// <summary>
/// Represents a job card (work order) for vehicle maintenance/repairs
/// Legacy: Managed via DEV_INS_NewJobCards, DEV_UPD_Jobcards stored procedures
/// </summary>
[Table("job_cards")]
public class JobCard
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("job_card_id")]
    public int job_card_id { get; set; }

    [Required]
    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Required]
    [Column("extra_code")]
    public short extra_code { get; set; }

    [Required]
    [Column("status_code")]
    public int status_code { get; set; }

    [Column("priority")]
    [StringLength(1)]
    public string? priority { get; set; } // 'H' = High, 'N' = Normal, null = Unassigned

    [Column("assigned_to")]
    public int? assigned_to { get; set; }

    [Column("assigned_date")]
    public DateTime? assigned_date { get; set; }

    [Column("jcs_comment")]
    [StringLength(2000)]
    public string? jcs_comment { get; set; }

    [Column("damages")]
    [StringLength(2000)]
    public string? damages { get; set; }

    [Column("comments")]
    [StringLength(2000)]
    public string? comments { get; set; }

    [Column("authorizer")]
    public int? authorizer { get; set; }

    [Column("reviewed")]
    [StringLength(1)]
    public string? reviewed { get; set; } // 'Y' or 'N'

    // ── Repair cost fields (captured at close time by Maintenance unit) ──────
    // Assumption: costs captured when job card is closed.
    // QUESTIONS.md MX-1/MX-2 — confirm breakdown and amendment rules with users.

    [Column("labour_cost", TypeName = "decimal(10,2)")]
    public decimal? labour_cost { get; set; }

    [Column("parts_cost", TypeName = "decimal(10,2)")]
    public decimal? parts_cost { get; set; }

    [Column("other_cost", TypeName = "decimal(10,2)")]
    public decimal? other_cost { get; set; }

    /// <summary>Auto-calculated: labour_cost + parts_cost + other_cost. Stored for fast reporting.</summary>
    [Column("total_cost", TypeName = "decimal(10,2)")]
    public decimal? total_cost { get; set; }

    [Column("invoice_number")]
    [StringLength(50)]
    public string? invoice_number { get; set; }

    [Column("invoice_date")]
    public DateTime? invoice_date { get; set; }

    /// <summary>Internal workshop name or external service provider / merchant name.</summary>
    [Column("service_provider")]
    [StringLength(200)]
    public string? service_provider { get; set; }

    // Global audit fields
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

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("extra_code")]
    public virtual ExtraCode? ExtraCodeRef { get; set; }

    [ForeignKey("assigned_to")]
    public virtual User? AssignedToUser { get; set; }

    [ForeignKey("authorizer")]
    public virtual User? AuthorizerUser { get; set; }

    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }

    [NotMapped]
    public string? jc_number { get; set; }
}
