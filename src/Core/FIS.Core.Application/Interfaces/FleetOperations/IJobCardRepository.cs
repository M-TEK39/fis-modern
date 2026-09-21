using FIS.Core.Domain.Entities.Operations;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for JobCard operations
/// </summary>
public interface IJobCardRepository
{
    // CRUD operations
    Task<JobCard?> GetByIdAsync(int jobCardId);
    Task<JobCard?> GetByVehicleAndExtraAsync(int vmfCode, short extraCode);
    Task<IEnumerable<JobCard>> GetAllAsync();
    Task<JobCardPage> GetPageAsync(JobCardPageQuery query);
    Task<JobCardPage> GetAuthorizerPageAsync(JobCardPageQuery query);
    Task<JobCardPage> GetPriorityUnassignedPageAsync(PriorityUnassignedJobCardPageQuery query);
    Task<JobCardPage> GetAssignedPriorityPageAsync(PriorityUnassignedJobCardPageQuery query);
    Task<RepairCostReportPage> GetRepairCostReportPageAsync(RepairCostReportPageQuery query);
    Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber);
    Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync();
    Task<IEnumerable<JobCard>> GetAssignedPriorityAsync();
    Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode);
    Task<IEnumerable<JobCard>> GetByAuthorizerAsync(int authorizerUserId);
    Task<IReadOnlyList<JobCardCaptureVehicle>?> GetVehiclesAvailableForCaptureAsync();
    Task<JobCardAuthorizerGgStatsPage?> GetAuthorizerGgStatsAsync(
        JobCardAuthorizerGgStatsQuery query
    );
    Task<IReadOnlyList<JobCardCaptureVehicleSummary>?> GetCaptureVehicleSummaryAsync(
        string ggNumber
    );
    Task<IReadOnlyList<JobCardCaptureExtra>?> GetCaptureExtrasInCategoryAsync(string ggNumber);
    Task<IReadOnlyList<string>?> GetCaptureFittedExtraDescriptionsAsync(string ggNumber);
    Task<IReadOnlyList<string>?> GetCaptureJobcardsOnStatusDescriptionsAsync(string ggNumber);
    Task<IReadOnlyList<JobCardAuthorizerDetails>?> GetAuthorizerDetailsAsync(
        string ggNumber,
        string extraCode
    );
    Task<IReadOnlyList<JobCardAuthorizerStatus>?> GetAuthorizerStatusCodesAsync();
    Task<IReadOnlyList<JobCardCapturerDetails>?> GetCapturerDetailsAsync(
        string ggNumber,
        string extraCode
    );
    Task<IReadOnlyList<JobCardCapturerStatus>?> GetCapturerStatusCodesAsync();
    Task<IReadOnlyList<JobCardPrintSummary>?> GetPrintableJobCardsAsync(string ggNumber);
    Task<IReadOnlyList<JobCardPrintSnapshot>?> GetPrintJobCardsAsync(
        string ggNumber,
        string? jcNumber
    );

    Task<JobCard> CreateAsync(JobCard jobCard, int currentUserId);
    Task<JobCard> UpdateAsync(JobCard jobCard, int currentUserId);
    Task DeleteAsync(int jobCardId, int currentUserId);

    // Workflow operations
    Task<JobCard> AuthorizeAsync(int jobCardId, int authorizerUserId, string? comment);
    Task<JobCard> DeclineAsync(int jobCardId, int authorizerUserId, string declineReason);
    Task<JobCard> CancelAsync(int jobCardId, int currentUserId, string? cancelReason);
    Task<JobCard> CloseAsync(
        int jobCardId,
        int currentUserId,
        string? closeNotes,
        decimal? labourCost = null,
        decimal? partsCost = null,
        decimal? otherCost = null,
        string? invoiceNumber = null,
        DateTime? invoiceDate = null,
        string? serviceProvider = null,
        string? damages = null,
        string? damageComment = null,
        string? barcode = null,
        DateTime? closeDate = null
    );
    Task<JobCard> UpdateStatusAsync(int jobCardId, int newStatusCode, int currentUserId);

    // Cost operations
    /// <summary>
    /// Amend costs on an already-closed job card (e.g. when invoice arrives late).
    /// Assumption: allowed post-close — confirm with users (QUESTIONS.md MX-1).
    /// </summary>
    Task<JobCard> UpdateCostsAsync(
        int jobCardId,
        int currentUserId,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider
    );
}

