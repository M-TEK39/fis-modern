using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("batch")]
public class Batch
{
    [Key]
    [Column("batch_code")]
    public int batch_code { get; set; }

    [Column("batch_date")]
    public DateTime batch_date { get; set; }

    [Column("batch_turnover")]
    public decimal? batch_turnover { get; set; }

    [Column("batch_header_date")]
    [StringLength(50)]
    public string? batch_header_date { get; set; }

    [Column("batch_header_time")]
    [StringLength(50)]
    public string? batch_header_time { get; set; }

    [Column("financial_system_code")]
    public byte financial_system_code { get; set; }

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
