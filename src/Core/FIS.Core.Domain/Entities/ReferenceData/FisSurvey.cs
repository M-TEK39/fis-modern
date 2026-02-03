using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("FIS_Survey")]
public class FisSurvey
{
    [Key]
    [Column("satisfaction_survey_code")]
    public int satisfaction_survey_code { get; set; }

    [Column("used_service")]
    [StringLength(255)]
    public string? used_service { get; set; }

    [Column("department_fleet")]
    [StringLength(255)]
    public string? department_fleet { get; set; }

    [Column("customer_service")]
    [StringLength(255)]
    public string? customer_service { get; set; }

    [Column("professionalism")]
    [StringLength(255)]
    public string? professionalism { get; set; }

    [Column("quality_of_vehicles")]
    [StringLength(255)]
    public string? quality_of_vehicles { get; set; }

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
