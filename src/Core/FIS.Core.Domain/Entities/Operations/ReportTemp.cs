using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Operations;

[Table("rpt_temp")]
public class ReportTemp
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("date")]
    [StringLength(50)]
    public string? date { get; set; }

    [Column("reg_num")]
    [StringLength(50)]
    public string? reg_num { get; set; }

    [Column("site_code")]
    public int? site_code { get; set; }

    [Column("department")]
    public int? department { get; set; }

    [Column("merchant_name")]
    [StringLength(255)]
    public string? merchant_name { get; set; }

    [Column("merchant_area")]
    [StringLength(255)]
    public string? merchant_area { get; set; }

    [Column("voucher_number")]
    [StringLength(50)]
    public string? voucher_number { get; set; }

    [Column("odometer")]
    public int? odometer { get; set; }

    [Column("litres")]
    public decimal? litres { get; set; }

    [Column("amount")]
    public decimal? amount { get; set; }

    [Column("capacity")]
    public decimal? capacity { get; set; }

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