public sealed record JobCardPageQuery(
    int Page = 1,
    int PageSize = 24,
    string? SearchTerm = null,
    string SearchType = "GG",
    IReadOnlyCollection<int>? StatusCodes = null,
    int? JobCardId = null,
    IReadOnlyCollection<int>? AllowedVmfCodes = null
);

public sealed record JobCardPage(
    IReadOnlyList<JobCard> Items,
    int Page,
    int PageSize,
    int TotalRecords
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public sealed record JobCardCaptureVehicle(
    int VmfCode,
    string? FleetNumber,
    string? RegistrationNumber,
    short? ModelCode
);

public sealed record JobCardCaptureVehicleSummary(
    int? VmfCode,
    string GgNumber,
    string? RegistrationNumber,
    string? ClassDescription,
    string? ModelDescription,
    string? OdoReading,
    string? VinNumber,
    string? EngineNumber,
    string? YearModel,
    string? PurchasedFrom,
    string? HireType,
    string? HiredFrom,
    string? Location
);

public sealed record JobCardCaptureExtra(short ExtraCode, string? Description);

public sealed record JobCardAuthorizerDetails(
    int? JobCardId,
    string? JcNumber,
    string? GgNumber,
    string? ExtraDescription,
    string? InitialCapturedDate,
    string? InitialCapturer,
    string? Barcode,
    string? CapturedDate,
    string? JobCardsCapturer,
    string? HandoverName,
    string? HandoverDate,
    string? Damages,
    string? Comments,
    string? StatusDescription,
    string? Priority,
    string? Authorizer,
    string? AuthorizedDate,
    string? AuthorizerComments
);

public sealed record JobCardAuthorizerStatus(int StatusCode, string? Description);

public sealed record JobCardCapturerDetails(
    int? JobCardId,
    string? JcNumber,
    string? GgNumber,
    string? ExtraDescription,
    string? Barcode,
    string? InitialCapturer,
    string? InitialCapturedDate,
    string? JobCardsCapturer,
    string? CapturedDate,
    string? HandoverName,
    string? HandoverDate,
    string? Damages,
    string? Comments,
    string? StatusDescription,
    string? JobcardComment,
    string? Authorizer,
    string? AuthorizerDate,
    string? AuthorizerComments
);

public sealed record JobCardCapturerStatus(int StatusCode, string? Description);

public sealed record JobCardPrintSummary(
    string JobcardNumber,
    string GgNumber,
    string? RegistrationNumber,
    string? JobcardDescription
);

public sealed record JobCardPrintSnapshot(
    string? GgNumber,
    string? RegistrationNumber,
    string? DateDelivered,
    string? OdoReading,
    string? VinNumber,
    string? EngineNumber,
    string? ModelDescription,
    string? YearModel,
    string? ClassDescription,
    string? HireType,
    string? HiredFrom,
    string? Location,
    string? CapturedDate,
    string? ReceivedBy,
    string? Status,
    string? StatusDate,
    string? PurchasedFrom,
    string? PurchasedDate,
    string? JobcardNumber,
    string? JobDescription,
    string? JobcardStatus,
    string? CapturedBy,
    string? JcsDate,
    string? AssignedTo,
    string? AssignedDate
);

public sealed record JobCardAuthorizerGgStatsQuery(
    int Page = 1,
    int PageSize = 24,
    string? GgNumber = null,
    IReadOnlyCollection<int>? AllowedVmfCodes = null
);

public sealed record JobCardAuthorizerGgStats(
    string GgNumber,
    int? Jobcards,
    int? Pending,
    int? AwaitingAuthorisation,
    int? Authorised,
    int? InProgress,
    int? Canceled,
    int? Failed,
    int? Completed
);

public sealed record JobCardAuthorizerGgStatsPage(
    IReadOnlyList<JobCardAuthorizerGgStats> Items,
    int Page,
    int PageSize,
    int TotalRecords
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public sealed record PriorityUnassignedJobCardPageQuery(
    int Page = 1,
    int PageSize = 24,
    IReadOnlyCollection<int>? AllowedVmfCodes = null
);

public sealed record RepairCostReportPageQuery(
    int Page = 1,
    int PageSize = 24,
    int? VmfCode = null,
    IReadOnlyCollection<int>? VmfCodes = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

public sealed record RepairCostReportPage(
    IReadOnlyList<JobCard> Items,
    int Page,
    int PageSize,
    int TotalRecords,
    decimal GrandTotal,
    decimal TotalLabour,
    decimal TotalParts,
    decimal TotalOther
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}
