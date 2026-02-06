using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FuelCardController : BaseApiController
{
    private readonly IFuelCardRepository _repository;
    private readonly ILogger<FuelCardController> _logger;

    public FuelCardController(IFuelCardRepository repository, ILogger<FuelCardController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FuelCard>>> GetAll()
    {
        try
        {
            var items = await _repository.GetActiveFuelCardsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fuel cards");
            return StatusCode(500, "Error retrieving fuel cards");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<FuelCard>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fuel card {Id}", id);
            return StatusCode(500, "Error retrieving fuel card");
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<FuelCard>>> GetByVehicle(int vmfCode)
    {
        try
        {
            var items = await _repository.GetFuelCardsByVehicleAsync(vmfCode);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fuel cards for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "Error retrieving fuel cards");
        }
    }

    [HttpPost]
    public async Task<ActionResult<FuelCard>> Create([FromBody] FuelCard item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Fuel_card_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fuel card");
            return StatusCode(500, "Error creating fuel card");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] FuelCard item)
    {
        try
        {
            if (id != item.Fuel_card_code)
                return BadRequest("ID mismatch");

            await _repository.UpdateAsync(item, GetCurrentUserId());
            return Ok(new { message = "Fuel card updated successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating fuel card {Id}", id);
            return StatusCode(500, "Error updating fuel card");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fuel card {Id}", id);
            return StatusCode(500, "Error deleting fuel card");
        }
    }

    #region Specialized Operations

    /// <summary>
    /// Get fuel card menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<FuelCardMenuDto> GetMenu()
    {
        var menu = new FuelCardMenuDto
        {
            Options = new List<string>
            {
                "Maintenance",
                "Collection (Multiple)",
                "Delete Card",
                "Private Hire Maintenance",
                "Private Hire Collection",
                "Private Hire Delete",
                "Reports"
            }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Search fuel cards for vehicle maintenance
    /// </summary>
    [HttpPost("maintenance/vehicle/search")]
    public ActionResult<FuelCardSearchResultDto> SearchVehicleForMaintenance([FromBody] FuelCardVehicleSearchDto request)
    {
        // TODO: Implement vehicle search for maintenance
        var result = new FuelCardSearchResultDto
        {
            Found = false,
            Message = $"Search for vehicle: {request.VehicleIdentifier}",
            FuelCards = new List<FuelCardSummaryDto>()
        };
        return Ok(result);
    }

    /// <summary>
    /// Search fuel cards for multiple collection
    /// </summary>
    [HttpPost("collection/multiple/search")]
    public ActionResult<FuelCardCollectionResultDto> SearchCollectionMultiple([FromBody] FuelCardCollectionSearchDto request)
    {
        // TODO: Implement collection search
        var result = new FuelCardCollectionResultDto
        {
            Success = true,
            Message = "Collection search executed",
            Cards = new List<FuelCardSummaryDto>()
        };
        return Ok(result);
    }

    /// <summary>
    /// Search fuel cards for deletion
    /// </summary>
    [HttpPost("delete/search")]
    public ActionResult<FuelCardSearchResultDto> SearchForDelete([FromBody] FuelCardDeleteSearchDto request)
    {
        // TODO: Implement delete search
        var result = new FuelCardSearchResultDto
        {
            Found = false,
            Message = $"Search for card to delete: {request.CardNumber}",
            FuelCards = new List<FuelCardSummaryDto>()
        };
        return Ok(result);
    }

    /// <summary>
    /// Search private hire fuel cards for maintenance
    /// </summary>
    [HttpPost("privatehire/maintenance/vehicle/search")]
    public ActionResult<FuelCardSearchResultDto> SearchPrivateHireVehicleForMaintenance([FromBody] FuelCardVehicleSearchDto request)
    {
        // TODO: Implement private hire vehicle search for maintenance
        var result = new FuelCardSearchResultDto
        {
            Found = false,
            Message = $"Private hire search for vehicle: {request.VehicleIdentifier}",
            FuelCards = new List<FuelCardSummaryDto>()
        };
        return Ok(result);
    }

    /// <summary>
    /// Search private hire fuel cards for multiple collection
    /// </summary>
    [HttpPost("privatehire/collection/multiple/search")]
    public ActionResult<FuelCardCollectionResultDto> SearchPrivateHireCollectionMultiple([FromBody] FuelCardCollectionSearchDto request)
    {
        // TODO: Implement private hire collection search
        var result = new FuelCardCollectionResultDto
        {
            Success = true,
            Message = "Private hire collection search executed",
            Cards = new List<FuelCardSummaryDto>()
        };
        return Ok(result);
    }

    /// <summary>
    /// Search private hire fuel cards for deletion
    /// </summary>
    [HttpPost("privatehire/delete/search")]
    public ActionResult<FuelCardSearchResultDto> SearchPrivateHireForDelete([FromBody] FuelCardDeleteSearchDto request)
    {
        // TODO: Implement private hire delete search
        var result = new FuelCardSearchResultDto
        {
            Found = false,
            Message = $"Private hire search for card to delete: {request.CardNumber}",
            FuelCards = new List<FuelCardSummaryDto>()
        };
        return Ok(result);
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get fuel card reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<FuelCardReportMenuDto> GetReportsMenu()
    {
        var menu = new FuelCardReportMenuDto
        {
            Reports = new List<string>
            {
                "Latest Report",
                "One Vehicle",
                "All Vehicles",
                "Private Hire - All Vehicles"
            }
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate latest fuel card report
    /// </summary>
    [HttpPost("reports/latest")]
    public ActionResult<FuelCardReportDto> GetReportLatest([FromBody] FuelCardLatestReportRequestDto request)
    {
        // TODO: Implement latest report generation
        var report = new FuelCardReportDto
        {
            ReportType = "Latest",
            Data = new List<object>()
        };
        return Ok(report);
    }

    /// <summary>
    /// Generate fuel card report for one vehicle
    /// </summary>
    [HttpPost("reports/one-vehicle")]
    public ActionResult<FuelCardReportDto> GetReportOneVehicle([FromBody] FuelCardOneVehicleReportRequestDto request)
    {
        // TODO: Implement one vehicle report generation
        var report = new FuelCardReportDto
        {
            ReportType = "OneVehicle",
            Data = new List<object>()
        };
        return Ok(report);
    }

    /// <summary>
    /// Generate fuel card report for all vehicles
    /// </summary>
    [HttpPost("reports/all-vehicles")]
    public ActionResult<FuelCardReportDto> GetReportAllVehicles([FromBody] FuelCardAllVehiclesReportRequestDto request)
    {
        // TODO: Implement all vehicles report generation
        var report = new FuelCardReportDto
        {
            ReportType = "AllVehicles",
            Data = new List<object>()
        };
        return Ok(report);
    }

    /// <summary>
    /// Generate private hire fuel card report for all vehicles
    /// </summary>
    [HttpPost("privatehire/reports/all")]
    public ActionResult<FuelCardReportDto> GetReportPrivateHireAll([FromBody] FuelCardPrivateHireReportRequestDto request)
    {
        // TODO: Implement private hire all vehicles report generation
        var report = new FuelCardReportDto
        {
            ReportType = "PrivateHireAll",
            Data = new List<object>()
        };
        return Ok(report);
    }

    #endregion
}

#region Fuel Card DTOs
public class FuelCardMenuDto { public List<string> Options { get; set; } = new(); }
public class FuelCardVehicleSearchDto { public string VehicleIdentifier { get; set; } = ""; }
public class FuelCardSearchResultDto { public bool Found { get; set; } public string Message { get; set; } = ""; public List<FuelCardSummaryDto> FuelCards { get; set; } = new(); }
public class FuelCardSummaryDto { public int FuelCardCode { get; set; } public string? CardNumber { get; set; } public int? VmfCode { get; set; } public string? Status { get; set; } }
public class FuelCardCollectionSearchDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public int? SiteCode { get; set; } }
public class FuelCardCollectionResultDto { public bool Success { get; set; } public string Message { get; set; } = ""; public List<FuelCardSummaryDto> Cards { get; set; } = new(); }
public class FuelCardDeleteSearchDto { public string CardNumber { get; set; } = ""; }
public class FuelCardReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class FuelCardLatestReportRequestDto { public DateTime? AsOfDate { get; set; } }
public class FuelCardOneVehicleReportRequestDto { public int VmfCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class FuelCardAllVehiclesReportRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public int? DepartmentCode { get; set; } }
public class FuelCardPrivateHireReportRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class FuelCardReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
