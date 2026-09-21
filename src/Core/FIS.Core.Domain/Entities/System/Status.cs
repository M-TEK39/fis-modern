using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("Status", Schema = "Workflow")]
public class Status
{
    [Key]
    [Column("StatusID")]
    public int StatusID { get; set; }

    [Column("StepID")]
    public int StepID { get; set; }

    [Column("DateCompleted")]
    public DateTime? DateCompleted { get; set; }

    [Column("DateStarted")]
    public DateTime? DateStarted { get; set; }

    [Column("IsBusy")]
    public bool IsBusy { get; set; }

    [Column("StartedByUserName")]
    [StringLength(255)]
    public string? StartedByUserName { get; set; }

    [Column("Identifier")]
    [StringLength(50)]
    public string Identifier { get; set; } = string.Empty;

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
