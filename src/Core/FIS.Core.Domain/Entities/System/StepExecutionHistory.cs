using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Tracks detailed execution history for workflow steps
/// </summary>
[Table("StepExecutionHistory", Schema = "Workflow")]
public class StepExecutionHistory
{
    [Key]
    [Column("ExecutionHistoryID")]
    public int ExecutionHistoryID { get; set; }

    [Column("StatusID")]
    public int StatusID { get; set; }

    [Column("StepID")]
    public int StepID { get; set; }

    [Column("WorkflowID")]
    public int WorkflowID { get; set; }

    [Column("StepName")]
    [StringLength(255)]
    public string? StepName { get; set; }

    [Column("HandlerType")]
    [StringLength(100)]
    public string? HandlerType { get; set; }

    [Column("StartedAt")]
    public DateTime StartedAt { get; set; }

    [Column("CompletedAt")]
    public DateTime? CompletedAt { get; set; }

    [Column("DurationSeconds")]
    public int? DurationSeconds { get; set; }

    [Column("ExecutionStatus")]
    [StringLength(50)]
    public string ExecutionStatus { get; set; } = "Running"; // Running, Completed, Failed, Cancelled

    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("InputData")]
    public string? InputData { get; set; } // JSON

    [Column("OutputData")]
    public string? OutputData { get; set; } // JSON

    [Column("ExecutedByUserCode")]
    public int? ExecutedByUserCode { get; set; }

    // Global audit fields
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("StatusID")]
    public virtual Status? Status { get; set; }

    [ForeignKey("StepID")]
    public virtual Step? Step { get; set; }

    [ForeignKey("WorkflowID")]
    public virtual Workflow? Workflow { get; set; }

    [ForeignKey("ExecutedByUserCode")]
    public virtual User? ExecutedByUser { get; set; }
}
