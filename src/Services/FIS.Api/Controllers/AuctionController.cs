using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuctionController : BaseApiController
{
    private readonly IAuctionRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<AuctionController> _logger;

    public AuctionController(IAuctionRepository repository, IVehicleRepository vehicleRepository, ILogger<AuctionController> logger)
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
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
    public ActionResult<AuctionMenuDto> GetMenu() => Ok(new AuctionMenuDto { Options = new List<string> { "Capture", "Reports", "Help" } });

    #endregion

    #region Reports

    /// <summary>
    /// Get auction reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<AuctionReportMenuDto> GetReportsMenu() => Ok(new AuctionReportMenuDto { Reports = new List<string> { "One Vehicle", "All Vehicles", "Sale to Name", "Auction GG", "Auction Lot" } });

    /// <summary>
    /// Generate auction report for one vehicle
    /// </summary>
    [HttpPost("reports/one-vehicle")]
    public async Task<ActionResult<AuctionReportDto>> GetReportOneVehicle([FromBody] AuctionOneVehicleRequestDto request)
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => item.vmf_code == request.VmfCode)
            .OrderByDescending(item => item.auth_date)
            .Cast<object>()
            .ToList();

        return Ok(new AuctionReportDto { ReportType = "OneVehicle", Data = data });
    }

    /// <summary>
    /// Generate auction report for all vehicles
    /// </summary>
    [HttpPost("reports/all-vehicles")]
    public async Task<ActionResult<AuctionReportDto>> GetReportAllVehicles([FromBody] AuctionAllVehiclesRequestDto request)
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.auth_date, request.StartDate, request.EndDate))
            .Where(item => MatchesAuctionNumberAndGarage(item, request.AuctionNumber, request.Garage))
            .OrderByDescending(item => item.auth_date)
            .Cast<object>()
            .ToList();

        return Ok(new AuctionReportDto { ReportType = "AllVehicles", Data = data });
    }

    /// <summary>
    /// Generate sale to name report
    /// </summary>
    [HttpPost("reports/sale-to-name")]
    public async Task<ActionResult<AuctionReportDto>> GetReportSaleToName([FromBody] AuctionSaleToNameRequestDto request)
    {
        var buyer = request.BuyerName?.Trim();
        var query = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.auth_date, request.StartDate, request.EndDate));

        if (!string.IsNullOrWhiteSpace(buyer))
        {
            query = query.Where(item =>
                (!string.IsNullOrWhiteSpace(item.sold_to) && item.sold_to.Contains(buyer, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.sold_id) && item.sold_id.Contains(buyer, StringComparison.OrdinalIgnoreCase)));
        }

        return Ok(new AuctionReportDto
        {
            ReportType = "SaleToName",
            Data = query.OrderByDescending(item => item.auth_date).Cast<object>().ToList()
        });
    }

    /// <summary>
    /// Generate auction by GG number report
    /// </summary>
    [HttpPost("reports/auction-gg")]
    public async Task<ActionResult<AuctionReportDto>> GetReportAuctionGG([FromBody] AuctionGGRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.AuctionNumber))
        {
            var auctionData = (await GetLiveItemsAsync())
                .Where(item => MatchesAuctionNumberAndGarage(item, request.AuctionNumber, request.Garage))
                .OrderBy(item => item.vmf_code)
                .ThenByDescending(item => item.auth_date)
                .Cast<object>()
                .ToList();

            return Ok(new AuctionReportDto { ReportType = "AuctionGG", Data = auctionData });
        }

        var gg = request.GGNumber?.Trim();
        if (string.IsNullOrWhiteSpace(gg))
        {
            return Ok(new AuctionReportDto { ReportType = "AuctionGG", Data = Array.Empty<object>().ToList() });
        }

        var matchedVmfCodes = (await _vehicleRepository.GetAllAsync())
            .Where(vehicle => !string.IsNullOrWhiteSpace(vehicle.fleet_number) &&
                              vehicle.fleet_number.Contains(gg, StringComparison.OrdinalIgnoreCase))
            .Select(vehicle => vehicle.vmf_code)
            .ToHashSet();

        var data = (await GetLiveItemsAsync())
            .Where(item => matchedVmfCodes.Contains(item.vmf_code))
            .OrderByDescending(item => item.auth_date)
            .Cast<object>()
            .ToList();

        return Ok(new AuctionReportDto { ReportType = "AuctionGG", Data = data });
    }

    /// <summary>
    /// Generate auction by lot number report
    /// </summary>
    [HttpPost("reports/auction-lot")]
    public async Task<ActionResult<AuctionReportDto>> GetReportAuctionLot([FromBody] AuctionLotRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.AuctionNumber))
        {
            var auctionData = (await GetLiveItemsAsync())
                .Where(item => MatchesAuctionNumberAndGarage(item, request.AuctionNumber, request.Garage))
                .OrderBy(item => item.lot)
                .ThenBy(item => item.vmf_code)
                .Cast<object>()
                .ToList();

            return Ok(new AuctionReportDto { ReportType = "AuctionLot", Data = auctionData });
        }

        var lot = request.LotNumber?.Trim();
        var data = (await GetLiveItemsAsync())
            .Where(item => !string.IsNullOrWhiteSpace(lot) &&
                           item.lot.HasValue &&
                           item.lot.Value.ToString().Contains(lot, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.auth_date)
            .Cast<object>()
            .ToList();

        return Ok(new AuctionReportDto { ReportType = "AuctionLot", Data = data });
    }

    #endregion

    private async Task<List<Auction>> GetLiveItemsAsync()
        => (await _repository.GetAllAsync())
            .Where(item => !item.is_deleted)
            .ToList();

    private static bool MatchesAuctionNumberAndGarage(Auction item, string? auctionNumber, string? garage)
    {
        if (!string.IsNullOrWhiteSpace(auctionNumber) &&
            !string.Equals(item.auction_number?.Trim(), auctionNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return garage?.Trim().ToUpperInvariant() switch
        {
            "JHB" => item.auction_garage == 1,
            "PTA" => item.auction_garage == 2,
            _ => true
        };
    }

    private static bool IsWithinInclusiveDateRange(DateTime? candidate, DateTime startDate, DateTime endDate)
    {
        if (!candidate.HasValue)
        {
            return false;
        }

        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var value = candidate.Value.Date;
        return value >= start && value <= end;
    }
}

#region Auction DTOs
public class AuctionMenuDto { public List<string> Options { get; set; } = new(); }
public class AuctionReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class AuctionOneVehicleRequestDto { public int VmfCode { get; set; } }
public class AuctionAllVehiclesRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public string? AuctionNumber { get; set; } public string? Garage { get; set; } }
public class AuctionSaleToNameRequestDto { public string BuyerName { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class AuctionGGRequestDto { public string GGNumber { get; set; } = ""; public string? AuctionNumber { get; set; } public string? Garage { get; set; } }
public class AuctionLotRequestDto { public string LotNumber { get; set; } = ""; public string? AuctionNumber { get; set; } public string? Garage { get; set; } }
public class AuctionReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
