using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Financial;

[Table("transactions")]
public class FinancialTransaction
{
    [Key]
    [Column("trans_code")]
    public int trans_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("cost_category_code")]
    public short cost_category_code { get; set; }

    [Column("posting_month_code")]
    public short? posting_month_code { get; set; }

    [Column("date_service_delivered")]
    public DateTime date_service_delivered { get; set; }

    [Column("odometer")]
    public int odometer { get; set; }

    [Column("derived_odo")]
    [StringLength(50)]
    public string? derived_odo { get; set; }

    [Column("rejected_odo")]
    public int? rejected_odo { get; set; }

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
