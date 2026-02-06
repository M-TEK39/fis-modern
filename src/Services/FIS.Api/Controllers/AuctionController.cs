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
    private readonly ILogger<AuctionController> _logger;

    public AuctionController(IAuctionRepository repository, ILogger<AuctionController> logger)
    {
        _repository = repository;
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
    public ActionResult<AuctionReportDto> GetReportOneVehicle([FromBody] AuctionOneVehicleRequestDto request) => Ok(new AuctionReportDto { ReportType = "OneVehicle", Data = new List<object>() });

    /// <summary>
    /// Generate auction report for all vehicles
    /// </summary>
    [HttpPost("reports/all-vehicles")]
    public ActionResult<AuctionReportDto> GetReportAllVehicles([FromBody] AuctionAllVehiclesRequestDto request) => Ok(new AuctionReportDto { ReportType = "AllVehicles", Data = new List<object>() });

    /// <summary>
    /// Generate sale to name report
    /// </summary>
    [HttpPost("reports/sale-to-name")]
    public ActionResult<AuctionReportDto> GetReportSaleToName([FromBody] AuctionSaleToNameRequestDto request) => Ok(new AuctionReportDto { ReportType = "SaleToName", Data = new List<object>() });

    /// <summary>
    /// Generate auction by GG number report
    /// </summary>
    [HttpPost("reports/auction-gg")]
    public ActionResult<AuctionReportDto> GetReportAuctionGG([FromBody] AuctionGGRequestDto request) => Ok(new AuctionReportDto { ReportType = "AuctionGG", Data = new List<object>() });

    /// <summary>
    /// Generate auction by lot number report
    /// </summary>
    [HttpPost("reports/auction-lot")]
    public ActionResult<AuctionReportDto> GetReportAuctionLot([FromBody] AuctionLotRequestDto request) => Ok(new AuctionReportDto { ReportType = "AuctionLot", Data = new List<object>() });

    #endregion
}

#region Auction DTOs
public class AuctionMenuDto { public List<string> Options { get; set; } = new(); }
public class AuctionReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class AuctionOneVehicleRequestDto { public int VmfCode { get; set; } }
public class AuctionAllVehiclesRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class AuctionSaleToNameRequestDto { public string BuyerName { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class AuctionGGRequestDto { public string GGNumber { get; set; } = ""; }
public class AuctionLotRequestDto { public string LotNumber { get; set; } = ""; }
public class AuctionReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
