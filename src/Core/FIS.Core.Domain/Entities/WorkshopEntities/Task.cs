using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.WorkshopEntities;

[Table("task")]
public class Task
{
    [Key]
    [Column("task_code")]
    public int task_code { get; set; }

    [Column("profile_code")]
    public short profile_code { get; set; }

    [Column("bom_code")]
    public int? bom_code { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? description { get; set; }

    [Column("duration_hours")]
    public decimal? duration_hours { get; set; }

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
