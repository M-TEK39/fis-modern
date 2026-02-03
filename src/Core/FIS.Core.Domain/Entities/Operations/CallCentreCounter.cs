using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Operations;

[Table("Call_Centre_Counter")]
public class CallCentreCounter
{
    [Key]
    [Column("Call_Centre_Counter_code")]
    public short Call_Centre_Counter_code { get; set; }

    [Column("Call_Center_code")]
    public decimal Call_Center_code { get; set; }

    [Column("CounterCC")]
    public short? CounterCC { get; set; }

    [Column("DataCapture_id")]
    public short? DataCapture_id { get; set; }

    [Column("DataCapture_date")]
    public DateTime? DataCapture_date { get; set; }

    [Column("DataCapture_time")]
    public DateTime? DataCapture_time { get; set; }

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
