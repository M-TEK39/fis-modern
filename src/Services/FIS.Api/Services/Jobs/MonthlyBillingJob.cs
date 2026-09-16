using Hangfire;

namespace FIS.Api.Services;

/// <summary>
/// Hangfire entry point for the database-owned legacy contract billing
/// scheduler. The historical class name is retained for the existing job key.
/// </summary>
public sealed class MonthlyBillingJob
{
    private readonly LegacyContractBillingSchedulerService _legacyScheduler;

    public MonthlyBillingJob(LegacyContractBillingSchedulerService legacyScheduler)
    {
        _legacyScheduler = legacyScheduler;
    }

    /// <summary>
    /// Runs the legacy database-owned scheduler. Despite the historical class
    /// name, this must run daily because the legacy scheduler itself handles
    /// first-of-month recreation and daily uncharged-contract updates.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public Task RunAsync() => _legacyScheduler.RunAsync();
}
