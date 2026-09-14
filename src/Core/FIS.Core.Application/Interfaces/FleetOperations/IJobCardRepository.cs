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
    Task<JobCardPage> GetPriorityUnassignedPageAsync(PriorityUnassignedJobCardPageQuery query);
    Task<RepairCostReportPage> GetRepairCostReportPageAsync(RepairCostReportPageQuery query);
    Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber);
    Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync();
    Task<IEnumerable<JobCard>> GetAssignedPriorityAsync();
    Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode);
    Task<IEnumerable<JobCard>> GetByAuthorizerAsync(int authorizerUserId);

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
    int? JobCardId = null
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

public sealed record PriorityUnassignedJobCardPageQuery(int Page = 1, int PageSize = 24);

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
