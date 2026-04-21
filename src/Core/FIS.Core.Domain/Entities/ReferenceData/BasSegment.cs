using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("bassegment")]
public class BasSegment
{
    [Key]
    [Column("segment_code")]
    public int segment_code { get; set; }

    [Column("segment_number")]
    [StringLength(50)]
    public string? segment_number { get; set; }

    [Column("segment_name")]
    [StringLength(255)]
    public string? segment_name { get; set; }

    [Column("segment_group_code")]
    public int segment_group_code { get; set; }

    [Column("department_code")]
    public short department_code { get; set; }

    [Column("site_code")]
    public short? site_code { get; set; }

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

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }
}
