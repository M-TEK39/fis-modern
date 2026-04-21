using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("cost_revenue_map")]
public class CostRevenueMap
{
    [Key]
    [Column("cost_revenue_map_code")]
    public byte cost_revenue_map_code { get; set; }

    [Column("journal_detail_type_code")]
    public byte journal_detail_type_code { get; set; }

    [Column("journal_detail_revenue_type_code")]
    public byte journal_detail_revenue_type_code { get; set; }

    [Column("journal_detail_revenue_type_description")]
    [StringLength(255)]
    public string? journal_detail_revenue_type_description { get; set; }

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
