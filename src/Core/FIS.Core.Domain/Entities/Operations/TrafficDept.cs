using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Traffic_Dept")]
public class TrafficDept
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Traffic_dept_code")]
    public short Traffic_dept_code { get; set; }

    [Column("Traf_name")]
    public string? Traf_name { get; set; }

    [Column("Traf_res_person")]
    public string? Traf_res_person { get; set; }

    [Column("Traf_post_address1")]
    public string? Traf_post_address1 { get; set; }

    [Column("Traf_post_address2")]
    public string? Traf_post_address2 { get; set; }

    [Column("Traf_post_code")]
    public string? Traf_post_code { get; set; }

    [Column("Traf_telephone")]
    public string? Traf_telephone { get; set; }

    [Column("Traf_fax")]
    public string? Traf_fax { get; set; }

    [Column("Traf_email")]
    public string? Traf_email { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
