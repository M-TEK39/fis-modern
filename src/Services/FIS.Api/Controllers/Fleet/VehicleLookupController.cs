using FIS.Core.Application.Interfaces;
using FIS.Api.Services;
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
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private static readonly string[] VehicleLookupRoles =
    [
        "Vehicle Master",
        "Vehicle Inception Capturer",
        "Vehicle Inception Authorizer",
        "Reports",
        "Management Reports",
        "Financial Reports",
        "Financial Data (Own Department)",
        "Financial Data (All Departments)",
        "Accidents",
        "Auction",
        "Call Centre",
        "Clearance",
        "Contracts",
        "Fines",
        "Fuelcards",
        "Licence",
        "Logbooks",
        "Logsheets",
        "Losses",
        "Monitor",
        "Private Hire Vehicles",
        "Taxi information maintenance",
        "Towing",
        "Tracking",
        "Trip Authorities",
        "TripAuthorities",
        "Trouble Shooting",
        "Validation",
        "Workshop",
        "Asset Verification",
        "Lease Vehicle Pending",
        "Lease Vehicle Capturer",
        "Lease Vehicle Authorizer",
        "JobCard Capturer",
        "JobCard Authorizer",
    ];

    private readonly IVehicleRepository _vehicleRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly ILogger<VehicleLookupController> _logger;

    public VehicleLookupController(
        IVehicleRepository vehicleRepository,
        LegacyVehicleScopeService vehicleScope,
        ILogger<VehicleLookupController> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _vehicleScope = vehicleScope;
        _logger = logger;
    }

    /// <summary>
    /// Search vehicle-photo candidates with database-side filtering and paging.
    /// GG searches fleet_number and GP searches registration_number. If mode is
    /// omitted, either existing legacy field may match the keyword.
    /// </summary>
    [HttpGet("page")]
    [ProducesResponseType(typeof(VehicleLookupPageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VehicleLookupPageResponseDto>> GetPage(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? mode = null
    )
    {
        if (!HasVehicleLookupAccess())
            return Forbid();

        var normalizedMode = string.IsNullOrWhiteSpace(mode)
            ? null
            : mode.Trim().ToUpperInvariant();
        if (normalizedMode is not (null or "GG" or "GP"))
        {
            return BadRequest(new { error = "Search mode must be GG or GP." });
        }

        var normalizedKeyword = keyword?.Trim();
        if (normalizedKeyword?.Length > 100)
        {
            return BadRequest(new { error = "Keyword cannot exceed 100 characters." });
        }

        try
        {
            var result = await _vehicleRepository.GetVehicleLookupPageAsync(
                normalizedKeyword,
                normalizedMode,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, MaximumPageSize),
                await ResolveAllowedVehicleSiteCodesAsync(),
                GetCurrentUserId()
            );

            return Ok(
                new VehicleLookupPageResponseDto
                {
                    Items = result
                        .Items.Select(item => new VehiclePhotoSearchResultDto
                        {
                            VmfCode = item.VmfCode,
                            GgNumber = item.GgNumber,
                            RegistrationNumber = item.RegistrationNumber,
                            MakeAndModel = item.MakeAndModel,
                            ModelCode = item.ModelCode,
                            YearManufactured = item.YearManufactured,
                            Colour = item.Colour,
                            HireType = item.HireType,
                            Status = item.Status,
                            HiredFrom = item.HiredFrom,
                            StatusDate = item.StatusDate,
                        })
                        .ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    Total = result.Total,
                    TotalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving paged vehicle lookup for mode {SearchMode}",
                normalizedMode ?? "GG/GP"
            );
            return StatusCode(500, new { error = "Failed to search vehicles" });
        }
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
        if (!HasVehicleLookupAccess())
            return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return Ok(Array.Empty<VehicleSearchResultDto>());
            }

            // Search vehicles using repository
            var vehicles = await _vehicleRepository.SearchVehiclesAsync(
                keyword,
                await ResolveAllowedVehicleSiteCodesAsync(),
                GetCurrentUserId()
            );

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
        if (!HasVehicleLookupAccess())
            return Forbid();

        try
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(
                vmfCode,
                await ResolveAllowedVehicleSiteCodesAsync(),
                GetCurrentUserId()
            );
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

    private bool HasVehicleLookupAccess()
    {
        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == System.Security.Claims.ClaimTypes.Role
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
            VehicleLookupRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    private Task<IReadOnlySet<short>?> ResolveAllowedVehicleSiteCodesAsync() =>
        _vehicleScope.ResolveAllowedSiteCodesAsync(User, HttpContext.RequestAborted);
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

/// <summary>
/// Response contract for the vehicle-photo search table. MakeAndModel is the
/// legacy model description only; the legacy lookup does not expose a combined
/// make/model value. Lookup descriptions are null when their optional lookup
/// table or legacy column is unavailable.
/// </summary>
public sealed class VehicleLookupPageResponseDto
{
    public IReadOnlyList<VehiclePhotoSearchResultDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

public sealed class VehiclePhotoSearchResultDto
{
    public int VmfCode { get; set; }
    public string? GgNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? MakeAndModel { get; set; }
    public short ModelCode { get; set; }
    public short? YearManufactured { get; set; }
    public string? Colour { get; set; }
    public string? HireType { get; set; }
    public string? Status { get; set; }
    public string? HiredFrom { get; set; }
    public DateTime? StatusDate { get; set; }
}
