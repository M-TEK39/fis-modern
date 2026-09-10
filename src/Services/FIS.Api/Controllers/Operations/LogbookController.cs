using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LogbookController : BaseApiController
{
    private readonly ILogbookRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ILogger<LogbookController> _logger;

    public LogbookController(
        ILogbookRepository repository,
        IVehicleRepository vehicleRepository,
        ILogger<LogbookController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Logbook>>> GetAll()
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

    [HttpGet("{id}")]
    public async Task<ActionResult<Logbook>> GetById(short id)
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

    [HttpPost]
    public async Task<ActionResult<Logbook>> Create([FromBody] Logbook item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.logbookcode }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Logbook>> Update(short id, [FromBody] Logbook item)
    {
        try
        {
            if (id != item.logbookcode)
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

    /// <summary>
    /// Get logbook menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<LogbookMenuDto> GetMenu()
    {
        var menu = new LogbookMenuDto
        {
            Options = new List<string> { "Maintenance", "Collection", "Delete", "Reports", "Help" },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Get logbook help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<LogbookHelpDto> GetHelp()
    {
        var help = new LogbookHelpDto
        {
            Title = "Logbook Management Help",
            Description = "Manage vehicle logbooks, track collection and returns",
        };
        return Ok(help);
    }

    /// <summary>
    /// Search for vehicle by fleet number or registration
    /// </summary>
    [HttpGet("vehicle-search")]
    public async Task<ActionResult<VehicleLookupDto>> SearchVehicle([FromQuery] string identifier)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return BadRequest(new { message = "Vehicle identifier is required" });

            // Try to find vehicle by fleet number first
            var vehicle = await _vehicleRepository.GetByFleetNumberAsync(identifier);

            // If not found, try registration number
            if (vehicle == null)
                vehicle = await _vehicleRepository.GetByRegistrationNumberAsync(identifier);

            var result = new VehicleLookupDto
            {
                Found = vehicle != null,
                Message =
                    vehicle != null
                        ? $"Vehicle found: {vehicle.fleet_number}"
                        : "Vehicle not found",
                VmfCode = vehicle?.vmf_code,
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for vehicle: {Identifier}", identifier);
            return StatusCode(500, "Error searching for vehicle");
        }
    }

    /// <summary>
    /// Process logbook collection
    /// </summary>
    [HttpPost("collection")]
    public async Task<ActionResult<LogbookCollectionResultDto>> ProcessCollection(
        [FromBody] LogbookCollectionDto request
    )
    {
        try
        {
            // Create new logbook record for the collection
            var logbook = new Logbook
            {
                vmf_code = request.VmfCode,
                handout_date = request.CollectionDate,
                lb_receiver_name = request.ReceiverName,
                lb_tel_num = request.TelephoneNumber,
                begin_num = request.BeginNumber,
                end_num = request.EndNumber,
                site_code = request.SiteCode,
                lb_comment = request.Comments,
            };

            var created = await _repository.CreateAsync(logbook, GetCurrentUserId());

            var result = new LogbookCollectionResultDto
            {
                Success = true,
                Message = "Logbook collection processed successfully",
                LogbookCode = created.logbookcode,
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing logbook collection");
            return StatusCode(500, "Error processing logbook collection");
        }
    }

    /// <summary>
    /// Delete/return a handed out logbook
    /// </summary>
    [HttpDelete("handout/{id}")]
    public async Task<ActionResult> DeleteHandout(short id)
    {
        try
        {
            var logbook = await _repository.GetByIdAsync(id);
            if (logbook == null)
                return NotFound(new { message = $"Logbook with code {id} not found" });

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return Ok(new { message = "Logbook handout deleted/returned successfully", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logbook handout: {Id}", id);
            return StatusCode(500, "Error deleting logbook handout");
        }
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get logbook reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<ReportMenuDto> GetReportsMenu()
    {
        var menu = new ReportMenuDto
        {
            Reports = new List<string> { "One Number", "Department Period" },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate logbook report by number
    /// </summary>
    [HttpPost("reports/one-number")]
    public async Task<ActionResult<LogbookReportDto>> GetReportByNumber(
        [FromBody] LogbookOneNumberRequestDto request
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LogbookNumber))
                return BadRequest(new { message = "Logbook number is required" });

            var allLogbooks = await _repository.GetAllAsync();
            var matchingLogbooks = allLogbooks
                .Where(l => !l.is_deleted)
                .Where(l =>
                    (
                        l.begin_num != null
                        && l.begin_num.Contains(
                            request.LogbookNumber,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    || (
                        l.end_num != null
                        && l.end_num.Contains(
                            request.LogbookNumber,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                .OrderByDescending(l => l.handout_date)
                .ToList();

            var report = new LogbookReportDto
            {
                ReportType = "OneNumber",
                Data = matchingLogbooks.Cast<object>().ToList(),
                RecordCount = matchingLogbooks.Count,
                GeneratedDate = DateTime.UtcNow,
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating logbook report by number: {LogbookNumber}",
                request.LogbookNumber
            );
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate logbook report by department and period
    /// </summary>
    [HttpPost("reports/department-period")]
    public async Task<ActionResult<LogbookReportDto>> GetReportByDepartmentPeriod(
        [FromBody] DepartmentPeriodRequestDto request
    )
    {
        try
        {
            // Get logbooks by site (which represents department)
            var siteLogbooks = await _repository.GetBySiteAsync((short)request.DepartmentCode);

            // Filter by date range
            var filteredLogbooks = siteLogbooks
                .Where(l => !l.is_deleted)
                .Where(l =>
                    l.handout_date.HasValue
                    && l.handout_date.Value >= request.StartDate
                    && l.handout_date.Value <= request.EndDate
                )
                .OrderByDescending(l => l.handout_date)
                .ToList();

            var report = new LogbookReportDto
            {
                ReportType = "DepartmentPeriod",
                Data = filteredLogbooks.Cast<object>().ToList(),
                RecordCount = filteredLogbooks.Count,
                GeneratedDate = DateTime.UtcNow,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating logbook report by department period: Department={DepartmentCode}, Start={StartDate}, End={EndDate}",
                request.DepartmentCode,
                request.StartDate,
                request.EndDate
            );
            return StatusCode(500, "Error generating report");
        }
    }

    #endregion
}

#region Logbook DTOs
public class LogbookMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class LogbookHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public class VehicleLookupDto
{
    public bool Found { get; set; }
    public string Message { get; set; } = "";
    public int? VmfCode { get; set; }
}

public class LogbookCollectionDto
{
    public short LogbookCode { get; set; }
    public int VmfCode { get; set; }
    public DateTime CollectionDate { get; set; }
    public string? ReceiverName { get; set; }
    public string? TelephoneNumber { get; set; }
    public string? BeginNumber { get; set; }
    public string? EndNumber { get; set; }
    public short? SiteCode { get; set; }
    public string? Comments { get; set; }
}

public class LogbookCollectionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public short? LogbookCode { get; set; }
}

public class ReportMenuDto
{
    public List<string> Reports { get; set; } = new();
}

public class LogbookOneNumberRequestDto
{
    public string LogbookNumber { get; set; } = "";
}

public class DepartmentPeriodRequestDto
{
    public int DepartmentCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class LogbookReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public int RecordCount { get; set; }
    public DateTime GeneratedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
#endregion
