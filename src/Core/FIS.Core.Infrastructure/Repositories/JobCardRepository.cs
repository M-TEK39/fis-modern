using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for JobCard operations
/// Legacy: Replaces stored procedures DEV_SEL_JobcardPriotiyForCapturers, DEV_INS_NewJobCards, etc.
/// </summary>
public class JobCardRepository : IJobCardRepository
{
    private readonly FisDbContext _context;

    public JobCardRepository(FisDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get job card by ID with navigation properties
    /// </summary>
    public async Task<JobCard?> GetByIdAsync(int jobCardId)
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Include(jc => jc.CreatedByUser)
            .Include(jc => jc.ModifiedByUser)
            .Where(jc => !jc.is_deleted)
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId);
    }

    /// <summary>
    /// Get job card by vehicle and extra code combination
    /// </summary>
    public async Task<JobCard?> GetByVehicleAndExtraAsync(int vmfCode, short extraCode)
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted && jc.vmf_code == vmfCode && jc.extra_code == extraCode)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get all job cards with navigation properties
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetAllAsync()
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted)
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get job cards by GG number (fleet_number)
    /// Legacy: Replaces DEV_SEL_JobCards stored procedure
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber)
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted && jc.Vehicle != null && jc.Vehicle.fleet_number == ggNumber)
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get priority unassigned job cards
    /// Legacy: Replaces DEV_SEL_JobcardPriotiyForCapturers stored procedure
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync()
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted
                && jc.priority == "H"
                && jc.assigned_to == null
                && jc.status_code != 5  // Not Complete
                && jc.status_code != 7) // Not Canceled
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get assigned priority job cards
    /// Legacy: Replaces DEV_SEL_JobcardsForAssignedPriority stored procedure
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetAssignedPriorityAsync()
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted
                && jc.priority == "H"
                && jc.assigned_to != null
                && jc.status_code != 5  // Not Complete
                && jc.status_code != 7) // Not Canceled
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get job cards by status code
    /// Legacy: Used by DEV_SEL_JobcardsStatusReportForAuthorizer
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode)
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted && jc.status_code == statusCode)
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get job cards by authorizer user
    /// Legacy: Used by DEV_SEL_JobcardsForAuthorizers
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetByAuthorizerAsync(int authorizerUserId)
    {
        return await _context.JobCards
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted && jc.authorizer == authorizerUserId)
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Create a new job card
    /// Legacy: Replaces DEV_INS_NewJobCards stored procedure
    /// </summary>
    public async Task<JobCard> CreateAsync(JobCard jobCard, int currentUserId)
    {
        // Set audit fields
        jobCard.date_created = DateTime.UtcNow;
        jobCard.created_by_user_code = currentUserId;
        jobCard.is_deleted = false;

        // Set default status if not provided
        if (jobCard.status_code == 0)
            jobCard.status_code = 1; // Pending

        _context.JobCards.Add(jobCard);
        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCard.job_card_id)
            ?? throw new InvalidOperationException("Failed to retrieve created job card");
    }

    /// <summary>
    /// Update an existing job card
    /// Legacy: Replaces DEV_UPD_Jobcards stored procedure
    /// </summary>
    public async Task<JobCard> UpdateAsync(JobCard jobCard, int currentUserId)
    {
        var existingJobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCard.job_card_id && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCard.job_card_id}");

        // Update audit fields
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        // Preserve creation audit fields
        jobCard.date_created = existingJobCard.date_created;
        jobCard.created_by_user_code = existingJobCard.created_by_user_code;

        // Use tracking-safe update pattern
        _context.Entry(existingJobCard).CurrentValues.SetValues(jobCard);
        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCard.job_card_id)
            ?? throw new InvalidOperationException("Failed to retrieve updated job card");
    }

    /// <summary>
    /// Delete a job card (soft delete)
    /// </summary>
    public async Task DeleteAsync(int jobCardId, int currentUserId)
    {
        var jobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        jobCard.is_deleted = true;
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Authorize a job card
    /// Legacy: Replaces DEV_UPD_JobcardAuthorizersUpdates stored procedure (approve path)
    /// </summary>
    public async Task<JobCard> AuthorizeAsync(int jobCardId, int authorizerUserId, string? comment)
    {
        var jobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        // Update status to Authorized (3)
        jobCard.status_code = 3;
        jobCard.authorizer = authorizerUserId;
        jobCard.reviewed = "Y";
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = authorizerUserId;

        // Append authorization comment
        if (!string.IsNullOrEmpty(comment))
        {
            jobCard.comments = string.IsNullOrEmpty(jobCard.comments)
                ? $"Authorization: {comment}"
                : $"{jobCard.comments}\nAuthorization: {comment}";
        }

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve authorized job card");
    }

    /// <summary>
    /// Decline a job card
    /// Legacy: Replaces DEV_UPD_JobcardAuthorizersUpdates stored procedure (decline path)
    /// </summary>
    public async Task<JobCard> DeclineAsync(int jobCardId, int authorizerUserId, string declineReason)
    {
        var jobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        // Update status back to Pending (1)
        jobCard.status_code = 1;
        jobCard.authorizer = authorizerUserId;
        jobCard.reviewed = "Y";
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = authorizerUserId;

        // Append decline reason
        jobCard.comments = string.IsNullOrEmpty(jobCard.comments)
            ? $"Declined: {declineReason}"
            : $"{jobCard.comments}\nDeclined: {declineReason}";

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve declined job card");
    }

    /// <summary>
    /// Cancel a job card
    /// </summary>
    public async Task<JobCard> CancelAsync(int jobCardId, int currentUserId, string? cancelReason)
    {
        var jobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        // Update status to Canceled (7)
        jobCard.status_code = 7;
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        // Append cancel reason
        if (!string.IsNullOrEmpty(cancelReason))
        {
            jobCard.comments = string.IsNullOrEmpty(jobCard.comments)
                ? $"Canceled: {cancelReason}"
                : $"{jobCard.comments}\nCanceled: {cancelReason}";
        }

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve canceled job card");
    }

    /// <summary>
    /// Close a job card
    /// </summary>
    public async Task<JobCard> CloseAsync(int jobCardId, int currentUserId, string? closeNotes)
    {
        var jobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        // Update status to Complete (5)
        jobCard.status_code = 5;
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        // Append close notes
        if (!string.IsNullOrEmpty(closeNotes))
        {
            jobCard.comments = string.IsNullOrEmpty(jobCard.comments)
                ? $"Closed: {closeNotes}"
                : $"{jobCard.comments}\nClosed: {closeNotes}";
        }

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve closed job card");
    }

    /// <summary>
    /// Update job card status
    /// Legacy: Replaces DEV_UPD_JobcardUpdateStatusToInProgress and similar procedures
    /// </summary>
    public async Task<JobCard> UpdateStatusAsync(int jobCardId, int newStatusCode, int currentUserId)
    {
        var jobCard = await _context.JobCards
            .FirstOrDefaultAsync(jc => jc.job_card_id == jobCardId && !jc.is_deleted)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        jobCard.status_code = newStatusCode;
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve updated job card");
    }
}
