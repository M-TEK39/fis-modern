using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Operations;

[Table("Taxi_logs")]
public class TaxiLog
{
    [Key]
    [Column("log_id")]
    public int log_id { get; set; }

    [Column("request_id")]
    public int? request_id { get; set; }

    [Column("rek_num")]
    [StringLength(50)]
    public string? rek_num { get; set; }

    [Column("user_start_odo")]
    public decimal? user_start_odo { get; set; }

    [Column("user_end_odo")]
    public decimal? user_end_odo { get; set; }

    [Column("user_start_date")]
    public DateTime? user_start_date { get; set; }

    [Column("user_end_date")]
    public DateTime? user_end_date { get; set; }

    [Column("user_start_time")]
    public DateTime? user_start_time { get; set; }

    [Column("user_end_time")]
    public DateTime? user_end_time { get; set; }

    [Column("driver_start_odo")]
    public decimal? driver_start_odo { get; set; }

    [Column("driver_end_odo")]
    public decimal? driver_end_odo { get; set; }

    [Column("driver_start_date")]
    public DateTime? driver_start_date { get; set; }

    [Column("driver_end_date")]
    public DateTime? driver_end_date { get; set; }

    [Column("driver_start_time")]
    public DateTime? driver_start_time { get; set; }

    [Column("driver_end_time")]
    public DateTime? driver_end_time { get; set; }

    [Column("userid")]
    public short userid { get; set; }

    [Column("enter_date")]
    public DateTime enter_date { get; set; }

    [Column("invoiced_date")]
    public DateTime? invoiced_date { get; set; }

    [Column("division")]
    [StringLength(50)]
    public string? division { get; set; }

    [Column("distance")]
    public decimal? distance { get; set; }

    [Column("days")]
    public short? days { get; set; }

    [Column("hours")]
    public double? hours { get; set; }

    // Legacy accounting fields. The expanded schema may omit these columns, so they
    // are carried by the compatibility repository rather than mapped by EF.
    [NotMapped]
    public int? batch_num { get; set; }

    [NotMapped]
    public int? bas_batch { get; set; }

    [NotMapped]
    public int? prev_batch { get; set; }

    [NotMapped]
    public string? changed { get; set; }

    [NotMapped]
    public Guid? journal_detail_code { get; set; }

    [Column("quoted_tariff")]
    public float? quoted_tariff { get; set; }

    [Column("parent_taxi_log_code")]
    public int? parent_taxi_log_code { get; set; }

    [Column("taxi_log_note_code")]
    public short? taxi_log_note_code { get; set; }

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
