using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Vehicle Authorization Controller - Manages vehicle inception approval workflow
/// Handles approve/reject/comment operations for vehicles awaiting authorization
/// </summary>
[ApiController]
[Authorize]
[Route("api/vehicle/authorization")]
public class VehicleAuthorizationController : BaseApiController
{
    private readonly IVehicleAuthorizationRepository _repository;
    private readonly ILogger<VehicleAuthorizationController> _logger;

    public VehicleAuthorizationController(
        IVehicleAuthorizationRepository repository,
        ILogger<VehicleAuthorizationController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicles awaiting authorization (pending queue)
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<PreVehicleMasterDto>>> GetPendingAuthorizations()
    {
        try
        {
            _logger.LogInformation("Fetching vehicles awaiting authorization");
            var vehicles = await _repository.GetPendingAuthorizationsAsync();
            var dtos = vehicles.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending vehicle authorizations");
            return StatusCode(500, "Error retrieving pending authorizations");
        }
    }

    /// <summary>
    /// Get all authorized vehicles
    /// </summary>
    [HttpGet("authorized")]
    public async Task<ActionResult<IEnumerable<PreVehicleMasterDto>>> GetAuthorizedVehicles()
    {
        try
        {
            _logger.LogInformation("Fetching authorized vehicles");
            var vehicles = await _repository.GetAuthorizedVehiclesAsync();
            var dtos = vehicles.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching authorized vehicles");
            return StatusCode(500, "Error retrieving authorized vehicles");
        }
    }

    /// <summary>
    /// Get all rejected vehicles
    /// </summary>
    [HttpGet("rejected")]
    public async Task<ActionResult<IEnumerable<PreVehicleMasterDto>>> GetRejectedVehicles()
    {
        try
        {
            _logger.LogInformation("Fetching rejected vehicles");
            var vehicles = await _repository.GetRejectedVehiclesAsync();
            var dtos = vehicles.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching rejected vehicles");
            return StatusCode(500, "Error retrieving rejected vehicles");
        }
    }

    /// <summary>
    /// Get authorization history with optional date range
    /// </summary>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<PreVehicleMasterDto>>> GetAuthorizationHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            _logger.LogInformation("Fetching authorization history from {StartDate} to {EndDate}", startDate, endDate);
            var vehicles = await _repository.GetAuthorizationHistoryAsync(startDate, endDate);
            var dtos = vehicles.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching authorization history");
            return StatusCode(500, "Error retrieving authorization history");
        }
    }

    /// <summary>
    /// Get vehicle authorization by chassis number
    /// </summary>
    [HttpGet("chassis/{chassisNumber}")]
    public async Task<ActionResult<PreVehicleMasterDto>> GetByChassisNumber(string chassisNumber)
    {
        try
        {
            _logger.LogInformation("Fetching vehicle authorization for chassis {ChassisNumber}", chassisNumber);
            var vehicle = await _repository.GetByChassisNumberAsync(chassisNumber);

            if (vehicle == null)
                return NotFound(new { message = $"Vehicle authorization not found for chassis number: {chassisNumber}" });

            return Ok(MapToDto(vehicle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle authorization for chassis {ChassisNumber}", chassisNumber);
            return StatusCode(500, "Error retrieving vehicle authorization");
        }
    }

    /// <summary>
    /// Get vehicle authorization by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PreVehicleMasterDto>> GetById(int id)
    {
        try
        {
            _logger.LogInformation("Fetching vehicle authorization {Id}", id);
            var vehicle = await _repository.GetByIdAsync(id);

            if (vehicle == null)
                return NotFound(new { message = $"Vehicle authorization not found with ID: {id}" });

            return Ok(MapToDto(vehicle));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle authorization {Id}", id);
            return StatusCode(500, "Error retrieving vehicle authorization");
        }
    }

    /// <summary>
    /// Approve vehicle authorization
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> ApproveVehicle(int id, [FromBody] ApprovalDto? approval = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} approving vehicle authorization {Id}", userId, id);

            await _repository.ApproveAsync(id, userId, approval?.Comment);

            _logger.LogInformation("Vehicle authorization {Id} approved by user {UserId}", id, userId);
            return Ok(new { message = "Vehicle authorization approved successfully", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vehicle authorization {Id} not found for approval", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when approving vehicle authorization {Id}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving vehicle authorization {Id}", id);
            return StatusCode(500, "Error approving vehicle authorization");
        }
    }

    /// <summary>
    /// Reject vehicle authorization
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult> RejectVehicle(int id, [FromBody] RejectionDto rejection)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rejection?.RejectionReason))
                return BadRequest(new { message = "Rejection reason is required" });

            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} rejecting vehicle authorization {Id} with reason: {Reason}",
                userId, id, rejection.RejectionReason);

            await _repository.RejectAsync(id, userId, rejection.RejectionReason, rejection.Comment);

            _logger.LogInformation("Vehicle authorization {Id} rejected by user {UserId}", id, userId);
            return Ok(new { message = "Vehicle authorization rejected successfully", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vehicle authorization {Id} not found for rejection", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting vehicle authorization {Id}", id);
            return StatusCode(500, "Error rejecting vehicle authorization");
        }
    }

    /// <summary>
    /// Add comment to vehicle authorization
    /// </summary>
    [HttpPost("{id}/comment")]
    public async Task<ActionResult> AddComment(int id, [FromBody] CommentDto commentDto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(commentDto?.Comment))
                return BadRequest(new { message = "Comment cannot be empty" });

            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} adding comment to vehicle authorization {Id}", userId, id);

            await _repository.AddCommentAsync(id, commentDto.Comment, userId);

            _logger.LogInformation("Comment added to vehicle authorization {Id} by user {UserId}", id, userId);
            return Ok(new { message = "Comment added successfully", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vehicle authorization {Id} not found for comment", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to vehicle authorization {Id}", id);
            return StatusCode(500, "Error adding comment");
        }
    }

    /// <summary>
    /// Create new vehicle authorization entry
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PreVehicleMasterDto>> CreatePreVehicleMaster([FromBody] CreatePreVehicleMasterDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} creating vehicle authorization for chassis {ChassisNumber}",
                userId, dto.ChassisNumber);

            // Check if vehicle already exists
            var existing = await _repository.GetByChassisNumberAsync(dto.ChassisNumber);
            if (existing != null && !existing.is_deleted)
            {
                return Conflict(new { message = $"Vehicle authorization already exists for chassis number: {dto.ChassisNumber}" });
            }

            var vehicleAuth = new PreVehicleMaster
            {
                chassis_number = dto.ChassisNumber,
                engine_number = dto.EngineNumber,
                model_code = dto.ModelCode ?? 0, // Default to 0 if not provided
                colour = dto.Colour,
                purchase_amount = dto.PurchaseAmount,
                purchase_date = dto.PurchaseDate,
                purchase_from = dto.PurchaseFrom,
                take_on_date = dto.TakeOnDate,
                take_on_odo = dto.TakeOnOdo,
                replaced_gg_number = dto.ReplacedGGNumber,
                Fleet_Notes = dto.FleetNotes,
                damage_status = dto.DamageStatus,
                damages_comment = dto.DamagesComment
            };

            var created = await _repository.CreateAsync(vehicleAuth, userId);
            _logger.LogInformation("Vehicle authorization created with ID {Id} for chassis {ChassisNumber}",
                created.temp_vmf_code, created.chassis_number);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.temp_vmf_code },
                MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle authorization");
            return StatusCode(500, "Error creating vehicle authorization");
        }
    }

    /// <summary>
    /// Update vehicle authorization (only allowed if status is "Awaiting Authorization" or "Rejected")
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> UpdatePreVehicleMaster(int id, [FromBody] UpdatePreVehicleMasterDto dto)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} updating vehicle authorization {Id}", userId, id);

            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Vehicle authorization not found with ID: {id}" });

            if (existing.Authority_Status == "Authorized")
                return BadRequest(new { message = "Cannot update an already authorized vehicle" });

            // Update allowed fields
            existing.engine_number = dto.EngineNumber ?? existing.engine_number;
            existing.model_code = dto.ModelCode ?? existing.model_code;
            existing.colour = dto.Colour ?? existing.colour;
            existing.purchase_amount = dto.PurchaseAmount ?? existing.purchase_amount;
            existing.purchase_date = dto.PurchaseDate ?? existing.purchase_date;
            existing.purchase_from = dto.PurchaseFrom ?? existing.purchase_from;
            existing.take_on_date = dto.TakeOnDate ?? existing.take_on_date;
            existing.take_on_odo = dto.TakeOnOdo ?? existing.take_on_odo;
            existing.replaced_gg_number = dto.ReplacedGGNumber ?? existing.replaced_gg_number;
            existing.Fleet_Notes = dto.FleetNotes ?? existing.Fleet_Notes;
            existing.damage_status = dto.DamageStatus ?? existing.damage_status;
            existing.damages_comment = dto.DamagesComment ?? existing.damages_comment;

            await _repository.UpdateAsync(existing, userId);

            _logger.LogInformation("Vehicle authorization {Id} updated by user {UserId}", id, userId);
            return Ok(new { message = "Vehicle authorization updated successfully", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vehicle authorization {Id} not found for update", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle authorization {Id}", id);
            return StatusCode(500, "Error updating vehicle authorization");
        }
    }

    /// <summary>
    /// Delete vehicle authorization (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeletePreVehicleMaster(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} deleting vehicle authorization {Id}", userId, id);

            await _repository.DeleteAsync(id, userId);

            _logger.LogInformation("Vehicle authorization {Id} deleted by user {UserId}", id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vehicle authorization {Id} not found for deletion", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle authorization {Id}", id);
            return StatusCode(500, "Error deleting vehicle authorization");
        }
    }

    private PreVehicleMasterDto MapToDto(PreVehicleMaster v)
    {
        return new PreVehicleMasterDto
        {
            TempVmfCode = v.temp_vmf_code,
            ChassisNumber = v.chassis_number ?? string.Empty,
            EngineNumber = v.engine_number,
            ModelCode = v.model_code,
            ModelDescription = v.Model?.model_description,
            Colour = v.colour,
            PurchaseAmount = v.purchase_amount,
            PurchaseDate = v.purchase_date,
            PurchaseFrom = v.purchase_from,
            TakeOnDate = v.take_on_date,
            TakeOnOdo = v.take_on_odo,
            ReplacedGGNumber = v.replaced_gg_number,
            FleetNotes = v.Fleet_Notes,
            DamageStatus = v.damage_status,
            DamagesComment = v.damages_comment,
            AuthorityStatus = v.Authority_Status ?? "Awaiting Authorization",
            AuthorizedByUserCode = v.authorized_by_user_code,
            AuthorizedByUserName = v.AuthorizedByUser?.email,
            AuthorizationDate = v.authorization_date,
            RejectionReason = v.rejection_reason,
            AuthorizationComment = v.authorization_comment,
            VmfCode = v.vmf_code,
            DateCreated = v.date_created,
            CreatedByUserCode = v.created_by_user_code
        };
    }
}

