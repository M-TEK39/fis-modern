using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

[Table("approvers")]
public class Approver
{
    [Key]
    [Column("approver_code")]
    public int approver_code { get; set; }

    [Column("site_code")]
    public short? site_code { get; set; }

    [Column("department_code")]
    public int department_code { get; set; }

    [Column("rank_code")]
    public int rank_code { get; set; }

    [Column("Surname")]
    [StringLength(255)]
    public string? Surname { get; set; }

    [Column("Firstname")]
    [StringLength(255)]
    public string? Firstname { get; set; }

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

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }
}
