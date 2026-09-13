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
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly ITrackingRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<TrackingController> _logger;

    public TrackingController(
        ITrackingRepository repository,
        IVehicleRepository vehicleRepository,
        ILogger<TrackingController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetAll()
    {
        try
        {
            return Ok(await _repository.GetAllAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? vmfCode = null
    )
    {
        try
        {
            var result = await _repository.GetPageAsync(
                new TrackingPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    search,
                    vmfCode is > 0 ? vmfCode : null
                )
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
            _logger.LogError(ex, "Error retrieving paged tracking records");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tracking>> GetById(short id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetByVehicle(int vmfCode)
    {
        try
        {
            return Ok(await _repository.GetByVehicleAsync(vmfCode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetActive()
    {
        try
        {
            return Ok(await _repository.GetActiveTrackingAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Tracking>> Create([FromBody] Tracking item)
    {
        var validation = Validate(item);
        if (validation is not null)
            return BadRequest(validation);

        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.track_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Tracking>> Update(short id, [FromBody] Tracking item)
    {
        if (item is null)
            return BadRequest("Tracking data is required.");
        if (id != item.track_code)
            return BadRequest("Tracking code does not match the route.");
        var validation = Validate(item);
        if (validation is not null)
            return BadRequest(validation);

        try
        {
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
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
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    #region Specialized Operations

    [HttpGet("menu")]
    public ActionResult<TrackingMenuDto> GetMenu() =>
        Ok(
            new TrackingMenuDto
            {
                Options = new List<string> { "Maintenance", "Reports", "Help" },
            }
        );

    [HttpGet("vehicle-search")]
    public async Task<ActionResult<TrackingVehicleLookupDto>> SearchVehicle(
        [FromQuery] string identifier
    )
    {
        try
        {
            var query = identifier?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(
                    new TrackingVehicleLookupDto
                    {
                        Found = false,
                        Message = "Vehicle identifier is required.",
                    }
                );
            }

            var vehicles = await _vehicleRepository.SearchVehiclesAsync(query);
            var best = vehicles.FirstOrDefault();

            if (best == null)
            {
                return Ok(
                    new TrackingVehicleLookupDto
                    {
                        Found = false,
                        Message = $"No vehicle found for '{query}'.",
                    }
                );
            }

            return Ok(
                new TrackingVehicleLookupDto
                {
                    Found = true,
                    Message = "Vehicle found.",
                    VmfCode = best.vmf_code,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching vehicle for tracking identifier {Identifier}",
                identifier
            );
            return StatusCode(500);
        }
    }

    #endregion

    private static string? Validate(Tracking? item)
    {
        if (item is null)
            return "Tracking data is required.";
        if (item.vmf_code is <= 0)
            return "Vehicle code must be positive when supplied.";
        if (item.track_num?.Length > 50)
            return "Tracker number must be 50 characters or fewer.";
        if (item.gg_previous?.Length > 50)
            return "Previous GG must be 50 characters or fewer.";
        if (item.gg_follow?.Length > 50)
            return "Follow GG must be 50 characters or fewer.";
        if (item.track_status?.Length > 100)
            return "Tracker status must be 100 characters or fewer.";
        if (item.track_type?.Length > 100)
            return "Tracker type must be 100 characters or fewer.";
        return item.track_note?.Length > 4000
            ? "Tracking notes must be 4000 characters or fewer."
            : null;
    }

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<TrackingReportMenuDto> GetReportsMenu() =>
        Ok(
            new TrackingReportMenuDto
            {
                Reports = new List<string>
                {
                    "One Vehicle",
                    "One Device",
                    "All Vehicles",
                    "All Devices",
                    "Install Period",
                    "Site Period",
                    "Dept Period",
                },
            }
        );

    [HttpPost("reports/one-vehicle")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportOneVehicle(
        [FromBody] TrackingOneVehicleRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.OneVehicle,
                request,
                vmfCode: request.VmfCode,
                startDate: request.StartDate,
                endDate: request.EndDate
            )
        );
        return Ok(CreateReportPage("OneVehicle", result));
    }

    [HttpPost("reports/one-device")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportOneDevice(
        [FromBody] TrackingOneDeviceRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.OneDevice,
                request,
                deviceId: request.DeviceId
            )
        );
        return Ok(CreateReportPage("OneDevice", result));
    }

    [HttpPost("reports/all-vehicles")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportAllVehicles(
        [FromBody] TrackingAllVehiclesRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.AllVehicles,
                request,
                startDate: request.StartDate,
                endDate: request.EndDate,
                trackerType: request.TrackerType
            )
        );
        return Ok(CreateReportPage("AllVehicles", result));
    }

    [HttpPost("reports/all-devices")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportAllDevices(
        [FromBody] TrackingAllDevicesRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.AllDevices,
                request,
                startDate: request.StartDate,
                endDate: request.EndDate
            )
        );
        return Ok(CreateReportPage("AllDevices", result));
    }

    [HttpPost("reports/install-period")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportInstallPeriod(
        [FromBody] TrackingInstallPeriodRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.InstallPeriod,
                request,
                startDate: request.StartDate,
                endDate: request.EndDate
            )
        );
        return Ok(CreateReportPage("InstallPeriod", result));
    }

    [HttpPost("reports/site-period")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportSitePeriod(
        [FromBody] TrackingSitePeriodRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.SitePeriod,
                request,
                startDate: request.StartDate,
                endDate: request.EndDate,
                siteCode: request.SiteCode,
                allSites: request.AllSites
            )
        );
        return Ok(CreateReportPage("SitePeriod", result));
    }

    [HttpPost("reports/dept-period")]
    public async Task<ActionResult<TrackingReportPageDto>> GetReportDeptPeriod(
        [FromBody] TrackingDeptPeriodRequestDto request
    )
    {
        var result = await _repository.GetReportPageAsync(
            CreateReportPageQuery(
                TrackingReportKind.DepartmentPeriod,
                request,
                startDate: request.StartDate,
                endDate: request.EndDate,
                departmentCode: request.DepartmentCode,
                allDepartments: request.AllDepartments
            )
        );
        return Ok(CreateReportPage("DeptPeriod", result));
    }

    #endregion

    private static TrackingReportPageQuery CreateReportPageQuery(
        TrackingReportKind reportKind,
        TrackingReportPageRequestDto request,
        int? vmfCode = null,
        string? deviceId = null,
        DateTime startDate = default,
        DateTime endDate = default,
        string? trackerType = null,
        int? siteCode = null,
        int? departmentCode = null,
        bool allSites = false,
        bool allDepartments = false
    ) =>
        new(
            reportKind,
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, MaximumPageSize),
            vmfCode,
            deviceId,
            startDate,
            endDate,
            trackerType,
            siteCode,
            departmentCode,
            allSites,
            allDepartments
        );

    private static TrackingReportPageDto CreateReportPage(
        string reportType,
        TrackingPage result
    ) =>
        new()
        {
            ReportType = reportType,
            Items = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
        };
}

#region Tracking DTOs
public class TrackingMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class TrackingVehicleLookupDto
{
    public bool Found { get; set; }
    public string Message { get; set; } = "";
    public int? VmfCode { get; set; }
}

public class TrackingReportMenuDto
{
    public List<string> Reports { get; set; } = new();
}

public class TrackingReportPageRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class TrackingOneVehicleRequestDto : TrackingReportPageRequestDto
{
    public int VmfCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingOneDeviceRequestDto : TrackingReportPageRequestDto
{
    public string DeviceId { get; set; } = "";
}

public class TrackingAllVehiclesRequestDto : TrackingReportPageRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? TrackerType { get; set; }
}

public class TrackingAllDevicesRequestDto : TrackingReportPageRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingInstallPeriodRequestDto : TrackingReportPageRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingSitePeriodRequestDto : TrackingReportPageRequestDto
{
    public int SiteCode { get; set; }
    public bool AllSites { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingDeptPeriodRequestDto : TrackingReportPageRequestDto
{
    public int DepartmentCode { get; set; }
    public bool AllDepartments { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TrackingReportPageDto
{
    public string ReportType { get; set; } = "";
    public IReadOnlyList<Tracking> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}
#endregion
