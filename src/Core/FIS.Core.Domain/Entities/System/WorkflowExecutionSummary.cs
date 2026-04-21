using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Summary view of workflow execution for reporting
/// </summary>
[Table("WorkflowExecutionSummary", Schema = "Workflow")]
public class WorkflowExecutionSummary
{
    [Key]
    [Column("SummaryID")]
    public int SummaryID { get; set; }

    [Column("WorkflowID")]
    public int WorkflowID { get; set; }

    [Column("StatusID")]
    public int StatusID { get; set; }

    [Column("WorkflowName")]
    [StringLength(255)]
    public string? WorkflowName { get; set; }

    [Column("StartedAt")]
    public DateTime StartedAt { get; set; }

    [Column("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    [Column("DurationSeconds")]
    public int? DurationSeconds { get; set; }

    [Column("TotalSteps")]
    public int TotalSteps { get; set; }

    [Column("CompletedSteps")]
    public int CompletedSteps { get; set; }

    [Column("CurrentStepName")]
    [StringLength(255)]
    public string? CurrentStepName { get; set; }

    [Column("ExecutionStatus")]
    [StringLength(50)]
    public string ExecutionStatus { get; set; } = "Running"; // Running, Completed, Failed, Cancelled

    [Column("StartedByUserName")]
    [StringLength(255)]
    public string? StartedByUserName { get; set; }

    [Column("NotificationsSent")]
    public int NotificationsSent { get; set; }

    [Column("ErrorCount")]
    public int ErrorCount { get; set; }

    // Global audit fields
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("WorkflowID")]
    public virtual Workflow? Workflow { get; set; }

    [ForeignKey("StatusID")]
    public virtual Status? Status { get; set; }
}
