using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface IAssetVerificationRepository
{
    Task<AssetVerification?> GetByIdAsync(int verificationCode);
    Task<IEnumerable<AssetVerification>> GetAllAsync();
    Task<AssetVerificationPage> GetPageAsync(AssetVerificationPageQuery query);
    Task<AssetVerificationReportPage> GetReportPageAsync(
        AssetVerificationReportPageQuery query,
        CancellationToken cancellationToken = default
    );
    Task<IEnumerable<AssetVerification>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<AssetVerification>> GetBySiteAsync(int siteCode);
    Task<IEnumerable<AssetVerification>> GetByStatusAsync(string status);
    Task<AssetVerification> CreateAsync(AssetVerification verification, int currentUserId);
    Task<AssetVerification> UpdateAsync(AssetVerification verification, int currentUserId);
    Task DeleteAsync(int verificationCode, int currentUserId);
}

public sealed record AssetVerificationPageQuery(int Page = 1, int PageSize = 24);

public sealed record AssetVerificationPage(
    IReadOnlyList<AssetVerification> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public enum AssetVerificationReportMode
{
    PerSiteProvinceDate,
    VerifiedByDateRange,
    NotVerified,
}

public sealed record AssetVerificationReportPageQuery(
    AssetVerificationReportMode ReportMode,
    short? SiteCode = null,
    string? Province = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 24,
    bool IncludeAll = false
);

public sealed record AssetVerificationReportRow(
    int? AssetVerificationCode,
    string? VehicleRegNo,
    int? VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    string? DepartmentName,
    string? SiteName,
    short? SiteCode,
    string? Province,
    string? VehicleMake,
    string? VehicleModel,
    DateTime? LicenceExpiryDate,
    DateTime? DateLastVerified,
    string? Status,
    string? ResponsibleManager,
    int? CurrentKm,
    string? Barcode
);

public sealed record AssetVerificationReportPage(
    IReadOnlyList<AssetVerificationReportRow> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
