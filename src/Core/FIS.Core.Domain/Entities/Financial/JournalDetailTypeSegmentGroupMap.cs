using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.ReferenceData;

namespace FIS.Core.Domain.Entities.Financial;

[Table("journal_detail_type_segment_group_map")]
public class JournalDetailTypeSegmentGroupMap
{
    [Key]
    [Column("journal_detail_type_segment_group_map_code")]
    public int journal_detail_type_segment_group_map_code { get; set; }

    [Column("journal_detail_type_code")]
    public byte journal_detail_type_code { get; set; }

    [Column("segment_group_code")]
    public int segment_group_code { get; set; }

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

    [ForeignKey("segment_group_code")]
    public virtual SegmentGroup? SegmentGroup { get; set; }

    [ForeignKey("journal_detail_type_code")]
    public virtual JournalDetailType? JournalDetailType { get; set; }
}
