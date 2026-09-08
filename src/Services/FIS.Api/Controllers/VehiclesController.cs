using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Services;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class VehiclesController : BaseApiController
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IRecoveredVehicleRepository _recoveredVehicleRepository;
    private readonly VehicleService _vehicleService;
    private readonly FisDbContext _context; // Keep for db-status endpoint
    private readonly IVehicleTariffRepository _tariffRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRemarkRepository _remarkRepository;
    private readonly IVehicleLicenceHistoryRepository _licenceHistory;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IVehicleRepository vehicleRepository,
        IRecoveredVehicleRepository recoveredVehicleRepository,
        VehicleService vehicleService,
        FisDbContext context,
        IVehicleTariffRepository tariffRepository,
        IContractRepository contractRepository,
        IVehicleRemarkRepository remarkRepository,
        IVehicleLicenceHistoryRepository licenceHistory,
        ILogger<VehiclesController> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _recoveredVehicleRepository = recoveredVehicleRepository;
        _vehicleService = vehicleService;
        _context = context;
        _tariffRepository = tariffRepository;
        _contractRepository = contractRepository;
        _remarkRepository = remarkRepository;
        _licenceHistory = licenceHistory;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicles
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Vehicle>>> GetVehicles()
    {
        try
        {
            var vehicles = await _vehicleRepository.GetActiveVehiclesAsync();
            _logger.LogInformation("Retrieved {Count} active vehicles", vehicles.Count());
            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicles");
            return StatusCode(500, "An error occurred while retrieving vehicles");
        }
    }

    /// <summary>
    /// Get a vehicle by VMF code
    /// </summary>
    [HttpGet("{vmfCode}")]
    public async Task<ActionResult<Vehicle>> GetVehicle(int vmfCode)
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle with VMF code {VmfCode} not found", vmfCode);
                return NotFound();
            }

            _logger.LogInformation("Retrieved vehicle with VMF code {VmfCode}", vmfCode);
            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle with VMF code {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while retrieving the vehicle");
        }
    }

    /// <summary>
    /// Search vehicles by term (fleet number, registration, etc.)
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Vehicle>>> SearchVehicles(
        [FromQuery] string? searchTerm
    )
    {
        try
        {
            var vehicles = await _vehicleRepository.SearchVehiclesAsync(searchTerm ?? "");
            _logger.LogInformation(
                "Found {Count} vehicles matching search term '{SearchTerm}'",
                vehicles.Count(),
                searchTerm
            );
            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vehicles with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching vehicles");
        }
    }

    /// <summary>
    /// Search the legacy recovered-vehicle workflow by GG or GP number.
    /// </summary>
    [HttpGet("recovered/search")]
    public async Task<
        ActionResult<IReadOnlyList<RecoveredVehicleSearchRecord>>
    > SearchRecoveredVehicles([FromQuery] string? mode, [FromQuery] string? search)
    {
        if (!HasDemoVehicleRole())
        {
            return Forbid();
        }

        var normalizedMode = (mode ?? "GG").Trim().ToUpperInvariant();
        var searchTerm = (search ?? string.Empty).Trim();
        if (normalizedMode is not ("GG" or "GP"))
        {
            return BadRequest(new { message = "Search mode must be GG or GP." });
        }

        if (searchTerm.Length == 0)
        {
            return Ok(Array.Empty<RecoveredVehicleSearchRecord>());
        }

        try
        {
            return Ok(
                await _recoveredVehicleRepository.SearchAsync(searchTerm, normalizedMode == "GP")
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching recovered vehicles in {Mode} mode",
                normalizedMode
            );
            return StatusCode(
                500,
                new { message = "The recovered vehicle search could not be completed." }
            );
        }
    }

    /// <summary>
    /// Load the recovered-vehicle form and its latest legacy history value.
    /// </summary>
    [HttpGet("recovered/{vmfCode:int}")]
    public async Task<ActionResult<RecoveredVehicleDetails>> GetRecoveredVehicle(int vmfCode)
    {
        if (!HasDemoVehicleRole())
        {
            return Forbid();
        }

        try
        {
            var details = await _recoveredVehicleRepository.GetDetailsAsync(vmfCode);
            return details is null ? NotFound(new { message = "Vehicle not found." }) : Ok(details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading recovered vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { message = "The recovered vehicle could not be loaded." });
        }
    }

    /// <summary>
    /// Preserve the legacy recovered-GG transaction: mark the stolen row,
    /// create the recovered row, and append both vehicle history records.
    /// </summary>
    [HttpPost("recovered")]
    public async Task<ActionResult<RecoveredVehicleUpdateResult>> UpdateRecoveredVehicle(
        [FromBody] RecoveredVehicleUpdateRequest request
    )
    {
        if (!HasDemoVehicleRole())
        {
            return Forbid();
        }

        var recoveredFleetNumber =
            request.RecoveredFleetNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!IsValidRecoveredFleetNumber(recoveredFleetNumber))
        {
            return BadRequest(
                new
                {
                    message = "The recovered GG number must start with G and contain a valid numeric suffix.",
                }
            );
        }

        if (!request.DateChanged.HasValue)
        {
            return BadRequest(new { message = "A date changed value is required." });
        }

        if (request.NewStatusCode == 4)
        {
            return BadRequest(
                new { message = "A recovered vehicle cannot be saved with the Stolen status." }
            );
        }

        try
        {
            var result = await _recoveredVehicleRepository.UpdateAsync(
                new RecoveredVehicleUpdate(
                    request.VmfCode,
                    recoveredFleetNumber,
                    request.DateChanged.Value.Date,
                    request.NewStatusCode
                ),
                GetCurrentUserId()
            );
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Recovered vehicle {VmfCode} was not found", request.VmfCode);
            return NotFound(new { message = "Vehicle not found." });
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("must be different", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains(
                    "already been renumbered",
                    StringComparison.OrdinalIgnoreCase
                )
            )
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating recovered vehicle {VmfCode}", request.VmfCode);
            return StatusCode(500, new { message = "The recovered vehicle could not be updated." });
        }
    }

    private static bool IsValidRecoveredFleetNumber(string value)
    {
        if (value.Length is < 4 or > 8 || value[0] != 'G')
        {
            return false;
        }

        for (var index = 3; index < Math.Min(value.Length, 6); index++)
        {
            if (!char.IsDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private bool HasDemoVehicleRole()
    {
        if (User.IsInRole("Demo Vehicles"))
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
            string.Equals(role, "Demo Vehicles", StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Get vehicle by fleet number
    /// </summary>
    [HttpGet("fleet/{fleetNumber}")]
    public async Task<ActionResult<Vehicle>> GetVehicleByFleetNumber(string fleetNumber)
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByFleetNumberAsync(fleetNumber);

            if (vehicle == null)
            {
                _logger.LogWarning(
                    "Vehicle with fleet number {FleetNumber} not found",
                    fleetNumber
                );
                return NotFound();
            }

            _logger.LogInformation(
                "Retrieved vehicle with fleet number {FleetNumber}",
                fleetNumber
            );
            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving vehicle with fleet number {FleetNumber}",
                fleetNumber
            );
            return StatusCode(500, "An error occurred while retrieving the vehicle");
        }
    }

    /// <summary>
    /// Get database connection status and basic counts
    /// </summary>
    [HttpGet("db-status")]
    public async Task<IActionResult> GetDatabaseStatus()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            var vehicleCount = await _context.Vehicles.CountAsync();

            return Ok(
                new
                {
                    canConnect,
                    vehicleCount,
                    timestamp = DateTime.UtcNow,
                }
            );
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get vehicles that need service based on business rules
    /// </summary>
    [HttpGet("service-alerts")]
    public async Task<ActionResult> GetServiceAlerts()
    {
        try
        {
            var alerts = await _vehicleService.GetVehiclesNeedingServiceAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service alerts");
            return StatusCode(500, "An error occurred while retrieving service alerts");
        }
    }

    /// <summary>
    /// Create a new vehicle
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Vehicle>> CreateVehicle(
        [FromBody] VehicleCreationApiRequest request
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var vehicle = new Vehicle
            {
                fleet_number = request.fleet_number,
                registration_number = request.registration_number,
                location_code = request.location_code,
                model_code = request.model_code,
                type_code = request.type_code,
                vehicle_status_code = request.vehicle_status_code,
                colour = request.colour,
                chassis_number = request.chassis_number,
                engine_number_1 = request.engine_number_1,
                take_on_date = request.take_on_date ?? DateTime.UtcNow,
                take_on_odo = request.take_on_odo,
                current_odo = request.current_odo,
                tare = request.tare,
                gvm = request.gvm,
                year_manufactured = request.year_manufactured,
                purchase_date = request.purchase_date,
                purchase_amount = request.purchase_amount,
                ifms_vehicle_register_number = request.ifms_vehicle_register_number,
                natis_model_number = request.natis_model_number,
                date_created = DateTime.UtcNow,
                created_by_user_code = currentUserId,
                is_deleted = false,
            };

            var created = await _vehicleRepository.CreateAsync(vehicle, currentUserId);
            _logger.LogInformation("Created vehicle with vmf_code {VmfCode}", created.vmf_code);

            // Handle tariff recalculation if requested
            if (request.recalculate_tariff)
            {
                _logger.LogInformation(
                    "Tariff recalculation requested for vehicle {VmfCode}",
                    created.vmf_code
                );
                await _tariffRepository.RecalculateTariffAsync(created.vmf_code);
            }

            return CreatedAtAction(nameof(GetVehicle), new { vmfCode = created.vmf_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return StatusCode(500, "An error occurred while creating the vehicle");
        }
    }

    /// <summary>
    /// Update an existing vehicle
    /// </summary>
    [HttpPut("{vmfCode}")]
    public async Task<ActionResult<Vehicle>> UpdateVehicle(
        int vmfCode,
        [FromBody] VehicleUpdateApiRequest request
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existing = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (existing == null)
            {
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");
            }

            // If registration number is changing, save the old one to history
            if (
                request.registration_number != null
                && !string.Equals(
                    request.registration_number,
                    existing.registration_number,
                    StringComparison.OrdinalIgnoreCase
                )
                && !string.IsNullOrWhiteSpace(existing.registration_number)
            )
            {
                _context.Registrations.Add(
                    new FIS.Core.Domain.Entities.Vehicles.Registration
                    {
                        vmf_code = vmfCode,
                        RegistrationNumber = existing.registration_number,
                        RegistrationDate = DateTime.UtcNow,
                        date_created = DateTime.UtcNow,
                        created_by_user_code = currentUserId,
                        is_deleted = false,
                    }
                );
                _logger.LogInformation(
                    "Recording historical registration '{Old}' for vehicle {VmfCode} (replacing with '{New}')",
                    existing.registration_number,
                    vmfCode,
                    request.registration_number
                );
            }

            // Update fields (only if provided)
            existing.fleet_number = request.fleet_number ?? existing.fleet_number;
            existing.registration_number =
                request.registration_number ?? existing.registration_number;
            existing.location_code = request.location_code ?? existing.location_code;
            existing.model_code = request.model_code ?? existing.model_code;
            existing.type_code = request.type_code ?? existing.type_code;
            existing.vehicle_status_code =
                request.vehicle_status_code ?? existing.vehicle_status_code;
            existing.colour = request.colour ?? existing.colour;
            existing.chassis_number = request.chassis_number ?? existing.chassis_number;
            existing.engine_number_1 = request.engine_number_1 ?? existing.engine_number_1;
            existing.take_on_odo = request.take_on_odo ?? existing.take_on_odo;
            existing.current_odo = request.current_odo ?? existing.current_odo;
            existing.tare = request.tare ?? existing.tare;
            existing.gvm = request.gvm ?? existing.gvm;
            existing.year_manufactured = request.year_manufactured ?? existing.year_manufactured;
            existing.ifms_vehicle_register_number =
                request.ifms_vehicle_register_number ?? existing.ifms_vehicle_register_number;
            existing.natis_model_number = request.natis_model_number ?? existing.natis_model_number;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;

            await _vehicleRepository.UpdateAsync(existing, currentUserId);
            _logger.LogInformation("Updated vehicle with vmf_code {VmfCode}", vmfCode);

            // Handle tariff recalculation if requested
            if (request.recalculate_tariff)
            {
                _logger.LogInformation(
                    "Tariff recalculation requested for vehicle {VmfCode}",
                    vmfCode
                );
                await _tariffRepository.RecalculateTariffAsync(vmfCode);
            }

            return Ok(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while updating the vehicle");
        }
    }

    /// <summary>
    /// Update the barcode stored on a vehicle_master row by VMF code.
    /// This keeps the legacy barcode maintenance contract isolated from the
    /// broader vehicle update payload.
    /// </summary>
    [HttpPut("{vmfCode:int}/barcode")]
    public async Task<ActionResult<VehicleBarcodeUpdateResponse>> UpdateVehicleBarcode(
        int vmfCode,
        [FromBody] VehicleBarcodeUpdateRequest request
    )
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
            {
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");
            }

            return Ok(await SaveVehicleBarcodeAsync(vehicle, request.barcode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating barcode for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while updating the vehicle barcode");
        }
    }

    /// <summary>
    /// Preserve the legacy barcode endpoint, which identifies the vehicle by
    /// its GG/fleet number rather than VMF code.
    /// </summary>
    [HttpPut("barcode")]
    public async Task<ActionResult<VehicleBarcodeUpdateResponse>> UpdateVehicleBarcodeByFleetNumber(
        [FromBody] VehicleBarcodeUpdateByFleetNumberRequest request
    )
    {
        if (string.IsNullOrWhiteSpace(request.ggNumber))
        {
            return BadRequest("A GG number is required");
        }

        try
        {
            var vehicle = await _vehicleRepository.GetByFleetNumberAsync(request.ggNumber.Trim());
            if (vehicle == null)
            {
                return NotFound($"Vehicle with fleet number {request.ggNumber} not found");
            }

            return Ok(await SaveVehicleBarcodeAsync(vehicle, request.barcode));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating barcode for fleet number {FleetNumber}",
                request.ggNumber
            );
            return StatusCode(500, "An error occurred while updating the vehicle barcode");
        }
    }

    private async Task<VehicleBarcodeUpdateResponse> SaveVehicleBarcodeAsync(
        Vehicle vehicle,
        string? barcode
    )
    {
        var currentUserId = GetCurrentUserId();
        vehicle.barcode = barcode;
        vehicle.date_updated = DateTime.UtcNow;
        vehicle.modified_by_user_code = currentUserId;
        await _vehicleRepository.UpdateAsync(vehicle, currentUserId);

        return new VehicleBarcodeUpdateResponse
        {
            vmf_code = vehicle.vmf_code,
            fleet_number = vehicle.fleet_number,
            barcode = vehicle.barcode,
        };
    }

    /// <summary>
    /// Correct model code on a NEW vehicle (no active contract).
    /// Capturer/authorizer self-service — no RFC to admin required.
    /// </summary>
    [HttpPatch("{vmfCode}/correct-model")]
    public async Task<ActionResult> CorrectVehicleModel(
        int vmfCode,
        [FromBody] CorrectModelDto request
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existing = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (existing == null)
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");

            // Only allowed when vehicle has no active contract (i.e. still "new")
            var hasActiveContract = await _contractRepository.HasActiveContractAsync(vmfCode);
            if (hasActiveContract)
            {
                _logger.LogWarning(
                    "Model correction blocked: Vehicle {VmfCode} has an active contract",
                    vmfCode
                );
                return StatusCode(
                    403,
                    new
                    {
                        error = "Model correction is only allowed for vehicles that have no active contract. Please submit an RFC for this change.",
                        vmf_code = vmfCode,
                    }
                );
            }

            existing.model_code = request.model_code;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;

            await _vehicleRepository.UpdateAsync(existing, currentUserId);
            _logger.LogInformation(
                "Corrected model_code for vehicle {VmfCode} to {ModelCode} by user {UserId}",
                vmfCode,
                request.model_code,
                currentUserId
            );

            return Ok(new { vmf_code = vmfCode, model_code = existing.model_code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error correcting model for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while correcting the vehicle model");
        }
    }

    /// <summary>
    /// Correct GG/fleet number on a NEW vehicle (no active contract).
    /// Capturer/authorizer self-service — no RFC to admin required.
    /// </summary>
    [HttpPatch("{vmfCode}/correct-gg")]
    public async Task<ActionResult> CorrectVehicleGG(int vmfCode, [FromBody] CorrectGGDto request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existing = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (existing == null)
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");

            // Only allowed when vehicle has no active contract (i.e. still "new")
            var hasActiveContract = await _contractRepository.HasActiveContractAsync(vmfCode);
            if (hasActiveContract)
            {
                _logger.LogWarning(
                    "GG number correction blocked: Vehicle {VmfCode} has an active contract",
                    vmfCode
                );
                return StatusCode(
                    403,
                    new
                    {
                        error = "GG number correction is only allowed for vehicles that have no active contract. Please submit an RFC for this change.",
                        vmf_code = vmfCode,
                    }
                );
            }

            existing.fleet_number = request.fleet_number;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;

            await _vehicleRepository.UpdateAsync(existing, currentUserId);
            _logger.LogInformation(
                "Corrected fleet_number for vehicle {VmfCode} to '{FleetNumber}' by user {UserId}",
                vmfCode,
                request.fleet_number,
                currentUserId
            );

            return Ok(new { vmf_code = vmfCode, fleet_number = existing.fleet_number });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error correcting GG number for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while correcting the vehicle GG number");
        }
    }

    /// <summary>
    /// Search vehicles by invoice number
    /// </summary>
    [HttpGet("by-invoice/{invoiceNumber}")]
    public async Task<ActionResult<IEnumerable<VehicleSearchResultDto>>> GetVehiclesByInvoiceNumber(
        string invoiceNumber
    )
    {
        try
        {
            var vehicles = await _vehicleRepository.GetByInvoiceNumberAsync(invoiceNumber);
            var results = vehicles.Select(v => new VehicleSearchResultDto
            {
                VmfCode = v.vmf_code,
                FleetNumber = v.fleet_number ?? string.Empty,
                RegistrationNumber = v.registration_number,
                MakeCode = v.Model?.make_code,
                ModelCode = v.model_code,
                CurrentOdometer = v.current_odo,
                VehicleStatusCode = v.vehicle_status_code,
                IsAvailable = !v.is_deleted && v.vehicle_status_code == 1,
                InvoiceNumber = v.invoice_number,
                DisplayText = $"{v.fleet_number} - {v.registration_number}",
            });
            _logger.LogInformation(
                "Found {Count} vehicles with invoice number '{InvoiceNumber}'",
                results.Count(),
                invoiceNumber
            );
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving vehicles with invoice number '{InvoiceNumber}'",
                invoiceNumber
            );
            return StatusCode(500, "An error occurred while retrieving vehicles");
        }
    }

    /// <summary>
    /// Update invoice number for a vehicle
    /// </summary>
    [HttpPatch("{vmfCode}/invoice")]
    public async Task<ActionResult> UpdateVehicleInvoice(
        int vmfCode,
        [FromBody] UpdateInvoiceDto request
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existing = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (existing == null)
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");

            existing.invoice_number = request.invoice_number;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;

            await _vehicleRepository.UpdateAsync(existing, currentUserId);
            _logger.LogInformation(
                "Updated invoice number for vehicle {VmfCode} to '{InvoiceNumber}'",
                vmfCode,
                request.invoice_number
            );

            return Ok(new { vmf_code = vmfCode, invoice_number = existing.invoice_number });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating invoice number for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while updating the invoice number");
        }
    }

    /// <summary>
    /// Delete a vehicle (soft delete)
    /// </summary>
    [HttpDelete("{vmfCode}")]
    public async Task<ActionResult> DeleteVehicle(int vmfCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existing = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (existing == null)
            {
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");
            }

            await _vehicleRepository.DeleteAsync(vmfCode, currentUserId);
            _logger.LogInformation("Deleted vehicle with vmf_code {VmfCode}", vmfCode);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while deleting the vehicle");
        }
    }

    // ──────────────────────────────────────────────────────────
    // VEHICLE STATUS CHANGE  (with automatic side-effects)
    // ──────────────────────────────────────────────────────────

    private static string GetVehicleStatusDescription(short code) =>
        code switch
        {
            0 => "New",
            1 => "In Service",
            2 => "Withdrawn",
            3 => "Board of Survey",
            4 => "Stolen",
            5 => "Sold",
            6 => "Transferred",
            7 => "Subsidized",
            8 => "From Focus",
            9 => "Privatised",
            10 => "Recovered",
            11 => "Missing",
            12 => "Destroyed",
            _ => "Unknown",
        };

    /// <summary>
    /// Change a vehicle's status with automatic side-effects.
    ///
    /// When status is set to STOLEN (4):
    ///   1. Any active contract for the vehicle is automatically closed.
    ///   2. The vehicle is booked under the supplied site_code.
    ///   3. A VehicleStatusHistory entry is created.
    ///
    /// site_code is required when marking a vehicle as Stolen, optional otherwise.
    /// </summary>
    [HttpPatch("{vmfCode:int}/status")]
    public async Task<ActionResult> ChangeStatus(int vmfCode, [FromBody] ChangeVehicleStatusDto dto)
    {
        try
        {
            var currentUserId = GetCurrentUserId();

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            if (vehicle == null)
                return NotFound(new { error = $"Vehicle {vmfCode} not found." });

            if (dto.new_status_code == 4 && dto.site_code == null)
                return BadRequest(
                    new { error = "site_code is required when marking a vehicle as Stolen." }
                );

            var previousStatusCode = vehicle.vehicle_status_code;
            var previousStatusDesc = GetVehicleStatusDescription(previousStatusCode);
            var newStatusDesc = GetVehicleStatusDescription(dto.new_status_code);
            var effectiveDate = dto.effective_date ?? DateTime.UtcNow;

            // ── Actions list returned in response ──────────────────────
            var actionsPerformed = new List<string>();

            // ── STOLEN-specific side effects ───────────────────────────
            int? closedContractCode = null;
            if (dto.new_status_code == 4)
            {
                // 1. Close any active contract
                var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(
                    vmfCode
                );
                if (activeContract != null)
                {
                    var closeNotes =
                        $"Auto-closed: vehicle {vehicle.fleet_number ?? vmfCode.ToString()} reported stolen. {dto.notes}".Trim();
                    await _contractRepository.EndContractAsync(
                        activeContract.contract_code,
                        effectiveDate,
                        currentUserId,
                        endOdometer: vehicle.current_odo > 0 ? vehicle.current_odo : null,
                        notes: closeNotes
                    );

                    closedContractCode = activeContract.contract_code;
                    actionsPerformed.Add(
                        $"Contract {activeContract.contract_code} closed automatically (vehicle reported stolen)."
                    );
                }

                // 2. Book vehicle under the specified site
                vehicle.location_code = dto.site_code!.Value;
                actionsPerformed.Add($"Vehicle location updated to site {dto.site_code.Value}.");
            }

            // ── Update vehicle status ──────────────────────────────────
            vehicle.vehicle_status_code = dto.new_status_code;
            vehicle.vehicle_status_date = effectiveDate;
            vehicle.date_updated = DateTime.UtcNow;
            vehicle.modified_by_user_code = currentUserId;

            // ── Record status history ──────────────────────────────────
            var historyEntry = new FIS.Core.Domain.Entities.Vehicles.VehicleStatusHistory
            {
                vmf_code = vmfCode,
                vehicle_status_code = dto.new_status_code,
                vehicle_status_description = newStatusDesc,
                status_start_date = effectiveDate,
                // status_end_date is non-nullable — use far-future sentinel for "open" status
                status_end_date = new DateTime(2099, 12, 31),
                date_created = DateTime.UtcNow,
                created_by_user_code = currentUserId,
            };

            _context.VehicleStatusHistories.Add(historyEntry);
            await _context.SaveChangesAsync();

            actionsPerformed.Add(
                $"Vehicle status changed from '{previousStatusDesc}' ({previousStatusCode}) to '{newStatusDesc}' ({dto.new_status_code})."
            );

            _logger.LogInformation(
                "Vehicle {VmfCode} status changed to {NewStatus} by user {UserId}. Actions: {Actions}",
                vmfCode,
                newStatusDesc,
                currentUserId,
                string.Join(" | ", actionsPerformed)
            );

            return Ok(
                new
                {
                    vmf_code = vmfCode,
                    fleet_number = vehicle.fleet_number,
                    registration_number = vehicle.registration_number,
                    previous_status_code = previousStatusCode,
                    previous_status_description = previousStatusDesc,
                    new_status_code = dto.new_status_code,
                    new_status_description = newStatusDesc,
                    effective_date = effectiveDate,
                    location_code = vehicle.location_code,
                    closed_contract_code = closedContractCode,
                    actions_performed = actionsPerformed,
                }
            );
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing status for vehicle {VmfCode}", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to change vehicle status", detail = ex.Message }
            );
        }
    }

    // ──────────────────────────────────────────────────────────
    // VEHICLE LICENCE CAPTURE  (history-preserving update)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Capture a new licence for a vehicle.
    /// The current licence values are snapshotted to vehicle_licence_history BEFORE
    /// the vehicle record is updated, so the full renewal history is never lost.
    /// </summary>
    [HttpPatch("{vmfCode:int}/licence")]
    public async Task<ActionResult> CaptureLicence(
        int vmfCode,
        [FromBody] FIS.Api.DTOs.CaptureLicenceDto dto
    )
    {
        try
        {
            var currentUserId = GetCurrentUserId();

            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v =>
                v.vmf_code == vmfCode && !v.is_deleted
            );

            if (vehicle == null)
                return NotFound(new { error = $"Vehicle {vmfCode} not found." });

            // Snapshot current values BEFORE overwriting
            var snapshot = new FIS.Core.Domain.Entities.VehicleLicenceHistory
            {
                vmf_code = vmfCode,
                licence_due_date = vehicle.licence_due_date,
                lic_register_number = vehicle.lic_register_number,
                lic_registration_doc = vehicle.lic_registration_doc,
                licence_comments = vehicle.licence_comments,
                cof_last_done = vehicle.cof_last_done,
                cof_required = vehicle.cof_required,
                tare = vehicle.tare,
                Licence_receiver = vehicle.Licence_receiver,
                Licence_receiver_id = vehicle.Licence_receiver_id,
                Licence_receiver_tel = vehicle.Licence_receiver_tel,
                Licence_receiver_site = vehicle.Licence_receiver_site,
                Licence_date_taken = vehicle.Licence_date_taken,
                captured_by_user_code = currentUserId,
                update_notes = dto.update_notes,
            };

            // Only save a snapshot if there is something worth preserving
            if (
                await _licenceHistory.IsAvailableAsync()
                && (snapshot.licence_due_date.HasValue || snapshot.lic_register_number != null)
            )
                await _licenceHistory.CreateAsync(snapshot);

            // Apply new licence values
            await _vehicleRepository.UpdateLicenceFieldsAsync(
                vmfCode,
                new VehicleLicenceUpdate(
                    dto.licence_due_date,
                    dto.lic_register_number,
                    dto.lic_registration_doc,
                    dto.tare,
                    dto.Licence_receiver,
                    dto.Licence_receiver_id,
                    dto.Licence_receiver_tel,
                    dto.Licence_receiver_site,
                    dto.Licence_date_taken,
                    dto.cof_required,
                    dto.cof_last_done,
                    dto.licence_comments
                ),
                currentUserId
            );

            var updatedVehicle = await _vehicleRepository.GetByIdAsync(vmfCode) ?? vehicle;

            _logger.LogInformation(
                "Licence captured for vehicle {VmfCode} by user {UserId}. New due date: {DueDate}",
                vmfCode,
                currentUserId,
                dto.licence_due_date
            );

            return Ok(
                new
                {
                    vmf_code = vmfCode,
                    fleet_number = updatedVehicle.fleet_number,
                    registration_number = updatedVehicle.registration_number,
                    licence_due_date = updatedVehicle.licence_due_date,
                    lic_register_number = updatedVehicle.lic_register_number,
                    Licence_receiver = updatedVehicle.Licence_receiver,
                    Licence_date_taken = updatedVehicle.Licence_date_taken,
                    message = "Licence captured successfully. Previous licence saved to history.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing licence for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to capture licence" });
        }
    }

    /// <summary>
    /// Get the full licence history for a vehicle — all previous licence records,
    /// newest first.
    /// </summary>
    [HttpGet("{vmfCode:int}/licence/history")]
    public async Task<ActionResult> GetLicenceHistory(int vmfCode)
    {
        try
        {
            var history = await _licenceHistory.GetByVehicleAsync(vmfCode);
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

            return Ok(
                new
                {
                    vmf_code = vmfCode,
                    fleet_number = vehicle?.fleet_number,
                    registration_number = vehicle?.registration_number,
                    // Current live licence values
                    current = vehicle == null
                        ? null
                        : new
                        {
                            vehicle.licence_due_date,
                            vehicle.lic_register_number,
                            vehicle.lic_registration_doc,
                            vehicle.licence_comments,
                            vehicle.cof_last_done,
                            vehicle.cof_required,
                            vehicle.Licence_receiver,
                            vehicle.Licence_receiver_id,
                            vehicle.Licence_date_taken,
                        },
                    // Historical snapshots
                    history_count = history.Count(),
                    history = history.Select(h => new FIS.Api.DTOs.VehicleLicenceHistoryDto
                    {
                        licence_history_id = h.licence_history_id,
                        vmf_code = h.vmf_code,
                        fleet_number = h.Vehicle?.fleet_number,
                        registration_number = h.Vehicle?.registration_number,
                        licence_due_date = h.licence_due_date,
                        lic_register_number = h.lic_register_number,
                        lic_registration_doc = h.lic_registration_doc,
                        licence_comments = h.licence_comments,
                        cof_last_done = h.cof_last_done,
                        cof_required = h.cof_required,
                        tare = h.tare,
                        Licence_receiver = h.Licence_receiver,
                        Licence_receiver_id = h.Licence_receiver_id,
                        Licence_receiver_tel = h.Licence_receiver_tel,
                        Licence_receiver_site = h.Licence_receiver_site,
                        Licence_date_taken = h.Licence_date_taken,
                        captured_at = h.captured_at,
                        captured_by_user_code = h.captured_by_user_code,
                        captured_by_user_email = h.CapturedByUser?.email,
                        update_notes = h.update_notes,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching licence history for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to fetch licence history" });
        }
    }

    // ──────────────────────────────────────────────────────────
    // VEHICLE REMARKS  (missing, under investigation, general notes)
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Get all remarks for a vehicle (history — open and resolved).
    /// </summary>
    [HttpGet("{vmfCode:int}/remarks")]
    public async Task<ActionResult> GetRemarks(int vmfCode)
    {
        try
        {
            var remarks = await _remarkRepository.GetByVehicleAsync(vmfCode);
            return Ok(remarks.Select(MapRemark));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching remarks for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to fetch vehicle remarks" });
        }
    }

    /// <summary>
    /// Get only active (unresolved) remarks for a vehicle.
    /// </summary>
    [HttpGet("{vmfCode:int}/remarks/active")]
    public async Task<ActionResult> GetActiveRemarks(int vmfCode)
    {
        try
        {
            var remarks = await _remarkRepository.GetActiveByVehicleAsync(vmfCode);
            return Ok(remarks.Select(MapRemark));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active remarks for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to fetch active vehicle remarks" });
        }
    }

    /// <summary>
    /// Add a remark to a vehicle (e.g. missing, under investigation).
    /// Categories: General | Missing | UnderInvestigation | AccidentHold | Other
    /// </summary>
    [HttpPost("{vmfCode:int}/remarks")]
    public async Task<ActionResult> AddRemark(
        int vmfCode,
        [FromBody] FIS.Api.DTOs.CreateVehicleRemarkDto dto
    )
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var remark = new FIS.Core.Domain.Entities.VehicleRemark
            {
                vmf_code = vmfCode,
                remark_category = dto.remark_category,
                remark_text = dto.remark_text,
            };

            var created = await _remarkRepository.CreateAsync(remark, currentUserId);
            _logger.LogInformation(
                "Vehicle remark added: vmf={VmfCode}, category={Category}, by user {UserId}",
                vmfCode,
                dto.remark_category,
                currentUserId
            );

            return CreatedAtAction(nameof(GetRemarks), new { vmfCode }, MapRemark(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding remark to vehicle {VmfCode}", vmfCode);
            return StatusCode(500, new { error = "Failed to add vehicle remark" });
        }
    }

    /// <summary>
    /// Resolve (close) a vehicle remark — e.g. vehicle found, investigation concluded.
    /// </summary>
    [HttpPost("{vmfCode:int}/remarks/{remarkId:int}/resolve")]
    public async Task<ActionResult> ResolveRemark(
        int vmfCode,
        int remarkId,
        [FromBody] FIS.Api.DTOs.ResolveVehicleRemarkDto dto
    )
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var resolved = await _remarkRepository.ResolveAsync(
                remarkId,
                currentUserId,
                dto.resolution_notes
            );

            if (resolved.vmf_code != vmfCode)
                return BadRequest(new { error = "Remark does not belong to this vehicle." });

            _logger.LogInformation(
                "Vehicle remark {RemarkId} resolved by user {UserId}",
                remarkId,
                currentUserId
            );

            return Ok(MapRemark(resolved));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving remark {RemarkId}", remarkId);
            return StatusCode(500, new { error = "Failed to resolve vehicle remark" });
        }
    }

    /// <summary>
    /// Delete a vehicle remark (soft delete — for data entry errors only).
    /// </summary>
    [HttpDelete("{vmfCode:int}/remarks/{remarkId:int}")]
    public async Task<ActionResult> DeleteRemark(int vmfCode, int remarkId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            await _remarkRepository.DeleteAsync(remarkId, currentUserId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting remark {RemarkId}", remarkId);
            return StatusCode(500, new { error = "Failed to delete vehicle remark" });
        }
    }

    private static FIS.Api.DTOs.VehicleRemarkResponseDto MapRemark(
        FIS.Core.Domain.Entities.VehicleRemark r
    ) =>
        new()
        {
            remark_id = r.remark_id,
            vmf_code = r.vmf_code,
            fleet_number = r.Vehicle?.fleet_number,
            registration_number = r.Vehicle?.registration_number,
            remark_category = r.remark_category,
            remark_text = r.remark_text,
            is_resolved = r.is_resolved,
            resolved_date = r.resolved_date,
            resolved_by_user_email = r.ResolvedByUser?.email,
            resolution_notes = r.resolution_notes,
            date_created = r.date_created,
            date_updated = r.date_updated,
            created_by_user_code = r.created_by_user_code,
            created_by_user_email = r.CreatedByUser?.email,
        };
}

/// <summary>
/// API request for creating a new vehicle - uses snake_case to match frontend VehicleDto
/// </summary>
public class VehicleCreationApiRequest
{
    public short model_code { get; set; } = 1;
    public short type_code { get; set; } = 1;
    public short vehicle_status_code { get; set; } = 1;
    public short location_code { get; set; } = 1;

    [Required]
    public string? fleet_number { get; set; }

    [Required]
    public string? registration_number { get; set; }

    public string? engine_number_1 { get; set; }
    public string? chassis_number { get; set; }
    public DateTime? take_on_date { get; set; }
    public int take_on_odo { get; set; }
    public int current_odo { get; set; }
    public int? tare { get; set; }
    public int? gvm { get; set; }
    public short? year_manufactured { get; set; }
    public string? colour { get; set; }
    public DateTime? purchase_date { get; set; }
    public decimal? purchase_amount { get; set; }

    [StringLength(50)]
    public string? ifms_vehicle_register_number { get; set; }

    [StringLength(50)]
    public string? natis_model_number { get; set; }

    /// <summary>
    /// Flag to trigger tariff recalculation for this vehicle
    /// </summary>
    public bool recalculate_tariff { get; set; } = false;
}

/// <summary>
/// API request for updating an existing vehicle - uses snake_case to match frontend VehicleDto
/// </summary>
public class VehicleUpdateApiRequest
{
    public short? model_code { get; set; }
    public short? type_code { get; set; }
    public short? vehicle_status_code { get; set; }
    public short? location_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
    public string? engine_number_1 { get; set; }
    public string? chassis_number { get; set; }
    public int? take_on_odo { get; set; }
    public int? current_odo { get; set; }
    public int? tare { get; set; }
    public int? gvm { get; set; }
    public short? year_manufactured { get; set; }
    public string? colour { get; set; }

    [StringLength(50)]
    public string? ifms_vehicle_register_number { get; set; }

    [StringLength(50)]
    public string? natis_model_number { get; set; }

    /// <summary>
    /// Flag to trigger tariff recalculation for this vehicle
    /// </summary>
    public bool recalculate_tariff { get; set; } = false;
}

/// <summary>
/// Request for the legacy recovered-GG renumbering transaction.
/// </summary>
public class RecoveredVehicleUpdateRequest
{
    [Range(1, int.MaxValue)]
    public int VmfCode { get; set; }

    [Required]
    [StringLength(20)]
    public string? RecoveredFleetNumber { get; set; }

    [Required]
    public DateTime? DateChanged { get; set; }

    [Range(1, short.MaxValue)]
    public short NewStatusCode { get; set; }
}

/// <summary>
/// Request for updating a vehicle barcode by VMF code.
/// </summary>
public class VehicleBarcodeUpdateRequest
{
    public string? barcode { get; set; }
}

/// <summary>
/// Legacy-compatible request for updating a vehicle barcode by GG number.
/// </summary>
public class VehicleBarcodeUpdateByFleetNumberRequest
{
    public string? ggNumber { get; set; }
    public string? barcode { get; set; }
}

public class VehicleBarcodeUpdateResponse
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? barcode { get; set; }
}

