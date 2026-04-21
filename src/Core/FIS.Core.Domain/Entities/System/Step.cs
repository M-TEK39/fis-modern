using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

[Table("Step", Schema = "Workflow")]
public class Step
{
    [Key]
    [Column("StepID")]
    public int StepID { get; set; }

    [Column("StepName")]
    [StringLength(255)]
    public string? StepName { get; set; }

    [Column("StepOrder")]
    public int StepOrder { get; set; }

    [Column("StepTypeID")]
    public int StepTypeID { get; set; }

    [Column("WorkflowID")]
    public int WorkflowID { get; set; }

    [Column("ParentStepID")]
    public int? ParentStepID { get; set; }

    [Column("StepParameters")]
    public string? StepParameters { get; set; } // JSON string containing handler parameters

    [Column("HandlerType")]
    [StringLength(100)]
    public string? HandlerType { get; set; } // Type of step handler to execute

    // Conditional branching properties
    [Column("IsConditional")]
    public bool IsConditional { get; set; } = false;

    [Column("ConditionExpression")]
    public string? ConditionExpression { get; set; } // JSON logic expression

    [Column("TrueStepID")]
    public int? TrueStepID { get; set; } // FK to next step if condition true

    [Column("FalseStepID")]
    public int? FalseStepID { get; set; } // FK to next step if condition false

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
