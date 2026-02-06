using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TowingController : BaseApiController
{
    private readonly ITowingRepository _repository;
    private readonly ILogger<TowingController> _logger;

    public TowingController(ITowingRepository repository, ILogger<TowingController> logger)
    {
        _repository = repository;
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

    [HttpGet("menu")]
    public ActionResult<TowingMenuDto> GetMenu() => Ok(new TowingMenuDto { Options = new List<string> { "Request", "Towtruck Data", "Reports", "Help" } });

    [HttpPost("request")]
    public ActionResult<TowingRequestResultDto> CreateRequest([FromBody] TowingRequestDto request)
    {
        return Ok(new TowingRequestResultDto { Success = true, TowingCode = 0, Message = "Towing request created" });
    }

    [HttpPut("towtruck/{id}")]
    public ActionResult<TowtruckUpdateResultDto> UpdateTowtruck(short id, [FromBody] TowtruckDataDto request)
    {
        return Ok(new TowtruckUpdateResultDto { Success = true, TowingCode = id, Message = "Towtruck data updated" });
    }

    #endregion

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<TowingReportMenuDto> GetReportsMenu() => Ok(new TowingReportMenuDto { Reports = new List<string> { "Request Report", "All Towtrucks", "Firm/Date Report" } });

    [HttpPost("reports/request")]
    public ActionResult<TowingReportDto> GetReportRequest([FromBody] TowingRequestReportDto request) => Ok(new TowingReportDto { ReportType = "Request", Data = new List<object>() });

    [HttpGet("reports/towtruck/all")]
    public ActionResult<TowingReportDto> GetReportAllTowtrucks() => Ok(new TowingReportDto { ReportType = "AllTowtrucks", Data = new List<object>() });

    [HttpPost("reports/firm-date")]
    public ActionResult<TowingReportDto> GetReportFirmDate([FromBody] TowingFirmDateReportDto request) => Ok(new TowingReportDto { ReportType = "FirmDate", Data = new List<object>() });

    #endregion
}

#region Towing DTOs
public class TowingMenuDto { public List<string> Options { get; set; } = new(); }
public class TowingRequestDto { public int VmfCode { get; set; } public DateTime RequestDate { get; set; } public string Location { get; set; } = ""; public string Reason { get; set; } = ""; }
public class TowingRequestResultDto { public bool Success { get; set; } public short TowingCode { get; set; } public string Message { get; set; } = ""; }
public class TowtruckDataDto { public string TowtruckCompany { get; set; } = ""; public string ContactNumber { get; set; } = ""; public decimal Cost { get; set; } }
public class TowtruckUpdateResultDto { public bool Success { get; set; } public short TowingCode { get; set; } public string Message { get; set; } = ""; }
public class TowingReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class TowingRequestReportDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TowingFirmDateReportDto { public string FirmName { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TowingReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
