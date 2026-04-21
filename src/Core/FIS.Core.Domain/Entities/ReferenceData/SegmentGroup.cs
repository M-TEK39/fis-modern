using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("segment_group")]
public class SegmentGroup
{
    [Key]
    [Column("segment_group_code")]
    public int segment_group_code { get; set; }

    [Column("segment_group_name")]
    [StringLength(255)]
    public string? segment_group_name { get; set; }

    [Column("financial_system_code")]
    public byte financial_system_code { get; set; }

    [Column("segment_type_code")]
    public byte? segment_type_code { get; set; }

    [Column("segment_group_isdebit")]
    public bool segment_group_isdebit { get; set; }

    [Column("segment_group_isledger")]
    public bool segment_group_isledger { get; set; }

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
