using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

[Table("AccessLevels")]
public class AccessLevel
{
    [Key]
    [Column("AccessLevelID")]
    public short AccessLevelID { get; set; }

    [Column("AccessLevelName")]
    [StringLength(255)]
    public string? AccessLevelName { get; set; }

    [Column("AccessLevelValue")]
    public long AccessLevelValue { get; set; }

    [Column("AccessLevelCalc")]
    public long? AccessLevelCalc { get; set; }

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
