using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

/// <summary>
/// Bridge table linking Azure Entra ID users to legacy TS_Users records.
/// Enables dual authentication (Entra ID SSO + Legacy JWT) during migration.
/// </summary>
[Table("EntraId_User_Mapping")]
public class EntraIdUserMapping
{
    /// <summary>
    /// Primary key - auto-incrementing identity
    /// </summary>
    [Key]
    [Column("mapping_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int mapping_id { get; set; }

    /// <summary>
    /// Azure Entra ID Object ID (GUID from Entra ID token)
    /// Format: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    /// </summary>
    [Required]
    [Column("entra_object_id")]
    [StringLength(100)]
    public string entra_object_id { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to TS_Users.user_access_code
    /// Links Entra ID identity to legacy FIS user account
    /// </summary>
    [Required]
    [Column("user_access_code")]
    public int user_access_code { get; set; }

    /// <summary>
    /// Timestamp when mapping was created
    /// </summary>
    [Column("created_date")]
    public DateTime created_date { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey(nameof(user_access_code))]
    public virtual User? User { get; set; }
}
