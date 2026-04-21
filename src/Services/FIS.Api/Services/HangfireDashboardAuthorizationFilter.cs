using Hangfire.Dashboard;

namespace FIS.Api.Services;

/// <summary>
/// Authorization filter for Hangfire dashboard (development only)
/// </summary>
public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Allow access in development environment
        // In production, implement proper authentication
        return true;
    }
}
