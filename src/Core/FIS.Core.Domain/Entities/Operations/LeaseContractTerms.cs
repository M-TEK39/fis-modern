using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("LeaseContractTerms")]
public class LeaseContractTerms
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("VehicleContractTermID")]
    public int VehicleContractTermID { get; set; }

    [Column("vmf_Code")]
    public int vmf_Code { get; set; }

    [Column("AgreedTerms")]
    public int? AgreedTerms { get; set; }

    [Column("AgreedKilos")]
    public long? AgreedKilos { get; set; }

    [Column("AppliedInterest")]
    public decimal? AppliedInterest { get; set; }

    [Column("FixedMonthlyAmount")]
    public decimal? FixedMonthlyAmount { get; set; }

    [Column("AuthorityStatus")]
    public int? AuthorityStatus { get; set; }

    [Column("CreatedBy")]
    public int? CreatedBy { get; set; }

    [Column("CreatedDate")]
    public DateTime? CreatedDate { get; set; }

    [Column("ModifiedBy")]
    public int? ModifiedBy { get; set; }

    [Column("ModifiedDate")]
    public DateTime? ModifiedDate { get; set; }

    [Column("StartDate")]
    public DateTime? StartDate { get; set; }

    [Column("EndDate")]
    public DateTime? EndDate { get; set; }

    // Navigation properties
    [ForeignKey("vmf_Code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("CreatedBy")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("ModifiedBy")]
    public virtual User? ModifiedByUser { get; set; }

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
    

    
}
