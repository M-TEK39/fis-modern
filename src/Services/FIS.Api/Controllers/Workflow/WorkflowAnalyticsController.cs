using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Controller for workflow analytics and performance reporting
/// </summary>
[ApiController]
// Workflow analytics is an administrative/reporting surface, not a generic
// authenticated data feed. Background metrics use the application service
// directly and do not depend on this controller.
[Authorize(Roles = "Management Reports")]
[Route("api/workflow-analytics")]
public class WorkflowAnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public WorkflowAnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Get real-time dashboard statistics
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatistics>> GetDashboardStatistics()
    {
        var stats = await _analyticsService.GetDashboardStatisticsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Get workflow performance report for a date range
    /// </summary>
    [HttpGet("workflow/{workflowId}/performance")]
    public async Task<ActionResult<WorkflowPerformanceReport>> GetWorkflowPerformance(
        int workflowId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null
    )
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var report = await _analyticsService.GetWorkflowPerformanceAsync(workflowId, start, end);
        return Ok(report);
    }

    /// <summary>
    /// Get step performance report
    /// </summary>
    [HttpGet("step/{stepId}/performance")]
    public async Task<ActionResult<StepPerformanceReport>> GetStepPerformance(int stepId)
    {
        var report = await _analyticsService.GetStepPerformanceAsync(stepId);
        return Ok(report);
    }

    /// <summary>
    /// Detect workflow bottlenecks
    /// </summary>
    [HttpGet("workflow/{workflowId}/bottlenecks")]
    public async Task<ActionResult<List<BottleneckInfo>>> DetectBottlenecks(int workflowId)
    {
        var bottlenecks = await _analyticsService.DetectBottlenecksAsync(workflowId);
        return Ok(bottlenecks);
    }

    /// <summary>
    /// Generate daily metrics for a specific workflow (manual trigger)
    /// </summary>
    [HttpPost("workflow/{workflowId}/generate-metrics")]
    public async Task<IActionResult> GenerateWorkflowMetrics(
        int workflowId,
        [FromQuery] DateTime? date = null
    )
    {
        var targetDate = date ?? DateTime.UtcNow.Date;
        var metric = await _analyticsService.GenerateWorkflowMetricsAsync(workflowId, targetDate);
        return Ok(metric);
    }

    /// <summary>
    /// Get workflow execution history
    /// </summary>
    [HttpGet("workflow/{workflowId}/history")]
    public async Task<ActionResult<WorkflowPerformanceReport>> GetWorkflowHistory(
        int workflowId,
        [FromQuery] int days = 30
    )
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-days);

        var report = await _analyticsService.GetWorkflowPerformanceAsync(
            workflowId,
            startDate,
            endDate
        );
        return Ok(report);
    }
}
