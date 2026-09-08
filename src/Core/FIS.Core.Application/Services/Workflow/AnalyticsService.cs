using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;
using FIS.Core.Domain.Entities.System;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Analytics service for tracking and analyzing workflow performance
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IStepExecutionHistoryRepository _historyRepository;
    private readonly IWorkflowMetricRepository _metricRepository;
    private readonly IWorkflowExecutionSummaryRepository _summaryRepository;
    private readonly IStepRepository _stepRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        IStepExecutionHistoryRepository historyRepository,
        IWorkflowMetricRepository metricRepository,
        IWorkflowExecutionSummaryRepository summaryRepository,
        IStepRepository stepRepository,
        IWorkflowRepository workflowRepository,
        ICurrentUserContext currentUserContext,
        ILogger<AnalyticsService> logger
    )
    {
        _historyRepository = historyRepository;
        _metricRepository = metricRepository;
        _summaryRepository = summaryRepository;
        _stepRepository = stepRepository;
        _workflowRepository = workflowRepository;
        _currentUserContext = currentUserContext;
        _logger = logger;
    }

    public async Task<int> RecordStepExecutionAsync(
        int statusId,
        int stepId,
        int workflowId,
        string? handlerType
    )
    {
        try
        {
            var step = await _stepRepository.GetByIdAsync(stepId);

            var history = new StepExecutionHistory
            {
                StatusID = statusId,
                StepID = stepId,
                WorkflowID = workflowId,
                StepName = step?.StepName,
                HandlerType = handlerType,
                StartedAt = DateTime.UtcNow,
                ExecutionStatus = "Running",
            };

            var created = await _historyRepository.CreateAsync(history);
            _logger.LogInformation(
                "Recorded step execution: WorkflowID={WorkflowId}, StepID={StepId}, HistoryID={HistoryId}",
                workflowId,
                stepId,
                created.ExecutionHistoryID
            );

            return created.ExecutionHistoryID;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording step execution");
            return 0; // Return 0 to indicate failure
        }
    }

    public async Task CompleteStepExecutionAsync(
        int executionHistoryId,
        bool success,
        string? errorMessage = null
    )
    {
        try
        {
            var history = await _historyRepository.GetByIdAsync(executionHistoryId);
            if (history == null)
                return;

            history.CompletedAt = DateTime.UtcNow;
            history.DurationSeconds = (int)
                (history.CompletedAt.Value - history.StartedAt).TotalSeconds;
            history.ExecutionStatus = success ? "Completed" : "Failed";
            history.ErrorMessage = errorMessage;

            await _historyRepository.UpdateAsync(history);
            _logger.LogInformation(
                "Completed step execution: HistoryID={HistoryId}, Status={Status}",
                executionHistoryId,
                history.ExecutionStatus
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing step execution");
        }
    }

    public async Task<WorkflowMetric> GenerateWorkflowMetricsAsync(int workflowId, DateTime date)
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            var executions = await _historyRepository.GetByWorkflowIdAsync(workflowId);
            var dayExecutions = executions.Where(e => e.StartedAt.Date == date.Date).ToList();

            var metric = new WorkflowMetric
            {
                WorkflowID = workflowId,
                WorkflowName = workflow?.WorkflowName,
                MetricDate = date,
                TotalExecutions = dayExecutions.Count,
                SuccessfulExecutions = dayExecutions.Count(e => e.ExecutionStatus == "Completed"),
                FailedExecutions = dayExecutions.Count(e => e.ExecutionStatus == "Failed"),
                CancelledExecutions = dayExecutions.Count(e => e.ExecutionStatus == "Cancelled"),
                AverageDurationSeconds = dayExecutions.Any(e => e.DurationSeconds.HasValue)
                    ? (decimal)
                        dayExecutions
                            .Where(e => e.DurationSeconds.HasValue)
                            .Average(e => e.DurationSeconds!.Value)
                    : 0,
                MinDurationSeconds = dayExecutions.Any(e => e.DurationSeconds.HasValue)
                    ? dayExecutions
                        .Where(e => e.DurationSeconds.HasValue)
                        .Min(e => e.DurationSeconds!.Value)
                    : null,
                MaxDurationSeconds = dayExecutions.Any(e => e.DurationSeconds.HasValue)
                    ? dayExecutions
                        .Where(e => e.DurationSeconds.HasValue)
                        .Max(e => e.DurationSeconds!.Value)
                    : null,
                TotalStepsExecuted = dayExecutions.Count,
                AverageStepsPerWorkflow = dayExecutions.Any()
                    ? dayExecutions.Count
                        / (decimal)dayExecutions.Select(e => e.WorkflowID).Distinct().Count()
                    : 0,
            };

            // Detect bottleneck
            var stepPerformance = dayExecutions
                .Where(e => e.DurationSeconds.HasValue)
                .GroupBy(e => e.StepID)
                .Select(g => new
                {
                    StepID = g.Key,
                    AvgDuration = g.Average(e => e.DurationSeconds!.Value),
                })
                .OrderByDescending(s => s.AvgDuration)
                .FirstOrDefault();

            if (stepPerformance != null)
            {
                metric.BottleneckStepID = stepPerformance.StepID;
            }

            await _metricRepository.CreateAsync(
                metric,
                _currentUserContext.GetCurrentUserIdOrDefault()
            ); // System-generated
            return metric;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating workflow metrics");
            throw;
        }
    }

    public async Task<WorkflowPerformanceReport> GetWorkflowPerformanceAsync(
        int workflowId,
        DateTime startDate,
        DateTime endDate
    )
    {
        try
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            var executions = await _historyRepository.GetByWorkflowIdAsync(workflowId);
            var periodExecutions = executions
                .Where(e => e.StartedAt >= startDate && e.StartedAt <= endDate)
                .ToList();

            var report = new WorkflowPerformanceReport
            {
                WorkflowID = workflowId,
                WorkflowName = workflow?.WorkflowName,
                TotalExecutions = periodExecutions.Count,
                SuccessfulExecutions = periodExecutions.Count(e =>
                    e.ExecutionStatus == "Completed"
                ),
                FailedExecutions = periodExecutions.Count(e => e.ExecutionStatus == "Failed"),
                SuccessRate = periodExecutions.Any()
                    ? (decimal)periodExecutions.Count(e => e.ExecutionStatus == "Completed")
                        / periodExecutions.Count
                        * 100
                    : 0,
                AverageDurationMinutes = periodExecutions.Any(e => e.DurationSeconds.HasValue)
                    ? (decimal)
                        periodExecutions
                            .Where(e => e.DurationSeconds.HasValue)
                            .Average(e => e.DurationSeconds!.Value) / 60
                    : 0,
            };

            // Step performance breakdown
            var stepPerformances = periodExecutions
                .Where(e => e.DurationSeconds.HasValue)
                .GroupBy(e => new { e.StepID, e.StepName })
                .Select(g => new StepPerformanceSummary
                {
                    StepID = g.Key.StepID,
                    StepName = g.Key.StepName,
                    AverageDurationSeconds = (decimal)g.Average(e => e.DurationSeconds!.Value),
                    ExecutionCount = g.Count(),
                })
                .OrderByDescending(s => s.AverageDurationSeconds)
                .ToList();

            report.StepPerformances = stepPerformances;

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow performance");
            throw;
        }
    }

    public async Task<StepPerformanceReport> GetStepPerformanceAsync(int stepId)
    {
        try
        {
            var step = await _stepRepository.GetByIdAsync(stepId);
            var executions = await _historyRepository.GetByStepIdAsync(stepId);

            var report = new StepPerformanceReport
            {
                StepID = stepId,
                StepName = step?.StepName,
                TotalExecutions = executions.Count(),
                AverageDurationSeconds = executions.Any(e => e.DurationSeconds.HasValue)
                    ? (decimal)
                        executions
                            .Where(e => e.DurationSeconds.HasValue)
                            .Average(e => e.DurationSeconds!.Value)
                    : 0,
                FailureCount = executions.Count(e => e.ExecutionStatus == "Failed"),
                FailureRate = executions.Any()
                    ? (decimal)executions.Count(e => e.ExecutionStatus == "Failed")
                        / executions.Count()
                        * 100
                    : 0,
            };

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting step performance");
            throw;
        }
    }

    public async Task<List<BottleneckInfo>> DetectBottlenecksAsync(int workflowId)
    {
        try
        {
            var executions = await _historyRepository.GetByWorkflowIdAsync(workflowId);
            var completedExecutions = executions.Where(e => e.DurationSeconds.HasValue).ToList();

            if (!completedExecutions.Any())
                return new List<BottleneckInfo>();

            var totalTime = completedExecutions.Sum(e => e.DurationSeconds!.Value);

            var bottlenecks = completedExecutions
                .GroupBy(e => new { e.StepID, e.StepName })
                .Select(g => new BottleneckInfo
                {
                    StepID = g.Key.StepID,
                    StepName = g.Key.StepName,
                    AverageDurationSeconds = (decimal)g.Average(e => e.DurationSeconds!.Value),
                    PercentOfTotalTime = (int)(
                        g.Sum(e => e.DurationSeconds!.Value) / (decimal)totalTime * 100
                    ),
                })
                .OrderByDescending(b => b.AverageDurationSeconds)
                .Take(5)
                .ToList();

            return bottlenecks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting bottlenecks");
            throw;
        }
    }

    public async Task<DashboardStatistics> GetDashboardStatisticsAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var activeSummaries = await _summaryRepository.GetActiveExecutionsAsync();
            var allSummaries = await _summaryRepository.GetAllAsync();
            var todaySummaries = allSummaries.Where(s => s.StartedAt.Date == today).ToList();

            var stats = new DashboardStatistics
            {
                ActiveWorkflows = activeSummaries.Count(),
                CompletedToday = todaySummaries.Count(s => s.ExecutionStatus == "Completed"),
                FailedToday = todaySummaries.Count(s => s.ExecutionStatus == "Failed"),
                AverageCompletionTime = todaySummaries.Any(s => s.DurationSeconds.HasValue)
                    ? (decimal)
                        todaySummaries
                            .Where(s => s.DurationSeconds.HasValue)
                            .Average(s => s.DurationSeconds!.Value) / 60
                    : 0,
                RecentExecutions = (await _summaryRepository.GetRecentExecutionsAsync(10)).ToList(),
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard statistics");
            throw;
        }
    }
}
