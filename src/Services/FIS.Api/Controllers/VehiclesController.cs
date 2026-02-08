using System.ComponentModel.DataAnnotations;
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
    private readonly VehicleService _vehicleService;
    private readonly FisDbContext _context; // Keep for db-status endpoint
    private readonly IVehicleTariffRepository _tariffRepository;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IVehicleRepository vehicleRepository,
        VehicleService vehicleService,
        FisDbContext context,
        IVehicleTariffRepository tariffRepository,
        ILogger<VehiclesController> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _vehicleService = vehicleService;
        _context = context;
        _tariffRepository = tariffRepository;
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
    public async Task<ActionResult<Vehicle>> CreateVehicle([FromBody] VehicleCreationApiRequest request)
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
                date_created = DateTime.UtcNow,
                created_by_user_code = currentUserId,
                is_deleted = false
            };

            var created = await _vehicleRepository.CreateAsync(vehicle, currentUserId);
            _logger.LogInformation("Created vehicle with vmf_code {VmfCode}", created.vmf_code);

            // Handle tariff recalculation if requested
            if (request.recalculate_tariff)
            {
                _logger.LogInformation("Tariff recalculation requested for vehicle {VmfCode}", created.vmf_code);
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
    public async Task<ActionResult<Vehicle>> UpdateVehicle(int vmfCode, [FromBody] VehicleUpdateApiRequest request)
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            
            var existing = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (existing == null)
            {
                return NotFound($"Vehicle with vmf_code {vmfCode} not found");
            }

            // Update fields (only if provided)
            existing.fleet_number = request.fleet_number ?? existing.fleet_number;
            existing.registration_number = request.registration_number ?? existing.registration_number;
            existing.location_code = request.location_code ?? existing.location_code;
            existing.model_code = request.model_code ?? existing.model_code;
            existing.type_code = request.type_code ?? existing.type_code;
            existing.vehicle_status_code = request.vehicle_status_code ?? existing.vehicle_status_code;
            existing.colour = request.colour ?? existing.colour;
            existing.chassis_number = request.chassis_number ?? existing.chassis_number;
            existing.engine_number_1 = request.engine_number_1 ?? existing.engine_number_1;
            existing.take_on_odo = request.take_on_odo ?? existing.take_on_odo;
            existing.current_odo = request.current_odo ?? existing.current_odo;
            existing.tare = request.tare ?? existing.tare;
            existing.gvm = request.gvm ?? existing.gvm;
            existing.year_manufactured = request.year_manufactured ?? existing.year_manufactured;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;

            await _vehicleRepository.UpdateAsync(existing, currentUserId);
            _logger.LogInformation("Updated vehicle with vmf_code {VmfCode}", vmfCode);

            // Handle tariff recalculation if requested
            if (request.recalculate_tariff)
            {
                _logger.LogInformation("Tariff recalculation requested for vehicle {VmfCode}", vmfCode);
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

    /// <summary>
    /// Flag to trigger tariff recalculation for this vehicle
    /// </summary>
    public bool recalculate_tariff { get; set; } = false;
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
