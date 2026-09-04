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

    // Present in the legacy Traffic_Dept table. The expanded modern table
    // was created without it, so it is loaded through the compatibility repository.
    [NotMapped]
    public string? Traf_cell { get; set; }

    [Column("Traf_email")]
    public string? Traf_email { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    [NotMapped]
    public DateTime date_created { get; set; }

    [NotMapped]
    public DateTime? date_updated { get; set; }

    [NotMapped]
    public int? created_by_user_code { get; set; }

    [NotMapped]
    public int? modified_by_user_code { get; set; }

    [NotMapped]
    public bool is_deleted { get; set; } = false;

    // Navigation properties for audit trail
    [NotMapped]
    public virtual User? CreatedByUser { get; set; }

    [NotMapped]
    public virtual User? ModifiedByUser { get; set; }
}
