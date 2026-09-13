using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces
{
    /// <summary>
    /// Repository interface for CallCentre entity operations
    /// Provides contract for CRUD operations on call centre incidents
    /// </summary>
    public interface ICallCentreRepository
    {
        Task<CallCentre?> GetByIdAsync(short callCentreCode);
        Task<IEnumerable<CallCentre>> GetAllAsync();
        Task<IEnumerable<CallCentre>> GetByVehicleAsync(int vmfCode);
        Task<IEnumerable<CallCentre>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<CallCentreReportPage> GetReportPageAsync(CallCentreReportPageQuery query);
        Task<CallCentreCaptureStatistics> GetCaptureStatisticsAsync(
            DateTime startDate,
            DateTime endDate,
            string? captureName
        );
        Task<CallCentre> CreateAsync(CallCentre callCentre, int currentUserId);
        Task<CallCentre> UpdateAsync(CallCentre callCentre, int currentUserId);
        Task DeleteAsync(short callCentreCode, int currentUserId);
    }

    public enum CallCentreReportMode
    {
        OneVehicle,
        AllReference,
        DeptSitePeriod,
        CloReport,
        OpenCalls,
    }

    public enum CallCentreIncidentType
    {
        Accident,
        Hijack,
        Loss,
        Road,
    }

    public sealed record CallCentreReportPageQuery(
        CallCentreReportMode Mode,
        int Page = 1,
        int PageSize = 24,
        int? VmfCode = null,
        CallCentreIncidentType? IncidentType = null,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        int? SiteCode = null,
        string? Department = null
    );

    public sealed record CallCentreReportPage(
        IReadOnlyList<CallCentre> Items,
        int Page,
        int PageSize,
        int Total
    )
    {
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    }

    public sealed record CallCentreCaptureStatistic(string CaptureName, int Count);

    public sealed record CallCentreCaptureStatistics(
        int TotalCalls,
        IReadOnlyList<CallCentreCaptureStatistic> Items
    );
}
