using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("Monthly_burn_rate")]
public class MonthlyBurnRate
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Added missing PK

    [Column("month")]
    public int? month { get; set; }

    [Column("year")]
    public int? year { get; set; }

    [Column("department_number")]
    public int? department_number { get; set; }

    [Column("Fixed_income_total")]
    public decimal? Fixed_income_total { get; set; }

    [Column("Fixed_cost_total")]
    public decimal? Fixed_cost_total { get; set; }

    [Column("percent_Replacement")]
    public decimal? percent_Replacement { get; set; }

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
