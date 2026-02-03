using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("anchor_points")]
public class AnchorPoint
{
    [Key]
    [Column("anchor_point_code")]
    public int anchor_point_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("anchor_odo_meter")]
    public int anchor_odo_meter { get; set; }

    [Column("anchor_date")]
    public DateTime anchor_date { get; set; }

    [Column("anchor_type_code")]
    public byte anchor_type_code { get; set; }

    [Column("bas_journal_record_code")]
    public long? bas_journal_record_code { get; set; }

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
