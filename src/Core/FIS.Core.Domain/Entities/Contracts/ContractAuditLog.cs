using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Contracts;

/// <summary>
/// Append-only audit log for contract state changes and field edits.
/// One row per event — never updated, never deleted.
/// </summary>
[Table("contract_audit_log")]
public class ContractAuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int id { get; set; }

    [Required]
    [Column("contract_code")]
    public int contract_code { get; set; }

    /// <summary>
    /// Action performed: Created, Submitted, Recalled, Approved, ApprovedAndActivated,
    /// DeclinedForCorrection, Declined, Edited, Closed, Cancelled, Extended, Reassigned
    /// </summary>
    [Required]
    [StringLength(50)]
    [Column("action")]
    public string action { get; set; } = string.Empty;

    [Required]
    [Column("performed_by_user_code")]
    public int performed_by_user_code { get; set; }

    [Required]
    [Column("performed_at")]
    public DateTime performed_at { get; set; }

    /// <summary>Status code before this action (null for Created events)</summary>
    [Column("old_status_code")]
    public short? old_status_code { get; set; }

    /// <summary>Status code after this action</summary>
    [Column("new_status_code")]
    public short? new_status_code { get; set; }

    /// <summary>
    /// For field-level edits: the field that changed (e.g. "site_code", "target_return_date")
    /// </summary>
    [StringLength(100)]
    [Column("field_changed")]
    public string? field_changed { get; set; }

    /// <summary>Previous value (serialised as string for simplicity)</summary>
    [StringLength(500)]
    [Column("old_value")]
    public string? old_value { get; set; }

    /// <summary>New value</summary>
    [StringLength(500)]
    [Column("new_value")]
    public string? new_value { get; set; }

    /// <summary>Free-text note (approval comment, decline reason, recall reason, etc.)</summary>
    [StringLength(1000)]
    [Column("notes")]
    public string? notes { get; set; }

    // Navigation
    [ForeignKey("contract_code")]
    public virtual Contract? Contract { get; set; }

    [ForeignKey("performed_by_user_code")]
    public virtual User? PerformedByUser { get; set; }
}
