using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("InvalidSegmentNumbersUsed")]
public class InvalidSegmentNumber
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("journal_detail_code")]
    public Guid journal_detail_code { get; set; }

    [Column("segment_number")]
    public int segment_number { get; set; }

    [Column("segment_group_code")]
    public int segment_group_code { get; set; }

    [Column("segment_type_code")]
    public byte segment_type_code { get; set; }

    [Column("site_code")]
    public short? site_code { get; set; }

    [Column("department_code")]
    public short department_code { get; set; }

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
