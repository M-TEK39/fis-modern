using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

[Table("daily_transactions")]
public class DailyTransaction
{
    [Key]
    [Column("daily_transaction_code")]
    public int daily_transaction_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("cost_category_code")]
    public short cost_category_code { get; set; }

    [Column("posting_month_code")]
    public short? posting_month_code { get; set; }

    [Column("file_sequence_number")]
    public short file_sequence_number { get; set; }

    [Column("transaction_date")]
    public DateTime transaction_date { get; set; }

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

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
