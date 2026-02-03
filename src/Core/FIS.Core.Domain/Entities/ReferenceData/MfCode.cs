using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("mf_code")]
public class MfCode
{
    [Key]
    [Column("mf_code_code")]
    public int mf_code_code { get; set; }

    [Column("mf_code_number")]
    [StringLength(50)]
    public string? mf_code_number { get; set; }

    [Column("mf_code_name")]
    [StringLength(255)]
    public string? mf_code_name { get; set; }

    [Column("mf_code_value_mask")]
    [StringLength(50)]
    public string? mf_code_value_mask { get; set; }

    [Column("segment_group_code")]
    public int segment_group_code { get; set; }

    [Column("mf_code_order")]
    public byte mf_code_order { get; set; }

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
}
