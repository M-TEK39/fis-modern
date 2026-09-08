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
        string? serviceProvider = null
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
