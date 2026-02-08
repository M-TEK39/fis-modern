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
    Task<JobCard> CloseAsync(int jobCardId, int currentUserId, string? closeNotes);
    Task<JobCard> UpdateStatusAsync(int jobCardId, int newStatusCode, int currentUserId);
}
