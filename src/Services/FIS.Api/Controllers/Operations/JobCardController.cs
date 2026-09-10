using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Controller for managing job cards (work orders for vehicle maintenance/repairs)
/// Legacy: Replaces stored procedures DEV_SEL_JobcardPriotiyForCapturers, DEV_INS_NewJobCards, etc.
/// </summary>
[ApiController]
[Authorize]
[Route("api/jobcards")]
public class JobCardController : BaseApiController
{
    private readonly IJobCardRepository _repository;
    private readonly IContractRepository _contractRepository;
    private readonly ILogger<JobCardController> _logger;

    public JobCardController(
        IJobCardRepository repository,
        IContractRepository contractRepository,
        ILogger<JobCardController> logger
    )
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all job cards
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobCardResponseDto>>> GetAll()
    {
        try
        {
            _logger.LogInformation("Getting all job cards");
            var jobCards = await _repository.GetAllAsync();
            var dtos = jobCards.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all job cards");
            return StatusCode(500, new { error = "An error occurred while retrieving job cards" });
        }
    }

    /// <summary>
    /// Get job card by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> GetById(int id)
    {
        try
        {
            _logger.LogInformation("Getting job card with ID: {JobCardId}", id);
            var jobCard = await _repository.GetByIdAsync(id);

            if (jobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }

            return Ok(MapToDto(jobCard));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job card {JobCardId}", id);
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving the job card" }
            );
        }
    }

    /// <summary>
    /// Get job cards by GG number (fleet number)
    /// Legacy: Replaces DEV_SEL_JobCards stored procedure
    /// </summary>
    [HttpGet("by-gg/{ggNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobCardResponseDto>>> GetByGGNumber(string ggNumber)
    {
        try
        {
            _logger.LogInformation("Getting job cards for GG number: {GGNumber}", ggNumber);
            var jobCards = await _repository.GetByGGNumberAsync(ggNumber);
            var dtos = jobCards.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job cards for GG number: {GGNumber}", ggNumber);
            return StatusCode(500, new { error = "An error occurred while retrieving job cards" });
        }
    }

    /// <summary>
    /// Get priority jobcards that are unassigned
    /// Legacy: Replaces DEV_SEL_JobcardPriotiyForCapturers stored procedure
    /// </summary>
    [HttpGet("priority/unassigned")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobCardResponseDto>>> GetPriorityUnassigned()
    {
        try
        {
            _logger.LogInformation("Getting priority unassigned job cards");
            var jobCards = await _repository.GetPriorityUnassignedAsync();
            var dtos = jobCards.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting priority unassigned job cards");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving priority unassigned job cards" }
            );
        }
    }

    /// <summary>
    /// Create a new job card
    /// Legacy: Replaces DEV_INS_NewJobCards stored procedure
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Create(
        [FromBody] CreateJobCardDto createDto
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "Creating new job card for vehicle {VmfCode}, extra {ExtraCode} by user {UserId}",
                createDto.vmf_code,
                createDto.extra_code,
                currentUserId
            );

            var jobCard = new JobCard
            {
                vmf_code = createDto.vmf_code,
                extra_code = createDto.extra_code,
                jcs_comment = createDto.jcs_comment,
                damages = createDto.damages,
                priority = createDto.priority,
                status_code = 1, // Pending
                reviewed = "N",
            };

            var created = await _repository.CreateAsync(jobCard, currentUserId);
            var dto = MapToDto(created);

            _logger.LogInformation("Job card created with ID: {JobCardId}", created.job_card_id);
            return CreatedAtAction(nameof(GetById), new { id = created.job_card_id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job card");
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while creating the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Update an existing job card
    /// Legacy: Replaces DEV_UPD_Jobcards stored procedure
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Update(
        int id,
        [FromBody] UpdateJobCardDto updateDto
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "Updating job card {JobCardId} by user {UserId}",
                id,
                currentUserId
            );

            var existingJobCard = await _repository.GetByIdAsync(id);
            if (existingJobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }

            // Update fields
            existingJobCard.jcs_comment = updateDto.jcs_comment;
            existingJobCard.damages = updateDto.damages;
            existingJobCard.comments = updateDto.comments;
            existingJobCard.assigned_to = updateDto.assigned_to;
            existingJobCard.assigned_date = updateDto.assigned_date;
            existingJobCard.priority = updateDto.priority;

            var updated = await _repository.UpdateAsync(existingJobCard, currentUserId);
            var dto = MapToDto(updated);

            _logger.LogInformation("Job card {JobCardId} updated successfully", id);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while updating the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Authorize a job card
    /// Legacy: Replaces DEV_UPD_JobcardAuthorizersUpdates stored procedure (approve path)
    /// </summary>
    [HttpPost("{id}/authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Authorize(
        int id,
        [FromBody] JobCardAuthorizationDto? authDto = null
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} authorizing job card {JobCardId}",
                currentUserId,
                id
            );

            // Fetch job card to validate self-approval prevention
            var jobCard = await _repository.GetByIdAsync(id);
            if (jobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(jobCard, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            var authorized = await _repository.AuthorizeAsync(id, currentUserId, authDto?.comment);
            var dto = MapToDto(authorized);

            _logger.LogInformation(
                "Job card {JobCardId} authorized by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while authorizing the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Decline a job card
    /// Legacy: Replaces DEV_UPD_JobcardAuthorizersUpdates stored procedure (decline path)
    /// </summary>
    [HttpPost("{id}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Decline(
        int id,
        [FromBody] JobCardDeclineDto declineDto
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(declineDto?.decline_reason))
            {
                return BadRequest(new { error = "Decline reason is required" });
            }

            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} declining job card {JobCardId} with reason: {Reason}",
                currentUserId,
                id,
                declineDto.decline_reason
            );

            // Fetch job card to validate self-approval prevention
            var jobCard = await _repository.GetByIdAsync(id);
            if (jobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }

            // Prevent self-review/decline
            var selfApprovalCheck = ValidateSelfApprovalPrevention(jobCard, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            var declined = await _repository.DeclineAsync(
                id,
                currentUserId,
                declineDto.decline_reason
            );
            var dto = MapToDto(declined);

            _logger.LogInformation(
                "Job card {JobCardId} declined by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while declining the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Cancel a job card
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Cancel(
        int id,
        [FromBody] JobCardCancelDto? cancelDto = null
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} canceling job card {JobCardId}",
                currentUserId,
                id
            );

            var canceled = await _repository.CancelAsync(
                id,
                currentUserId,
                cancelDto?.cancel_reason
            );
            var dto = MapToDto(canceled);

            _logger.LogInformation(
                "Job card {JobCardId} canceled by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while canceling the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Close a job card
    /// </summary>
    [HttpPost("{id}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Close(
        int id,
        [FromBody] JobCardCloseDto? closeDto = null
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} closing job card {JobCardId}", currentUserId, id);

            var closed = await _repository.CloseAsync(
                id,
                currentUserId,
                closeDto?.close_notes,
                labourCost: closeDto?.labour_cost,
                partsCost: closeDto?.parts_cost,
                otherCost: closeDto?.other_cost,
                invoiceNumber: closeDto?.invoice_number,
                invoiceDate: closeDto?.invoice_date,
                serviceProvider: closeDto?.service_provider
            );
            var dto = MapToDto(closed);

            _logger.LogInformation(
                "Job card {JobCardId} closed by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing job card {JobCardId}", id);
            return StatusCode(
                500,
                new { error = "An error occurred while closing the job card", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Delete a job card (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} deleting job card {JobCardId}",
                currentUserId,
                id
            );

            await _repository.DeleteAsync(id, currentUserId);

            _logger.LogInformation(
                "Job card {JobCardId} deleted by user {UserId}",
                id,
                currentUserId
            );
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while deleting the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Amend repair costs on a job card (including after it is closed).
    /// Used when the invoice arrives after the job card was already closed,
    /// or to correct a capturing error.
    /// Only provided fields are updated — omit any field to leave it unchanged.
    /// Assumption: post-close amendment allowed. Confirm with users (QUESTIONS.md MX-1).
    /// </summary>
    [HttpPatch("{id}/costs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobCardResponseDto>> UpdateCosts(
        int id,
        [FromBody] JobCardCostDto costDto
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            var updated = await _repository.UpdateCostsAsync(
                id,
                currentUserId,
                labourCost: costDto.labour_cost,
                partsCost: costDto.parts_cost,
                otherCost: costDto.other_cost,
                invoiceNumber: costDto.invoice_number,
                invoiceDate: costDto.invoice_date,
                serviceProvider: costDto.service_provider
            );

            _logger.LogInformation(
                "Repair costs updated on job card {JobCardId} by user {UserId}",
                id,
                currentUserId
            );
            return Ok(MapToDto(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating costs on job card {JobCardId}", id);
            return StatusCode(500, new { error = "Failed to update costs", message = ex.Message });
        }
    }

    /// <summary>
    /// Repair cost report — returns all closed job cards with their captured costs,
    /// filterable by vehicle, site/department, and date range.
    /// Used by client departments to query repair expenditure on their vehicles.
    /// Assumption: site_code filter uses the vehicle's current contract site.
    /// Confirm scope with users (QUESTIONS.md MX-3).
    /// </summary>
    [HttpGet("repair-cost-report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RepairCostReport(
        [FromQuery] int? vmfCode = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null
    )
    {
        try
        {
            var results = (await _repository.GetByStatusAsync(5)).AsEnumerable();

            if (vmfCode.HasValue)
                results = results.Where(j => j.vmf_code == vmfCode.Value);

            if (fromDate.HasValue)
                results = results.Where(j => j.date_updated >= fromDate.Value);

            if (toDate.HasValue)
                results = results.Where(j => j.date_updated <= toDate.Value.AddDays(1));

            // Site filter: use the compatibility contract repository so the
            // original contract table does not go through EF's static model.
            if (siteCode.HasValue)
            {
                var vehiclesAtSite = (await _contractRepository.GetAllAsync())
                    .Where(c => c.site_code == siteCode.Value)
                    .Select(c => c.vmf_code)
                    .Distinct()
                    .ToHashSet();
                results = results.Where(j => vehiclesAtSite.Contains(j.vmf_code));
            }

            var orderedResults = results.OrderByDescending(j => j.date_updated).ToList();

            var lineItems = orderedResults
                .Select(j => new
                {
                    job_card_id = j.job_card_id,
                    vmf_code = j.vmf_code,
                    fleet_number = j.Vehicle?.fleet_number,
                    registration = j.Vehicle?.registration_number,
                    damages = j.damages,
                    service_provider = j.service_provider,
                    invoice_number = j.invoice_number,
                    invoice_date = j.invoice_date?.ToString("yyyy-MM-dd"),
                    labour_cost = j.labour_cost,
                    parts_cost = j.parts_cost,
                    other_cost = j.other_cost,
                    total_cost = j.total_cost,
                    closed_date = j.date_updated?.ToString("yyyy-MM-dd"),
                })
                .ToList();

            return Ok(
                new
                {
                    filters_applied = new
                    {
                        vmfCode,
                        siteCode,
                        fromDate,
                        toDate,
                    },
                    total_records = lineItems.Count,
                    grand_total = lineItems.Sum(i => i.total_cost ?? 0),
                    total_labour = lineItems.Sum(i => i.labour_cost ?? 0),
                    total_parts = lineItems.Sum(i => i.parts_cost ?? 0),
                    total_other = lineItems.Sum(i => i.other_cost ?? 0),
                    line_items = lineItems,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating repair cost report");
            return StatusCode(
                500,
                new { error = "Failed to generate report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Validates that the current user is not the job card capturer (prevents self-approval)
    /// </summary>
    /// <returns>Null if validation passes, or ForbidResult with error message if validation fails</returns>
    private ActionResult? ValidateSelfApprovalPrevention(JobCard jobCard, int currentUserId)
    {
        // Check if current user is the job card capturer
        if (
            jobCard.created_by_user_code.HasValue
            && jobCard.created_by_user_code.Value == currentUserId
        )
        {
            _logger.LogWarning(
                "Self-approval blocked: User {UserId} attempted to authorize their own job card {JobCardId}",
                currentUserId,
                jobCard.job_card_id
            );

            return StatusCode(
                403,
                new
                {
                    error = "You cannot authorize or review your own job card.",
                    jobCardId = jobCard.job_card_id,
                    userId = currentUserId,
                }
            );
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Get friendly status text from status code
    /// </summary>
    private string GetStatusText(int statusCode)
    {
        return statusCode switch
        {
            0 => "Pending Review",
            1 => "Pending",
            2 => "Awaiting Authorization",
            3 => "Authorized",
            4 => "In Progress",
            5 => "Complete",
            6 => "Failed",
            7 => "Canceled",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Map JobCard entity to JobCardResponseDto
    /// </summary>
    private JobCardResponseDto MapToDto(JobCard jobCard)
    {
        return new JobCardResponseDto
        {
            job_card_id = jobCard.job_card_id,
            vmf_code = jobCard.vmf_code,
            gg_number = jobCard.Vehicle?.fleet_number,
            registration_number = jobCard.Vehicle?.registration_number,
            extra_code = jobCard.extra_code,
            extra_description = jobCard.ExtraCodeRef?.extra_description,
            status_code = jobCard.status_code,
            status_text = GetStatusText(jobCard.status_code),
            priority = jobCard.priority,
            assigned_to = jobCard.assigned_to,
            assigned_to_name =
                jobCard.AssignedToUser?.email
                ?? jobCard.AssignedToUser?.user_access_code.ToString(),
            assigned_date = jobCard.assigned_date,
            jcs_comment = jobCard.jcs_comment,
            damages = jobCard.damages,
            comments = jobCard.comments,
            authorizer = jobCard.authorizer,
            authorizer_name =
                jobCard.AuthorizerUser?.email
                ?? jobCard.AuthorizerUser?.user_access_code.ToString(),
            reviewed = jobCard.reviewed,
            captured_by_user_code = jobCard.created_by_user_code,
            authorized_by_user_code = jobCard.authorizer,
            date_created = jobCard.date_created,
            date_updated = jobCard.date_updated,
            created_by_user_code = jobCard.created_by_user_code,
            modified_by_user_code = jobCard.modified_by_user_code,
            // Repair costs
            labour_cost = jobCard.labour_cost,
            parts_cost = jobCard.parts_cost,
            other_cost = jobCard.other_cost,
            total_cost = jobCard.total_cost,
            invoice_number = jobCard.invoice_number,
            invoice_date = jobCard.invoice_date,
            service_provider = jobCard.service_provider,
        };
    }
}
