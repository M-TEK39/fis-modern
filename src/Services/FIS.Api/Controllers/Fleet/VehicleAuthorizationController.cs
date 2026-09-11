using System.Security.Claims;
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
    private const long VehicleManagementPermission = 1;
    private static readonly string[] InceptionRoles =
    [
        "vehicle inception capturer",
        "vehicle inception authorizer",
    ];

    private readonly IVehicleAuthorizationRepository _repository;
    private readonly ILogger<VehicleAuthorizationController> _logger;

    public VehicleAuthorizationController(
        IVehicleAuthorizationRepository repository,
        ILogger<VehicleAuthorizationController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the current user is not the vehicle capturer (prevents self-approval)
    /// </summary>
    /// <returns>Null if validation passes, or ForbidResult with error message if validation fails</returns>
    private ActionResult? ValidateSelfApprovalPrevention(
        PreVehicleMaster preVehicle,
        int currentUserId
    )
    {
        // Check if current user is the vehicle capturer
        if (
            preVehicle.created_by_user_code.HasValue
            && preVehicle.created_by_user_code.Value == currentUserId
        )
        {
            _logger.LogWarning(
                "Self-approval blocked: User {UserId} attempted to approve their own captured vehicle {VehicleId}",
                currentUserId,
                preVehicle.temp_vmf_code
            );

            return StatusCode(
                403,
                new
                {
                    error = "You cannot review or approve your own captured vehicle.",
                    vehicleId = preVehicle.temp_vmf_code,
                    userId = currentUserId,
                }
            );
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Get all vehicles awaiting authorization (pending queue)
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAuthorizations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!CanAccessAuthorizationQueue())
            return Forbid();

        try
        {
            _logger.LogInformation("Fetching vehicles awaiting authorization");
            var result = await _repository.GetPendingAuthorizationsAsync(
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100)
            );
            return Ok(ToPageResponse(result));
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
    public async Task<IActionResult> GetAuthorizedVehicles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!CanAccessAuthorizationQueue())
            return Forbid();

        try
        {
            _logger.LogInformation("Fetching authorized vehicles");
            var result = await _repository.GetAuthorizedVehiclesAsync(
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100)
            );
            return Ok(ToPageResponse(result));
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
    public async Task<IActionResult> GetRejectedVehicles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!CanAccessAuthorizationQueue())
            return Forbid();

        try
        {
            _logger.LogInformation("Fetching rejected vehicles");
            var result = await _repository.GetRejectedVehiclesAsync(
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100)
            );
            return Ok(ToPageResponse(result));
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
        [FromQuery] DateTime? endDate = null
    )
    {
        try
        {
            _logger.LogInformation(
                "Fetching authorization history from {StartDate} to {EndDate}",
                startDate,
                endDate
            );
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
    /// Gets the maintenance options exposed by the connected legacy database.
    /// Missing legacy procedure support is an expected compatibility state.
    /// </summary>
    [HttpGet("maintenance-types")]
    public async Task<ActionResult<IEnumerable<object>>> GetMaintenanceTypes()
    {
        if (!HasVehicleManagementPermission())
            return Forbid();

        try
        {
            var types = await _repository.GetMaintenanceTypesAsync();
            return Ok(types.Select(type => new { code = type.Code, name = type.Name }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle maintenance types");
            return StatusCode(
                503,
                new { message = "Vehicle maintenance options are unavailable." }
            );
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
            _logger.LogInformation(
                "Fetching vehicle authorization for chassis {ChassisNumber}",
                chassisNumber
            );
            var vehicle = await _repository.GetByChassisNumberAsync(chassisNumber);

            if (vehicle == null)
                return NotFound(
                    new
                    {
                        message = $"Vehicle authorization not found for chassis number: {chassisNumber}",
                    }
                );

            return Ok(MapToDto(vehicle));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching vehicle authorization for chassis {ChassisNumber}",
                chassisNumber
            );
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApproveVehicle(int id, [FromBody] ApprovalDto? approval = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} approving vehicle authorization {Id}",
                userId,
                id
            );

            // Fetch vehicle to validate self-approval prevention
            var preVehicle = await _repository.GetByIdAsync(id);
            if (preVehicle == null)
                return NotFound(new { message = $"Vehicle authorization not found with ID: {id}" });

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(preVehicle, userId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            await _repository.ApproveAsync(id, userId, approval?.Comment);

            _logger.LogInformation(
                "Vehicle authorization {Id} approved by user {UserId}",
                id,
                userId
            );
            return Ok(new { message = "Vehicle authorization approved successfully", id });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Vehicle authorization {Id} not found for approval", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid operation when approving vehicle authorization {Id}",
                id
            );
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RejectVehicle(int id, [FromBody] RejectionDto rejection)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rejection?.RejectionReason))
                return BadRequest(new { message = "Rejection reason is required" });

            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} rejecting vehicle authorization {Id} with reason: {Reason}",
                userId,
                id,
                rejection.RejectionReason
            );

            // Fetch vehicle to validate self-approval prevention
            var preVehicle = await _repository.GetByIdAsync(id);
            if (preVehicle == null)
                return NotFound(new { message = $"Vehicle authorization not found with ID: {id}" });

            // Prevent self-review/rejection
            var selfApprovalCheck = ValidateSelfApprovalPrevention(preVehicle, userId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            await _repository.RejectAsync(id, userId, rejection.RejectionReason, rejection.Comment);

            _logger.LogInformation(
                "Vehicle authorization {Id} rejected by user {UserId}",
                id,
                userId
            );
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
            _logger.LogInformation(
                "User {UserId} adding comment to vehicle authorization {Id}",
                userId,
                id
            );

            await _repository.AddCommentAsync(id, commentDto.Comment, userId);

            _logger.LogInformation(
                "Comment added to vehicle authorization {Id} by user {UserId}",
                id,
                userId
            );
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
    public async Task<ActionResult<PreVehicleMasterDto>> CreatePreVehicleMaster(
        [FromBody] CreatePreVehicleMasterDto dto
    )
    {
        try
        {
            if (!HasVehicleManagementPermission())
                return Forbid();

            var validationError = ValidateCreateRequest(dto);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var userId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} creating vehicle authorization for chassis {ChassisNumber}",
                userId,
                dto.ChassisNumber
            );

            var vehicleAuth = new PreVehicleMaster
            {
                fleet_number = dto.FleetNumber,
                registration_number = dto.RegistrationNumber,
                chassis_number = dto.ChassisNumber.Trim().ToUpperInvariant(),
                engine_number = dto.EngineNumber?.Trim().ToUpperInvariant(),
                model_code = dto.ModelCode ?? (short)0,
                colour = dto.Colour?.Trim(),
                year_manufactured = dto.YearManufactured,
                location_code = dto.LocationCode,
                vehicle_status_code = dto.VehicleStatusCode,
                vehicle_status_date = dto.VehicleStatusDate,
                type_code = dto.TypeCode,
                vs_code = dto.VsCode,
                comment = dto.Comment?.Trim(),
                purchase_amount = dto.PurchaseAmount,
                purchase_date = dto.PurchaseDate,
                purchase_from = dto.PurchaseFrom?.Trim().ToUpperInvariant(),
                take_on_date = dto.TakeOnDate,
                take_on_odo = dto.TakeOnOdo,
                replaced_gg_number = dto.ReplacedGGNumber?.Trim().ToUpperInvariant(),
                site_code = dto.SiteCode,
                invoice_number = dto.InvoiceNumber?.Trim().ToUpperInvariant(),
                gp_number = dto.GpNumber?.Trim().ToUpperInvariant(),
                Fleet_Notes = dto.FleetNotes,
                damage_status = dto.DamageStatus,
                damages_comment = dto.DamagesComment,
                ExtraCodes = dto.ExtraCodes?.Distinct().ToList() ?? [],
                MaintenanceTypeCode = dto.MaintenanceTypeCode,
                MaintenanceStartDate = dto.MaintenanceStartDate,
                MaintenancePeriodMonths = dto.MaintenancePeriodMonths,
                MaintenanceKilos = dto.MaintenanceKilos,
                MaintenanceValue = dto.MaintenanceValue,
            };

            var created = await _repository.CreateAsync(vehicleAuth, userId);
            _logger.LogInformation(
                "Vehicle authorization created with ID {Id} for chassis {ChassisNumber}",
                created.temp_vmf_code,
                created.chassis_number
            );

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.temp_vmf_code },
                MapToDto(created)
            );
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Duplicate vehicle inception capture rejected");
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid vehicle inception capture rejected");
            return BadRequest(new { message = ex.Message });
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
    public async Task<ActionResult> UpdatePreVehicleMaster(
        int id,
        [FromBody] UpdatePreVehicleMasterDto dto
    )
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
            existing.fleet_number = dto.FleetNumber ?? existing.fleet_number;
            existing.registration_number = dto.RegistrationNumber ?? existing.registration_number;
            existing.engine_number = dto.EngineNumber ?? existing.engine_number;
            existing.model_code = dto.ModelCode ?? existing.model_code;
            existing.colour = dto.Colour ?? existing.colour;
            existing.year_manufactured = dto.YearManufactured ?? existing.year_manufactured;
            existing.location_code = dto.LocationCode ?? existing.location_code;
            existing.vehicle_status_code = dto.VehicleStatusCode ?? existing.vehicle_status_code;
            existing.vehicle_status_date = dto.VehicleStatusDate ?? existing.vehicle_status_date;
            existing.type_code = dto.TypeCode ?? existing.type_code;
            existing.vs_code = dto.VsCode ?? existing.vs_code;
            existing.comment = dto.Comment ?? existing.comment;
            existing.purchase_amount = dto.PurchaseAmount ?? existing.purchase_amount;
            existing.purchase_date = dto.PurchaseDate ?? existing.purchase_date;
            existing.purchase_from = dto.PurchaseFrom ?? existing.purchase_from;
            existing.take_on_date = dto.TakeOnDate ?? existing.take_on_date;
            existing.take_on_odo = dto.TakeOnOdo ?? existing.take_on_odo;
            existing.replaced_gg_number = dto.ReplacedGGNumber ?? existing.replaced_gg_number;
            existing.site_code = dto.SiteCode ?? existing.site_code;
            existing.invoice_number = dto.InvoiceNumber ?? existing.invoice_number;
            existing.gp_number = dto.GpNumber ?? existing.gp_number;
            existing.Fleet_Notes = dto.FleetNotes ?? existing.Fleet_Notes;
            existing.damage_status = dto.DamageStatus ?? existing.damage_status;
            existing.damages_comment = dto.DamagesComment ?? existing.damages_comment;

            await _repository.UpdateAsync(existing, userId);

            _logger.LogInformation(
                "Vehicle authorization {Id} updated by user {UserId}",
                id,
                userId
            );
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

            _logger.LogInformation(
                "Vehicle authorization {Id} deleted by user {UserId}",
                id,
                userId
            );
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
            FleetNumber = v.fleet_number,
            RegistrationNumber = v.registration_number,
            ChassisNumber = v.chassis_number ?? string.Empty,
            EngineNumber = v.engine_number,
            ModelCode = v.model_code,
            ModelDescription = v.Model?.model_description,
            Colour = v.colour,
            YearManufactured = v.year_manufactured,
            LocationCode = v.location_code,
            VehicleStatusCode = v.vehicle_status_code,
            VehicleStatusDate = v.vehicle_status_date,
            TypeCode = v.type_code,
            VsCode = v.vs_code,
            Comment = v.comment,
            PurchaseAmount = v.purchase_amount,
            PurchaseDate = v.purchase_date,
            PurchaseFrom = v.purchase_from,
            TakeOnDate = v.take_on_date,
            TakeOnOdo = v.take_on_odo,
            ReplacedGGNumber = v.replaced_gg_number,
            SiteCode = v.site_code,
            InvoiceNumber = v.invoice_number,
            GpNumber = v.gp_number,
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
            CreatedByUserCode = v.created_by_user_code,
        };
    }

    private object ToPageResponse(VehicleAuthorizationPage page) =>
        new
        {
            data = page.Data.Select(MapToDto),
            page = page.Page,
            pageSize = page.PageSize,
            totalRecords = page.TotalRecords,
            totalPages = page.TotalPages,
        };

    private bool CanAccessAuthorizationQueue() =>
        HasVehicleManagementPermission()
        && (HasAnyRole("vehicle inception authorizer") || !HasAnyRole(InceptionRoles));

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

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

    private bool HasVehicleManagementPermission()
    {
        var accessLevelClaim = User.FindFirst("access_level")?.Value;
        return long.TryParse(accessLevelClaim, out var accessLevel)
            && (accessLevel & VehicleManagementPermission) == VehicleManagementPermission;
    }

    private static string? ValidateCreateRequest(CreatePreVehicleMasterDto? dto)
    {
        if (dto is null)
        {
            return "Vehicle inception data is required.";
        }

        if (
            string.IsNullOrWhiteSpace(dto.ChassisNumber)
            || string.IsNullOrWhiteSpace(dto.EngineNumber)
            || string.IsNullOrWhiteSpace(dto.Colour)
            || string.IsNullOrWhiteSpace(dto.PurchaseFrom)
            || string.IsNullOrWhiteSpace(dto.Comment)
        )
        {
            return "Complete all required vehicle identity, purchase, and comment fields.";
        }

        if (
            dto.ModelCode is null or <= 0
            || dto.LocationCode is null or <= 0
            || dto.TypeCode is null or <= 0
            || dto.VsCode is null or <= 0
            || dto.VehicleStatusCode is null or < 0
            || dto.YearManufactured is null or < 1900
            || dto.TakeOnOdo is null or < 0 or > 999999
            || dto.TakeOnDate is null
            || dto.PurchaseDate is null
            || dto.PurchaseAmount is null or < 5000 or > 9999999
        )
        {
            return "Enter valid positive reference values and measurements. Purchase amount must be between 5,000 and 9,999,999.";
        }

        var lengthError = FirstLengthError(
            ("Fleet number", dto.FleetNumber, 20),
            ("Registration number", dto.RegistrationNumber, 50),
            ("Replace GG number", dto.ReplacedGGNumber, 20),
            ("Colour", dto.Colour, 15),
            ("Chassis number", dto.ChassisNumber, 60),
            ("Engine number", dto.EngineNumber, 60),
            ("Comment", dto.Comment, 90),
            ("Purchase from", dto.PurchaseFrom, 60),
            ("Invoice number", dto.InvoiceNumber, 60),
            ("GP number", dto.GpNumber, 9),
            ("Fleet notes", dto.FleetNotes, 255),
            ("Damage details", dto.DamagesComment, 355)
        );
        if (lengthError is not null)
        {
            return lengthError;
        }

        if (
            dto
                .EngineNumber.Trim()
                .Equals(dto.ChassisNumber.Trim(), StringComparison.OrdinalIgnoreCase)
        )
        {
            return "Chassis and engine numbers must be different.";
        }

        if (!string.IsNullOrWhiteSpace(dto.FleetNumber) && !IsValidGgNumber(dto.FleetNumber))
        {
            return "Current GG number must use the legacy format G|letters|letters|numbers|numbers|numbers|G, for example GVN001G.";
        }

        if (
            !string.IsNullOrWhiteSpace(dto.ReplacedGGNumber)
            && !IsValidGgNumber(dto.ReplacedGGNumber)
        )
        {
            return "Replace GG number must use the legacy format G|letters|letters|numbers|numbers|numbers|G, for example GVN001G.";
        }

        if (
            dto.TakeOnDate.Value.Date > DateTime.Today
            || dto.PurchaseDate.Value.Date > DateTime.Today
        )
        {
            return "Take-on and purchase dates cannot be in the future.";
        }

        if (dto.PurchaseDate.Value.Date < dto.TakeOnDate.Value.Date)
        {
            return "Purchase date cannot be before the take-on date.";
        }

        if (
            dto.DamageStatus is not null
            && !dto.DamageStatus.Equals("Y", StringComparison.OrdinalIgnoreCase)
            && !dto.DamageStatus.Equals("N", StringComparison.OrdinalIgnoreCase)
        )
        {
            return "Damage status must be Y or N.";
        }

        if (
            dto.DamageStatus?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true
            && string.IsNullOrWhiteSpace(dto.DamagesComment)
        )
        {
            return "Damage details are required when the vehicle has damage.";
        }

        var hireType = dto.TypeCode.Value;
        var hiredFrom = dto.VsCode.Value;
        if ((hireType is 1 or 2 or 3) && hiredFrom != 1)
        {
            return "VIP Services, Permanent Hire, and Pool Vehicle entries must use g-Fleet Normal as Hired From.";
        }

        if (hireType == 4 && hiredFrom is not (2 or 3))
        {
            return "Lease entries must use SMMT-Lease or g-Fleet Lease as Hired From.";
        }

        if (hireType == 5 && hiredFrom is not (4 or 5))
        {
            return "Rental entries must use a rental vehicle source.";
        }

        if (dto.ExtraCodes?.Any(code => code <= 0) == true)
        {
            return "Vehicle extra codes must be positive.";
        }

        if (dto.MaintenanceTypeCode is <= 0)
        {
            return "Maintenance type must be positive when supplied.";
        }

        if (dto.MaintenanceTypeCode.HasValue)
        {
            if (dto.MaintenanceValue is null or <= 0)
            {
                return "Maintenance value must be greater than zero when a maintenance option is selected.";
            }

            if (dto.MaintenanceStartDate is null)
            {
                return "Maintenance start date is required when a maintenance option is selected.";
            }

            if (dto.MaintenanceStartDate.Value.Year < dto.YearManufactured.Value)
            {
                return "Maintenance start date cannot be earlier than the vehicle's manufactured year.";
            }
        }

        if (
            dto.MaintenancePeriodMonths is < 0
            || dto.MaintenanceKilos is < 0
            || dto.MaintenanceValue is < 0
        )
        {
            return "Maintenance measurements cannot be negative.";
        }

        return null;
    }

    private static string? FirstLengthError(
        params (string Name, string? Value, int Maximum)[] values
    )
    {
        foreach (var (name, value, maximum) in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maximum)
            {
                return $"{name} cannot exceed {maximum} characters.";
            }
        }

        return null;
    }

    private static bool IsValidGgNumber(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            value.Trim(),
            "^G[A-Z]{2}[0-9]{3}G$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
}

