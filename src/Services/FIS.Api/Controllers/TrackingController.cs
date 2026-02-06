using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrackingController : BaseApiController
{
    private readonly ITrackingRepository _repository;
    private readonly ILogger<TrackingController> _logger;
    public TrackingController(ITrackingRepository repository, ILogger<TrackingController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tracking>> GetById(short id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetByVehicle(int vmfCode) { try { return Ok(await _repository.GetByVehicleAsync(vmfCode)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetActive() { try { return Ok(await _repository.GetActiveTrackingAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<Tracking>> Create([FromBody] Tracking item) { try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.track_code }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<Tracking>> Update(short id, [FromBody] Tracking item) { try { if (id != item.track_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    #region Specialized Operations

    [HttpGet("menu")]
    public ActionResult<TrackingMenuDto> GetMenu() => Ok(new TrackingMenuDto { Options = new List<string> { "Maintenance", "Reports", "Help" } });

    [HttpGet("vehicle-search")]
    public ActionResult<TrackingVehicleLookupDto> SearchVehicle([FromQuery] string identifier) => Ok(new TrackingVehicleLookupDto { Found = false, Message = $"Search for: {identifier}" });

    #endregion

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<TrackingReportMenuDto> GetReportsMenu() => Ok(new TrackingReportMenuDto { Reports = new List<string> { "One Vehicle", "One Device", "All Vehicles", "All Devices", "Install Period", "Site Period", "Dept Period" } });

    [HttpPost("reports/one-vehicle")]
    public ActionResult<TrackingReportDto> GetReportOneVehicle([FromBody] TrackingOneVehicleRequestDto request) => Ok(new TrackingReportDto { ReportType = "OneVehicle", Data = new List<object>() });

    [HttpPost("reports/one-device")]
    public ActionResult<TrackingReportDto> GetReportOneDevice([FromBody] TrackingOneDeviceRequestDto request) => Ok(new TrackingReportDto { ReportType = "OneDevice", Data = new List<object>() });

    [HttpPost("reports/all-vehicles")]
    public ActionResult<TrackingReportDto> GetReportAllVehicles([FromBody] TrackingAllVehiclesRequestDto request) => Ok(new TrackingReportDto { ReportType = "AllVehicles", Data = new List<object>() });

    [HttpPost("reports/all-devices")]
    public ActionResult<TrackingReportDto> GetReportAllDevices([FromBody] TrackingAllDevicesRequestDto request) => Ok(new TrackingReportDto { ReportType = "AllDevices", Data = new List<object>() });

    [HttpPost("reports/install-period")]
    public ActionResult<TrackingReportDto> GetReportInstallPeriod([FromBody] TrackingInstallPeriodRequestDto request) => Ok(new TrackingReportDto { ReportType = "InstallPeriod", Data = new List<object>() });

    [HttpPost("reports/site-period")]
    public ActionResult<TrackingReportDto> GetReportSitePeriod([FromBody] TrackingSitePeriodRequestDto request) => Ok(new TrackingReportDto { ReportType = "SitePeriod", Data = new List<object>() });

    [HttpPost("reports/dept-period")]
    public ActionResult<TrackingReportDto> GetReportDeptPeriod([FromBody] TrackingDeptPeriodRequestDto request) => Ok(new TrackingReportDto { ReportType = "DeptPeriod", Data = new List<object>() });

    #endregion
}

#region Tracking DTOs
public class TrackingMenuDto { public List<string> Options { get; set; } = new(); }
public class TrackingVehicleLookupDto { public bool Found { get; set; } public string Message { get; set; } = ""; public int? VmfCode { get; set; } }
public class TrackingReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class TrackingOneVehicleRequestDto { public int VmfCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingOneDeviceRequestDto { public string DeviceId { get; set; } = ""; }
public class TrackingAllVehiclesRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingAllDevicesRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingInstallPeriodRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingSitePeriodRequestDto { public int SiteCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingDeptPeriodRequestDto { public int DepartmentCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
