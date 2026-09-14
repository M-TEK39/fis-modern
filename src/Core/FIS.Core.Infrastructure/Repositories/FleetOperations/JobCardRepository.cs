using System.Data;
using System.Data.Common;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for JobCard operations
/// Legacy: Replaces stored procedures DEV_SEL_JobcardPriotiyForCapturers, DEV_INS_NewJobCards, etc.
/// </summary>
public class JobCardRepository : IJobCardRepository
{
    private readonly FisDbContext _context;
    private readonly LegacyJobCardRepository _legacyRepository;
    private bool? _modernSchemaAvailable;

    private static readonly string[] ModernRequiredColumns =
    [
        "job_card_id",
        "vmf_code",
        "extra_code",
        "status_code",
        "priority",
        "assigned_to",
        "assigned_date",
        "jcs_comment",
        "damages",
        "comments",
        "authorizer",
        "reviewed",
        "labour_cost",
        "parts_cost",
        "other_cost",
        "total_cost",
        "invoice_number",
        "invoice_date",
        "service_provider",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    public JobCardRepository(FisDbContext context)
    {
        _context = context;
        _legacyRepository = new LegacyJobCardRepository(context);
    }

    /// <summary>
    /// Get job card by ID with navigation properties
    /// </summary>
    public async Task<JobCard?> GetByIdAsync(int jobCardId)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByIdAsync(jobCardId);

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByVehicleAndExtraAsync(vmfCode, extraCode);

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetAllAsync();

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted)
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    public async Task<JobCardPage> GetPageAsync(JobCardPageQuery query)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetPageAsync(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var searchTerm = query.SearchTerm?.Trim() ?? string.Empty;
        var searchType = string.Equals(query.SearchType, "GP", StringComparison.OrdinalIgnoreCase)
            ? "GP"
            : "GG";
        var statusCodes = query.StatusCodes?.Distinct().ToArray() ?? [];

        var filtered = _context
            .JobCards.AsNoTracking()
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc => !jc.is_deleted);

        if (statusCodes.Length > 0)
            filtered = filtered.Where(jc => statusCodes.Contains(jc.status_code));

        if (query.JobCardId.HasValue)
            filtered = filtered.Where(jc => jc.job_card_id == query.JobCardId.Value);

        if (searchTerm.Length > 0)
        {
            var searchId = int.TryParse(searchTerm, out var parsedSearchId)
                ? parsedSearchId
                : (int?)null;

            filtered =
                searchType == "GP"
                    ? filtered.Where(jc =>
                        (
                            jc.Vehicle != null
                            && jc.Vehicle.registration_number != null
                            && jc.Vehicle.registration_number.Contains(searchTerm)
                        ) || (searchId.HasValue && jc.job_card_id == searchId.Value)
                    )
                    : filtered.Where(jc =>
                        (
                            jc.Vehicle != null
                            && jc.Vehicle.fleet_number != null
                            && jc.Vehicle.fleet_number.Contains(searchTerm)
                        ) || (searchId.HasValue && jc.job_card_id == searchId.Value)
                    );
        }

        var totalRecords = await filtered.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        var items = await filtered
            .OrderByDescending(jc => jc.date_created)
            .ThenByDescending(jc => jc.job_card_id)
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync();