/// <summary>
/// DTO for correcting model code on a new vehicle (no active contract)
/// </summary>
public class CorrectModelDto
{
    [Required]
    public short model_code { get; set; }
}

/// <summary>
/// DTO for correcting GG/fleet number on a new vehicle (no active contract)
/// </summary>
public class CorrectGGDto
{
    [Required]
    [StringLength(20)]
    public string fleet_number { get; set; } = string.Empty;
}

/// <summary>
/// DTO for invoice number update requests
/// </summary>
public class UpdateInvoiceDto
{
    [StringLength(50)]
    public string? invoice_number { get; set; }
}

/// <summary>
/// DTO for odometer update requests
/// </summary>
public class UpdateOdometerDto
{
    /// <summary>
    /// New odometer reading
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int NewOdometer { get; set; }

    /// <summary>
    /// Optional notes about the odometer update
    /// </summary>
    [StringLength(200)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for PATCH /api/vehicles/{vmfCode}/status
/// </summary>
public class ChangeVehicleStatusDto
{
    /// <summary>
    /// Target status code.
    /// Known values: 0=New, 1=In Service, 2=Withdrawn, 3=Board of Survey,
    /// 4=Stolen, 5=Sold, 6=Transferred, 7=Subsidized, 8=From Focus,
    /// 9=Privatised, 10=Recovered, 11=Missing, 12=Destroyed
    /// </summary>
    [Required]
    public short new_status_code { get; set; }

    /// <summary>
    /// Site under which the vehicle should be booked after the status change.
    /// REQUIRED when new_status_code = 4 (Stolen).
    /// </summary>
    public short? site_code { get; set; }

    /// <summary>
    /// Optional date/time the status change took effect (defaults to now).
    /// </summary>
    public DateTime? effective_date { get; set; }

    /// <summary>
    /// Optional notes — appended to the auto-closed contract's closure notes when stolen.
    /// </summary>
    [StringLength(2000)]
    public string? notes { get; set; }
}
