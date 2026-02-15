using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace FIS.Api.Controllers;

/// <summary>
/// Contract/Hire management API endpoints
/// Provides comprehensive contract operations with legacy business logic
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ContractsController : BaseApiController
{
    private readonly IContractRepository _contractRepository;
    private readonly IContractService _contractService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IContractAuditLogRepository _auditLog;
    private readonly IEmailNotificationService _emailNotification;
    private readonly FisDbContext _context;
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(
        IContractRepository contractRepository,
        IContractService contractService,
        IVehicleRepository vehicleRepository,
        IContractAuditLogRepository auditLog,
        IEmailNotificationService emailNotification,
        FisDbContext context,
        ILogger<ContractsController> logger)
    {
        _contractRepository = contractRepository;
        _contractService = contractService;
        _vehicleRepository = vehicleRepository;
        _auditLog = auditLog;
        _emailNotification = emailNotification;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the current user is not the contract owner (prevents self-approval)
    /// </summary>
    /// <returns>Null if validation passes, or ForbidResult with error message if validation fails</returns>
    private ActionResult? ValidateSelfApprovalPrevention(Contract contract, int currentUserId)
    {
        if (contract.created_by_user_code.HasValue &&
            contract.created_by_user_code.Value == currentUserId)
        {
            _logger.LogWarning(
                "Self-approval blocked: User {UserId} attempted to approve their own contract {ContractId}",
                currentUserId, contract.contract_code);

            return StatusCode(403, new
            {
                error = "You cannot review or approve your own contract.",
                contractId = contract.contract_code,
                userId = currentUserId
            });
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Hire a vehicle (create new contract)
    /// Uses ContractService with full validation and journal integration
    /// </summary>
    [HttpPost("hire")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Hire([FromBody] HireContractDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            int currentUserId = GetCurrentUserId();

            var hireRequest = new HireContractRequest
            {
                VmfCode = request.VmfCode,
                SiteCode = request.SiteCode,
                StartOdometer = request.StartOdometer,
                DriverId = request.DriverId,
                Notes = request.Notes,
                TargetReturnDate = request.TargetReturnDate,
                CreatedByUserId = currentUserId
            };

            var contract = await _contractService.HireVehicleAsync(hireRequest);
            if (contract == null)
                return BadRequest(new { error = "Failed to create contract" });

            return CreatedAtAction(nameof(GetById), new { id = contract.contract_code }, contract);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contract for vehicle {VmfCode}", request.VmfCode);
            return StatusCode(500, new { error = "Failed to create contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Return a vehicle (end active contract by VMF code)
    /// Uses ContractService for proper business logic
    /// </summary>
    [HttpPost("{vmfCode}/return")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ReturnVehicle(int vmfCode, [FromBody] ReturnContractDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var success = await _contractService.EndContractByVmfCodeAsync(vmfCode, request.EndOdometer, request.Notes);
            if (!success)
                return NotFound(new { error = "No active contract found for vehicle" });

            return Ok(new { message = "Contract ended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending contract for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to end contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Get a paginated, filterable list of contracts.
    /// Used by the frontend contracts table.
    /// Supports filtering by status, site, date range, and still_current flag.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] short? status = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] string? stillCurrent = null,
        [FromQuery] DateTime? startDateFrom = null,
        [FromQuery] DateTime? startDateTo = null,
        [FromQuery] int? vmfCode = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 25;

            var query = _context.Contracts
                .Where(c => !c.is_deleted)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(c => c.contract_status_code == status.Value);
            if (siteCode.HasValue)
                query = query.Where(c => c.site_code == siteCode.Value);
            if (!string.IsNullOrEmpty(stillCurrent))
                query = query.Where(c => c.still_current == stillCurrent);
            if (startDateFrom.HasValue)
                query = query.Where(c => c.start_date >= startDateFrom.Value);
            if (startDateTo.HasValue)
                query = query.Where(c => c.start_date <= startDateTo.Value);
            if (vmfCode.HasValue)
                query = query.Where(c => c.vmf_code == vmfCode.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.date_created)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(c => c.Vehicle)
                .Include(c => c.Site)
                .ToListAsync();

            return Ok(new
            {
                page,
                page_size = pageSize,
                total_records = total,
                total_pages = (int)Math.Ceiling(total / (double)pageSize),
                data = items.Select(MapToDto)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contracts list");
            return StatusCode(500, new { error = "Failed to retrieve contracts", message = ex.Message });
        }
    }

    /// <summary>
    /// Get all active contracts
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<ContractResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContractResponseDto>>> GetActive()
    {
        var list = await _contractService.GetActiveContractsAsync();
        var dtos = list.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Get contract by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ContractResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractResponseDto?>> GetById(int id)
    {
        var contract = await _contractRepository.GetByIdAsync(id);
        if (contract == null) return NotFound();
        return Ok(MapToDto(contract));
    }

    /// <summary>
    /// Returns a rich, print-ready JSON payload for a contract.
    /// Frontend renders this as a printable contract document using window.print().
    /// Includes vehicle, site, driver, tariff reference, audit trail, and all contract terms.
    /// </summary>
    [HttpGet("{id}/printout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetPrintout(int id)
    {
        try
        {
            var contract = await _context.Contracts
                .Include(c => c.Vehicle)
                .Include(c => c.Site)
                .FirstOrDefaultAsync(c => c.contract_code == id && !c.is_deleted);

            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Capturer and approver user info (names for print document)
            var capturer = contract.created_by_user_code.HasValue
                ? await _context.Users.FindAsync(contract.created_by_user_code.Value)
                : null;
            var approver = contract.approver_code.HasValue
                ? await _context.Users.FindAsync(contract.approver_code.Value)
                : null;

            // Audit trail
            var auditEntries = await _context.ContractAuditLogs
                .Where(a => a.contract_code == id)
                .OrderBy(a => a.performed_at)
                .ToListAsync();

            return Ok(new
            {
                printed_at = DateTime.Now,
                document_title = $"Contract #{contract.contract_code} — Fleet Management",

                contract = new
                {
                    contract_code = contract.contract_code,
                    status_code = contract.contract_status_code,
                    status_text = contract.contract_status_code.HasValue
                        ? GetStatusText(contract.contract_status_code.Value) : "Unknown",
                    still_current = contract.still_current,
                    start_date = contract.start_date.ToString("yyyy-MM-dd"),
                    start_time = contract.start_time.ToString("HH:mm"),
                    end_date = contract.end_date?.ToString("yyyy-MM-dd"),
                    target_return_date = contract.target_return_date?.ToString("yyyy-MM-dd"),
                    start_odometer = contract.start_odometer,
                    end_odometer = contract.end_odometer,
                    contract_type = contract.contract_type,
                    driver_id = contract.Driver_id,
                    notes = contract.Notes,
                    authorisation = contract.Authorisation,
                },

                vehicle = contract.Vehicle == null ? null : new
                {
                    vmf_code = contract.Vehicle.vmf_code,
                    fleet_number = contract.Vehicle.fleet_number,
                    registration_number = contract.Vehicle.registration_number,
                    year_manufactured = contract.Vehicle.year_manufactured,
                    model_code = contract.Vehicle.model_code,
                    current_odo = contract.Vehicle.current_odo,
                },

                site = contract.Site == null ? null : new
                {
                    site_code = contract.Site.Site_code,
                    description = contract.Site.description,
                    res_person = contract.Site.res_person,
                    net_address = contract.Site.net_address,
                    telephone = contract.Site.telephone,
                },

                parties = new
                {
                    capturer = capturer == null ? null : new
                    {
                        user_code = capturer.user_access_code,
                        email = capturer.email
                    },
                    approver = approver == null ? null : new
                    {
                        user_code = approver.user_access_code,
                        email = approver.email
                    }
                },

                audit_trail = auditEntries.Select(a => new
                {
                    a.action,
                    a.performed_by_user_code,
                    performed_at = a.performed_at.ToString("yyyy-MM-dd HH:mm"),
                    old_status = a.old_status_code.HasValue ? GetStatusText(a.old_status_code.Value) : null,
                    new_status = a.new_status_code.HasValue ? GetStatusText(a.new_status_code.Value) : null,
                    a.notes
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating printout for contract {Id}", id);
            return StatusCode(500, new { error = "Failed to generate printout", message = ex.Message });
        }
    }

    #region Mapping

    private static ContractResponseDto MapToDto(Contract contract)
    {
        return new ContractResponseDto
        {
            ContractCode = contract.contract_code,
            VmfCode = contract.vmf_code,
            SiteCode = contract.site_code,
            ContractTypeCode = contract.contract_type,
            ContractStatusCode = contract.contract_status_code,
            ContractStatusDate = contract.contract_status_date,
            StillCurrent = contract.still_current,
            StartDate = contract.start_date,
            StartTime = contract.start_time,
            StartOdometer = contract.start_odometer,
            EndDate = contract.end_date,
            EndTime = contract.end_time,
            EndOdometer = contract.end_odometer,
            MonthlyKm = contract.monthly_km,
            HoursUsed = contract.hours_used,
            TargetReturnDate = contract.target_return_date,
            DriverId = contract.Driver_id,
            DriverName = contract.Driver_name,
            SiteDriverCode = contract.site_driver_code,
            ApproverCode = contract.approver_code,
            ParentContractCode = contract.parent_contract_code,
            ReliefForContract = contract.relief_for_contract,
            VehicleAssessmentCode = contract.vehicle_assessment_code,
            JournalDetailCode = contract.journal_detail_code,
            LockedForTransfer = contract.locked_for_transfer,
            Notes = contract.Notes,
            Authorisation = contract.Authorisation,
            ChargedUntil = contract.Charged_Until,
            CollectorFirstname = contract.collector_firstname,
            UserCode = contract.user_code,
            ContractGroupCode = contract.contract_group_code,
            BasFundCode = contract.bas_fund_code,
            BasObjectiveCode = contract.bas_objective_code,
            BasProjectNumber = contract.bas_project_number,
            BasResponsibilityCode = contract.bas_responsibility_code,
            DateCreated = contract.date_created,
            DateUpdated = contract.date_updated,
            CreatedByUserCode = contract.created_by_user_code,
            ModifiedByUserCode = contract.modified_by_user_code,
            IsDeleted = contract.is_deleted,

            // Include nested data without circular references
            VehicleFleetNumber = contract.Vehicle?.fleet_number,
            VehicleRegistrationNumber = contract.Vehicle?.registration_number,
            SiteDescription = contract.Site?.description
        };
    }

    #endregion

    #region Legacy Contract Operations

    /// <summary>
    /// Close/end a contract by contract code
    /// Legacy: CloseContract operation with journal detail reversal
    /// </summary>
    [HttpPost("{contractCode}/close")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CloseContract(
        int contractCode,
        [FromBody] CloseContractRequest request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            // Note: This uses the internal AddContractAsync from ContractService
            // which isn't exposed in IContractService interface but exists in implementation
            // For now, use repository pattern directly or expose through interface
            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            var prevStatus = contract.contract_status_code;

            await _contractRepository.EndContractAsync(
                contractCode,
                request.EndDate,
                currentUserId,
                request.EndOdometer,
                request.Notes);

            await _auditLog.LogAsync(contractCode, "Closed", currentUserId,
                oldStatus: prevStatus, newStatus: 7,
                notes: request.Notes);

            _ = _emailNotification.SendContractClosedNotificationAsync(
                contractCode, currentUserId, closureReason: "Closed");

            return Ok(new { message = "Contract closed successfully", contractCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing contract {ContractCode}", contractCode);
            return StatusCode(500, new { error = "Failed to close contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Extend contract target return date
    /// Legacy: ExtendContractTargetReturnDate operation
    /// </summary>
    [HttpPut("{contractCode}/extend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ExtendContract(
        int contractCode,
        [FromBody] ExtendContractRequest request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            contract.target_return_date = request.NewTargetReturnDate;
            await _contractRepository.UpdateAsync(contract, currentUserId);

            return Ok(new { message = "Contract extended successfully", newTargetReturnDate = request.NewTargetReturnDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending contract {ContractCode}", contractCode);
            return StatusCode(500, new { error = "Failed to extend contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a contract
    /// Legacy: CancelContract operation
    /// </summary>
    [HttpPost("{contractCode}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CancelContract(
        int contractCode,
        [FromBody] CancelContractRequest? request = null)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            var prevStatus = contract.contract_status_code;
            contract.still_current = "N";
            contract.end_date = DateTime.Now;
            contract.contract_status_code = 6; // Cancelled
            contract.contract_status_date = DateTime.Now;
            contract.Notes = request?.CancellationReason ?? "Cancelled";

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractCode, "Cancelled", currentUserId,
                oldStatus: prevStatus, newStatus: 6,
                notes: request?.CancellationReason);

            _ = _emailNotification.SendContractClosedNotificationAsync(
                contractCode, currentUserId,
                closureReason: request?.CancellationReason ?? "Cancelled");

            return Ok(new { message = "Contract cancelled successfully", contractCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling contract {ContractCode}", contractCode);
            return StatusCode(500, new { error = "Failed to cancel contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Reassign contract to a different vehicle or department
    /// </summary>
    [HttpPost("{contractId}/reassign")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReassignContract(
        int contractId,
        [FromBody] ContractReassignDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Update contract assignment
            if (request.NewVmfCode.HasValue)
                contract.vmf_code = request.NewVmfCode.Value;

            if (request.NewSiteCode.HasValue)
                contract.site_code = request.NewSiteCode.Value;

            contract.Notes = $"{contract.Notes}\nReassigned: {request.Reason}";

            await _contractRepository.UpdateAsync(contract, currentUserId);

            return Ok(new { message = "Contract reassigned successfully", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reassigning contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to reassign contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Create a relief vehicle assignment for a contract
    /// </summary>
    [HttpPost("{contractId}/relief")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateReliefContract(
        int contractId,
        [FromBody] ReliefVehicleDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var parentContract = await _contractRepository.GetByIdAsync(contractId);
            if (parentContract == null)
                return NotFound(new { error = "Parent contract not found" });

            // Create relief contract linked to parent
            var reliefContract = new Contract
            {
                vmf_code = request.ReliefVmfCode,
                site_code = parentContract.site_code,
                start_date = DateTime.Now,
                start_time = DateTime.Now,
                start_odometer = request.StartOdometer ?? 0,
                still_current = "Y",
                contract_type = "R", // Relief
                relief_for_contract = contractId,
                parent_contract_code = contractId,
                Notes = $"Relief for contract {contractId}: {request.Reason}",
                target_return_date = request.TargetReturnDate,
                date_created = DateTime.Now,
                created_by_user_code = currentUserId
            };

            var created = await _contractRepository.CreateAsync(reliefContract, currentUserId);

            return CreatedAtAction(nameof(GetById), new { id = created.contract_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relief contract for {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to create relief contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Validate contract before submission
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ContractValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractValidationResultDto>> ValidateContract(
        [FromBody] ContractValidationRequestDto request)
    {
        try
        {
            var result = new ContractValidationResultDto
            {
                IsValid = true,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            // Check if vehicle exists
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VmfCode);
            if (vehicle == null)
            {
                result.IsValid = false;
                result.Errors.Add("Vehicle not found");
            }

            // Check for active contracts
            if (await _contractRepository.HasActiveContractAsync(request.VmfCode))
            {
                result.IsValid = false;
                result.Errors.Add("Vehicle already has an active contract");
            }

            // Add warnings if needed
            if (request.StartOdometer.HasValue && vehicle != null && request.StartOdometer < vehicle.current_odo)
            {
                result.Warnings.Add("Start odometer is less than current vehicle odometer");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating contract");
            return StatusCode(500, new { error = "Failed to validate contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Submit contract for approval (workflow)
    /// </summary>
    [HttpPost("{contractId}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SubmitContractForApproval(int contractId)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Only allow submit/resubmit from Draft (0/null) or Declined for Correction (4)
            var status = contract.contract_status_code;
            if (status != null && status != 0 && status != 4)
                return BadRequest(new
                {
                    error = "Contract cannot be submitted in its current state.",
                    current_status = status,
                    hint = "Only Draft (0) or Declined-for-Correction (4) contracts can be submitted."
                });

            // Verify the submitter is the original capturer
            if (contract.created_by_user_code.HasValue &&
                contract.created_by_user_code.Value != currentUserId)
                return StatusCode(403, new
                {
                    error = "Only the original capturer can submit this contract.",
                    contractId
                });

            var prevStatus = contract.contract_status_code;
            contract.contract_status_code = 1; // Pending Review
            contract.contract_status_date = DateTime.Now;

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "Submitted", currentUserId,
                oldStatus: prevStatus, newStatus: 1);

            return Ok(new { message = "Contract submitted for approval", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting contract {ContractId} for approval", contractId);
            return StatusCode(500, new { error = "Failed to submit contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Approve contract (without activation)
    /// </summary>
    [HttpPost("{contractId}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ApproveContract(
        int contractId,
        [FromBody] ContractApprovalDto? request = null)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to approved (status code 2 = Approved)
            contract.contract_status_code = 2; // Approved
            contract.contract_status_date = DateTime.Now;
            contract.approver_code = currentUserId;

            if (!string.IsNullOrEmpty(request?.ApprovalNotes))
                contract.Notes = $"{contract.Notes}\nApproval: {request.ApprovalNotes}";

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "Approved", currentUserId,
                oldStatus: 1, newStatus: 2, notes: request?.ApprovalNotes);

            return Ok(new { message = "Contract approved", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to approve contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Approve and activate contract in one step
    /// </summary>
    [HttpPost("{contractId}/approve-activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> ApproveAndActivateContract(
        int contractId,
        [FromBody] ContractApprovalDto? request = null)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to active (status code 3 = Active)
            contract.contract_status_code = 3; // Active
            contract.contract_status_date = DateTime.Now;
            contract.approver_code = currentUserId;
            contract.still_current = "Y";

            if (!string.IsNullOrEmpty(request?.ApprovalNotes))
                contract.Notes = $"{contract.Notes}\nApproved & Activated: {request.ApprovalNotes}";

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "ApprovedAndActivated", currentUserId,
                oldStatus: 1, newStatus: 3, notes: request?.ApprovalNotes);

            // Fire-and-forget notification — don't fail the request if email fails
            _ = _emailNotification.SendContractOpenedNotificationAsync(
                contractId,
                capturerUserId: contract.created_by_user_code ?? currentUserId,
                approverUserId: currentUserId);

            return Ok(new { message = "Contract approved and activated", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving and activating contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to approve and activate contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Decline contract with request for correction
    /// </summary>
    [HttpPost("{contractId}/decline-correction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeclineContractWithCorrection(
        int contractId,
        [FromBody] ContractDeclineDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Prevent self-review/decline
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to correction required (status code 4 = Needs Correction)
            contract.contract_status_code = 4; // Needs correction
            contract.contract_status_date = DateTime.Now;
            contract.approver_code = currentUserId;
            contract.Notes = $"{contract.Notes}\nCorrection Required: {request.DeclineReason}";

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "DeclinedForCorrection", currentUserId,
                oldStatus: 1, newStatus: 4, notes: request.DeclineReason);

            return Ok(new { message = "Contract declined with correction request", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining contract {ContractId} for correction", contractId);
            return StatusCode(500, new { error = "Failed to decline contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Decline contract (reject)
    /// </summary>
    [HttpPost("{contractId}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeclineContract(
        int contractId,
        [FromBody] ContractDeclineDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Prevent self-review/decline
            var selfApprovalCheck = ValidateSelfApprovalPrevention(contract, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            // Update contract status to declined (status code 5 = Declined)
            contract.contract_status_code = 5; // Declined
            contract.contract_status_date = DateTime.Now;
            contract.approver_code = currentUserId;
            contract.still_current = "N";
            contract.Notes = $"{contract.Notes}\nDeclined: {request.DeclineReason}";

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "Declined", currentUserId,
                oldStatus: 1, newStatus: 5, notes: request.DeclineReason);

            return Ok(new { message = "Contract declined", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to decline contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Recall a contract from Pending Review back to Draft.
    /// Only the original capturer can recall, and only when status is Pending Review (1).
    /// Use this to fix mistakes before the approver sees the contract.
    /// </summary>
    [HttpPost("{contractId}/recall")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> RecallContract(int contractId)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Only the original capturer can recall
            if (!contract.created_by_user_code.HasValue ||
                contract.created_by_user_code.Value != currentUserId)
                return StatusCode(403, new
                {
                    error = "Only the original capturer can recall this contract.",
                    contractId
                });

            // Can only recall from Pending Review (1)
            if (contract.contract_status_code != 1)
                return BadRequest(new
                {
                    error = "Contract can only be recalled when it is in Pending Review status.",
                    current_status = contract.contract_status_code
                });

            contract.contract_status_code = 0; // Back to Draft
            contract.contract_status_date = DateTime.Now;

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "Recalled", currentUserId,
                oldStatus: 1, newStatus: 0);

            _logger.LogInformation(
                "Contract {ContractId} recalled to Draft by user {UserId}", contractId, currentUserId);

            return Ok(new { message = "Contract recalled to draft. You may now edit and resubmit.", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalling contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to recall contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Edit a contract's details.
    /// Only allowed when the contract is in Draft (0) or Declined for Correction (4) status.
    /// After editing a Declined-for-Correction contract, call /submit to resubmit for approval.
    /// </summary>
    [HttpPut("{contractId}/edit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> EditContract(
        int contractId,
        [FromBody] EditContractDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Only original capturer can edit
            if (!contract.created_by_user_code.HasValue ||
                contract.created_by_user_code.Value != currentUserId)
                return StatusCode(403, new
                {
                    error = "Only the original capturer can edit this contract.",
                    contractId
                });

            // Only editable when Draft (0/null) or Declined for Correction (4)
            var status = contract.contract_status_code;
            if (status != null && status != 0 && status != 4)
                return BadRequest(new
                {
                    error = "Contract cannot be edited in its current state.",
                    current_status = status,
                    hint = "Recall the contract first (if Pending Review), or contact an admin."
                });

            // Apply updates — only overwrite fields that were provided
            if (request.SiteCode.HasValue) contract.site_code = request.SiteCode.Value;
            if (request.DriverId != null) contract.Driver_id = request.DriverId;
            if (request.Notes != null) contract.Notes = request.Notes;
            if (request.TargetReturnDate.HasValue) contract.target_return_date = request.TargetReturnDate;
            if (request.StartOdometer.HasValue) contract.start_odometer = request.StartOdometer.Value;

            contract.date_updated = DateTime.Now;
            contract.modified_by_user_code = currentUserId;

            await _contractRepository.UpdateAsync(contract, currentUserId);
            await _auditLog.LogAsync(contractId, "Edited", currentUserId,
                oldStatus: status, newStatus: status,
                notes: "Contract fields updated");

            _logger.LogInformation(
                "Contract {ContractId} edited by user {UserId} (status={Status})",
                contractId, currentUserId, status);

            return Ok(new
            {
                message = status == 4
                    ? "Contract updated. Call /submit to resubmit for approval."
                    : "Contract updated.",
                contractId,
                contract_status_code = contract.contract_status_code
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to edit contract", message = ex.Message });
        }
    }

    /// <summary>
    /// Get pending approval details for a contract
    /// </summary>
    [HttpGet("{contractId}/pending")]
    [ProducesResponseType(typeof(ContractPendingDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractPendingDetailsDto>> GetPendingApprovalDetails(int contractId)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            var result = new ContractPendingDetailsDto
            {
                ContractCode = contract.contract_code,
                VmfCode = contract.vmf_code,
                SiteCode = contract.site_code,
                StatusCode = contract.contract_status_code,
                StatusDate = contract.contract_status_date,
                IsPending = contract.contract_status_code == 1,
                SubmittedDate = contract.date_created,
                SubmittedByUserCode = contract.created_by_user_code
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending details for contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to get pending details", message = ex.Message });
        }
    }

    /// <summary>
    /// Get the full audit trail for a contract — every state change and edit, in chronological order.
    /// </summary>
    [HttpGet("{contractId}/audit-log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetAuditLog(int contractId)
    {
        try
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            var entries = await _auditLog.GetByContractAsync(contractId);
            return Ok(new
            {
                contract_code = contractId,
                total_entries = entries.Count(),
                audit_trail = entries.Select(e => new
                {
                    e.id,
                    e.action,
                    e.performed_by_user_code,
                    e.performed_at,
                    e.old_status_code,
                    old_status_text = e.old_status_code.HasValue ? GetStatusText(e.old_status_code.Value) : null,
                    e.new_status_code,
                    new_status_text = e.new_status_code.HasValue ? GetStatusText(e.new_status_code.Value) : null,
                    e.field_changed,
                    e.old_value,
                    e.new_value,
                    e.notes
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit log for contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to retrieve audit log", message = ex.Message });
        }
    }

    private static string GetStatusText(short status) => status switch
    {
        0 => "Draft",
        1 => "Pending Review",
        2 => "Approved",
        3 => "Active",
        4 => "Declined for Correction",
        5 => "Declined",
        6 => "Cancelled",
        7 => "Closed",
        _ => $"Unknown ({status})"
    };

    /// <summary>
    /// Search for available relief vehicles
    /// </summary>
    [HttpGet("relief/search")]
    [ProducesResponseType(typeof(IEnumerable<ReliefVehicleSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ReliefVehicleSearchResultDto>>> SearchReliefVehicles(
        [FromQuery] string? query = null)
    {
        try
        {
            // Get vehicles without active contracts
            var allVehicles = await _vehicleRepository.GetAvailableVehiclesAsync();

            var results = allVehicles
                .Where(v => string.IsNullOrEmpty(query) ||
                           (v.fleet_number?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (v.registration_number?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(v => new ReliefVehicleSearchResultDto
                {
                    VmfCode = v.vmf_code,
                    FleetNumber = v.fleet_number ?? string.Empty,
                    RegistrationNumber = v.registration_number,
                    MakeCode = null, // Make is accessed through Model relationship
                    ModelCode = v.model_code,
                    IsAvailable = true
                })
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching relief vehicles");
            return StatusCode(500, new { error = "Failed to search relief vehicles", message = ex.Message });
        }
    }

    #endregion

    #region Data Repair Endpoints

    /// <summary>
    /// Dry-run preview of the contract status data repair.
    /// Shows exactly how many contracts would be updated, broken down by category.
    /// No data is changed — safe to call multiple times.
    ///
    /// Repair logic (mirrors legacy migration script 12):
    ///   still_current='Y' + status NULL  → set status 3 (Active)
    ///   still_current='N' + status NULL  → set status 7 (Closed)
    ///   still_current='N' + status NULL + end_date NULL → flagged as data quality issue
    /// </summary>
    [HttpGet("data-repair/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DataRepairPreview()
    {
        try
        {
            var affected = await _context.Contracts
                .Where(c => !c.is_deleted && c.contract_status_code == null)
                .Select(c => new
                {
                    c.contract_code,
                    c.still_current,
                    c.end_date,
                    c.vmf_code
                })
                .ToListAsync();

            var activeRepairs = affected
                .Where(c => c.still_current == "Y")
                .ToList();

            var closedWithEndDate = affected
                .Where(c => c.still_current == "N" && c.end_date.HasValue)
                .ToList();

            var closedMissingEndDate = affected
                .Where(c => c.still_current == "N" && !c.end_date.HasValue)
                .ToList();

            return Ok(new
            {
                summary = new
                {
                    total_affected = affected.Count,
                    will_set_active = activeRepairs.Count,
                    will_set_closed_normal = closedWithEndDate.Count,
                    will_set_closed_data_quality_flag = closedMissingEndDate.Count
                },
                details = new
                {
                    active_contracts = activeRepairs.Select(c => new { c.contract_code, c.vmf_code, proposed_status = 3, proposed_status_text = "Active" }),
                    closed_contracts = closedWithEndDate.Select(c => new { c.contract_code, c.vmf_code, c.end_date, proposed_status = 7, proposed_status_text = "Closed" }),
                    data_quality_issues = closedMissingEndDate.Select(c => new
                    {
                        c.contract_code,
                        c.vmf_code,
                        proposed_status = 7,
                        proposed_status_text = "Closed",
                        warning = "end_date is NULL — contract closed without a recorded end date"
                    })
                },
                note = "Call POST /data-repair/run to apply these changes."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data repair preview");
            return StatusCode(500, new { error = "Failed to generate preview", message = ex.Message });
        }
    }

    /// <summary>
    /// Execute the contract status data repair.
    /// Sets contract_status_code on all contracts where it is currently NULL,
    /// using still_current as the authoritative source of truth.
    ///
    /// All changes are individually audit-logged under action "DataRepair".
    /// This endpoint is idempotent — running it twice is safe (NULL check prevents re-processing).
    /// </summary>
    [HttpPost("data-repair/run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DataRepairRun()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var nullStatusContracts = await _context.Contracts
                .Where(c => !c.is_deleted && c.contract_status_code == null)
                .ToListAsync();

            if (nullStatusContracts.Count == 0)
                return Ok(new { message = "No contracts require repair. All contracts already have a status code.", repaired = 0 });

            int repairedActive = 0;
            int repairedClosed = 0;
            int dataQualityFlags = 0;

            var auditTasks = new List<Task>();

            foreach (var contract in nullStatusContracts)
            {
                short newStatus;
                string? note = null;

                if (contract.still_current == "Y")
                {
                    newStatus = 3; // Active
                    repairedActive++;
                }
                else
                {
                    newStatus = 7; // Closed
                    repairedClosed++;

                    if (!contract.end_date.HasValue)
                    {
                        note = "Data quality: end_date was NULL at time of repair";
                        dataQualityFlags++;
                    }
                }

                contract.contract_status_code = newStatus;
                contract.contract_status_date = DateTime.Now;
                contract.modified_by_user_code = currentUserId;
                contract.date_updated = DateTime.Now;

                auditTasks.Add(_auditLog.LogAsync(
                    contract.contract_code,
                    "DataRepair",
                    currentUserId,
                    oldStatus: null,
                    newStatus: newStatus,
                    notes: note ?? $"Status backfilled from still_current='{contract.still_current}' (legacy v2.1.05 migration)"));
            }

            await _context.SaveChangesAsync();
            await Task.WhenAll(auditTasks);

            _logger.LogInformation(
                "Data repair completed by user {UserId}: {Active} active, {Closed} closed ({DQ} data quality flags)",
                currentUserId, repairedActive, repairedClosed, dataQualityFlags);

            return Ok(new
            {
                message = "Data repair completed successfully.",
                repaired = nullStatusContracts.Count,
                breakdown = new
                {
                    set_to_active = repairedActive,
                    set_to_closed = repairedClosed,
                    data_quality_flags = dataQualityFlags,
                    data_quality_note = dataQualityFlags > 0
                        ? $"{dataQualityFlags} contracts were closed (still_current='N') but had no end_date recorded. Status set to Closed; audit log notes the discrepancy."
                        : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing data repair");
            return StatusCode(500, new { error = "Failed to execute data repair", message = ex.Message });
        }
    }

    #endregion
}

#region DTOs

public class HireContractDto
{
    [Required]
    public int VmfCode { get; set; }
    [Required]
    public short SiteCode { get; set; }
    public int? StartOdometer { get; set; }
    public string? DriverId { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }
}

public class ReturnContractDto
{
    public int? EndOdometer { get; set; }
    public string? Notes { get; set; }
}

public class CloseContractRequest
{
    [Required]
    public DateTime EndDate { get; set; }
    [Required]
    public int EndOdometer { get; set; }
    public string? Notes { get; set; }
}

public class ExtendContractRequest
{
    [Required]
    public DateTime NewTargetReturnDate { get; set; }
}

public class CancelContractRequest
{
    public string? CancellationReason { get; set; }
}

/// <summary>
/// DTO for editing a Draft or Declined-for-Correction contract.
/// All fields are optional — only provided fields are updated.
/// </summary>
public class EditContractDto
{
    public short? SiteCode { get; set; }
    public string? DriverId { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }
    public int? StartOdometer { get; set; }
}

// New DTOs for Phase 1 endpoints

public class ContractReassignDto
{
    public int? NewVmfCode { get; set; }
    public short? NewSiteCode { get; set; }
    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ReliefVehicleDto
{
    [Required]
    public int ReliefVmfCode { get; set; }
    public int? StartOdometer { get; set; }
    public DateTime? TargetReturnDate { get; set; }
    [Required]
    public string Reason { get; set; } = string.Empty;
}

public class ContractValidationRequestDto
{
    [Required]
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public int? StartOdometer { get; set; }
}

public class ContractValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ContractApprovalDto
{
    public string? ApprovalNotes { get; set; }
}

public class ContractDeclineDto
{
    [Required]
    public string DeclineReason { get; set; } = string.Empty;
}

public class ContractPendingDetailsDto
{
    public int ContractCode { get; set; }
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public short? StatusCode { get; set; }
    public DateTime? StatusDate { get; set; }
    public bool IsPending { get; set; }
    public DateTime SubmittedDate { get; set; }
    public int? SubmittedByUserCode { get; set; }
}

public class ReliefVehicleSearchResultDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public short? MakeCode { get; set; }
    public short? ModelCode { get; set; }
    public bool IsAvailable { get; set; }
}

public class ContractResponseDto
{
    public int ContractCode { get; set; }
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public string? ContractTypeCode { get; set; }
    public short? ContractStatusCode { get; set; }
    public DateTime? ContractStatusDate { get; set; }
    public string? StillCurrent { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime StartTime { get; set; }
    public int StartOdometer { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EndTime { get; set; }
    public int? EndOdometer { get; set; }
    public int? MonthlyKm { get; set; }
    public short? HoursUsed { get; set; }
    public DateTime? TargetReturnDate { get; set; }
    public string? DriverId { get; set; }
    public string? DriverName { get; set; }
    public int? SiteDriverCode { get; set; }
    public int? ApproverCode { get; set; }
    public int? ParentContractCode { get; set; }
    public int? ReliefForContract { get; set; }
    public int? VehicleAssessmentCode { get; set; }
    public Guid? JournalDetailCode { get; set; }
    public bool LockedForTransfer { get; set; }
    public string? Notes { get; set; }
    public string? Authorisation { get; set; }
    public DateTime? ChargedUntil { get; set; }
    public string? CollectorFirstname { get; set; }
    public short? UserCode { get; set; }
    public int? ContractGroupCode { get; set; }
    public string? BasFundCode { get; set; }
    public string? BasObjectiveCode { get; set; }
    public string? BasProjectNumber { get; set; }
    public string? BasResponsibilityCode { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public int? CreatedByUserCode { get; set; }
    public int? ModifiedByUserCode { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation properties - flattened to prevent circular references
    public string? VehicleFleetNumber { get; set; }
    public string? VehicleRegistrationNumber { get; set; }
    public string? SiteDescription { get; set; }
}

#endregion
