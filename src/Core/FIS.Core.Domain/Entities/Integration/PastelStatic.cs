using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Integration;

[Table("PastelStatic")]
public class PastelStatic
{
    [Key]
    [Column("staticID")]
    public byte staticID { get; set; }

    [Column("idLinePermanent")]
    public int? idLinePermanent { get; set; }

    [Column("iValidateFlag")]
    public int? iValidateFlag { get; set; }

    [Column("iAccountCurrencyID")]
    public int? iAccountCurrencyID { get; set; }

    [Column("cAccountCurrencySymbol")]
    [StringLength(10)]
    public string? cAccountCurrencySymbol { get; set; }

    [Column("bTrCodeHasTax")]
    public bool? bTrCodeHasTax { get; set; }

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
