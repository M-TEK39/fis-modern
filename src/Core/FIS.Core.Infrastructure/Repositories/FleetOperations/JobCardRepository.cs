using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Job-card persistence. Mutations always go through the archived
/// procedure-first compatibility repository. The expanded <c>job_cards</c>
/// EF mapping is not used for workflow writes: those procedures own status,
/// capturer, authoriser, and close behaviour.
/// </summary>
public class JobCardRepository : IJobCardRepository
{
    private readonly LegacyJobCardRepository _legacyRepository;

    public JobCardRepository(FisDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _legacyRepository = new LegacyJobCardRepository(context);
    }

    public Task<JobCard?> GetByIdAsync(int jobCardId) =>
        _legacyRepository.GetByIdAsync(jobCardId);

    public Task<JobCard?> GetByVehicleAndExtraAsync(int vmfCode, short extraCode) =>
        _legacyRepository.GetByVehicleAndExtraAsync(vmfCode, extraCode);

    public Task<IEnumerable<JobCard>> GetAllAsync() => _legacyRepository.GetAllAsync();

    public Task<JobCardPage> GetPageAsync(JobCardPageQuery query) =>
        _legacyRepository.GetPageAsync(query);

    public Task<JobCardPage> GetPriorityUnassignedPageAsync(
        PriorityUnassignedJobCardPageQuery query
    ) => _legacyRepository.GetPriorityUnassignedPageAsync(query);

    public Task<RepairCostReportPage> GetRepairCostReportPageAsync(RepairCostReportPageQuery query) =>
        _legacyRepository.GetRepairCostReportPageAsync(query);

    public Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber) =>
        _legacyRepository.GetByGGNumberAsync(ggNumber);

    public Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync() =>
        _legacyRepository.GetPriorityUnassignedAsync();

    public Task<IEnumerable<JobCard>> GetAssignedPriorityAsync() =>
        _legacyRepository.GetAssignedPriorityAsync();

    public Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode) =>
        _legacyRepository.GetByStatusAsync(statusCode);

    public Task<IEnumerable<JobCard>> GetByAuthorizerAsync(int authorizerUserId) =>
        _legacyRepository.GetByAuthorizerAsync(authorizerUserId);

    public Task<JobCard> CreateAsync(JobCard jobCard, int currentUserId) =>
        _legacyRepository.CreateAsync(jobCard, currentUserId);

    public Task<JobCard> UpdateAsync(JobCard jobCard, int currentUserId) =>
        _legacyRepository.UpdateAsync(jobCard, currentUserId);

    public Task DeleteAsync(int jobCardId, int currentUserId) =>
        _legacyRepository.DeleteAsync(jobCardId, currentUserId);

    public Task<JobCard> AuthorizeAsync(int jobCardId, int authorizerUserId, string? comment) =>
        _legacyRepository.AuthorizeAsync(jobCardId, authorizerUserId, comment);

    public Task<JobCard> DeclineAsync(int jobCardId, int authorizerUserId, string declineReason) =>
        _legacyRepository.DeclineAsync(jobCardId, authorizerUserId, declineReason);

    public Task<JobCard> CancelAsync(int jobCardId, int currentUserId, string? cancelReason) =>
        _legacyRepository.CancelAsync(jobCardId, currentUserId, cancelReason);

    public Task<JobCard> CloseAsync(
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
    ) =>
        _legacyRepository.CloseAsync(
            jobCardId,
            currentUserId,
            closeNotes,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider,
            damages,
            damageComment,
            barcode,
            closeDate
        );

    public Task<JobCard> UpdateStatusAsync(int jobCardId, int newStatusCode, int currentUserId) =>
        _legacyRepository.UpdateStatusAsync(jobCardId, newStatusCode, currentUserId);

    public Task<JobCard> UpdateCostsAsync(
        int jobCardId,
        int currentUserId,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider
    ) =>
        _legacyRepository.UpdateCostsAsync(
            jobCardId,
            currentUserId,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider
        );
}
