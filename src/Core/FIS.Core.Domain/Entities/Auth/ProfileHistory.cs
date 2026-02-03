using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Auth;

[Table("profile_history")]
public class ProfileHistory
{
    [Key]
    [Column("profile_history_code")]
    public int profile_history_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("trans_code")]
    public int? trans_code { get; set; }

    [Column("profile_code")]
    public short profile_code { get; set; }

    [Column("activity_odo")]
    public int activity_odo { get; set; }

    [Column("activity_date")]
    public DateTime activity_date { get; set; }

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
