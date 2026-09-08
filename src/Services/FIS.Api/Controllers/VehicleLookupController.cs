using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Vehicle lookup API for autocomplete/typeahead scenarios
/// Returns lightweight DTOs optimized for search results
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class VehicleLookupController : BaseApiController
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<VehicleLookupController> _logger;

    public VehicleLookupController(
        IVehicleRepository vehicleRepository,
        ILogger<VehicleLookupController> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    /// <summary>
    /// Search vehicles by keyword (fleet number, registration, VMF code)
    /// Optimized for autocomplete/typeahead - returns lightweight results
    /// </summary>
    /// <param name="keyword">Search keyword (fleet number, registration, or VMF code)</param>
    /// <param name="limit">Maximum number of results (default 20)</param>
    /// <returns>List of matching vehicles with essential details only</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VehicleSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<VehicleSearchResultDto>>> Lookup(
        [FromQuery] string keyword,
        [FromQuery] int limit = 20
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Ok(Array.Empty<VehicleSearchResultDto>());
            }

            // Search vehicles using repository
            var vehicles = await _vehicleRepository.SearchVehiclesAsync(keyword);

            // Map to lightweight DTOs
            var results = vehicles
                .Take(limit)
                .Select(v => new VehicleSearchResultDto
                {
                    VmfCode = v.vmf_code,
                    FleetNumber = v.fleet_number ?? string.Empty,
                    RegistrationNumber = v.registration_number,
                    ChassisNumber = v.chassis_number,
                    EngineNumber = v.engine_number_1,
                    MakeCode = v.Model?.make_code,
                    ModelCode = v.model_code,
                    CurrentOdometer = v.current_odo,
                    VehicleStatusCode = v.vehicle_status_code,
                    IsAvailable = !v.is_deleted && v.vehicle_status_code == 1,
                    InvoiceNumber = v.invoice_number,
                    DisplayText = $"{v.fleet_number} - {v.registration_number}",
                })
                .ToList();

            _logger.LogInformation(
                "Vehicle lookup: '{Keyword}' returned {Count} results",
                keyword,
                results.Count
            );

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error performing vehicle lookup for keyword '{Keyword}'",
                keyword
            );
            return StatusCode(
                500,
                new { error = "Failed to search vehicles", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Get vehicle by VMF code for lookup
    /// </summary>
    [HttpGet("{vmfCode}")]
    [ProducesResponseType(typeof(VehicleSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleSearchResultDto>> GetByVmfCode(int vmfCode)
    {
        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
            {
                return NotFound(new { error = "Vehicle not found", vmfCode });
            }

            var dto = new VehicleSearchResultDto
            {
                VmfCode = vehicle.vmf_code,
                FleetNumber = vehicle.fleet_number ?? string.Empty,
                RegistrationNumber = vehicle.registration_number,
                ChassisNumber = vehicle.chassis_number,
                EngineNumber = vehicle.engine_number_1,
                MakeCode = vehicle.Model?.make_code,
                ModelCode = vehicle.model_code,
                CurrentOdometer = vehicle.current_odo,
                VehicleStatusCode = vehicle.vehicle_status_code,
                IsAvailable = !vehicle.is_deleted && vehicle.vehicle_status_code == 1,
                InvoiceNumber = vehicle.invoice_number,
                DisplayText = $"{vehicle.fleet_number} - {vehicle.registration_number}",
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle {VmfCode} for lookup", vmfCode);
            return StatusCode(
                500,
                new { error = "Failed to retrieve vehicle", message = ex.Message }
            );
        }
    }
}

/// <summary>
/// Lightweight DTO for vehicle lookup/search results
/// Used in autocomplete/typeahead scenarios
/// </summary>
public class VehicleSearchResultDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public short? MakeCode { get; set; }
    public short? ModelCode { get; set; }
    public int? CurrentOdometer { get; set; }
    public short? VehicleStatusCode { get; set; }
    public bool IsAvailable { get; set; }
    public string? InvoiceNumber { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}
