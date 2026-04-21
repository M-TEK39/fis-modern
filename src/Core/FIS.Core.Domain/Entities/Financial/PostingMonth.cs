using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("posting_month")]
public class PostingMonth
{
    [Key]
    [Column("posting_month_code")]
    public short posting_month_code { get; set; }

    [Column("posting_year_code")]
    public short posting_year_code { get; set; }

    [Column("month_number")]
    public byte month_number { get; set; }

    [Column("month_name")]
    [StringLength(50)]
    public string? month_name { get; set; }

    [Column("is_closed")]
    public bool is_closed { get; set; }

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
