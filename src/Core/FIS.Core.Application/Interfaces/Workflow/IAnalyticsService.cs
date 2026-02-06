using FIS.Core.Domain.Entities.System;

namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Service for tracking and analyzing workflow performance
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Record step execution for analytics
    /// </summary>
    Task<int> RecordStepExecutionAsync(int statusId, int stepId, int workflowId, string? handlerType);

    /// <summary>
    /// Update step execution with completion details
    /// </summary>
    Task CompleteStepExecutionAsync(int executionHistoryId, bool success, string? errorMessage = null);

    /// <summary>
    /// Generate daily metrics for a workflow
    /// </summary>
    Task<WorkflowMetric> GenerateWorkflowMetricsAsync(int workflowId, DateTime date);

    /// <summary>
    /// Get workflow performance report
    /// </summary>
    Task<WorkflowPerformanceReport> GetWorkflowPerformanceAsync(int workflowId, DateTime startDate, DateTime endDate);

    /// <summary>
    /// Get step performance report
    /// </summary>
    Task<StepPerformanceReport> GetStepPerformanceAsync(int stepId);

    /// <summary>
    /// Detect workflow bottlenecks
    /// </summary>
    Task<List<BottleneckInfo>> DetectBottlenecksAsync(int workflowId);

    /// <summary>
    /// Get real-time dashboard statistics
    /// </summary>
    Task<DashboardStatistics> GetDashboardStatisticsAsync();
}

// Report DTOs
public class WorkflowPerformanceReport
{
    public int WorkflowID { get; set; }
    public string? WorkflowName { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public decimal SuccessRate { get; set; }
    public decimal AverageDurationMinutes { get; set; }
    public List<StepPerformanceSummary> StepPerformances { get; set; } = new();
}

public class StepPerformanceReport
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public int TotalExecutions { get; set; }
    public decimal AverageDurationSeconds { get; set; }
    public int FailureCount { get; set; }
    public decimal FailureRate { get; set; }
}

public class StepPerformanceSummary
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public decimal AverageDurationSeconds { get; set; }
    public int ExecutionCount { get; set; }
}

public class BottleneckInfo
{
    public int StepID { get; set; }
    public string? StepName { get; set; }
    public decimal AverageDurationSeconds { get; set; }
    public int PercentOfTotalTime { get; set; }
}

public class DashboardStatistics
{
    public int ActiveWorkflows { get; set; }
    public int CompletedToday { get; set; }
    public int FailedToday { get; set; }
    public decimal AverageCompletionTime { get; set; }
    public List<WorkflowExecutionSummary> RecentExecutions { get; set; } = new();
}
