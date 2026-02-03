using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("Income_Split_TempTable")]
public class IncomeSplitTempTable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Added missing PK

    [Column("Type")]
    [StringLength(50)]
    public string? Type { get; set; }

    [Column("source_date")]
    public DateTime? source_date { get; set; }

    [Column("TransactionFinYear")]
    [StringLength(50)]
    public string? TransactionFinYear { get; set; }

    [Column("journal_detail_id")]
    public int? journal_detail_id { get; set; }

    [Column("journal_detail_code")]
    public Guid? journal_detail_code { get; set; }

    [Column("journal_code")]
    public long? journal_code { get; set; }

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
}
