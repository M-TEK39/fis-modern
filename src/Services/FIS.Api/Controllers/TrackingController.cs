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
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly ILogger<TrackingController> _logger;
    public TrackingController(
        ITrackingRepository repository,
        IVehicleRepository vehicleRepository,
        ISiteRepository siteRepository,
        ILogger<TrackingController> logger)
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _siteRepository = siteRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tracking>> GetById(short id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetByVehicle(int vmfCode) { try { return Ok(await _repository.GetByVehicleAsync(vmfCode)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetActive() { try { return Ok(await _repository.GetActiveTrackingAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<Tracking>> Create([FromBody] Tracking item)
    {
        var validation = Validate(item);
        if (validation is not null) return BadRequest(validation);

        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.track_code }, created);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Tracking>> Update(short id, [FromBody] Tracking item)
    {
        if (item is null) return BadRequest("Tracking data is required.");
        if (id != item.track_code) return BadRequest("Tracking code does not match the route.");
        var validation = Validate(item);
        if (validation is not null) return BadRequest(validation);

        try
        {
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    #region Specialized Operations

    [HttpGet("menu")]
    public ActionResult<TrackingMenuDto> GetMenu() => Ok(new TrackingMenuDto { Options = new List<string> { "Maintenance", "Reports", "Help" } });

    [HttpGet("vehicle-search")]
    public async Task<ActionResult<TrackingVehicleLookupDto>> SearchVehicle([FromQuery] string identifier)
    {
        try
        {
            var query = identifier?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new TrackingVehicleLookupDto
                {
                    Found = false,
                    Message = "Vehicle identifier is required."
                });
            }

            var vehicles = await _vehicleRepository.SearchVehiclesAsync(query);
            var best = vehicles.FirstOrDefault();

            if (best == null)
            {
                return Ok(new TrackingVehicleLookupDto
                {
                    Found = false,
                    Message = $"No vehicle found for '{query}'."
                });
            }

            return Ok(new TrackingVehicleLookupDto
            {
                Found = true,
                Message = "Vehicle found.",
                VmfCode = best.vmf_code
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vehicle for tracking identifier {Identifier}", identifier);
            return StatusCode(500);
        }
    }

    #endregion

    private static string? Validate(Tracking? item)
    {
        if (item is null) return "Tracking data is required.";
        if (item.vmf_code is <= 0) return "Vehicle code must be positive when supplied.";
        if (item.track_num?.Length > 50) return "Tracker number must be 50 characters or fewer.";
        if (item.gg_previous?.Length > 50) return "Previous GG must be 50 characters or fewer.";
        if (item.gg_follow?.Length > 50) return "Follow GG must be 50 characters or fewer.";
        if (item.track_status?.Length > 100) return "Tracker status must be 100 characters or fewer.";
        if (item.track_type?.Length > 100) return "Tracker type must be 100 characters or fewer.";
        return item.track_note?.Length > 4000 ? "Tracking notes must be 4000 characters or fewer." : null;
    }

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<TrackingReportMenuDto> GetReportsMenu() => Ok(new TrackingReportMenuDto { Reports = new List<string> { "One Vehicle", "One Device", "All Vehicles", "All Devices", "Install Period", "Site Period", "Dept Period" } });

    [HttpPost("reports/one-vehicle")]
    public async Task<ActionResult<TrackingReportDto>> GetReportOneVehicle([FromBody] TrackingOneVehicleRequestDto request)
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => item.vmf_code == request.VmfCode)
            .Where(item => IsWithinInclusiveDateRange(item.install_date, request.StartDate, request.EndDate))
            .OrderByDescending(item => item.install_date)
            .Cast<object>()
            .ToList();
        return Ok(new TrackingReportDto { ReportType = "OneVehicle", Data = data });
    }

    [HttpPost("reports/one-device")]
    public async Task<ActionResult<TrackingReportDto>> GetReportOneDevice([FromBody] TrackingOneDeviceRequestDto request)
    {
        var device = request.DeviceId?.Trim() ?? string.Empty;
        var data = (await GetLiveItemsAsync())
            .Where(item => !string.IsNullOrWhiteSpace(device) &&
                           string.Equals(item.track_num, device, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.install_date)
            .Cast<object>()
            .ToList();
        return Ok(new TrackingReportDto { ReportType = "OneDevice", Data = data });
    }

    [HttpPost("reports/all-vehicles")]
    public async Task<ActionResult<TrackingReportDto>> GetReportAllVehicles([FromBody] TrackingAllVehiclesRequestDto request)
    {
        var query = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.install_date, request.StartDate, request.EndDate));

        if (!string.IsNullOrWhiteSpace(request.TrackerType) &&
            !string.Equals(request.TrackerType, "All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item => TrackerTypeMatches(item.track_type, request.TrackerType));
        }

        return Ok(new TrackingReportDto
        {
            ReportType = "AllVehicles",
            Data = query.OrderByDescending(item => item.install_date).Cast<object>().ToList()
        });
    }

    [HttpPost("reports/all-devices")]
    public async Task<ActionResult<TrackingReportDto>> GetReportAllDevices([FromBody] TrackingAllDevicesRequestDto request)
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.install_date, request.StartDate, request.EndDate))
            .OrderByDescending(item => item.install_date)
            .Cast<object>()
            .ToList();
        return Ok(new TrackingReportDto { ReportType = "AllDevices", Data = data });
    }

    [HttpPost("reports/install-period")]
    public async Task<ActionResult<TrackingReportDto>> GetReportInstallPeriod([FromBody] TrackingInstallPeriodRequestDto request)
    {
        var data = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.install_date, request.StartDate, request.EndDate))
            .OrderByDescending(item => item.install_date)
            .Cast<object>()
            .ToList();
        return Ok(new TrackingReportDto { ReportType = "InstallPeriod", Data = data });
    }

    [HttpPost("reports/site-period")]
    public async Task<ActionResult<TrackingReportDto>> GetReportSitePeriod([FromBody] TrackingSitePeriodRequestDto request)
    {
        var tracking = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.install_date, request.StartDate, request.EndDate))
            .ToList();

        if (!request.AllSites)
        {
            var vmfForSite = (await _vehicleRepository.GetAllAsync())
                .Where(vehicle => vehicle.veh_site_code.HasValue && vehicle.veh_site_code.Value == request.SiteCode)
                .Select(vehicle => vehicle.vmf_code)
                .ToHashSet();

            tracking = tracking
                .Where(item => item.vmf_code.HasValue && vmfForSite.Contains(item.vmf_code.Value))
                .ToList();
        }

        return Ok(new TrackingReportDto
        {
            ReportType = "SitePeriod",
            Data = tracking.OrderByDescending(item => item.install_date).Cast<object>().ToList()
        });
    }

    [HttpPost("reports/dept-period")]
    public async Task<ActionResult<TrackingReportDto>> GetReportDeptPeriod([FromBody] TrackingDeptPeriodRequestDto request)
    {
        var tracking = (await GetLiveItemsAsync())
            .Where(item => IsWithinInclusiveDateRange(item.install_date, request.StartDate, request.EndDate))
            .ToList();

        if (!request.AllDepartments)
        {
            var departmentSiteCodes = (await _siteRepository.GetActiveSitesAsync())
                .Where(site => site.Depatrment_code.HasValue && site.Depatrment_code.Value == request.DepartmentCode)
                .Select(site => site.Site_code)
                .ToHashSet();

            var vmfForDepartment = (await _vehicleRepository.GetAllAsync())
                .Where(vehicle => vehicle.veh_site_code.HasValue && departmentSiteCodes.Contains(vehicle.veh_site_code.Value))
                .Select(vehicle => vehicle.vmf_code)
                .ToHashSet();

            tracking = tracking
                .Where(item => item.vmf_code.HasValue && vmfForDepartment.Contains(item.vmf_code.Value))
                .ToList();
        }

        return Ok(new TrackingReportDto
        {
            ReportType = "DeptPeriod",
            Data = tracking.OrderByDescending(item => item.install_date).Cast<object>().ToList()
        });
    }

    #endregion

    private async Task<List<Tracking>> GetLiveItemsAsync()
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

    private static bool TrackerTypeMatches(string? currentType, string requestedType)
    {
        var normalizedRequest = requestedType.Trim();
        if (normalizedRequest.Equals("Reused", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(currentType, "Reused", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentType, "Re-used", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(currentType, normalizedRequest, StringComparison.OrdinalIgnoreCase);
    }
}

#region Tracking DTOs
public class TrackingMenuDto { public List<string> Options { get; set; } = new(); }
public class TrackingVehicleLookupDto { public bool Found { get; set; } public string Message { get; set; } = ""; public int? VmfCode { get; set; } }
public class TrackingReportMenuDto { public List<string> Reports { get; set; } = new(); }
public class TrackingOneVehicleRequestDto { public int VmfCode { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingOneDeviceRequestDto { public string DeviceId { get; set; } = ""; }
public class TrackingAllVehiclesRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public string? TrackerType { get; set; } }
public class TrackingAllDevicesRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingInstallPeriodRequestDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingSitePeriodRequestDto { public int SiteCode { get; set; } public bool AllSites { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingDeptPeriodRequestDto { public int DepartmentCode { get; set; } public bool AllDepartments { get; set; } public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class TrackingReportDto { public string ReportType { get; set; } = ""; public List<object> Data { get; set; } = new(); }
#endregion
