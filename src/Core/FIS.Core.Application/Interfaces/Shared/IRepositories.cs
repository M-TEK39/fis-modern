using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Core.Domain.Entities.Financial;
using FIS.Core.Domain.Entities.Operations;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Domain.Entities.System;
using FIS.Core.Domain.Entities.Vehicles;
using TypeEntity = FIS.Core.Domain.Entities.ReferenceData.VehicleType;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for vehicle operations
/// </summary>
public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(
        int vmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<Vehicle?> GetByFleetNumberAsync(
        string fleetNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<Vehicle?> GetByRegistrationNumberAsync(
        string registrationNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IEnumerable<Vehicle>> GetActiveVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<VehicleMasterSnapshotPage> GetSnapshotPageAsync(
        int page,
        int pageSize,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<RenumberedVehicleReportPage> GetRenumberedVehicleReportPageAsync(int page, int pageSize);
    Task<VehicleLookupPage> GetVehicleLookupPageAsync(
        string? keyword,
        string? searchMode,
        int page,
        int pageSize,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IEnumerable<Vehicle>> GetAvailableVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IEnumerable<Vehicle>> GetAllAsync(
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IEnumerable<Vehicle>> SearchVehiclesAsync(
        string searchTerm,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IEnumerable<Vehicle>> GetByInvoiceNumberAsync(
        string invoiceNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<Vehicle> CreateAsync(Vehicle vehicle, int currentUserId);
    Task UpdateAsync(Vehicle vehicle, int currentUserId);
    Task UpdateLicenceFieldsAsync(int vmfCode, VehicleLicenceUpdate update, int currentUserId);
    Task AddLicenceReceiveNoteAsync(int vmfCode, string username, int currentUserId);
    Task DeleteAsync(int vmfCode, int currentUserId);
}

public sealed record VehicleMasterSnapshotPage(
    IReadOnlyList<Vehicle> Data,
    int Page,
    int PageSize,
    int TotalRecords
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public sealed record RenumberedVehicleReportRow(
    int OldVmfCode,
    string? OldFleetNumber,
    string? OldStatusDescription,
    string? NewFleetNumber,
    string? NewStatusDescription
);

public sealed record RenumberedVehicleReportPage(
    IReadOnlyList<RenumberedVehicleReportRow> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Server-paginated vehicle lookup data for the vehicle-photo search table.
/// The description fields are nullable because their legacy lookup tables may
/// be unavailable on a compatible database. No replacement value is inferred.
/// MakeAndModel is the existing model description; the legacy photo lookup did
/// not expose a combined make/model value.
/// </summary>
public sealed record VehicleLookupPageItem(
    int VmfCode,
    string? GgNumber,
    string? RegistrationNumber,
    string? MakeAndModel,
    short? YearManufactured,
    string? Colour,
    string? HireType,
    string? Status,
    string? HiredFrom,
    DateTime? StatusDate,
    short ModelCode
);

public sealed record VehicleLookupPage(
    IReadOnlyList<VehicleLookupPageItem> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Values written by the legacy licence capture workflow. The repository
/// updates only columns that exist in the connected vehicle_master table.
/// </summary>
public sealed record VehicleLicenceUpdate(
    DateTime? LicenceDueDate,
    string? LicenceRegisterNumber,
    string? LicenceRegistrationDocument,
    int? Tare,
    string? LicenceReceiver,
    string? LicenceReceiverId,
    string? LicenceReceiverTelephone,
    short? LicenceReceiverSite,
    DateTime? LicenceDateTaken,
    string? CofRequired,
    DateTime? CofLastDone,
    string? LicenceComments
);

/// <summary>
/// Repository interface for GG block number range maintenance.
/// </summary>
public interface IGgBlockRepository
{
    Task<GgBlockHistoryPage> GetHistoryAsync(int page, int pageSize);
    Task<GgBlockHistoryRecord> CreateAsync(
        string startGgNumber,
        string endGgNumber,
        int currentUserId
    );
}

public sealed record GgBlockHistoryRecord(
    short BlockId,
    string CapturedBy,
    DateTime? DateCreated,
    string StartGgNumber,
    string EndGgNumber
);

public sealed record GgBlockHistoryPage(
    IReadOnlyList<GgBlockHistoryRecord> Items,
    int Page,
    int PageSize,
    int TotalRecords
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public sealed class GgBlockRangeConflictException : Exception
{
    public GgBlockRangeConflictException()
        : base("The requested GG block range overlaps an existing range.") { }
}

/// <summary>
/// Repository interface for vehicle source maintenance.
/// </summary>
public interface IVehicleSourceRepository
{
    Task<VehicleSourcePage> GetPageAsync();
    Task<VehicleSourceCapabilities> GetCapabilitiesAsync();
    Task<VehicleSourceRecord?> GetByIdAsync(byte sourceCode);
    Task<VehicleSourceRecord> CreateAsync(VehicleSourceInput source, int currentUserId);
    Task<VehicleSourceRecord> UpdateAsync(
        byte sourceCode,
        VehicleSourceInput source,
        int currentUserId
    );
}

public sealed record VehicleSourceInput(
    string Name,
    string PhysicalAddress,
    string PostalAddress,
    string TelephoneNumber,
    string FaxNumber,
    string EmailAddress,
    string ContactPerson
);

public sealed record VehicleSourceRecord(
    byte SourceCode,
    string? Name,
    string? PhysicalAddress,
    string? PostalAddress,
    string? TelephoneNumber,
    string? FaxNumber,
    string? EmailAddress,
    string? ContactPerson,
    DateTime? DateCreated,
    DateTime? DateUpdated,
    int? CreatedByUserCode,
    int? ModifiedByUserCode
);

public sealed record VehicleSourceCapabilities(bool HasEmailAddress, bool HasContactPerson);

public sealed record VehicleSourcePage(
    IReadOnlyList<VehicleSourceRecord> Items,
    VehicleSourceCapabilities Capabilities
);

public sealed class VehicleSourceFieldUnavailableException : Exception
{
    public VehicleSourceFieldUnavailableException(string fieldName)
        : base($"The vehicle source {fieldName} field is not available in this database.")
    {
        FieldName = fieldName;
    }

    public string FieldName { get; }
}

/// <summary>
/// Repository interface for the vehicle status report. The report is read
/// directly from the legacy vehicle/reference columns so expanded columns do
/// not make an older client database unusable.
/// </summary>
public interface IVehicleStatusReportRepository
{
    Task<VehicleStatusReportPage> GetPageAsync(VehicleStatusReportQuery query);
}

public sealed record VehicleStatusReportQuery(
    int Page = 1,
    int PageSize = 24,
    byte? VehicleSourceCode = null,
    short? TypeCode = null,
    short? LocationCode = null,
    short? MakeCode = null,
    short? ModelCode = null,
    short? VehicleStatusCode = null,
    string? Search = null
);

public sealed record VehicleStatusReportLookup(int Code, string Description);

public sealed record VehicleStatusReportModelLookup(int Code, string Description, int? MakeCode);

public sealed record VehicleStatusReportRemark(
    int RemarkId,
    string? Category,
    string? Text,
    DateTime? DateCreated
);

public sealed record VehicleStatusReportVehicle(
    int VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    short? VehicleStatusCode,
    short? TypeCode,
    byte? VehicleSourceCode,
    short? ModelCode,
    short? LocationCode,
    string? ChassisNumber,
    string? EngineNumber,
    short? YearManufactured,
    DateTime? TakeOnDate,
    string? InvoiceNumber,
    DateTime? DateCreated,
    int? CurrentOdometer,
    VehicleStatusReportRemark? ActiveRemark
);

public sealed record VehicleStatusReportPage(
    IReadOnlyList<VehicleStatusReportVehicle> Vehicles,
    IReadOnlyList<VehicleStatusReportLookup> Sites,
    IReadOnlyList<VehicleStatusReportLookup> Types,
    IReadOnlyList<VehicleStatusReportLookup> Makes,
    IReadOnlyList<VehicleStatusReportModelLookup> Models,
    IReadOnlyList<VehicleStatusReportLookup> Statuses,
    bool RemarksAvailable,
    int Page,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

/// <summary>
/// Repository interface for the recovered-vehicle renumbering workflow.
/// Updating a recovered vehicle changes the original vehicle, creates a new
/// vehicle row, and appends the legacy history records in one transaction.
/// </summary>
public interface IRecoveredVehicleRepository
{
    Task<IReadOnlyList<RecoveredVehicleSearchRecord>> SearchAsync(
        string searchTerm,
        bool byRegistration
    );
    Task<RecoveredVehicleDetails?> GetDetailsAsync(int vmfCode);
    Task<RecoveredVehicleUpdateResult> UpdateAsync(
        RecoveredVehicleUpdate update,
        int currentUserId
    );
}

public sealed record RecoveredVehicleSearchRecord(
    int VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    short VehicleStatusCode,
    string? StatusDescription,
    string? RenumberedTo
);

public sealed record RecoveredVehicleStatusOption(short Code, string Description);

public sealed record RecoveredVehicleDetails(
    int VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    short VehicleStatusCode,
    string? StatusDescription,
    string? RenumberedTo,
    string? PreviousFleetNumber,
    DateTime? PreviousDateChanged,
    IReadOnlyList<RecoveredVehicleStatusOption> StatusOptions
);

public sealed record RecoveredVehicleUpdate(
    int VmfCode,
    string RecoveredFleetNumber,
    DateTime DateChanged,
    short NewStatusCode
);

public sealed record RecoveredVehicleUpdateResult(
    RecoveredVehicleDetails UpdatedVehicle,
    int NewVmfCode
);

/// <summary>
/// Repository interface for the legacy Demo Vehicles module. The demo vehicle
/// table exists in both the original client schema and the expanded schema,
/// so the implementation must negotiate optional audit columns at runtime.
/// </summary>
public interface IDemoVehicleRepository
{
    Task<IReadOnlyList<DemoVehicleRecord>> GetAllAsync();
    Task<DemoVehiclePage> GetPageAsync(int page = 1, int pageSize = 24);
    Task<IReadOnlyList<DemoVehicleRecord>> SearchAsync(string searchTerm, bool byRegistration);
    Task<DemoVehicleRecord?> GetByIdAsync(int demoVehicleCode);
    Task<DemoVehicleRecord> CreateAsync(DemoVehicleInput input, int currentUserId);
    Task<DemoVehicleRecord> UpdateAsync(
        int demoVehicleCode,
        DemoVehicleInput input,
        int currentUserId
    );
    Task DeleteAsync(int demoVehicleCode, int currentUserId);
}

public sealed record DemoVehicleInput(
    string? GgNumber,
    string? RegistrationNumber,
    string ModelDescription,
    short? SiteCode,
    int? YearManufactured,
    string? BankCode,
    short? Tank,
    string? Colour,
    string? EngineNumber,
    string? ChassisNumber
);

public sealed record DemoVehicleRecord(
    int DemoVehicleCode,
    string? GgNumber,
    string? RegistrationNumber,
    string? ModelDescription,
    int? YearManufactured,
    short? SiteCode,
    string? SiteDescription,
    string? BankCode,
    short? Tank,
    string? Colour,
    string? EngineNumber,
    string? ChassisNumber,
    DateTime? DateCreated,
    DateTime? DateUpdated,
    int? CreatedByUserCode,
    int? ModifiedByUserCode
);

public sealed record DemoVehiclePage(
    IReadOnlyList<DemoVehicleRecord> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for vehicle authorization (pre-capture) operations
/// </summary>
public interface IVehicleAuthorizationRepository
{
    Task<PreVehicleMaster?> GetByIdAsync(
        int tempVmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<PreVehicleMaster?> GetByChassisNumberAsync(
        string chassisNumber,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IReadOnlyList<PreVehicleMaster>> SearchPreVehiclesAsync(
        string chassisNo,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<VehicleAuthorizationPage> GetPendingAuthorizationsAsync(
        int page = 1,
        int pageSize = 24,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<VehicleAuthorizationPage> GetAuthorizedVehiclesAsync(
        int page = 1,
        int pageSize = 24,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<VehicleAuthorizationPage> GetRejectedVehiclesAsync(
        int page = 1,
        int pageSize = 24,
        int? capturedByUserCode = null,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IEnumerable<PreVehicleMaster>> GetByStatusAsync(string status);
    Task<IEnumerable<PreVehicleMaster>> GetAuthorizationHistoryAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? capturedByUserCode = null,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IReadOnlyList<VehicleMaintenanceTypeOption>> GetMaintenanceTypesAsync();
    Task<PreVehicleMaster> CreateAsync(PreVehicleMaster vehicleAuth, int currentUserId);
    Task UpdateAsync(PreVehicleMaster vehicleAuth, int currentUserId);
    Task<VehicleAuthorizationApprovalResult> ApproveAsync(
        int tempVmfCode,
        int authorizedByUserId,
        string? comment = null
    );
    Task RejectAsync(
        int tempVmfCode,
        int rejectedByUserId,
        string rejectionReason,
        string? comment = null
    );
    Task AddCommentAsync(int tempVmfCode, string comment, int modifiedByUserId);
    Task DeleteAsync(int tempVmfCode, int currentUserId);
    Task ClearFromAuthorityListAsync(int tempVmfCode);
    Task<VehicleAuthorizationPrintSnapshot?> GetPrintSnapshotAsync(
        int tempVmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null,
        int? currentUserId = null
    );
    Task<IReadOnlyList<VehicleStatusComment>> GetVehicleStatusCommentsAsync(
        string chassisNumber
    );
}

public sealed record VehicleAuthorizationApprovalResult(
    string? AllocatedGgNumber,
    int? AvailableGgNumbers,
    int? ReturnStatus
);

public sealed record VehicleStatusComment(
    string? CapturedBy,
    DateTime? CommentDate,
    string? AuthorityStatus,
    string? Comment
);

public sealed record VehicleAuthorizationPrintSnapshot(
    PreVehicleMaster Vehicle,
    IReadOnlyList<string> Extras,
    string? SiteName,
    string? LocationDescription,
    string? HiredFromDescription,
    string? HireTypeDescription,
    string? StatusDescription,
    string? CapturedByUserName,
    DateTime? CapturedAt,
    string? AuthorizedByUserName,
    DateTime? AuthorizedAt
);

public sealed record VehicleAuthorizationPage(
    IReadOnlyList<PreVehicleMaster> Data,
    int Page,
    int PageSize,
    int TotalRecords
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public sealed record VehicleMaintenanceTypeOption(short Code, string Name);

/// <summary>
/// Repository interface for the contract audit log (append-only event trail)
/// </summary>
public interface IContractAuditLogRepository
{
    Task<IEnumerable<ContractAuditLog>> GetByContractAsync(int contractCode);
    Task LogAsync(
        int contractCode,
        string action,
        int performedByUserId,
        short? oldStatus = null,
        short? newStatus = null,
        string? notes = null,
        string? fieldChanged = null,
        string? oldValue = null,
        string? newValue = null
    );
}

/// <summary>
/// Repository interface for contract operations
/// </summary>
public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(int contractCode);
    Task<IEnumerable<Contract>> GetActiveContractsAsync();
    Task<IEnumerable<Contract>> GetContractsByVehicleAsync(int vmfCode);
    Task<IEnumerable<Contract>> GetAllAsync();
    Task<ContractPage> GetPageAsync(ContractPageQuery query);
    Task<IEnumerable<ContractVehicleLookup>> SearchVehiclesForContractsAsync(
        string searchTerm,
        IReadOnlyCollection<short>? allowedSiteCodes = null
    );
    Task<ContractVehicleLookup?> GetVehicleForContractAsync(
        int vmfCode,
        IReadOnlyCollection<short>? allowedSiteCodes = null
    );
    Task<Contract?> GetActiveContractByVehicleAsync(int vmfCode);
    Task<bool> HasActiveContractAsync(int vmfCode);
    /// <summary>
    /// Creates an ordinary pending contract through the legacy approval
    /// procedure. The procedure owns status normalization, contract-group
    /// initialization, status history, and the database transaction/trigger
    /// chain; callers must not substitute a direct insert when it is absent.
    /// </summary>
    Task<Contract> CreateForApprovalAsync(Contract contract, int currentUserId);
    Task<Contract> CreateAsync(Contract contract, int currentUserId);
    Task UpdateAsync(Contract contract, int currentUserId);
    /// <summary>
    /// Updates historical contract dates and odometers through the archived
    /// follow-up procedure. The procedure owns the linked follow-up contract,
    /// billing, journal, and transaction behavior; callers must not replace
    /// it with a generic contract update.
    /// </summary>
    Task<Contract> UpdateHistoryAsync(
        int contractCode,
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        string fleetNumber
    );
    Task<Contract> UpdatePendingForApprovalAsync(Contract contract, int currentUserId);
    Task<Contract> UpdatePendingDecisionAsync(Contract contract, int currentUserId);
    Task<Contract> ActivatePendingAsync(
        Contract pendingContract,
        int existingContractCode,
        int currentUserId
    );
    Task<Contract> ReassignExistingAsync(
        Contract existingContract,
        Contract reassignment,
        int currentUserId
    );
    Task<Contract> ExtendExistingAsync(Contract contract, int currentUserId);
    Task DeleteAsync(int contractCode, int currentUserId);
    Task EndContractAsync(
        int contractCode,
        DateTime endDate,
        int currentUserId,
        int? endOdometer = null,
        string? notes = null
    );
}

public sealed record ContractPageQuery(
    int Page = 1,
    int PageSize = 24,
    short? StatusCode = null,
    short? SiteCode = null,
    string? StillCurrent = null,
    DateTime? StartDateFrom = null,
    DateTime? StartDateTo = null,
    int? VmfCode = null,
    IReadOnlyCollection<short>? AllowedSiteCodes = null,
    int? OwnerUserCode = null
);

public sealed record ContractPage(
    IReadOnlyList<Contract> Items,
    int TotalRecords,
    int Page,
    int PageSize
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public sealed record ContractVehicleLookup(
    int VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    string? ChassisNumber,
    string? EngineNumber,
    string? InvoiceNumber,
    short? VehicleStatusCode = null,
    short? SiteCode = null
);

/// <summary>
/// Repository interface for user operations
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int userAccessCode);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByTelephoneAsync(string telephone);
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User> CreateAsync(User user, int currentUserId);
    Task UpdateAsync(User user, int currentUserId);
    Task DeleteAsync(int userAccessCode, int currentUserId);
}

/// <summary>
/// Repository interface for user profile operations (user_access_old1 table)
/// </summary>
public interface IUserProfileRepository
{
    Task<UserAccessOld?> GetByIdAsync(short userAccessCode);
    Task<UserAccessOld?> GetByFirstNameAsync(string firstName);
    Task<UserAccessOld?> GetByEmailAsync(string email);
    Task<IEnumerable<UserAccessOld>> GetAllActiveAsync();
    Task<UserProfileAdministrationPage> GetAdministrationPageAsync(
        string alphabet,
        int page,
        int pageSize
    );
    Task<IEnumerable<UserAccessOld>> GetBySiteAsync(short siteCode);
    Task<IEnumerable<UserAccessOld>> SearchAsync(string searchTerm);
    Task<UserAccessOld> CreateAsync(UserAccessOld userProfile, int currentUserId);
    Task UpdateAsync(UserAccessOld userProfile, int currentUserId);
    Task DeleteAsync(short userAccessCode, int currentUserId);
    Task<bool> ValidateCredentialsAsync(string firstName, string password);
}

public sealed record UserProfileAdministrationPage(
    IReadOnlyList<UserAccessOld> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for access level operations (bitwise permissions)
/// </summary>
public interface IAccessLevelRepository
{
    Task<AccessLevel?> GetByIdAsync(short accessLevelId);
    Task<AccessLevel?> GetByNameAsync(string accessLevelName);
    Task<IEnumerable<AccessLevel>> GetAllAsync();
    Task<AccessLevel> CreateAsync(AccessLevel accessLevel, int currentUserId);
    Task UpdateAsync(AccessLevel accessLevel, int currentUserId);
    Task DeleteAsync(short accessLevelId, int currentUserId);
    Task<bool> UserHasPermissionAsync(long userAccessLevel, string permissionName);
    Task<IEnumerable<string>> GetUserPermissionsAsync(long userAccessLevel);
}

/// <summary>
/// Repository interface for site operations
/// </summary>
public interface ISiteRepository
{
    Task<Site?> GetByIdAsync(int siteCode);
    Task<Site?> GetByNameAsync(string siteName);
    Task<IEnumerable<Site>> GetActiveSitesAsync();
    Task<SitePage> GetPageAsync(int page = 1, int pageSize = 24);
    Task<IEnumerable<Site>> SearchSitesAsync(string searchTerm);
    Task<SiteDeleteCheck> GetDeleteCheckAsync(int siteCode);
    Task<bool> HasActiveContractsAsync(int siteCode);
    Task<Site> CreateAsync(Site site, int currentUserId);
    Task UpdateAsync(Site site, int currentUserId);
    Task DeleteAsync(int siteCode, int currentUserId);
}

public sealed record SiteDeleteCheck(int ContractCount)
{
    public bool CanDelete => ContractCount == 0;
}

public sealed record SitePage(IReadOnlyList<Site> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for department operations
/// </summary>
public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(int departmentCode);
    Task<Department?> GetByNameAsync(string departmentName);
    Task<IEnumerable<Department>> GetAllAsync();
    Task<DepartmentPage> GetPageAsync(int page = 1, int pageSize = 24);
    Task<IEnumerable<Department>> GetActiveDepartmentsAsync();
    Task<IEnumerable<Department>> GetByCompanyAsync(int companyCode);
    Task<IEnumerable<Department>> SearchDepartmentsAsync(string searchTerm);
    Task<DepartmentDeleteCheck> GetDeleteCheckAsync(int departmentCode);
    Task<bool> HasActiveContractsAsync(int departmentCode);
    Task<Department> CreateAsync(Department department, int currentUserId);
    Task UpdateAsync(Department department, int currentUserId);
    Task DeleteAsync(int departmentCode, int currentUserId);
}

public sealed record DepartmentDeleteCheck(int SiteCount, int LogsheetCount)
{
    public bool CanDelete => SiteCount == 0 && LogsheetCount == 0;
}

public sealed record DepartmentPage(
    IReadOnlyList<Department> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for driver operations
/// </summary>
public interface IDriverRepository
{
    Task<Driver?> GetByIdAsync(string driverId);
    Task<Driver?> GetByLicenceNumberAsync(string licenceNumber);
    Task<IEnumerable<Driver>> GetActiveDriversAsync();
    Task<IEnumerable<Driver>> GetAllDriversAsync();
    Task<IEnumerable<Driver>> SearchDriversAsync(string searchTerm);
    Task<Driver> CreateAsync(Driver driver, int currentUserId);
    Task UpdateAsync(Driver driver, int currentUserId);
    Task DeleteAsync(string driverId, int currentUserId);
}

/// <summary>
/// Repository interface for trip operations
/// </summary>
public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(int tripId, IReadOnlySet<short>? allowedSiteCodes = null);
    Task<TripAuthorityDetails?> GetDetailsAsync(int tripId, IReadOnlySet<short>? allowedSiteCodes = null);
    Task<IEnumerable<Trip>> GetAllAsync(IReadOnlySet<short>? allowedSiteCodes = null);
    Task<TripSummaryPage> GetTripSummaryPageAsync(TripSummaryPageQuery query);
    Task<IEnumerable<TripAuthorityVehicle>> GetTripAuthorityVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null
    );
    Task<TripAuthorityVehiclePage> GetTripAuthorityInServicePageAsync(
        TripAuthorityVehiclePageQuery query
    );
    Task<TripAuthorityVehiclePage> GetTripAuthorityOutPageAsync(
        TripAuthorityVehiclePageQuery query
    );
    Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode, IReadOnlySet<short>? allowedSiteCodes = null);
    Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId, IReadOnlySet<short>? allowedSiteCodes = null);
    Task<IEnumerable<Trip>> GetTripsByContractAsync(int contractCode, IReadOnlySet<short>? allowedSiteCodes = null);
    /// <summary>
    /// Checks for unexpired trip authorities using the archived
    /// NEW_DEV_VAL_OpenTripAuthority procedure when it is available.
    /// </summary>
    Task<bool> HasOpenTripAuthoritiesAsync(int contractCode);
    Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(DateTime startDate, DateTime endDate, IReadOnlySet<short>? allowedSiteCodes = null);
    Task<Trip> CreateAsync(Trip trip, int currentUserId);
    Task<Trip> CreateAuthorityAsync(
        Trip trip,
        IReadOnlyList<TripAuthorityDriverInput> drivers,
        IReadOnlyList<TripAuthorityPassengerInput> passengers,
        IReadOnlyList<TripAuthorityRouteInput> routes,
        int currentUserId
    );
    Task UpdateAsync(Trip trip, int currentUserId);
    Task<Trip> RenewAsync(
        int tripId,
        DateTime newExpiryDate,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer,
        int currentUserId,
        IReadOnlySet<short>? allowedSiteCodes = null
    );
    Task CloseAsync(
        int tripId,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer,
        int currentUserId
    );
    Task DeleteAsync(int tripId, int currentUserId);
}

/// <summary>
/// Filters for the two server-paginated Trip Authority vehicle lists.
/// The selected location filters are data filters. AllowedSiteCodes is an
/// authenticated resource scope resolved by the API and is never caller-owned.
/// </summary>
public sealed record TripAuthorityVehiclePageQuery(
    int Page = 1,
    int PageSize = 24,
    string SearchMode = "GG",
    string? SearchTerm = null,
    short? DepartmentCode = null,
    short? SiteCode = null,
    int? TripAuthorityCode = null,
    IReadOnlySet<short>? AllowedSiteCodes = null
);

public sealed record TripAuthorityVehiclePageItem(
    int VmfCode,
    int ContractCode,
    short SiteCode,
    string? FleetNumber,
    string? RegistrationNumber,
    DateTime? LicenceDueDate,
    string? MakeDescription,
    string? ModelDescription,
    string? ContractType,
    int? TripAuthorityCode = null
);

public sealed record TripAuthorityVehiclePage(
    IReadOnlyList<TripAuthorityVehiclePageItem> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for fuel card operations
/// </summary>
public interface IFuelCardRepository
{
    Task<FuelCard?> GetByIdAsync(int fuelCardId);
    Task<FuelCard?> GetByCardNumberAsync(string cardNumber);
    Task<IEnumerable<FuelCard>> GetActiveFuelCardsAsync();
    Task<IEnumerable<FuelCard>> GetFuelCardsByVehicleAsync(int vmfCode);
    Task<FuelCardActivityPage> GetRecentActivityPageAsync(
        int? siteCode = null,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default
    );
    Task<FuelCard> CreateAsync(FuelCard fuelCard, int currentUserId);
    Task UpdateAsync(FuelCard fuelCard, int currentUserId);
    Task DeleteAsync(int fuelCardId, int currentUserId);
}

public sealed record FuelCardActivityPage(
    IReadOnlyList<FuelCard> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for private hire fuel card operations
/// </summary>
public interface IPrivateHireFuelCardRepository
{
    Task<PrivateHireFuelCard?> GetByIdAsync(int privateHireFuelCardId);
    Task<PrivateHireFuelCard?> GetByCardNumberAsync(string cardNumber);
    Task<IEnumerable<PrivateHireFuelCard>> GetByPrivateHireCodeAsync(int privateHireCode);
    Task<IEnumerable<PrivateHireFuelCard>> GetByRegistrationNumberAsync(string registrationNumber);
    Task<IEnumerable<PrivateHireFuelCard>> GetActiveFuelCardsAsync();
    Task<IEnumerable<PrivateHireFuelCard>> GetActiveFuelCardsBySiteAsync(int siteCode);
    Task<int?> GetPrivateHireCodeByRegistrationAsync(string registrationNumber);
    Task<PrivateHireFuelCard> CreateAsync(PrivateHireFuelCard fuelCard, int currentUserId);
    Task UpdateAsync(PrivateHireFuelCard fuelCard, int currentUserId);
    Task DeleteAsync(int privateHireFuelCardId, int currentUserId);
}

/// <summary>
/// Repository interface for make operations
/// </summary>
public interface IMakeRepository
{
    Task<Make?> GetByIdAsync(short makeCode);
    Task<Make?> GetByNameAsync(string makeName);
    Task<IEnumerable<Make>> GetAllMakesAsync();
    Task<MakePage> GetPageAsync(int page = 1, int pageSize = 24);
    Task<IEnumerable<Make>> SearchMakesAsync(string searchTerm);
    Task<MakeDeleteCheck> GetDeleteCheckAsync(short makeCode);
    Task<Make> CreateAsync(Make make, int currentUserId);
    Task<Make> UpdateAsync(Make make, int currentUserId);
    Task DeleteAsync(short makeCode, int currentUserId);
}

public sealed record MakeDeleteCheck(int ModelCount)
{
    public bool CanDelete => ModelCount == 0;
}

public sealed record MakePage(IReadOnlyList<Make> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for model operations
/// </summary>
public interface IModelRepository
{
    Task<Model?> GetByIdAsync(short modelCode);
    Task<Model?> GetByNameAsync(string modelName);
    Task<IEnumerable<Model>> GetAllModelsAsync();
    Task<ModelPage> GetPageAsync(int page = 1, int pageSize = 24, short? makeCode = null);
    Task<IEnumerable<Model>> GetModelsByMakeAsync(short makeCode);
    Task<IEnumerable<Model>> GetModelsByEngineTypeAsync(string engineType);
    Task<IEnumerable<Model>> SearchModelsAsync(string searchTerm);
    Task<ModelDeleteCheck> GetDeleteCheckAsync(short modelCode);
    Task<Model> CreateAsync(Model model, int currentUserId);
    Task<Model> UpdateAsync(Model model, int currentUserId);
    Task<Model> UpdateLicenceFeeAsync(short modelCode, short licenceFeeCode, int currentUserId);
    Task DeleteAsync(short modelCode, int currentUserId);
}

public sealed record ModelDeleteCheck(int VehicleCount)
{
    public bool CanDelete => VehicleCount == 0;
}

public sealed record ModelPage(IReadOnlyList<Model> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for type operations
/// </summary>
public interface ITypeRepository
{
    Task<TypeEntity?> GetByIdAsync(short typeCode);
    Task<TypeEntity?> GetByNameAsync(string typeName);
    Task<IEnumerable<TypeEntity>> GetAllTypesAsync();
    Task<IEnumerable<TypeEntity>> SearchTypesAsync(string searchTerm);
    Task<TypePage> GetPageAsync(int page, int pageSize);
    Task<TypeEntity> CreateAsync(TypeEntity type, int currentUserId);
    Task<TypeEntity> UpdateAsync(TypeEntity type, int currentUserId);
    Task DeleteAsync(short typeCode, int currentUserId);
}

public sealed record TypePage(IReadOnlyList<TypeEntity> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for trip driver operations
/// </summary>
public interface ITripDriverRepository
{
    Task<TripDriver?> GetByIdAsync(int tripDriverCode);
    Task<TripDriver?> GetByNameAsync(string tripDriverName);
    Task<IEnumerable<TripDriver>> GetByTripAuthorityAsync(int tripAuthorityCode);
    Task<IEnumerable<TripDriver>> GetBySiteAsync(int siteCode);
    Task<IEnumerable<TripDriver>> GetPrimaryDriversAsync();
    Task<IEnumerable<TripDriver>> GetActiveDriversAsync();
    Task<IEnumerable<TripDriver>> GetByLicenseTypeAsync(int licenseTypeId);
    Task<TripDriver> CreateAsync(TripDriver tripDriver, int currentUserId);
    Task UpdateAsync(TripDriver tripDriver, int currentUserId);
    Task DeleteAsync(int tripDriverCode, int currentUserId);
    Task<IEnumerable<TripDriver>> SearchDriversAsync(string searchTerm);
}

/// <summary>
/// Repository interface for private hire operations
/// </summary>
public interface IPrivateHireRepository
{
    Task<PrivateHire?> GetByIdAsync(int privateHireCode);
    Task<IEnumerable<PrivateHire>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<PrivateHire>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<PrivateHire>> GetActiveHiresAsync();
    Task<PrivateHirePage> GetPageAsync(PrivateHirePageQuery query);
    Task<PrivateHire> CreateAsync(PrivateHire privateHire, int currentUserId);
    Task UpdateAsync(PrivateHire privateHire, int currentUserId);
    Task DeleteAsync(int privateHireCode, int currentUserId);
    Task<IEnumerable<PrivateHire>> SearchHiresAsync(string searchTerm);
    Task<IEnumerable<PrivateHireContractorRecord>> GetContractorsAsync();
    Task<PrivateHireContractorPage> GetContractorPageAsync(PrivateHireContractorPageQuery query);
    Task<PrivateHireContractorRecord?> GetContractorByIdAsync(int contractorId);
    Task<PrivateHireContractorRecord> CreateContractorAsync(
        PrivateHireContractorRecord contractor,
        int currentUserId
    );
    Task UpdateContractorAsync(PrivateHireContractorRecord contractor, int currentUserId);
    Task DeleteContractorAsync(int contractorId, int currentUserId);
}

public sealed record PrivateHirePageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchTerm = null
);

public sealed record PrivateHirePage(
    IReadOnlyList<PrivateHire> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record PrivateHireContractorPageQuery(int Page = 1, int PageSize = 24);

public sealed record PrivateHireContractorPage(
    IReadOnlyList<PrivateHireContractorRecord> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for location operations
/// </summary>
public interface ILocationRepository
{
    Task<Location?> GetByIdAsync(int locationId);
    Task<Location?> GetByNameAsync(string locationName);
    Task<IEnumerable<Location>> GetAllLocationsAsync();
    Task<LocationPage> GetPageAsync(int page = 1, int pageSize = 24);
    Task<IEnumerable<Location>> GetByCountryAsync(string country);
    Task<IEnumerable<Location>> GetByProvinceAsync(string province);
    Task<Location> CreateAsync(Location location, int currentUserId);
    Task UpdateAsync(Location location, int currentUserId);
    Task DeleteAsync(int locationId, int currentUserId);
    Task<IEnumerable<Location>> SearchLocationsAsync(string searchTerm);
}

public sealed record LocationPage(IReadOnlyList<Location> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for maintenance record operations
/// </summary>
public interface IMaintenanceRecordRepository
{
    Task<MaintenanceRecord?> GetByIdAsync(int maintenanceId);
    Task<IEnumerable<MaintenanceRecord>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<MaintenanceRecord>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<MaintenanceRecord>> GetByServiceTypeAsync(string serviceType);
    Task<IEnumerable<MaintenanceRecord>> GetAllAsync();
    Task<MaintenanceRecord> CreateAsync(MaintenanceRecord maintenanceRecord, int currentUserId);
    Task UpdateAsync(MaintenanceRecord maintenanceRecord, int currentUserId);
    Task DeleteAsync(int maintenanceId, int currentUserId);
    Task<IEnumerable<MaintenanceRecord>> SearchMaintenanceRecordsAsync(string searchTerm);
}

/// <summary>
/// Repository interface for journal detail operations (financial transactions)
/// </summary>
public interface IJournalDetailRepository
{
    Task<IEnumerable<JournalDetail>> GetAllAsync();
    Task<JournalDetailPage> GetUninvoicedPageAsync(int? departmentCode, int page, int pageSize);
    Task<JournalDetail?> GetByIdAsync(int journalDetailId);
    Task<JournalDetail?> GetByCodeAsync(Guid journalDetailCode);
    Task<IEnumerable<JournalDetail>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<JournalDetail>> GetBySiteAsync(short siteCode);
    Task<IEnumerable<JournalDetail>> GetByDepartmentAsync(int departmentCode);
    Task<IEnumerable<JournalDetail>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<JournalDetail>> GetByFinancialYearAsync(string financialYear);
    Task<IEnumerable<JournalDetail>> GetReversalsForJournalAsync(Guid journalDetailCode);
    /// <summary>
    /// Executes the archived journal reversal workflow when the connected
    /// database exposes it. Returns false only when the procedure is absent;
    /// a present but incompatible procedure is a hard compatibility failure.
    /// </summary>
    Task<bool> TryGenerateReversalAsync(Guid journalDetailCode);
    Task<JournalDetail> CreateAsync(JournalDetail journalDetail, int currentUserId);
    Task UpdateAsync(JournalDetail journalDetail, int currentUserId);
    Task DeleteAsync(int journalDetailId, int currentUserId);
}

/// <summary>
/// Bounded journal-detail result for operational finance grids. The repository
/// owns the compatibility projection so callers never need to materialize the
/// full legacy journal_detail table merely to display a page.
/// </summary>
public sealed record JournalDetailPage(
    IReadOnlyList<JournalDetail> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository interface for workflow operations
/// </summary>
public interface IWorkflowRepository
{
    Task<FIS.Core.Domain.Entities.System.Workflow?> GetByIdAsync(int workflowId);
    Task<FIS.Core.Domain.Entities.System.Workflow?> GetByNameAsync(string workflowName);
    Task<IEnumerable<FIS.Core.Domain.Entities.System.Workflow>> GetAllAsync();
    Task<IEnumerable<FIS.Core.Domain.Entities.System.Workflow>> GetActiveWorkflowsAsync();
    Task<FIS.Core.Domain.Entities.System.Workflow> CreateAsync(
        FIS.Core.Domain.Entities.System.Workflow workflow,
        int currentUserId
    );
    Task UpdateAsync(FIS.Core.Domain.Entities.System.Workflow workflow, int currentUserId);
    Task DeleteAsync(int workflowId, int currentUserId);
}

/// <summary>
/// Repository interface for workflow step operations
/// </summary>
public interface IStepRepository
{
    Task<Step?> GetByIdAsync(int stepId);
    Task<IEnumerable<Step>> GetAllAsync();
    Task<IEnumerable<Step>> GetByWorkflowIdAsync(int workflowId);
    Task<IEnumerable<Step>> GetByStepTypeIdAsync(int stepTypeId);
    Task<IEnumerable<Step>> GetChildStepsAsync(int parentStepId);
    Task<Step> CreateAsync(Step step, int currentUserId);
    Task UpdateAsync(Step step, int currentUserId);
    Task DeleteAsync(int stepId, int currentUserId);
}

/// <summary>
/// Repository interface for workflow step type operations
/// </summary>
public interface IStepTypeRepository
{
    Task<StepType?> GetByIdAsync(int stepTypeId);
    Task<StepType?> GetByNameAsync(string stepTypeName);
    Task<IEnumerable<StepType>> GetAllAsync();
    Task<StepType> CreateAsync(StepType stepType, int currentUserId);
    Task UpdateAsync(StepType stepType, int currentUserId);
    Task DeleteAsync(int stepTypeId, int currentUserId);
}

/// <summary>
/// Repository interface for workflow status operations
/// </summary>
public interface IStatusRepository
{
    Task<FIS.Core.Domain.Entities.System.Status?> GetByIdAsync(int statusId);
    Task<IEnumerable<FIS.Core.Domain.Entities.System.Status>> GetAllAsync();
    Task<IEnumerable<FIS.Core.Domain.Entities.System.Status>> GetByStepIdAsync(int stepId);
    Task<IEnumerable<FIS.Core.Domain.Entities.System.Status>> GetActiveStatusesAsync();
    Task<IEnumerable<FIS.Core.Domain.Entities.System.Status>> GetByUserAsync(string userName);
    Task<FIS.Core.Domain.Entities.System.Status> CreateAsync(
        FIS.Core.Domain.Entities.System.Status status,
        int currentUserId
    );
    Task UpdateAsync(FIS.Core.Domain.Entities.System.Status status, int currentUserId);
    Task DeleteAsync(int statusId, int currentUserId);
}

/// <summary>
/// Repository interface for workflow template operations
/// </summary>
public interface IWorkflowTemplateRepository
{
    Task<WorkflowTemplate?> GetByIdAsync(int templateId);
    Task<IEnumerable<WorkflowTemplate>> GetAllAsync();
    Task<IEnumerable<WorkflowTemplate>> GetActiveTemplatesAsync();
    Task<IEnumerable<WorkflowTemplate>> GetByCategoryAsync(string category);
    Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, int currentUserId);
    Task UpdateAsync(WorkflowTemplate template, int currentUserId);
    Task DeleteAsync(int templateId, int currentUserId);
}

/// <summary>
/// Repository interface for workflow notification operations
/// </summary>
public interface IWorkflowNotificationRepository
{
    Task<WorkflowNotification?> GetByIdAsync(int notificationId);
    Task<IEnumerable<WorkflowNotification>> GetAllAsync();
    Task<IEnumerable<WorkflowNotification>> GetByWorkflowIdAsync(int workflowId);
    Task<IEnumerable<WorkflowNotification>> GetByStepIdAsync(int stepId);
    Task<IEnumerable<WorkflowNotification>> GetByEventTypeAsync(string eventType);
    Task<IEnumerable<WorkflowNotification>> GetActiveNotificationsAsync();
    Task<WorkflowNotification> CreateAsync(WorkflowNotification notification, int currentUserId);
    Task UpdateAsync(WorkflowNotification notification, int currentUserId);
    Task DeleteAsync(int notificationId, int currentUserId);
}

/// <summary>
/// Repository interface for notification template operations
/// </summary>
public interface INotificationTemplateRepository
{
    Task<NotificationTemplate?> GetByIdAsync(int templateId);
    Task<NotificationTemplate?> GetByNameAsync(string templateName);
    Task<IEnumerable<NotificationTemplate>> GetAllAsync();
    Task<IEnumerable<NotificationTemplate>> GetActiveTemplatesAsync();
    Task<IEnumerable<NotificationTemplate>> GetByTypeAsync(string templateType);
    Task<NotificationTemplate> CreateAsync(NotificationTemplate template, int currentUserId);
    Task UpdateAsync(NotificationTemplate template, int currentUserId);
    Task DeleteAsync(int templateId, int currentUserId);
}

/// <summary>
/// Repository interface for notification log operations
/// </summary>
public interface INotificationLogRepository
{
    Task<NotificationLog?> GetByIdAsync(int logId);
    Task<IEnumerable<NotificationLog>> GetAllAsync();
    Task<IEnumerable<NotificationLog>> GetByWorkflowIdAsync(int workflowId);
    Task<IEnumerable<NotificationLog>> GetByStatusAsync(string status);
    Task<IEnumerable<NotificationLog>> GetFailedNotificationsAsync();
    Task<IEnumerable<NotificationLog>> GetPendingNotificationsAsync();
    Task<NotificationLog> CreateAsync(NotificationLog log);
    Task UpdateAsync(NotificationLog log);
    Task DeleteAsync(int logId);
}

/// <summary>
/// Repository interface for step execution history operations
/// </summary>
public interface IStepExecutionHistoryRepository
{
    Task<StepExecutionHistory?> GetByIdAsync(int executionHistoryId);
    Task<IEnumerable<StepExecutionHistory>> GetAllAsync();
    Task<IEnumerable<StepExecutionHistory>> GetByWorkflowIdAsync(int workflowId);
    Task<IEnumerable<StepExecutionHistory>> GetByStepIdAsync(int stepId);
    Task<IEnumerable<StepExecutionHistory>> GetByStatusAsync(string status);
    Task<IEnumerable<StepExecutionHistory>> GetRecentExecutionsAsync(int count);
    Task<StepExecutionHistory> CreateAsync(StepExecutionHistory history);
    Task UpdateAsync(StepExecutionHistory history);
}

/// <summary>
/// Repository interface for workflow metric operations
/// </summary>
public interface IWorkflowMetricRepository
{
    Task<WorkflowMetric?> GetByIdAsync(int metricId);
    Task<IEnumerable<WorkflowMetric>> GetAllAsync();
    Task<IEnumerable<WorkflowMetric>> GetByWorkflowIdAsync(int workflowId);
    Task<IEnumerable<WorkflowMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<WorkflowMetric?> GetLatestMetricAsync(int workflowId);
    Task<WorkflowMetric> CreateAsync(WorkflowMetric metric, int currentUserId);
    Task UpdateAsync(WorkflowMetric metric, int currentUserId);
}

/// <summary>
/// Repository interface for workflow execution summary operations
/// </summary>
public interface IWorkflowExecutionSummaryRepository
{
    Task<WorkflowExecutionSummary?> GetByIdAsync(int summaryId);
    Task<IEnumerable<WorkflowExecutionSummary>> GetAllAsync();
    Task<IEnumerable<WorkflowExecutionSummary>> GetByWorkflowIdAsync(int workflowId);
    Task<IEnumerable<WorkflowExecutionSummary>> GetByStatusAsync(string status);
    Task<IEnumerable<WorkflowExecutionSummary>> GetActiveExecutionsAsync();
    Task<IEnumerable<WorkflowExecutionSummary>> GetRecentExecutionsAsync(int count);
    Task<WorkflowExecutionSummary> CreateAsync(WorkflowExecutionSummary summary);
    Task UpdateAsync(WorkflowExecutionSummary summary);
}

/// <summary>
/// Repository interface for driver licence operations
/// </summary>
public interface IDriverLicenceRepository
{
    Task<DriverLicence?> GetByIdAsync(short licenceCode);
    Task<DriverLicence?> GetByDescriptionAsync(string description);
    Task<IEnumerable<DriverLicence>> GetAllAsync();
    Task<DriverLicencePage> GetPageAsync(DriverLicencePageQuery query);
    Task<IEnumerable<DriverLicence>> SearchAsync(string searchTerm);
    Task<DriverLicenceDeleteCheck> GetDeleteCheckAsync(short licenceCode);
    Task<DriverLicence> CreateAsync(DriverLicence driverLicence, int currentUserId);
    Task UpdateAsync(DriverLicence driverLicence, int currentUserId);
    Task DeleteAsync(short licenceCode, int currentUserId);
}

public sealed record DriverLicencePageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchTerm = null
);

public sealed record DriverLicencePage(
    IReadOnlyList<DriverLicence> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record DriverLicenceDeleteCheck(int ModelCount)
{
    public bool CanDelete => ModelCount == 0;
}

/// <summary>
/// Repository interface for licence fee operations
/// </summary>
public interface ILicenseFeeRepository
{
    Task<LicenseFee?> GetByIdAsync(short licenceFeeCode);
    Task<LicenseFee?> GetByDescriptionAsync(string description);
    Task<IEnumerable<LicenseFee>> GetAllAsync();
    Task<LicenseFeePage> GetPageAsync(LicenseFeePageQuery query);
    Task<IEnumerable<LicenseFee>> SearchAsync(string searchTerm);
    Task<LicenseFeeDeleteCheck> GetDeleteCheckAsync(short licenceFeeCode);
    Task<LicenseFee> CreateAsync(LicenseFee licenseFee, int currentUserId);
    Task UpdateAsync(LicenseFee licenseFee, int currentUserId);
    Task DeleteAsync(short licenceFeeCode, int currentUserId);
}

public sealed record LicenseFeePageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchTerm = null
);

public sealed record LicenseFeePage(
    IReadOnlyList<LicenseFee> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record LicenseFeeDeleteCheck(int ModelCount)
{
    public bool CanDelete => ModelCount == 0;
}

/// <summary>
/// Repository interface for extra code operations
/// </summary>
public interface IExtraCodeRepository
{
    Task<ExtraCode?> GetByIdAsync(short extraCode);
    Task<ExtraCode?> GetByDescriptionAsync(string description);
    Task<IEnumerable<ExtraCode>> GetAllAsync();
    Task<ExtraCodePage> GetPageAsync(ExtraCodePageQuery query);
    Task<IEnumerable<ExtraCode>> SearchAsync(string searchTerm);
    Task<IEnumerable<ExtraCode>> GetByCategoryAsync(int categoryTypeCode);
    Task<ExtraCodeDeleteCheck> GetDeleteCheckAsync(short extraCode);
    Task<ExtraCode> CreateAsync(ExtraCode extraCode, int currentUserId);
    Task UpdateAsync(ExtraCode extraCode, int currentUserId);
    Task DeleteAsync(short extraCode, int currentUserId);
}

public sealed record ExtraCodePageQuery(int Page = 1, int PageSize = 24, string? SearchTerm = null);

public sealed record ExtraCodePage(
    IReadOnlyList<ExtraCode> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record ExtraCodeDeleteCheck(
    int VehicleCount,
    IReadOnlyList<string> FleetNumbers,
    bool CheckAvailable = true
)
{
    public bool CanDelete => CheckAvailable && VehicleCount == 0;
}

/// <summary>
/// Repository interface for loss type operations
/// </summary>
public interface ILossTypeRepository
{
    Task<LossType?> GetByIdAsync(short lossTypeCode);
    Task<LossType?> GetByDescriptionAsync(string description);
    Task<IEnumerable<LossType>> GetAllAsync();
    Task<LossTypePage> GetPageAsync(LossTypePageQuery query);
    Task<IEnumerable<LossType>> SearchAsync(string searchTerm);
    Task<LossType> CreateAsync(LossType lossType, int currentUserId);
    Task UpdateAsync(LossType lossType, int currentUserId);
    Task<LossTypeDeleteCheck> GetDeleteCheckAsync(short lossTypeCode);
    Task DeleteAsync(short lossTypeCode, int currentUserId);
}

public sealed record LossTypePageQuery(int Page = 1, int PageSize = 24, string? SearchTerm = null);

public sealed record LossTypePage(IReadOnlyList<LossType> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record LossTypeDeleteDependency(
    string? FleetNumber,
    DateTime? LossDate,
    string? LossReference
);

public sealed record LossTypeDeleteCheck(
    int LossCount,
    IReadOnlyList<LossTypeDeleteDependency> Losses,
    bool CheckAvailable = true
)
{
    public bool CanDelete => CheckAvailable && LossCount == 0;
}

/// <summary>
/// Repository interface for class-based tariff CRUD and approval workflow.
/// Approval threshold: monthly_fixed_amount > R100,000 requires approval.
/// Self-approval is blocked on all approve/reject operations.
/// </summary>
public interface ITariffManagementRepository
{
    Task<Tariff?> GetByIdAsync(int tariffCode);
    Task<IEnumerable<Tariff>> GetAllAsync();
    Task<IEnumerable<Tariff>> GetApprovedAsync(); // Only status=2, effective tariffs
    Task<IEnumerable<Tariff>> GetPendingApprovalAsync(); // Status=1
    Task<Tariff> CreateAsync(Tariff tariff, int currentUserId);
    Task UpdateAsync(Tariff tariff, int currentUserId);
    Task<Tariff> SubmitForApprovalAsync(int tariffCode, int currentUserId);
    Task<Tariff> ApproveAsync(int tariffCode, int approverUserId);
    Task<Tariff> RejectAsync(int tariffCode, int approverUserId, string rejectionReason);
}

/// <summary>
/// Repository interface for vehicle tariff operations
/// </summary>
public interface IVehicleTariffRepository
{
    Task<VehicleTariff?> GetByIdAsync(int vehicleTariffCode);
    Task<VehicleTariff?> GetCurrentTariffForVehicleAsync(int vmfCode);
    Task<VehicleTariff?> GetTariffForVehicleAsync(
        int vmfCode,
        int? parameterYear,
        DateTime effectiveDate
    );
    Task<IEnumerable<VehicleTariff>> GetTariffHistoryForVehicleAsync(int vmfCode);
    Task<VehicleTariff> CreateAsync(VehicleTariff tariff);
    Task UpdateAsync(VehicleTariff tariff);
    Task RecalculateTariffAsync(int vmfCode);
}

/// <summary>
/// Repository interface for vehicle remarks (operational notes, missing/investigation flags).
/// </summary>
public interface IVehicleRemarkRepository
{
    Task<VehicleRemark?> GetByIdAsync(int remarkId);
    Task<IEnumerable<VehicleRemark>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<VehicleRemark>> GetActiveByVehicleAsync(int vmfCode);
    Task<VehicleRemark?> GetLatestActiveByVehicleAsync(int vmfCode);
    Task<IEnumerable<VehicleRemark>> GetAllActiveAsync(); // All open remarks across fleet
    Task<IEnumerable<VehicleRemark>> GetAllAsync();
    Task<VehicleRemark> CreateAsync(VehicleRemark remark, int currentUserId);
    Task<VehicleRemark> ResolveAsync(int remarkId, int resolvedByUserId, string? resolutionNotes);
    Task DeleteAsync(int remarkId, int currentUserId);
}

/// <summary>
/// Repository interface for vehicle licence history.
/// A snapshot is written here before every new licence capture so the full
/// renewal history is preserved.
/// </summary>
public interface IVehicleLicenceHistoryRepository
{
    Task<bool> IsAvailableAsync();
    Task<IEnumerable<VehicleLicenceHistory>> GetByVehicleAsync(int vmfCode);
    Task<VehicleLicenceHistory?> GetLatestByVehicleAsync(int vmfCode);
    Task<VehicleLicenceHistory> CreateAsync(VehicleLicenceHistory history);
}

/// <summary>
/// Reads and writes licence certificate scans across the expanded document
/// table and the client's original scan_docs table.
/// </summary>
public interface ILicenseCertificateRepository
{
    Task<IEnumerable<LicenseCertificateDocument>> GetAllAsync();
    Task<LicenseCertificatePage> GetPageAsync(int page = 1, int pageSize = 24);
    Task<MissingLicenseCertificatePage> GetMissingPageAsync(
        short? locationCode,
        int page = 1,
        int pageSize = 24
    );
    Task<IEnumerable<LicenseCertificateDocument>> GetByVehicleAsync(int vmfCode);
    Task<LicenseCertificateDocument?> GetByKeyAsync(string source, int vmfCode, string documentKey);
    Task<bool> HasAnyForVehicleAsync(int vmfCode);
    Task<string?> GetPreferredWriteSourceAsync();
    Task<LicenseCertificateDocument> CreateAsync(
        LicenseCertificateDocument document,
        int currentUserId
    );
    Task DeleteAsync(string source, int vmfCode, string documentKey, int currentUserId);
}

public sealed record LicenseCertificatePageItem(
    LicenseCertificateDocument Document,
    string? FleetNumber,
    string? RegistrationNumber
);

public sealed record LicenseCertificatePage(
    IReadOnlyList<LicenseCertificatePageItem> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record MissingLicenseCertificatePageItem(
    int VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    short LocationCode
);

public sealed record MissingLicenseCertificatePage(
    IReadOnlyList<MissingLicenseCertificatePageItem> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>
/// Repository for vehicle document attachments (scans, photos, PDFs).
/// </summary>
public interface IVehicleDocumentRepository
{
    Task<IEnumerable<VehicleDocument>> GetByVehicleAsync(int vmfCode, string? category = null);
    Task<IEnumerable<VehicleDocument>> GetByReferenceAsync(string referenceType, int referenceId);
    Task<VehicleDocument?> GetByIdAsync(int documentId);
    Task<IEnumerable<VehicleDocument>> GetAllAsync();
    Task<VehicleDocument> CreateAsync(VehicleDocument document);
    Task DeleteAsync(int documentId);
}