        return new JobCardPage(items, page, pageSize, totalRecords);
    }

    public async Task<JobCardPage> GetPriorityUnassignedPageAsync(
        PriorityUnassignedJobCardPageQuery query
    )
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetPriorityUnassignedPageAsync(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var filtered = _context
            .JobCards.AsNoTracking()
            .Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc =>
                !jc.is_deleted
                && jc.priority == "H"
                && jc.assigned_to == null
                && jc.status_code != 5
                && jc.status_code != 7
            );

        var totalRecords = await filtered.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        var items = await filtered
            .OrderByDescending(jc => jc.date_created)
            .ThenByDescending(jc => jc.job_card_id)
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync();

        return new JobCardPage(items, page, pageSize, totalRecords);
    }

    public async Task<RepairCostReportPage> GetRepairCostReportPageAsync(
        RepairCostReportPageQuery query
    )
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetRepairCostReportPageAsync(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var filtered = _context
            .JobCards.AsNoTracking()
            .Where(jc => !jc.is_deleted && jc.status_code == 5);

        if (query.VmfCode.HasValue)
            filtered = filtered.Where(jc => jc.vmf_code == query.VmfCode.Value);

        if (query.VmfCodes is not null)
        {
            var vmfCodes = query.VmfCodes.Distinct().ToArray();
            filtered =
                vmfCodes.Length == 0
                    ? filtered.Where(_ => false)
                    : filtered.Where(jc => vmfCodes.Contains(jc.vmf_code));
        }

        if (query.FromDate.HasValue)
            filtered = filtered.Where(jc => jc.date_updated >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            filtered = filtered.Where(jc => jc.date_updated <= query.ToDate.Value.AddDays(1));

        var totalRecords = await filtered.CountAsync();
        var grandTotal = await filtered.SumAsync(jc => jc.total_cost ?? 0m);
        var totalLabour = await filtered.SumAsync(jc => jc.labour_cost ?? 0m);
        var totalParts = await filtered.SumAsync(jc => jc.parts_cost ?? 0m);
        var totalOther = await filtered.SumAsync(jc => jc.other_cost ?? 0m);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        var items = await filtered
            .Include(jc => jc.Vehicle)
            .OrderByDescending(jc => jc.date_updated)
            .ThenByDescending(jc => jc.job_card_id)
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync();

        return new RepairCostReportPage(
            items,
            page,
            pageSize,
            totalRecords,
            grandTotal,
            totalLabour,
            totalParts,
            totalOther
        );
    }

    /// <summary>
    /// Get job cards by GG number (fleet_number)
    /// Legacy: Replaces DEV_SEL_JobCards stored procedure
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByGGNumberAsync(ggNumber);

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc =>
                !jc.is_deleted && jc.Vehicle != null && jc.Vehicle.fleet_number == ggNumber
            )
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get priority unassigned job cards
    /// Legacy: Replaces DEV_SEL_JobcardPriotiyForCapturers stored procedure
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync()
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetPriorityUnassignedAsync();

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc =>
                !jc.is_deleted
                && jc.priority == "H"
                && jc.assigned_to == null
                && jc.status_code != 5 // Not Complete
                && jc.status_code != 7
            ) // Not Canceled
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get assigned priority job cards
    /// Legacy: Replaces DEV_SEL_JobcardsForAssignedPriority stored procedure
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetAssignedPriorityAsync()
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetAssignedPriorityAsync();

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
            .Include(jc => jc.ExtraCodeRef)
            .Include(jc => jc.AssignedToUser)
            .Include(jc => jc.AuthorizerUser)
            .Where(jc =>
                !jc.is_deleted
                && jc.priority == "H"
                && jc.assigned_to != null
                && jc.status_code != 5 // Not Complete
                && jc.status_code != 7
            ) // Not Canceled
            .OrderByDescending(jc => jc.date_created)
            .ToListAsync();
    }

    /// <summary>
    /// Get job cards by status code
    /// Legacy: Used by DEV_SEL_JobcardsStatusReportForAuthorizer
    /// </summary>
    public async Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByStatusAsync(statusCode);

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByAuthorizerAsync(authorizerUserId);

        return await _context
            .JobCards.Include(jc => jc.Vehicle)
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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.CreateAsync(jobCard, currentUserId);

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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.UpdateAsync(jobCard, currentUserId);

        var existingJobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCard.job_card_id && !jc.is_deleted
            )
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
        if (!await IsModernSchemaAvailableAsync())
        {
            await _legacyRepository.DeleteAsync(jobCardId, currentUserId);
            return;
        }

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.AuthorizeAsync(jobCardId, authorizerUserId, comment);

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

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
    public async Task<JobCard> DeclineAsync(
        int jobCardId,
        int authorizerUserId,
        string declineReason
    )
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.DeclineAsync(jobCardId, authorizerUserId, declineReason);

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

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
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.CancelAsync(jobCardId, currentUserId, cancelReason);

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

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
    public async Task<JobCard> CloseAsync(
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
    )
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.CloseAsync(
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

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

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

        // Capture repair costs if provided
        ApplyCosts(
            jobCard,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider
        );

        await _context.SaveChangesAsync();

        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve closed job card");
    }

    public async Task<JobCard> UpdateCostsAsync(
        int jobCardId,
        int currentUserId,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider
    )
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.UpdateCostsAsync(
                jobCardId,
                currentUserId,
                labourCost,
                partsCost,
                otherCost,
                invoiceNumber,
                invoiceDate,
                serviceProvider
            );

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        ApplyCosts(
            jobCard,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider
        );
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve updated job card");
    }

    /// <summary>
    /// Applies cost fields and auto-calculates total_cost.
    /// Only overwrites fields that are explicitly provided (non-null).
    /// </summary>
    private static void ApplyCosts(
        JobCard jobCard,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider
    )
    {
        if (labourCost.HasValue)
            jobCard.labour_cost = labourCost;
        if (partsCost.HasValue)
            jobCard.parts_cost = partsCost;
        if (otherCost.HasValue)
            jobCard.other_cost = otherCost;
        if (invoiceNumber != null)
            jobCard.invoice_number = invoiceNumber;
        if (invoiceDate.HasValue)
            jobCard.invoice_date = invoiceDate;
        if (serviceProvider != null)
            jobCard.service_provider = serviceProvider;

        // Auto-calculate total from whatever is now set
        jobCard.total_cost =
            (jobCard.labour_cost ?? 0) + (jobCard.parts_cost ?? 0) + (jobCard.other_cost ?? 0);
    }

    /// <summary>
    /// Update job card status
    /// Legacy: Replaces DEV_UPD_JobcardUpdateStatusToInProgress and similar procedures
    /// </summary>
    public async Task<JobCard> UpdateStatusAsync(
        int jobCardId,
        int newStatusCode,
        int currentUserId
    )
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.UpdateStatusAsync(
                jobCardId,
                newStatusCode,
                currentUserId
            );

        var jobCard =
            await _context.JobCards.FirstOrDefaultAsync(jc =>
                jc.job_card_id == jobCardId && !jc.is_deleted
            ) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        jobCard.status_code = newStatusCode;
        jobCard.date_updated = DateTime.UtcNow;
        jobCard.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();

        // Reload with navigation properties
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Failed to retrieve updated job card");
    }

    private async Task<bool> IsModernSchemaAvailableAsync()
    {
        if (_modernSchemaAvailable.HasValue)
            return _modernSchemaAvailable.Value;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, "job_cards");

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            _modernSchemaAvailable = ModernRequiredColumns.All(columns.Contains);
            return _modernSchemaAvailable.Value;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
