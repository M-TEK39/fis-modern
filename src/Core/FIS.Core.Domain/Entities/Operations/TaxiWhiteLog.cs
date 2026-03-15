using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Operations;

[Table("Taxi_white_log")]
public class TaxiWhiteLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Log_id")]
    public int Log_id { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("start_odo")]
    public long start_odo { get; set; }

    [Column("end_odo")]
    public long end_odo { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("end_date")]
    public DateTime end_date { get; set; }

    [Column("driver")]
    [StringLength(255)]
    public string? driver { get; set; }

    [Column("user_access_code")]
    public short user_access_code { get; set; }

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
