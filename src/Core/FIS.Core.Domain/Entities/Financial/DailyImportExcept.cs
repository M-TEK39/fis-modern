using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("daily_import_except")]
public class DailyImportExcept
{
    [Key]
    [Column("except_ID")]
    public short except_ID { get; set; }

    [Column("PAN")]
    [StringLength(50)]
    public string? PAN { get; set; }

    [Column("reg_number")]
    [StringLength(50)]
    public string? reg_number { get; set; }

    [Column("voucher")]
    [StringLength(50)]
    public string? voucher { get; set; }

    [Column("import_date")]
    public DateTime import_date { get; set; }

    [Column("exception_desc")]
    [StringLength(255)]
    public string? exception_desc { get; set; }

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
