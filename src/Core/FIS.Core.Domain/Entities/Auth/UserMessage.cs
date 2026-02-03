using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

[Table("user_message")]
public class UserMessage
{
    [Key]
    [Column("user_message_code")]
    public int user_message_code { get; set; }

    [Column("user_access_code")]
    public int user_access_code { get; set; }

    [Column("message")]
    [StringLength(2000)]
    public string? message { get; set; }

    [Column("message_read")]
    [StringLength(1)]
    public string? message_read { get; set; }

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
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }

    [ForeignKey("user_access_code")]
    public virtual User? User { get; set; }
}
