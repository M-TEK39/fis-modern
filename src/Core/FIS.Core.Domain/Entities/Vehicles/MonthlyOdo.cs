using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("monthly_odo")]
public class MonthlyOdo
{
    [Key]
    [Column("monthly_odo_code")]
    public int monthly_odo_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("posting_month_code")]
    public short posting_month_code { get; set; }

    [Column("max_odometer")]
    public int max_odometer { get; set; }

    [Column("derived_odo")]
    [StringLength(50)]
    public string? derived_odo { get; set; }

    [Column("odometer_date")]
    public DateTime odometer_date { get; set; }

    [Column("trans_type")]
    [StringLength(50)]
    public string? trans_type { get; set; }

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