#region DTOs

public class PreVehicleMasterDto
{
    public int TempVmfCode { get; set; }
    public string? FleetNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string ChassisNumber { get; set; } = string.Empty;
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? ModelDescription { get; set; }
    public string? Colour { get; set; }
    public short? YearManufactured { get; set; }
    public short? LocationCode { get; set; }
    public short? VehicleStatusCode { get; set; }
    public DateTime? VehicleStatusDate { get; set; }
    public short? TypeCode { get; set; }
    public byte? VsCode { get; set; }
    public string? Comment { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public short? SiteCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? GpNumber { get; set; }
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
    public string? FleetNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string ChassisNumber { get; set; } = string.Empty;
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? Colour { get; set; }
    public short? YearManufactured { get; set; }
    public short? LocationCode { get; set; }
    public short? VehicleStatusCode { get; set; }
    public DateTime? VehicleStatusDate { get; set; }
    public short? TypeCode { get; set; }
    public byte? VsCode { get; set; }
    public string? Comment { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public short? SiteCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? GpNumber { get; set; }
    public string? FleetNotes { get; set; }
    public string? DamageStatus { get; set; }
    public string? DamagesComment { get; set; }
    public IReadOnlyCollection<short>? ExtraCodes { get; set; }
    public short? MaintenanceTypeCode { get; set; }
    public DateTime? MaintenanceStartDate { get; set; }
    public int? MaintenancePeriodMonths { get; set; }
    public int? MaintenanceKilos { get; set; }
    public decimal? MaintenanceValue { get; set; }
}

public class UpdatePreVehicleMasterDto
{
    public string? FleetNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? Colour { get; set; }
    public short? YearManufactured { get; set; }
    public short? LocationCode { get; set; }
    public short? VehicleStatusCode { get; set; }
    public DateTime? VehicleStatusDate { get; set; }
    public short? TypeCode { get; set; }
    public byte? VsCode { get; set; }
    public string? Comment { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public short? SiteCode { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? GpNumber { get; set; }
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
