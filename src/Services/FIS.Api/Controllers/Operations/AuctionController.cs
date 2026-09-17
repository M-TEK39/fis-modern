using System.Security.Claims;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Auction,Reports,SystemAdministrator,System Administrator")]
public class AuctionController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly IAuctionRepository _repository;
    private readonly AuctionMaintenanceCompatibilityService _maintenanceService;
    private readonly ILogger<AuctionController> _logger;

    public AuctionController(
        IAuctionRepository repository,
        AuctionMaintenanceCompatibilityService maintenanceService,
        ILogger<AuctionController> logger
    )
    {
        _repository = repository;
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetMaintenancePage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? searchType = null,
        [FromQuery] string? searchQuery = null
    )
    {
        if (!HasReportsRole())
        {
            return Forbid();
        }

        try
        {
            var result = await _maintenanceService.GetPageAsync(
                NormalizeSearchTerm(searchQuery),
                string.Equals(searchType?.Trim(), "GP", StringComparison.OrdinalIgnoreCase),
                Math.Clamp(page, 1, 1_000_000),
                Math.Clamp(pageSize, 1, MaximumPageSize),
                HttpContext.RequestAborted
            );
            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged auction maintenance records");
            return StatusCode(500, "Error retrieving auction maintenance records");
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Auction>>> GetAll()
    {
        try
        {
            var items = await _repository.GetAllAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auctions");
            return StatusCode(500, "Error retrieving auctions");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Auction>> GetById(short id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auction {Id}", id);
            return StatusCode(500, "Error retrieving auction");
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Auction>>> GetByVehicle(int vmfCode)
    {
        try
        {
            var items = await _repository.GetByVehicleAsync(vmfCode);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auctions for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "Error retrieving auctions");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Auction>> Create([FromBody] Auction item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.auction_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating auction");
            return StatusCode(500, "Error creating auction");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Auction>> Update(short id, [FromBody] Auction item)
    {
        try
        {
            if (id != item.auction_code)
                return BadRequest("ID mismatch");

            var updated = await _repository.UpdateAsync(item, GetCurrentUserId());
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auction {Id}", id);
            return StatusCode(500, "Error updating auction");
        }
    }

    /// <summary>
    /// Updates the complete legacy auction maintenance workflow, including
    /// barcode and sale fields stored on the related vehicle_master row.
    /// </summary>
    [HttpPut("{id}/maintenance")]
    public async Task<ActionResult<Auction>> UpdateMaintenance(short id, [FromBody] Auction item)
    {
        try
        {
            if (id != item.auction_code)
                return BadRequest("ID mismatch");

            var updated = await _repository.UpdateMaintenanceAsync(item, GetCurrentUserId());
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auction maintenance {Id}", id);
            return StatusCode(500, "Error updating auction maintenance");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting auction {Id}", id);
            return StatusCode(500, "Error deleting auction");
        }
    }

    #region Specialized Operations

    /// <summary>
    /// Get auction menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<AuctionMenuDto> GetMenu() =>
        Ok(
            new AuctionMenuDto
            {
                Options = new List<string> { "Capture", "Reports", "Help" },
            }
        );

    #endregion

    #region Reports

    /// <summary>
    /// Get auction reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<AuctionReportMenuDto> GetReportsMenu() =>
        Ok(
            new AuctionReportMenuDto
            {
                Reports = new List<string>
                {
                    "One Vehicle",
                    "All Vehicles",
                    "Sale to Name",
                    "Auction GG",
                    "Auction Lot",
                },
            }
        );

    /// <summary>
    /// Generate auction report for one vehicle
    /// </summary>
    [HttpPost("reports/one-vehicle")]
    public async Task<ActionResult<AuctionReportDto>> GetReportOneVehicle(
        [FromBody] AuctionOneVehicleRequestDto request
    )
    {
        var result = await _repository.GetOneVehicleReportPageAsync(
            new AuctionOneVehicleReportPageQuery(
                request.VmfCode,
                NormalizeReportPage(request.Page),
                NormalizeReportPageSize(request.PageSize)
            )
        );

        return Ok(CreateReportDto("OneVehicle", result));
    }

    /// <summary>
    /// Generate auction report for all vehicles
    /// </summary>
    [HttpPost("reports/all-vehicles")]
    public async Task<ActionResult<AuctionReportDto>> GetReportAllVehicles(
        [FromBody] AuctionAllVehiclesRequestDto request
    )
    {
        var result = await _repository.GetAllVehiclesReportPageAsync(
            new AuctionAllVehiclesReportPageQuery(
                request.StartDate,
                request.EndDate,
                request.AuctionNumber,
                request.Garage,
                NormalizeReportPage(request.Page),
                NormalizeReportPageSize(request.PageSize)
            )
        );

        return Ok(CreateReportDto("AllVehicles", result));
    }

    /// <summary>
    /// Generate sale to name report
    /// </summary>
    [HttpPost("reports/sale-to-name")]
    public async Task<ActionResult<AuctionReportDto>> GetReportSaleToName(
        [FromBody] AuctionSaleToNameRequestDto request
    )
    {
        var result = await _repository.GetSaleToNameReportPageAsync(
            new AuctionSaleToNameReportPageQuery(
                request.BuyerName,
                request.StartDate,
                request.EndDate,
                NormalizeReportPage(request.Page),
                NormalizeReportPageSize(request.PageSize)
            )
        );

        return Ok(CreateReportDto("SaleToName", result));
    }

    /// <summary>
    /// Generate auction by GG number report
    /// </summary>
    [HttpPost("reports/auction-gg")]
    public async Task<ActionResult<AuctionReportDto>> GetReportAuctionGG(
        [FromBody] AuctionGGRequestDto request
    )
    {
        var result = await _repository.GetAuctionGgReportPageAsync(
            new AuctionGgReportPageQuery(
                request.GGNumber,
                request.AuctionNumber,
                request.Garage,
                NormalizeReportPage(request.Page),
                NormalizeReportPageSize(request.PageSize)
            )
        );

        return Ok(CreateReportDto("AuctionGG", result));
    }

    /// <summary>
    /// Generate auction by lot number report
    /// </summary>
    [HttpPost("reports/auction-lot")]
    public async Task<ActionResult<AuctionReportDto>> GetReportAuctionLot(
        [FromBody] AuctionLotRequestDto request
    )
    {
        var result = await _repository.GetAuctionLotReportPageAsync(
            new AuctionLotReportPageQuery(
                request.LotNumber,
                request.AuctionNumber,
                request.Garage,
                NormalizeReportPage(request.Page),
                NormalizeReportPageSize(request.PageSize)
            )
        );

        return Ok(CreateReportDto("AuctionLot", result));
    }

    #endregion

    private static AuctionReportDto CreateReportDto(string reportType, AuctionReportPage result) =>
        new()
        {
            ReportType = reportType,
            Data = result.Items.Cast<object>().ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
        };

    private static int NormalizeReportPage(int page) => Math.Clamp(page, 1, 1_000_000);

    private static int NormalizeReportPageSize(int pageSize) =>
        Math.Clamp(pageSize, 1, MaximumPageSize);

    private bool HasReportsRole() => HasAnyRole("Auction", "Reports");

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

        return User.Claims.Any(claim =>
            (
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            && claim
                .Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Any(role =>
                    expectedRoles.Any(expected =>
                        string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
                    )
                )
        );
    }

    private static string? NormalizeSearchTerm(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 8)];
    }

}

#region Auction DTOs
public class AuctionMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class AuctionReportMenuDto
{
    public List<string> Reports { get; set; } = new();
}

public class AuctionOneVehicleRequestDto
{
    public int VmfCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class AuctionAllVehiclesRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? AuctionNumber { get; set; }
    public string? Garage { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class AuctionSaleToNameRequestDto
{
    public string BuyerName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class AuctionGGRequestDto
{
    public string GGNumber { get; set; } = "";
    public string? AuctionNumber { get; set; }
    public string? Garage { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class AuctionLotRequestDto
{
    public string LotNumber { get; set; } = "";
    public string? AuctionNumber { get; set; }
    public string? Garage { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class AuctionReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}
#endregion
