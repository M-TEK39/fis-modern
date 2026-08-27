using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

[Table("fis_session_tokens")]
public class SessionToken
{
    [Key]
    [MaxLength(64)]
    public string token_id { get; set; } = null!;

    [Required]
    [MaxLength(16)]
    public string token_type { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string session_id { get; set; } = null!;

    [Required]
    public string claims_json { get; set; } = null!;

    public DateTime expires_at { get; set; }

    public DateTime created_at { get; set; }
}
