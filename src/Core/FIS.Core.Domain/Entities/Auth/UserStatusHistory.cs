using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

/// <summary>
/// Tracks every enable / disable / lock / unlock / password-reset event per user.
/// This table preserves the full history — not just current state.
/// </summary>
[Table("user_status_history")]
public class UserStatusHistory
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("status_history_id")]
    public int status_history_id { get; set; }

    /// <summary>FK to TS_Users.user_access_code — which user's status changed.</summary>
    [Required]
    [Column("user_access_code")]
    public int user_access_code { get; set; }

    /// <summary>
    /// The status after the change.
    /// Values: enabled | disabled | locked | unlocked | password_reset | created
    /// </summary>
    [Required]
    [Column("new_status")]
    [StringLength(20)]
    public string new_status { get; set; } = string.Empty;

    /// <summary>The status before the change (null when first created).</summary>
    [Column("previous_status")]
    [StringLength(20)]
    public string? previous_status { get; set; }

    /// <summary>FK to TS_Users.user_access_code — who triggered the change.</summary>
    [Column("changed_by_user_code")]
    public int? changed_by_user_code { get; set; }

    /// <summary>UTC timestamp of the status change.</summary>
    [Column("changed_at")]
    public DateTime changed_at { get; set; } = DateTime.UtcNow;

    /// <summary>Optional free-text reason (e.g. "Account inactive per HR instruction").</summary>
    [Column("reason")]
    [StringLength(500)]
    public string? reason { get; set; }

    // ── Navigation properties ──────────────────────────────────────────────

    [ForeignKey(nameof(user_access_code))]
    public virtual User? User { get; set; }

    [ForeignKey(nameof(changed_by_user_code))]
    public virtual User? ChangedByUser { get; set; }
}
