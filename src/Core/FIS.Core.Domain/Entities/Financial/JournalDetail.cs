using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// JournalDetail Entity - Financial journal entries for contract billing
/// EXACT legacy schema match from Database.cs
/// Maps to journal_detail table
/// Used for tracking financial transactions related to contracts
/// </summary>
[Table("journal_detail")]
public class JournalDetail
{
    [Key]
    [Column("journal_detail_id")]
    public int journal_detail_id { get; set; }

    [Column("journal_detail_code")]
    public Guid journal_detail_code { get; set; }

    [Column("journal_code")]
    public long? journal_code { get; set; }

    [Column("department_code")]
    public int department_code { get; set; }

    [Column("site_code")]
    public short site_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("journal_detail_type_code")]
    public short journal_detail_type_code { get; set; }

    [Column("journal_detail_isdebit")]
    public bool journal_detail_isdebit { get; set; }

    [Column("journal_detail_quantity")]
    public int journal_detail_quantity { get; set; }

    [Column("journal_detail_tariff")]
    public decimal journal_detail_tariff { get; set; }

    [Column("journal_detail_amount")]
    public decimal journal_detail_amount { get; set; }

    [Column("journal_detail_description")]
    public string? journal_detail_description { get; set; }

    [Column("journal_detail_reversalof")]
    public Guid? journal_detail_reversalof { get; set; }

    [Column("journal_detail_date_created")]
    public DateTime journal_detail_date_created { get; set; }

    [Column("journal_detail_date_updated")]
    public DateTime? journal_detail_date_updated { get; set; }

    [Column("journal_detail_date_posted")]
    public DateTime? journal_detail_date_posted { get; set; }

    [Column("journal_detail_isaccepted")]
    public bool journal_detail_isaccepted { get; set; }

    [Column("journal_detail_financial_year")]
    public string? journal_detail_financial_year { get; set; }

    [Column("journal_detail_date")]
    public DateTime journal_detail_date { get; set; }

    [Column("journal_detail_date_approved")]
    public DateTime? journal_detail_date_approved { get; set; }

    [Column("journal_detail_rebill_code")]
    public Guid? journal_detail_rebill_code { get; set; }

    [Column("journal_detail_isreversaldenied")]
    public bool journal_detail_isreversaldenied { get; set; }

    [Column("journal_detail_debitamount")]
    public decimal? journal_detail_debitamount { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }

    [ForeignKey("department_code")]
    public virtual Department? Department { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
