using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace FIS.Core.Domain.Entities;

[Table("workshop")]
public class Workshop
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("ww_code")]
    public short ww_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("receive_time")]
    public DateTime? receive_time { get; set; }

    [Column("receive_date")]
    public DateTime? receive_date { get; set; }

    [Column("complete_time")]
    public DateTime? complete_time { get; set; }

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
