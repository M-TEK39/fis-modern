using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

[Table("budget_amount")]
public class BudgetAmount
{
    [Key]
    [Column("budget_amount_code")]
    public int budget_amount_code { get; set; }

    [Column("budget_code")]
    public int budget_code { get; set; }

    [Column("cost_category_code")]
    public short cost_category_code { get; set; }

    [Column("amount")]
    public decimal amount { get; set; }

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

    [ForeignKey("budget_code")]
    public virtual Budget? Budget { get; set; }
}
