using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TowingController : BaseApiController
{
    private readonly ITowingRepository _repository;
    private readonly TowTruckCompatibilityService _towTruckService;
    private readonly ILogger<TowingController> _logger;

    public TowingController(
        ITowingRepository repository,
        TowTruckCompatibilityService towTruckService,
        ILogger<TowingController> logger)
    {
        _repository = repository;
        _towTruckService = towTruckService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Towing>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Towing>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Towing>> Create([FromBody] Towing item)
    {
        try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.Towing_code }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Towing>> Update(short id, [FromBody] Towing item)
    {
        try { if (id != item.Towing_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    #region Specialized Operations

    [HttpGet("tow-trucks")]
    public async Task<ActionResult<IEnumerable<TowTruckOption>>> GetTowTrucks()
    {
        try
        {
            return Ok(await _towTruckService.GetAllAsync(HttpContext.RequestAborted));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tow truck options");
            return StatusCode(500, "Error retrieving tow truck options");
        }
    }

    [HttpGet("menu")]
    public ActionResult<TowingMenuDto> GetMenu() => Ok(new TowingMenuDto { Options = new List<string> { "Request", "Towtruck Data", "Reports", "Help" } });

    [HttpPost("request")]
    public async Task<ActionResult<TowingRequestResultDto>> CreateRequest([FromBody] TowingRequestDto request)
    {
        try
        {
            var item = new Towing
            {
                vmf_code = request.VmfCode,
                Tow_request_date = request.RequestDate == default ? DateTime.Today : request.RequestDate.Date,
                Tow_request_time = request.RequestDate == default ? DateTime.Now : request.RequestDate,
                Tow_location_start = request.Location?.Trim(),
                Vehicle_problem = request.Reason?.Trim(),
                Site_code = request.SiteCode,
                Keys = request.Keys?.Trim()
            };

            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return Ok(new TowingRequestResultDto
            {
                Success = true,
                TowingCode = created.Towing_code,
                Message = "Towing request created"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating towing request");
            return StatusCode(500, new TowingRequestResultDto
            {
                Success = false,
                Message = "Failed to create towing request"
            });
        }
    }

    [HttpPut("towtruck/{id}")]
    public async Task<ActionResult<TowtruckUpdateResultDto>> UpdateTowtruck(short id, [FromBody] TowtruckDataDto request)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null || existing.is_deleted)
            {
                return NotFound(new TowtruckUpdateResultDto
                {
                    Success = false,
                    TowingCode = id,
                    Message = "Towing request not found"
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Location))
            {
                existing.Tow_location_start = request.Location.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.VehicleProblem))
            {
                existing.Vehicle_problem = request.VehicleProblem.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Keys))
            {
                existing.Keys = request.Keys.Trim();
            }

            await _repository.UpdateAsync(existing, GetCurrentUserId());
            return Ok(new TowtruckUpdateResultDto
            {
                Success = true,
                TowingCode = id,
                Message = "Towtruck data updated"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating towtruck data for towing {TowingCode}", id);
            return StatusCode(500, new TowtruckUpdateResultDto
            {
                Success = false,
                TowingCode = id,
                Message = "Failed to update towtruck data"
            });
        }
    }

    #endregion

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<TowingReportMenuDto> GetReportsMenu() => Ok(new TowingReportMenuDto { Reports = new List<string> { "Request Report", "All Towtrucks", "Firm/Date Report" } });

    [HttpPost("reports/request")]
    public async Task<ActionResult<TowingReportDto>> GetReportRequest([FromBody] TowingRequestReportDto request)
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.Tow_request_date, request.StartDate, request.EndDate))
            .OrderByDescending(item => item.Tow_request_date)
            .Cast<object>()
            .ToList();

        return Ok(new TowingReportDto { ReportType = "Request", Data = data });
    }

    [HttpGet("reports/towtruck/all")]
    public async Task<ActionResult<TowingReportDto>> GetReportAllTowtrucks()
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => !string.IsNullOrWhiteSpace(item.Tow_location_start) ||
                           !string.IsNullOrWhiteSpace(item.Keys) ||
                           !string.IsNullOrWhiteSpace(item.Vehicle_problem))
            .OrderByDescending(item => item.Tow_request_date)
            .Cast<object>()
            .ToList();

        return Ok(new TowingReportDto { ReportType = "AllTowtrucks", Data = data });
    }

    [HttpPost("reports/firm-date")]
    public async Task<ActionResult<TowingReportDto>> GetReportFirmDate([FromBody] TowingFirmDateReportDto request)
    {
        var firm = request.FirmName?.Trim();
        var query = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.Tow_request_date, request.StartDate, request.EndDate));

        if (!string.IsNullOrWhiteSpace(firm))
        {
            query = query.Where(item =>
                (!string.IsNullOrWhiteSpace(item.Tow_location_start) && item.Tow_location_start.Contains(firm, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.Keys) && item.Keys.Contains(firm, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(item.Vehicle_problem) && item.Vehicle_problem.Contains(firm, StringComparison.OrdinalIgnoreCase)));
        }

        return Ok(new TowingReportDto
        {
            ReportType = "FirmDate",
            Data = query.OrderByDescending(item => item.Tow_request_date).Cast<object>().ToList()
        });
    }

    #endregion

    private async Task<List<Towing>> GetLiveItemsAsync()
        => (await _repository.GetAllAsync())
            .Where(item => !item.is_deleted)
            .ToList();

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

#region Towing DTOs
public class TowingMenuDto { public List<string> Options { get; set; } = new(); }
public class TowingRequestDto
{
    public int VmfCode { get; set; }
    public DateTime RequestDate { get; set; }
    public string Location { get; set; } = "";
    public string Reason { get; set; } = "";
    public short? SiteCode { get; set; }
    public string? Keys { get; set; }
}
public class TowingRequestResultDto { public bool Success { get; set; } public short TowingCode { get; set; } public string Message { get; set; } = ""; }
public class TowtruckDataDto
{
    public string? Location { get; set; }
    public string? VehicleProblem { get; set; }
    public string? Keys { get; set; }
}
public class TowtruckUpdateResultDto { public bool Success { get; set; } public short TowingCode { get; set; } public string Message { get; set; } = ""; }
public class TowingReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class TowingRequestReportDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TowingFirmDateReportDto { public string FirmName { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TowingReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
