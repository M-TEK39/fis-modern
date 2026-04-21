using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("Journal_WithInvalidBasCodes")]
public class JournalWithInvalidBasCode
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Added missing PK

    [Column("JournalType")]
    [StringLength(50)]
    public string? JournalType { get; set; }

    [Column("GGNumber")]
    [StringLength(50)]
    public string? GGNumber { get; set; }

    [Column("BasCode_FinancialYear")]
    [StringLength(50)]
    public string? BasCode_FinancialYear { get; set; }

    [Column("Enter_Correct_Responsibility_Number_Only")]
    [StringLength(50)]
    public string? ResponsibilityNumber { get; set; }

    [Column("Enter_Correct_Objective_Number_Only")]
    [StringLength(50)]
    public string? ObjectiveNumber { get; set; }

    [Column("SiteName")]
    [StringLength(255)]
    public string? SiteName { get; set; }

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
