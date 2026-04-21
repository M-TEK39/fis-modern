using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Aggregated metrics for workflow performance analysis
/// </summary>
[Table("WorkflowMetric", Schema = "Workflow")]
public class WorkflowMetric
{
    [Key]
    [Column("MetricID")]
    public int MetricID { get; set; }

    [Column("WorkflowID")]
    public int WorkflowID { get; set; }

    [Column("WorkflowName")]
    [StringLength(255)]
    public string? WorkflowName { get; set; }

    [Column("MetricDate")]
    public DateTime MetricDate { get; set; }

    [Column("TotalExecutions")]
    public int TotalExecutions { get; set; }

    [Column("SuccessfulExecutions")]
    public int SuccessfulExecutions { get; set; }

    [Column("FailedExecutions")]
    public int FailedExecutions { get; set; }

    [Column("CancelledExecutions")]
    public int CancelledExecutions { get; set; }

    [Column("AverageDurationSeconds")]
    public decimal? AverageDurationSeconds { get; set; }

    [Column("MinDurationSeconds")]
    public int? MinDurationSeconds { get; set; }

    [Column("MaxDurationSeconds")]
    public int? MaxDurationSeconds { get; set; }

    [Column("TotalStepsExecuted")]
    public int TotalStepsExecuted { get; set; }

    [Column("AverageStepsPerWorkflow")]
    public decimal? AverageStepsPerWorkflow { get; set; }

    [Column("NotificationsSent")]
    public int NotificationsSent { get; set; }

    [Column("BottleneckStepID")]
    public int? BottleneckStepID { get; set; } // Step that takes longest on average

    // Global audit fields
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("created_by_user_code")]
    public int? created_by_user_code { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("WorkflowID")]
    public virtual Workflow? Workflow { get; set; }

    [ForeignKey("BottleneckStepID")]
    public virtual Step? BottleneckStep { get; set; }

    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }
}
