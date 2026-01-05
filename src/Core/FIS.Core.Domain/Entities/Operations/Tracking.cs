using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Tracking")]
public class Tracking
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("track_code")]
    public short track_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("track_num")]
    public string? track_num { get; set; }

    [Column("gg_previous")]
    public string? gg_previous { get; set; }

    [Column("gg_follow")]
    public string? gg_follow { get; set; }

    [Column("install_date")]
    public DateTime? install_date { get; set; }

    [Column("remove_date")]
    public DateTime? remove_date { get; set; }

    [Column("track_status")]
    public string? track_status { get; set; }

    [Column("track_type")]
    public string? track_type { get; set; }

    [Column("track_note")]
    public string? track_note { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}
