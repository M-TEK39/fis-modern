using System.Security.Claims;
using FIS.Api.Services;
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
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const int MaximumReportPageSize = 100;

    private readonly ITowingRepository _repository;
    private readonly TowTruckCompatibilityService _towTruckService;
    private readonly ILogger<TowingController> _logger;

    public TowingController(
        ITowingRepository repository,
        TowTruckCompatibilityService towTruckService,
        ILogger<TowingController> logger
    )
    {
        _repository = repository;
        _towTruckService = towTruckService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Towing>>> GetAll()
    {
        if (!HasTowingRole())
            return Forbid();
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
        [FromQuery] int[]? vmfCode = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasTowingRole())
            return Forbid();

        try
        {
            var result = await _repository.GetPageAsync(
                new TowingPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    vmfCode
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
            _logger.LogError(ex, "Error retrieving paged towing requests");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Towing>> GetById(short id)
    {
        if (!HasTowingRole())
            return Forbid();
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

    [HttpPost]
    public async Task<ActionResult<Towing>> Create([FromBody] Towing item)
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Towing_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Towing>> Update(short id, [FromBody] Towing item)
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            if (id != item.Towing_code)
                return BadRequest();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
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
        if (!HasTowingRole())
            return Forbid();
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

    [HttpGet("tow-trucks")]
    [Authorize(Roles = "Towing,Call Centre")]
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

    [HttpGet("tow-trucks/page")]
    public async Task<ActionResult> GetTowTruckPage(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasTowingRole())
            return Forbid();

        try
        {
            var result = await _towTruckService.GetPageAsync(
                search,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, MaximumPageSize),
                HttpContext.RequestAborted
            );

            return Ok(
                new
                {
                    items = result.Items,
                    total = result.Total,
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged tow truck options");
            return StatusCode(500, "Error retrieving paged tow truck options");
        }
    }

    [HttpGet("tow-trucks/{id}")]
    public async Task<ActionResult<TowTruckOption>> GetTowTruck(short id)
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            var towTruck = await _towTruckService.GetByIdAsync(id, HttpContext.RequestAborted);
            return towTruck == null ? NotFound() : Ok(towTruck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tow truck {TowCode}", id);
            return StatusCode(500);
        }
    }

    [HttpPost("tow-trucks")]
    public async Task<ActionResult<TowTruckOption>> CreateTowTruck(
        [FromBody] TowTruckRequestDto request
    )
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            var towTruck = await _towTruckService.CreateAsync(
                request.ToRequest(),
                GetCurrentUserId(),
                HttpContext.RequestAborted
            );
            return Ok(towTruck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tow truck");
            return StatusCode(500);
        }
    }

    [HttpPut("tow-trucks/{id}")]
    public async Task<ActionResult<TowTruckOption>> UpdateTowTruck(
        short id,
        [FromBody] TowTruckRequestDto request
    )
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            var towTruck = await _towTruckService.UpdateAsync(
                id,
                request.ToRequest(),
                GetCurrentUserId(),
                HttpContext.RequestAborted
            );
            return towTruck == null ? NotFound() : Ok(towTruck);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tow truck {TowCode}", id);
            return StatusCode(500);
        }
    }

    [HttpDelete("tow-trucks/{id}")]
    public async Task<ActionResult> DeleteTowTruck(short id)
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            return await _towTruckService.DeleteAsync(
                id,
                GetCurrentUserId(),
                HttpContext.RequestAborted
            )
                ? NoContent()
                : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tow truck {TowCode}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("menu")]
    public ActionResult<TowingMenuDto> GetMenu() =>
        HasTowingRole()
            ? Ok(
                new TowingMenuDto
                {
                    Options = new List<string> { "Request", "Towtruck Data", "Reports", "Help" },
                }
            )
            : Forbid();

    [HttpPost("request")]
    public async Task<ActionResult<TowingRequestResultDto>> CreateRequest(
        [FromBody] TowingRequestDto request
    )
    {
        if (!HasTowingRole())
            return Forbid();
        try
        {
            var item = new Towing
            {
                vmf_code = request.VmfCode,
                Tow_request_date =
                    request.RequestDate == default ? DateTime.Today : request.RequestDate.Date,
                Tow_request_time =
                    request.RequestDate == default ? DateTime.Now : request.RequestDate,
                Tow_location_start = request.Location?.Trim(),
                Vehicle_problem = request.Reason?.Trim(),
                Site_code = request.SiteCode,
                Keys = request.Keys?.Trim(),
            };

            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return Ok(
                new TowingRequestResultDto
                {
                    Success = true,
                    TowingCode = created.Towing_code,
                    Message = "Towing request created",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating towing request");
            return StatusCode(
                500,
                new TowingRequestResultDto
                {
                    Success = false,
                    Message = "Failed to create towing request",
                }
            );
        }
    }

    [HttpPut("towtruck/{id}")]
    public async Task<ActionResult<TowtruckUpdateResultDto>> UpdateTowtruck(
        short id,
        [FromBody] TowtruckDataDto request
    )
    {
        if (!HasTowingRole())
            return Forbid();

        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null || existing.is_deleted)
            {
                return NotFound(
                    new TowtruckUpdateResultDto
                    {
                        Success = false,
                        TowingCode = id,
                        Message = "Towing request not found",
                    }
                );
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
            return Ok(
                new TowtruckUpdateResultDto
                {
                    Success = true,
                    TowingCode = id,
                    Message = "Towtruck data updated",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating towtruck data for towing {TowingCode}", id);
            return StatusCode(
                500,
                new TowtruckUpdateResultDto
                {
                    Success = false,
                    TowingCode = id,
                    Message = "Failed to update towtruck data",
                }
            );
        }
    }

    #endregion

    #region Reports

    [HttpGet("reports/menu")]
    public ActionResult<TowingReportMenuDto> GetReportsMenu() =>
        HasReportsRole()
            ? Ok(
                new TowingReportMenuDto
                {
                    Reports = new List<string>
                    {
                        "Request Report",
                        "All Towtrucks",
                        "Firm/Date Report",
                    },
                }
            )
            : Forbid();

    [HttpPost("reports/request")]
    public async Task<ActionResult<TowingReportDto>> GetReportRequest(
        [FromBody] TowingRequestReportDto request
    )
    {
        if (!HasReportsRole())
            return Forbid();
        var result = await _repository.GetReportPageAsync(
            new TowingReportPageQuery(
                TowingReportKind.Request,
                request.StartDate,
                request.EndDate,
                CallReference: request.CallReference,
                Page: Math.Max(1, request.Page),
                PageSize: Math.Clamp(request.PageSize, 1, MaximumReportPageSize)
            )
        );

        return Ok(CreateReportDto("Request", result));
    }

    [HttpGet("reports/towtruck/all")]
    public async Task<ActionResult<TowingReportDto>> GetReportAllTowtrucks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasReportsRole())
            return Forbid();
        var result = await _repository.GetReportPageAsync(
            new TowingReportPageQuery(
                TowingReportKind.AllTowtrucks,
                Page: Math.Max(1, page),
                PageSize: Math.Clamp(pageSize, 1, MaximumReportPageSize)
            )
        );

        return Ok(CreateReportDto("AllTowtrucks", result));
    }

    [HttpPost("reports/firm-date")]
    public async Task<ActionResult<TowingReportDto>> GetReportFirmDate(
        [FromBody] TowingFirmDateReportDto request
    )
    {
        if (!HasReportsRole())
            return Forbid();
        var result = await _repository.GetReportPageAsync(
            new TowingReportPageQuery(
                TowingReportKind.FirmDate,
                request.StartDate,
                request.EndDate,
                request.FirmName,
                Page: Math.Max(1, request.Page),
                PageSize: Math.Clamp(request.PageSize, 1, MaximumReportPageSize)
            )
        );

        return Ok(CreateReportDto("FirmDate", result));
    }

    #endregion

    private static TowingReportDto CreateReportDto(string reportType, TowingReportPage result) =>
        new()
        {
            ReportType = reportType,
            Data = result.Items.Cast<object>().ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
        };

    private bool HasTowingRole() => HasAnyRole("Towing");

    private bool HasReportsRole() => HasAnyRole("Reports");

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );

        return roleClaims.Any(role =>
            expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

}

