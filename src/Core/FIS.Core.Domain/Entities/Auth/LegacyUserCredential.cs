using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

/// <summary>
/// Legacy User Credential Entity
/// Stores password hashes for legacy JWT authentication
/// </summary>
[Table("Legacy_User_Credentials")]
public class LegacyUserCredential
{
    [Key]
    [Column("credential_id")]
    public int credential_id { get; set; }

    [Required]
    [Column("user_access_code")]
    public int user_access_code { get; set; }

    [Required]
    [Column("password_hash")]
    [StringLength(255)]
    public string password_hash { get; set; } = string.Empty;

    [Required]
    [Column("password_salt")]
    [StringLength(255)]
    public string password_salt { get; set; } = string.Empty;

    [Column("password_reset_token")]
    [StringLength(255)]
    public string? password_reset_token { get; set; }

    [Column("password_reset_token_expiry")]
    public DateTime? password_reset_token_expiry { get; set; }

    [Column("failed_login_attempts")]
    public int failed_login_attempts { get; set; } = 0;

    [Column("account_locked_until")]
    public DateTime? account_locked_until { get; set; }

    [Column("last_password_change")]
    public DateTime last_password_change { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Absolute date/time after which the password is expired.
    /// Calculated at change time as last_password_change + 90 days.
    /// Null for legacy records (fall back to config-based calculation).
    /// </summary>
    [Column("password_expiry_date")]
    public DateTime? password_expiry_date { get; set; }

    /// <summary>FK to TS_Users — who last changed this password (admin reset or self-service).</summary>
    [Column("changed_by_user_code")]
    public int? changed_by_user_code { get; set; }

    [Column("created_date")]
    public DateTime created_date { get; set; } = DateTime.UtcNow;

    [Column("modified_date")]
    public DateTime? modified_date { get; set; }

    [Column("is_active")]
    public bool is_active { get; set; } = true;

    // Navigation property
    [ForeignKey(nameof(user_access_code))]
    public virtual User? User { get; set; }
}
