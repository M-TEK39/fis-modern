using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Interfaces.Workflow;

namespace FIS.Api.Services;

/// <summary>
/// Background job for generating daily workflow metrics
/// </summary>
public class WorkflowMetricsJob
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly ILogger<WorkflowMetricsJob> _logger;

    public WorkflowMetricsJob(
        IAnalyticsService analyticsService,
        IWorkflowRepository workflowRepository,
        ILogger<WorkflowMetricsJob> logger)
    {
        _analyticsService = analyticsService;
        _workflowRepository = workflowRepository;
        _logger = logger;
    }

    /// <summary>
    /// Generate daily metrics for all active workflows
    /// </summary>
    public async Task GenerateDailyMetricsAsync()
    {
        _logger.LogInformation("Starting daily workflow metrics generation");
        
        var workflows = await _workflowRepository.GetAllAsync();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var processed = 0;
        var errors = 0;

        foreach (var workflow in workflows.Where(w => !w.is_deleted))
        {
            try
            {
                await _analyticsService.GenerateWorkflowMetricsAsync(workflow.WorkflowID, yesterday);
                processed++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Error generating metrics for workflow {WorkflowId}", workflow.WorkflowID);
            }
        }

        _logger.LogInformation(
            "Completed daily workflow metrics generation. Processed: {Processed}, Errors: {Errors}", 
            processed, errors);
    }
}
