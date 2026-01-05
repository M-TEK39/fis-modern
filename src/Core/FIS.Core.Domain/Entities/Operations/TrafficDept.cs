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
}
