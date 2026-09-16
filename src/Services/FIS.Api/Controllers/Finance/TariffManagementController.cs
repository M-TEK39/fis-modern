using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Core.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Class-based tariff management: CRUD + approval workflow.
///
/// Approval rules:
///   - Tariffs ≤ R100,000/month → auto-approved on creation.
///   - Tariffs > R100,000/month → require explicit approval (Draft → Submit → Approve).
///   - Segregation of duties: the capturer cannot approve their own tariff.
///
/// Status codes:
///   0 = Draft       — created but not yet submitted
///   1 = PendingApproval — submitted, awaiting approver
///   2 = Approved    — effective, used in billing calculations
///   3 = Rejected    — sent back with reason; capturer must edit and resubmit
/// </summary>
[ApiController]
[Authorize]
[Route("api/tariff-management")]
public class TariffManagementController : BaseApiController
{
    private readonly ITariffManagementRepository _repository;
    private readonly ILogger<TariffManagementController> _logger;

    private const string TariffParametersRole = "Financial Tariff Parameters";
    private const string TariffApproverRole = "Financial Tariff Parameters (Approver)";

    public TariffManagementController(
        ITariffManagementRepository repository,
        ILogger<TariffManagementController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    private ActionResult? ValidateSelfApprovalPrevention(Tariff tariff, int currentUserId)
    {
        if (
            tariff.created_by_user_code.HasValue
            && tariff.created_by_user_code.Value == currentUserId
        )
        {
            _logger.LogWarning(
                "Self-approval blocked: User {UserId} attempted to approve their own tariff {TariffCode}",
                currentUserId,
                tariff.tariff_code
            );

            return StatusCode(
                403,
                new
                {
                    error = "You cannot approve or reject your own tariff.",
                    tariffCode = tariff.tariff_code,
                    userId = currentUserId,
                }
            );
        }
        return null;
    }

    private static string GetStatusText(short status) =>
        status switch
        {
            0 => "Draft",
            1 => "Pending Approval",
            2 => "Approved",
            3 => "Rejected",
            _ => "Unknown",
        };

    private static object MapToDto(Tariff t) =>
        new
        {
            t.tariff_code,
            t.class_code,
            t.year_manufactured,
            t.monthly_fixed_amount,
            t.monthly_odo_amount,
            t.daily_fixed_amount,
            t.hourly_fixed_amount,
            t.effective_start_date,
            t.effective_end_date,
            t.replacement_percent,
            t.loss_percent,
            t.profit_percent,
            t.overhead_percent,
            t.accident_percent,
            t.fuel_kilo_tariff,
            t.tariff_approval_status,
            approval_status_text = GetStatusText(t.tariff_approval_status),
            t.approver_code,
            t.approval_date,
            t.rejection_reason,
            t.created_by_user_code,
            t.date_created,
            t.modified_by_user_code,
            t.date_updated,
            requires_approval = t.monthly_fixed_amount
                > TariffManagementRepository.ApprovalThreshold,
            approval_threshold = TariffManagementRepository.ApprovalThreshold,
        };

    private bool HasTariffParametersAccess() =>
        HasAnyRole(TariffParametersRole, TariffApproverRole);

    private bool HasTariffApproverAccess() => HasAnyRole(TariffApproverRole);

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
            return true;

        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );

