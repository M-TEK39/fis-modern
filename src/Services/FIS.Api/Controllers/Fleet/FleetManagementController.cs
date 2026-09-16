using FIS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Fleet Management Controller - Modern API with Legacy Database Compatibility
///
/// Demonstrates how to build modern REST APIs that work seamlessly with 34-year-old legacy systems
/// without requiring database migrations. Showcases "partially run" deployment strategy.
///
/// Business Logic Features:
/// - Fuel card issuance and management using legacy Fuel_card table
/// - Vehicle-driver allocation using legacy trip_drivers and relationships
/// - Fleet reporting that leverages existing legacy data structures
/// - Modern API patterns with legacy backend compatibility
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FleetManagementController : BaseApiController
{
    private const int DefaultReportPageSize = 24;
    private const int MaximumReportPageSize = 100;

    private readonly FuelCardManagementService _fuelCardService;
    private readonly ILogger<FleetManagementController> _logger;

    public FleetManagementController(
        FuelCardManagementService fuelCardService,
        ILogger<FleetManagementController> logger
    )
    {
        _fuelCardService = fuelCardService;
        _logger = logger;
    }

    /// <summary>
    /// Issue a new fuel card to a vehicle - Modern API with legacy backend
    /// POST /api/fleetmanagement/fuelcards/issue
    /// </summary>
    [HttpPost("fuelcards/issue")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<FuelCardIssueResponse>> IssueFuelCard(
        [FromBody] IssueFuelCardRequest request
    )
    {
        try
        {
            _logger.LogInformation("Issuing fuel card for vehicle {VmfCode}", request.VmfCode);

            var fuelCard = await _fuelCardService.IssueFuelCardAsync(
                request.VmfCode,
                request.ReceiverName,
                request.ReceiverTelephone,
                request.SiteCode,
                GetCurrentUserId()
            );

            var response = new FuelCardIssueResponse
            {
                Success = true,
                Message = "Fuel card issued successfully using legacy workflow",
                FuelCard = new FuelCardDto
                {
                    FuelCardCode = fuelCard.Fuel_card_code, // Use exact legacy field name
                    CardNumber = fuelCard.card_number ?? "Unknown", // Use exact legacy field name
                    VmfCode = fuelCard.vmf_code ?? 0, // Use exact legacy field name with null coalescing
                    ReceiverName = fuelCard.PetReceiver ?? "Unknown",
                    IssuedDate = fuelCard.PetTaken ?? DateTime.MinValue,
                    ExpiryDate = fuelCard.PetExpire ?? DateTime.MinValue,
                    Status = fuelCard.ExpReason ?? "Unknown",
                    SiteCode = fuelCard.Petrecsite ?? 0,
                },
                LegacyCompatibility = new
                {
                    TableMapping = "Fuel_card",
                    LegacyFields = new
                    {
                        ExpReason = fuelCard.ExpReason,
                        PetReceiver = fuelCard.PetReceiver,
                        Garage = fuelCard.Garage,
                        Counter = fuelCard.Counter,
                    },
                    WorkflowPreserved = "Legacy status transitions and audit trail maintained",
                },
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing fuel card for vehicle {VmfCode}", request.VmfCode);

            return BadRequest(
                new FuelCardIssueResponse
                {
                    Success = false,
                    Message = $"Failed to issue fuel card: {ex.Message}",
                    Error = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Return/cancel a fuel card - Legacy-compatible status transitions
    /// PUT /api/fleetmanagement/fuelcards/{fuelCardCode}/return
    /// </summary>
    [HttpPut("fuelcards/{fuelCardCode}/return")]
    [Authorize(Roles = "Fuelcards")]
    public async Task<ActionResult<ApiResponse>> ReturnFuelCard(
        int fuelCardCode,
        [FromBody] ReturnFuelCardRequest request
    )
    {
        try
        {
            var result = await _fuelCardService.ReturnFuelCardAsync(
                fuelCardCode,
                request.Reason,
                GetCurrentUserId()
            );

            if (!result)
            {
                return NotFound(
                    new ApiResponse
                    {
                        Success = false,
                        Message = $"Fuel card {fuelCardCode} not found",
                    }
                );
            }

            return Ok(
                new ApiResponse
                {
                    Success = true,
                    Message =
                        $"Fuel card {fuelCardCode} returned successfully using legacy workflow",
                    Data = new
                    {
                        FuelCardCode = fuelCardCode,
                        ReturnReason = request.Reason,
                        ProcessedDate = DateTime.Now,
                        LegacyCompatibility = "ExpReason field updated, status transition preserved",
                    },
                }
            );
        }
        catch (Exception ex)
        {
            return BadRequest(
                new ApiResponse
                {
                    Success = false,
                    Message = $"Error returning fuel card: {ex.Message}",
                    Error = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Get fuel card allocation report - Legacy data with modern analytics
    /// GET /api/fleetmanagement/reports/fuelcard-allocation
    /// </summary>
    [HttpGet("reports/fuelcard-allocation")]
    [Authorize(Roles = "Reports")]
    public async Task<ActionResult<FuelCardReportResponse>> GetFuelCardReport(
        [FromQuery] int? siteCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultReportPageSize,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var report = await _fuelCardService.GetAllocationReportAsync(
                siteCode,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, MaximumReportPageSize),
                cancellationToken
            );

            return Ok(
                new FuelCardReportResponse
                {
                    Success = true,
                    Message = "Fuel card report generated from legacy data",
                    Report = report,
                    LegacyDataSources = new
                    {
                        PrimaryTable = "Fuel_card",
                        StatusField = "ExpReason (legacy CHAR field)",
                        RelationshipFields = new[] { "vmf_code", "Petrecsite" },
                        DataAge = "Real legacy data from 34-year-old system",
                        ModernAnalytics = "Business intelligence on legacy structures",
                    },
                    GeneratedAt = DateTime.Now,
                }
            );
        }
        catch (Exception ex)
        {
            return BadRequest(
                new FuelCardReportResponse
                {
                    Success = false,
                    Message = $"Error generating report: {ex.Message}",
                    Error = ex.Message,
                }
            );
        }
    }
}

// Request/Response DTOs
public class IssueFuelCardRequest
{
    public int VmfCode { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverTelephone { get; set; } = string.Empty;
    public int SiteCode { get; set; }
}

public class ReturnFuelCardRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class FuelCardDto
{
    public int FuelCardCode { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public int VmfCode { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public DateTime IssuedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public short SiteCode { get; set; }
}

public class FuelCardIssueResponse : ApiResponse
{
    public FuelCardDto? FuelCard { get; set; }
    public object? LegacyCompatibility { get; set; }
}

public class FuelCardReportResponse : ApiResponse
{
    public FuelCardAllocationReport? Report { get; set; }
    public object? LegacyDataSources { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public object? Data { get; set; }
}