#region DTOs

public class PreVehicleMasterDto
{
    public int TempVmfCode { get; set; }
    public string ChassisNumber { get; set; } = string.Empty;
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? ModelDescription { get; set; }
    public string? Colour { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public string? FleetNotes { get; set; }
    public string? DamageStatus { get; set; }
    public string? DamagesComment { get; set; }
    public string AuthorityStatus { get; set; } = string.Empty;
    public int? AuthorizedByUserCode { get; set; }
    public string? AuthorizedByUserName { get; set; }
    public DateTime? AuthorizationDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? AuthorizationComment { get; set; }
    public int? VmfCode { get; set; }
    public DateTime DateCreated { get; set; }
    public int? CreatedByUserCode { get; set; }
}

public class CreatePreVehicleMasterDto
{
    public string ChassisNumber { get; set; } = string.Empty;
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? Colour { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public string? FleetNotes { get; set; }
    public string? DamageStatus { get; set; }
    public string? DamagesComment { get; set; }
}

public class UpdatePreVehicleMasterDto
{
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? Colour { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public string? FleetNotes { get; set; }
    public string? DamageStatus { get; set; }
    public string? DamagesComment { get; set; }
}

public class ApprovalDto
{
    public string? Comment { get; set; }
}

public class RejectionDto
{
    public string RejectionReason { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public class CommentDto
{
    public string Comment { get; set; } = string.Empty;
}

#endregion
