using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(
        IContractRepository contractRepository,
        IContractService contractService,
        IVehicleRepository vehicleRepository,
        ILogger<ContractsController> logger)
    {
        _contractRepository = contractRepository;
        _contractService = contractService;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the current user is not the contract owner (prevents self-approval)
    /// </summary>
    /// <returns>Null if validation passes, or ForbidResult with error message if validation fails</returns>
    private ActionResult? ValidateSelfApprovalPrevention(Contract contract, int currentUserId)
    {
        // Determine contract owner: use user_code, fallback to created_by_user_code
        int? contractOwnerCode = contract.user_code ?? contract.created_by_user_code;

        if (contractOwnerCode.HasValue && contractOwnerCode.Value == currentUserId)
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
                TargetReturnDate = request.TargetReturnDate
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

            await _contractRepository.EndContractAsync(
                contractCode,
                request.EndDate,
                request.EndOdometer,
                request.Notes);

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

            // Mark as cancelled
            contract.still_current = "N";
            contract.end_date = DateTime.Now;
            contract.Notes = request?.CancellationReason ?? "Cancelled";

            await _contractRepository.UpdateAsync(contract, currentUserId);

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

            // Update contract status to pending approval (status code 1 = Pending)
            contract.contract_status_code = 1; // Pending approval
            contract.contract_status_date = DateTime.Now;

            await _contractRepository.UpdateAsync(contract, currentUserId);

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

            return Ok(new { message = "Contract declined", contractId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining contract {ContractId}", contractId);
            return StatusCode(500, new { error = "Failed to decline contract", message = ex.Message });
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
