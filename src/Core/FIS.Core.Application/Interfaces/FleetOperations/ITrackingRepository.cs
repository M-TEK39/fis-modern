using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface ITrackingRepository
{
    Task<Tracking?> GetByIdAsync(short trackCode);
    Task<IEnumerable<Tracking>> GetAllAsync();
    Task<TrackingPage> GetPageAsync(TrackingPageQuery query);
    Task<TrackingPage> GetReportPageAsync(TrackingReportPageQuery query);
    Task<IEnumerable<Tracking>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<Tracking>> GetActiveTrackingAsync();
    Task<Tracking> CreateAsync(Tracking tracking, int currentUserId);
    Task<Tracking> UpdateAsync(Tracking tracking, int currentUserId);
    Task DeleteAsync(short trackCode, int currentUserId);
}

public sealed record TrackingPageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchTerm = null,
    int? VmfCode = null
);

public sealed record TrackingPage(IReadOnlyList<Tracking> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public enum TrackingReportKind
{
    OneVehicle,
    OneDevice,
    AllVehicles,
    AllDevices,
    InstallPeriod,
    SitePeriod,
    DepartmentPeriod,
}

public sealed record TrackingReportPageQuery(
    TrackingReportKind ReportKind,
    int Page = 1,
    int PageSize = 24,
    int? VmfCode = null,
    string? DeviceId = null,
    DateTime StartDate = default,
    DateTime EndDate = default,
    string? TrackerType = null,
    int? SiteCode = null,
    int? DepartmentCode = null,
    bool AllSites = false,
    bool AllDepartments = false
);