        return roleClaims.Any(role =>
            expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    /// <summary>
    /// Get all tariffs with optional filters.
    /// </summary>
    /// <param name="class_code">Filter by vehicle class</param>
    /// <param name="year_manufactured">Filter by year of manufacture</param>
    /// <param name="tariff_approval_status">Filter by status: 0=Draft, 1=PendingApproval, 2=Approved, 3=Rejected</param>
    /// <param name="effective_on">Return only tariffs whose effective date range covers this date</param>
    [HttpGet]
    public async Task<ActionResult> GetAll(
        [FromQuery] short? class_code = null,
        [FromQuery] short? year_manufactured = null,
        [FromQuery] short? tariff_approval_status = null,
        [FromQuery] DateTime? effective_on = null
    )
    {
        if (!HasTariffParametersAccess())
            return Forbid();

        try
        {
            var tariffs = await _repository.GetAllAsync();

            IEnumerable<Tariff> filtered = tariffs;

            if (class_code.HasValue)
                filtered = filtered.Where(t => t.class_code == class_code.Value);

            if (year_manufactured.HasValue)
                filtered = filtered.Where(t => t.year_manufactured == year_manufactured.Value);

            if (tariff_approval_status.HasValue)
                filtered = filtered.Where(t =>
                    t.tariff_approval_status == tariff_approval_status.Value
                );

            if (effective_on.HasValue)
            {
                var date = effective_on.Value.Date;
                filtered = filtered.Where(t =>
                    t.effective_start_date.Date <= date
                    && (t.effective_end_date == null || t.effective_end_date.Value.Date >= date)
                );
            }

            var result = filtered.Select(MapToDto).ToList();

            return Ok(
                new
                {
                    total_count = result.Count,
                    filters_applied = new
                    {
                        class_code,
                        year_manufactured,
                        tariff_approval_status,
                        effective_on,
                    },
                    tariffs = result,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tariffs");
            return StatusCode(500, new { error = "Failed to retrieve tariffs" });
        }
    }

    /// <summary>
    /// Get approved tariffs only (used in billing)
    /// </summary>
    [HttpGet("approved")]
    public async Task<ActionResult> GetApproved()
    {
        if (!HasTariffParametersAccess())
            return Forbid();

        try
        {
            var tariffs = await _repository.GetApprovedAsync();
            return Ok(tariffs.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving approved tariffs");
            return StatusCode(500, new { error = "Failed to retrieve approved tariffs" });
        }
    }

    /// <summary>
    /// Get tariffs pending approval — the approver's queue
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult> GetPendingApproval()
    {
        if (!HasTariffApproverAccess())
            return Forbid();

        try
        {
            var tariffs = await _repository.GetPendingApprovalAsync();
            return Ok(tariffs.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending tariffs");
            return StatusCode(500, new { error = "Failed to retrieve pending tariffs" });
        }
    }

    /// <summary>
    /// Get a tariff by code
    /// </summary>
    [HttpGet("{tariffCode}")]
    public async Task<ActionResult> GetById(int tariffCode)
    {
        if (!HasTariffParametersAccess())
            return Forbid();

        try
        {
            var tariff = await _repository.GetByIdAsync(tariffCode);
            if (tariff == null)
                return NotFound();
            return Ok(MapToDto(tariff));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tariff {TariffCode}", tariffCode);
            return StatusCode(500, new { error = "Failed to retrieve tariff" });
        }
    }

    /// <summary>
    /// Create a new class-based tariff.
    /// Tariffs ≤ R100,000/month are auto-approved.
    /// Tariffs > R100,000/month are created as Draft and must be submitted for approval.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateTariffDto request)
    {
        if (!HasTariffParametersAccess())
            return Forbid();

        try
        {
            int currentUserId = GetCurrentUserId();

            var tariff = new Tariff
            {
                class_code = request.class_code,
                year_manufactured = request.year_manufactured,
                monthly_fixed_amount = request.monthly_fixed_amount,
                monthly_odo_amount = request.monthly_odo_amount,
                daily_fixed_amount = request.daily_fixed_amount,
                hourly_fixed_amount = request.hourly_fixed_amount,
                effective_start_date = request.effective_start_date,
                effective_end_date = request.effective_end_date,
                replacement_percent = request.replacement_percent,
                loss_percent = request.loss_percent,
                profit_percent = request.profit_percent,
                overhead_percent = request.overhead_percent,
                accident_percent = request.accident_percent,
                fuel_kilo_tariff = request.fuel_kilo_tariff,
            };

            var created = await _repository.CreateAsync(tariff, currentUserId);

            _logger.LogInformation(
                "Tariff {TariffCode} created by user {UserId} (status={Status}, monthly={Monthly})",
                created.tariff_code,
                currentUserId,
                GetStatusText(created.tariff_approval_status),
                created.monthly_fixed_amount
            );

            var needsApproval =
                created.monthly_fixed_amount > TariffManagementRepository.ApprovalThreshold;
            return CreatedAtAction(
                nameof(GetById),
                new { tariffCode = created.tariff_code },
                new
                {
                    tariff = MapToDto(created),
                    message = needsApproval
                        ? $"Tariff exceeds R{TariffManagementRepository.ApprovalThreshold:N0}/month. Call /submit to send for approval."
                        : "Tariff auto-approved (below threshold).",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tariff");
            return StatusCode(500, new { error = "Failed to create tariff", message = ex.Message });
        }
    }

    /// <summary>
    /// Edit a Draft (0) or Rejected (3) tariff.
    /// Approved or Pending tariffs cannot be edited directly.
    /// </summary>
    [HttpPut("{tariffCode}")]
    public async Task<ActionResult> Update(int tariffCode, [FromBody] UpdateTariffDto request)
    {
        if (!HasTariffParametersAccess())
            return Forbid();

        try
        {
            int currentUserId = GetCurrentUserId();

            var existing = await _repository.GetByIdAsync(tariffCode);
            if (existing == null)
                return NotFound();

            existing.class_code = request.class_code ?? existing.class_code;
            existing.year_manufactured = request.year_manufactured ?? existing.year_manufactured;
            existing.monthly_fixed_amount =
                request.monthly_fixed_amount ?? existing.monthly_fixed_amount;
            existing.monthly_odo_amount = request.monthly_odo_amount ?? existing.monthly_odo_amount;
            existing.daily_fixed_amount = request.daily_fixed_amount ?? existing.daily_fixed_amount;
            existing.hourly_fixed_amount =
                request.hourly_fixed_amount ?? existing.hourly_fixed_amount;
            existing.effective_start_date =
                request.effective_start_date ?? existing.effective_start_date;
            existing.effective_end_date = request.effective_end_date ?? existing.effective_end_date;
            existing.replacement_percent =
                request.replacement_percent ?? existing.replacement_percent;
            existing.loss_percent = request.loss_percent ?? existing.loss_percent;
            existing.profit_percent = request.profit_percent ?? existing.profit_percent;
            existing.overhead_percent = request.overhead_percent ?? existing.overhead_percent;
            existing.accident_percent = request.accident_percent ?? existing.accident_percent;
            existing.fuel_kilo_tariff = request.fuel_kilo_tariff ?? existing.fuel_kilo_tariff;

            await _repository.UpdateAsync(existing, currentUserId);

            _logger.LogInformation(
                "Tariff {TariffCode} updated by user {UserId}",
                tariffCode,
                currentUserId
            );

            return Ok(MapToDto(existing));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tariff {TariffCode}", tariffCode);
            return StatusCode(500, new { error = "Failed to update tariff", message = ex.Message });
        }
    }

    /// <summary>
    /// Submit a Draft or Rejected tariff for approval.
    /// Only required for tariffs > R100,000/month.
    /// Only the original capturer can submit.
    /// </summary>
    [HttpPost("{tariffCode}/submit")]
    public async Task<ActionResult> Submit(int tariffCode)
    {
        if (!HasTariffParametersAccess())
            return Forbid();

        try
        {
            int currentUserId = GetCurrentUserId();
            var tariff = await _repository.SubmitForApprovalAsync(tariffCode, currentUserId);

            _logger.LogInformation(
                "Tariff {TariffCode} submitted for approval by user {UserId}",
                tariffCode,
                currentUserId
            );

            return Ok(
                new
                {
                    message = "Tariff submitted for approval.",
                    tariffCode,
                    tariff_approval_status = tariff.tariff_approval_status,
                    approval_status_text = GetStatusText(tariff.tariff_approval_status),
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting tariff {TariffCode}", tariffCode);
            return StatusCode(500, new { error = "Failed to submit tariff", message = ex.Message });
        }
    }

    /// <summary>
    /// Approve a pending tariff.
    /// Segregation of duties: the capturer cannot approve their own tariff.
    /// </summary>
    [HttpPost("{tariffCode}/approve")]
    public async Task<ActionResult> Approve(
        int tariffCode,
        [FromBody] TariffApprovalDto? request = null
    )
    {
        if (!HasTariffApproverAccess())
            return Forbid();

        try
        {
            int currentUserId = GetCurrentUserId();

            var tariff = await _repository.GetByIdAsync(tariffCode);
            if (tariff == null)
                return NotFound();

            var selfApprovalCheck = ValidateSelfApprovalPrevention(tariff, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            await _repository.ApproveAsync(tariffCode, currentUserId);

            _logger.LogInformation(
                "Tariff {TariffCode} approved by user {UserId}",
                tariffCode,
                currentUserId
            );

            return Ok(
                new
                {
                    message = "Tariff approved. It is now effective for billing calculations.",
                    tariffCode,
                    approver_code = currentUserId,
                    approval_date = DateTime.Now,
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving tariff {TariffCode}", tariffCode);
            return StatusCode(
                500,
                new { error = "Failed to approve tariff", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Reject a pending tariff with a reason.
    /// Segregation of duties: the capturer cannot reject their own tariff.
    /// The capturer must edit and resubmit after rejection.
    /// </summary>
    [HttpPost("{tariffCode}/reject")]
    public async Task<ActionResult> Reject(int tariffCode, [FromBody] TariffRejectionDto request)
    {
        if (!HasTariffApproverAccess())
            return Forbid();

        try
        {
            int currentUserId = GetCurrentUserId();

            var tariff = await _repository.GetByIdAsync(tariffCode);
            if (tariff == null)
                return NotFound();

            var selfApprovalCheck = ValidateSelfApprovalPrevention(tariff, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            await _repository.RejectAsync(tariffCode, currentUserId, request.rejection_reason);

            _logger.LogInformation(
                "Tariff {TariffCode} rejected by user {UserId}: {Reason}",
                tariffCode,
                currentUserId,
                request.rejection_reason
            );

            return Ok(
                new
                {
                    message = "Tariff rejected. The capturer must edit and resubmit.",
                    tariffCode,
                    rejection_reason = request.rejection_reason,
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting tariff {TariffCode}", tariffCode);
            return StatusCode(500, new { error = "Failed to reject tariff", message = ex.Message });
        }
    }
}

// ── DTOs ────────────────────────────────────────────────────────────────────

public class CreateTariffDto
{
    [Required]
    public short class_code { get; set; }

    public short? year_manufactured { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal monthly_fixed_amount { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal monthly_odo_amount { get; set; }

    public decimal? daily_fixed_amount { get; set; }
    public decimal? hourly_fixed_amount { get; set; }

    [Required]
    public DateTime effective_start_date { get; set; }

    public DateTime? effective_end_date { get; set; }

    public short? replacement_percent { get; set; }
    public short? loss_percent { get; set; }
    public short? profit_percent { get; set; }
    public short? overhead_percent { get; set; }
    public short? accident_percent { get; set; }
    public decimal? fuel_kilo_tariff { get; set; }
}

public class UpdateTariffDto
{
    public short? class_code { get; set; }
    public short? year_manufactured { get; set; }
    public decimal? monthly_fixed_amount { get; set; }
    public decimal? monthly_odo_amount { get; set; }
    public decimal? daily_fixed_amount { get; set; }
    public decimal? hourly_fixed_amount { get; set; }
    public DateTime? effective_start_date { get; set; }
    public DateTime? effective_end_date { get; set; }
    public short? replacement_percent { get; set; }
    public short? loss_percent { get; set; }
    public short? profit_percent { get; set; }
    public short? overhead_percent { get; set; }
    public short? accident_percent { get; set; }
    public decimal? fuel_kilo_tariff { get; set; }
}

public class TariffApprovalDto
{
    public string? notes { get; set; }
}

public class TariffRejectionDto
{
    [Required]
    [StringLength(500)]
    public string rejection_reason { get; set; } = string.Empty;
}
