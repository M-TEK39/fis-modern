using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Contracts;

[Table("contract_status_history")]
public class ContractStatusHistory
{
    [Key]
    [Column("contract_status_history_code")]
    public int contract_status_history_code { get; set; }

    [Column("contract_code")]
    public int contract_code { get; set; }

    [Column("contract_status_code")]
    public short contract_status_code { get; set; }

    [Column("contract_status_description")]
    [StringLength(255)]
    public string? contract_status_description { get; set; }

    [Column("status_start_date")]
    public DateTime status_start_date { get; set; }

    [Column("status_end_date")]
    public DateTime status_end_date { get; set; }

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

    [ForeignKey("contract_status_code")]
    public virtual ContractStatus? ContractStatus { get; set; }
}
