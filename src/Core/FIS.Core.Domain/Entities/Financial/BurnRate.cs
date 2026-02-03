using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

[Table("burn_rate")]
public class BurnRate
{
    [Key]
    [Column("burn_rate_code")]
    public int burn_rate_code { get; set; }

    [Column("company_id")]
    public short? company_id { get; set; }

    [Column("company_name")]
    [StringLength(255)]
    public string? company_name { get; set; }

    [Column("department_id")]
    public short? department_id { get; set; }

    [Column("department_name")]
    [StringLength(255)]
    public string? department_name { get; set; }

    [Column("department_number")]
    [StringLength(50)]
    public string? department_number { get; set; }

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
