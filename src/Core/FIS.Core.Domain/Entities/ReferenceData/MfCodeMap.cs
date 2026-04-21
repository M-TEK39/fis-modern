using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("mf_code_map")]
public class MfCodeMap
{
    [Key]
    [Column("mf_code_map_code")]
    public int mf_code_map_code { get; set; }

    [Column("segment_journal_detail_map_code")]
    public long segment_journal_detail_map_code { get; set; }

    [Column("mf_code_code")]
    public int mf_code_code { get; set; }

    [Column("mf_code_value")]
    [StringLength(255)]
    public string? mf_code_value { get; set; }

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

    [ForeignKey("mf_code_code")]
    public virtual MfCode? MfCode { get; set; }

    [ForeignKey("segment_journal_detail_map_code")]
    public virtual SegmentJournalDetailMap? SegmentJournalDetailMap { get; set; }
}