#region Towing DTOs
public class TowingMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class TowingRequestDto
{
    public int VmfCode { get; set; }
    public DateTime RequestDate { get; set; }
    public string Location { get; set; } = "";
    public string Reason { get; set; } = "";
    public short? SiteCode { get; set; }
    public string? Keys { get; set; }
}

public class TowingRequestResultDto
{
    public bool Success { get; set; }
    public short TowingCode { get; set; }
    public string Message { get; set; } = "";
}

public class TowtruckDataDto
{
    public string? Location { get; set; }
    public string? VehicleProblem { get; set; }
    public string? Keys { get; set; }
}

public class TowtruckUpdateResultDto
{
    public bool Success { get; set; }
    public short TowingCode { get; set; }
    public string Message { get; set; } = "";
}

public class TowingReportMenuDto
{
    public List<string> Reports { get; set; } = new();
}

public class TowingReportPageRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class TowingRequestReportDto : TowingReportPageRequestDto
{
    public decimal? CallReference { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TowingFirmDateReportDto : TowingReportPageRequestDto
{
    public string FirmName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class TowingReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

public class TowTruckRequestDto
{
    public string? TowArea { get; set; }
    public string? TowName { get; set; }
    public string? TowTel { get; set; }
    public string? TowFax { get; set; }

    public TowTruckRequest ToRequest() =>
        new(TowArea?.Trim(), TowName?.Trim(), TowTel?.Trim(), TowFax?.Trim());
}
#endregion
