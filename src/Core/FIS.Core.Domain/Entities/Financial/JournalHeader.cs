using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("journal")]
public class JournalHeader
{
    [Key]
    [Column("journal_code")]
    public long journal_code { get; set; }

    [Column("batch_code")]
    public int batch_code { get; set; }

    [Column("journal_date")]
    public DateTime journal_date { get; set; }

    [Column("journal_installation_link")]
    public string? journal_installation_link { get; set; }

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

    [ForeignKey("batch_code")]
    public virtual Batch? Batch { get; set; }
}