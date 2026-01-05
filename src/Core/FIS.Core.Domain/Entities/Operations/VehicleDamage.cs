using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Vehicle_Damages")]
public class VehicleDamage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("damage_id")]
    public short damage_id { get; set; }

    [Column("temp_vmf_code")]
    public int? temp_vmf_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("damage_status")]
    public string? damage_status { get; set; }

    [Column("damages_comment")]
    public string? damages_comment { get; set; }

    [Column("status_date")]
    public DateTime? status_date { get; set; }

    [Column("modified_by_user_access_code")]
    public int? modified_by_user_access_code { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("modified_by_user_access_code")]
    public virtual User? ModifiedByUser { get; set; }
}
