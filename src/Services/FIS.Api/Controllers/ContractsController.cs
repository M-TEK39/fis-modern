using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FIS.Api.Controllers;

/// <summary>
/// Contract/Hire management API endpoints
/// Provides comprehensive contract operations with legacy business logic
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ContractsController : ControllerBase
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
    [ProducesResponseType(typeof(IEnumerable<Contract>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Contract>>> GetActive()
    {
        var list = await _contractService.GetActiveContractsAsync();
        return Ok(list);
    }

    /// <summary>
    /// Get contract by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Contract), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Contract?>> GetById(int id)
    {
        var contract = await _contractRepository.GetByIdAsync(id);
        if (contract == null) return NotFound();
        return Ok(contract);
    }

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
            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            contract.target_return_date = request.NewTargetReturnDate;
            await _contractRepository.UpdateAsync(contract);

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
            var contract = await _contractRepository.GetByIdAsync(contractCode);
            if (contract == null)
                return NotFound(new { error = "Contract not found" });

            // Mark as cancelled
            contract.still_current = "N";
            contract.end_date = DateTime.Now;
            contract.Notes = request?.CancellationReason ?? "Cancelled";

            await _contractRepository.UpdateAsync(contract);

            return Ok(new { message = "Contract cancelled successfully", contractCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling contract {ContractCode}", contractCode);
            return StatusCode(500, new { error = "Failed to cancel contract", message = ex.Message });
        }
    }

    #endregion
}

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
