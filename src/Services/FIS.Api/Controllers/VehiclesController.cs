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
public class VehiclesController : ControllerBase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly VehicleService _vehicleService;
    private readonly FisDbContext _context; // Keep for db-status endpoint
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IVehicleRepository vehicleRepository,
        VehicleService vehicleService,
        FisDbContext context,
        ILogger<VehiclesController> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _vehicleService = vehicleService;
        _context = context;
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
}

/// <summary>
/// API request for creating a new vehicle
/// </summary>
public class VehicleCreationApiRequest
{
    /// <summary>
    /// Fleet number (unique identifier for the vehicle)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string FleetNumber { get; set; } = string.Empty;

    /// <summary>
    /// Vehicle registration number
    /// </summary>
    [Required]
    [StringLength(20)]
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Site code where vehicle will be based
    /// </summary>
    [Required]
    public int SiteCode { get; set; }

    /// <summary>
    /// Vehicle model code (optional - defaults to 1)
    /// </summary>
    public short? ModelCode { get; set; }

    /// <summary>
    /// Vehicle type code (optional - defaults to 1)
    /// </summary>
    public short? TypeCode { get; set; }

    /// <summary>
    /// Year of manufacture
    /// </summary>
    [Range(1900, 2030)]
    public int? Year { get; set; }

    /// <summary>
    /// Initial mileage/odometer reading
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? InitialMileage { get; set; }

    /// <summary>
    /// Chassis number
    /// </summary>
    [StringLength(50)]
    public string? ChassisNumber { get; set; }

    /// <summary>
    /// Engine number
    /// </summary>
    [StringLength(50)]
    public string? EngineNumber { get; set; }

    /// <summary>
    /// Purchase date
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Purchase amount
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? PurchaseAmount { get; set; }
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
