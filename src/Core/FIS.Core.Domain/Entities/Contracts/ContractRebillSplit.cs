using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Contracts;

[Table("contract_rebillsplit")]
public class ContractRebillSplit
{
    [Key]
    [Column("rebillsplit_code")]
    public int rebillsplit_code { get; set; }

    [Column("contract_code")]
    public int contract_code { get; set; }

    [Column("rebill_percentage")]
    public decimal rebill_percentage { get; set; }

    [Column("site_code")]
    public short site_code { get; set; }

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

    [ForeignKey("contract_code")]
    public virtual Contract? Contract { get; set; }

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }
}
