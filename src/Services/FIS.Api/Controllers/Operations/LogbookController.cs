using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LogbookController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly ILogbookRepository _repository;
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<LogbookController> _logger;

    public LogbookController(
        ILogbookRepository repository,
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        ISiteRepository siteRepository,
        FisDbContext context,
        ILogger<LogbookController> logger
    )
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _vehicleRepository = vehicleRepository;
        _siteRepository = siteRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<IEnumerable<Logbook>>> GetAll()
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var records = await _repository.GetAllAsync();
            return Ok(FilterByAllowedSites(records, allowedSites).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult> GetPage(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] int? vmfCode = null
    )
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            if (allowedSites is not null)
            {
                var all = FilterByAllowedSites(await _repository.GetAllAsync(), allowedSites)
                    .Where(item => !vmfCode.HasValue || item.vmf_code == vmfCode)
                    .Where(item =>
                        string.IsNullOrWhiteSpace(search)
                        || string.Join(
                            " ",
                            item.begin_num,
                            item.end_num,
                            item.lb_receiver_name,
                            item.lb_comment,
                            item.Vehicle?.fleet_number,
                            item.Vehicle?.registration_number,
                            item.Site?.description
                        ).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderByDescending(item => item.handout_date)
                    .ThenByDescending(item => item.logbookcode)
                    .ToList();
                var boundedPage = Math.Min(Math.Max(1, page), Math.Max(1, (int)Math.Ceiling(all.Count / (double)Math.Clamp(pageSize, 1, MaximumPageSize))));
                var boundedSize = Math.Clamp(pageSize, 1, MaximumPageSize);
                return Ok(new
                {
                    items = all.Skip((boundedPage - 1) * boundedSize).Take(boundedSize).ToList(),
                    page = boundedPage,
                    pageSize = boundedSize,
                    total = all.Count,
                    totalPages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)boundedSize)),
                });
            }

            var result = await _repository.GetPageAsync(
                new LogbookPageQuery(
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
            _logger.LogError(ex, "Error paging logbooks");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<Logbook>> GetById(short id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item == null)
                return NotFound();
            if (!await IsSiteAllowedAsync(item.site_code))
                return Forbid();
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<Logbook>> Create([FromBody] Logbook item)
    {
        try
        {
            if (ValidateLogbookMutation(item) is { } validationError)
                return BadRequest(new { message = validationError });
            var vmfCode = item!.vmf_code!.Value;
            var siteCode = item.site_code!.Value;
            if (await _vehicleRepository.GetByIdAsync(vmfCode) is null)
                return BadRequest(new { message = "The selected vehicle was not found." });
            if (!await IsVehicleAllowedAsync(vmfCode))
                return Forbid();
            if (await _siteRepository.GetByIdAsync(siteCode) is null)
                return BadRequest(new { message = "The selected site was not found." });
            if (!await IsSiteAllowedAsync(item.site_code))
                return Forbid();
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
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<Logbook>> Update(short id, [FromBody] Logbook item)
    {
        try
        {
            if (item is null || id != item.logbookcode)
                return BadRequest();
            if (ValidateLogbookMutation(item) is { } validationError)
                return BadRequest(new { message = validationError });
            var vmfCode = item.vmf_code!.Value;
            var siteCode = item.site_code!.Value;
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound();
            if (await _vehicleRepository.GetByIdAsync(vmfCode) is null)
                return BadRequest(new { message = "The selected vehicle was not found." });
            if (!await IsVehicleAllowedAsync(vmfCode))
                return Forbid();
            if (await _siteRepository.GetByIdAsync(siteCode) is null)
                return BadRequest(new { message = "The selected site was not found." });
            if (!await IsSiteAllowedAsync(existing.site_code) || !await IsSiteAllowedAsync(item.site_code))
                return Forbid();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult> Delete(short id)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound();
            if (!await IsSiteAllowedAsync(existing.site_code))
                return Forbid();
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
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
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
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
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
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
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

            if (vehicle != null)
            {
                if (!await IsVehicleAllowedAsync(vehicle.vmf_code))
                    vehicle = null;
            }

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

    [HttpGet("vehicle-options")]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<IEnumerable<LogbookVehicleOptionDto>>> GetVehicleOptions()
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var vehicles = await _vehicleRepository.GetActiveVehiclesAsync();
            if (allowedSites is null)
            {
                return Ok(
                    vehicles.Select(vehicle => new LogbookVehicleOptionDto
                    {
                        VmfCode = vehicle.vmf_code,
                        FleetNumber = vehicle.fleet_number,
                        RegistrationNumber = vehicle.registration_number,
                    })
                );
            }

            var activeContracts = await _contractRepository.GetActiveContractsAsync();
            var allowedContractVehicles = activeContracts
                .Where(contract => allowedSites.Contains(contract.site_code))
                .Select(contract => contract.vmf_code)
                .ToHashSet();
            var allowedHistoricalVehicles = (await _repository.GetAllAsync())
                .Where(logbook => logbook.site_code.HasValue && allowedSites.Contains(logbook.site_code.Value))
                .Select(logbook => logbook.vmf_code)
                .Where(vmfCode => vmfCode.HasValue)
                .Select(vmfCode => vmfCode!.Value)
                .ToHashSet();

            return Ok(
                vehicles
                    .Where(vehicle =>
                        allowedContractVehicles.Contains(vehicle.vmf_code)
                        || allowedHistoricalVehicles.Contains(vehicle.vmf_code)
                    )
                    .Select(vehicle => new LogbookVehicleOptionDto
                    {
                        VmfCode = vehicle.vmf_code,
                        FleetNumber = vehicle.fleet_number,
                        RegistrationNumber = vehicle.registration_number,
                    })
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scoped logbook vehicle options");
            return StatusCode(500, "Error loading logbook vehicle options");
        }
    }

    /// <summary>
    /// Process logbook collection
    /// </summary>
    [HttpPost("collection")]
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<LogbookCollectionResultDto>> ProcessCollection(
        [FromBody] LogbookCollectionDto request
    )
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "Collection data is required." });
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

            if (ValidateLogbookMutation(logbook) is { } validationError)
                return BadRequest(new { message = validationError });
            if (!await IsSiteAllowedAsync(logbook.site_code))
                return Forbid();
            if (await _vehicleRepository.GetByIdAsync(request.VmfCode) is null)
                return BadRequest(new { error = "The selected vehicle was not found." });
            if (!await IsVehicleAllowedAsync(request.VmfCode))
                return Forbid();

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
    [Authorize(Roles = "Logbooks,SystemAdministrator,System Administrator")]
    public async Task<ActionResult> DeleteHandout(short id)
    {
        try
        {
            var logbook = await _repository.GetByIdAsync(id);
            if (logbook == null)
                return NotFound(new { message = $"Logbook with code {id} not found" });
            if (!await IsSiteAllowedAsync(logbook.site_code))
                return Forbid();

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
    [Authorize(Roles = "Reports,SystemAdministrator,System Administrator")]
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
    [Authorize(Roles = "Reports,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<LogbookReportDto>> GetReportByNumber(
        [FromBody] LogbookOneNumberRequestDto request
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LogbookNumber))
                return BadRequest(new { message = "Logbook number is required" });

            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var allLogbooks = await _repository.GetAllAsync();
            var matchingLogbooks = allLogbooks
                .Where(l => allowedSites is null || (l.site_code.HasValue && allowedSites.Contains(l.site_code.Value)))
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
    [Authorize(Roles = "Reports,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<LogbookReportDto>> GetReportByDepartmentPeriod(
        [FromBody] DepartmentPeriodRequestDto request
    )
    {
        try
        {
            if (request.DepartmentCode <= 0)
                return BadRequest(new { message = "A valid department code is required." });
            if (request.StartDate > request.EndDate)
                return BadRequest(new { message = "Start date cannot be after end date." });

            // Legacy RPT_dept_period_report_Logbk.aspx filters the logbook's
            // site through site.Depatrment_code. A department code is not a
            // site code; querying GetBySiteAsync(department) silently omitted
            // most of the department's logbooks.
            var requestedDepartment = request.DepartmentCode.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            );
            var departmentSiteCodes = (await _siteRepository.GetActiveSitesAsync())
                .Where(site =>
                    site.Depatrment_code == request.DepartmentCode
                    || string.Equals(
                        site.Department_number?.Trim(),
                        requestedDepartment,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .Select(site => site.Site_code)
                .ToHashSet();
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var siteLogbooks = await _repository.GetAllAsync();

            // Filter by date range
            var filteredLogbooks = siteLogbooks
                .Where(l => allowedSites is null || (l.site_code.HasValue && allowedSites.Contains(l.site_code.Value)))
                .Where(l => l.site_code.HasValue && departmentSiteCodes.Contains(l.site_code.Value))
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

    private async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalLogbookScope())
            return null;

        var userId = GetCurrentUserId();
        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userId)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return new HashSet<short>();

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
            return new HashSet<short>();

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (HasRole("Vehicle List for All Departments in Province") && profileSite.province_code.HasValue)
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        else if (HasRole("Vehicle List for All Sites in Department") && profileSite.Depatrment_code.HasValue)
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        else
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);

        return sites.Select(site => site.Site_code).ToHashSet();
    }

    private async Task<bool> IsSiteAllowedAsync(short? siteCode)
    {
        var allowed = await ResolveAllowedSiteCodesAsync();
        return allowed is null || (siteCode.HasValue && allowed.Contains(siteCode.Value));
    }

    private async Task<bool> IsVehicleAllowedAsync(int vmfCode)
    {
        var allowed = await ResolveAllowedSiteCodesAsync();
        if (allowed is null)
            return true;

        var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(vmfCode);
        if (activeContract is not null && allowed.Contains(activeContract.site_code))
            return true;

        return (await _repository.GetByVehicleAsync(vmfCode))
            .Any(logbook => logbook.site_code.HasValue && allowed.Contains(logbook.site_code.Value));
    }

    private static IEnumerable<Logbook> FilterByAllowedSites(
        IEnumerable<Logbook> records,
        IReadOnlySet<short>? allowedSites
    ) => allowedSites is null
        ? records
        : records.Where(item => item.site_code.HasValue && allowedSites.Contains(item.site_code.Value));

    private static string? ValidateLogbookMutation(Logbook? item)
    {
        if (item is null || item.vmf_code is not > 0)
            return "A valid vehicle is required.";
        if (!item.handout_date.HasValue || item.handout_date.Value.Year < 1900)
            return "The handout date is required.";
        if (item.site_code is not > 0)
            return "A valid site is required.";

        return FirstLengthError(
            ("Begin number", item.begin_num, 8),
            ("End number", item.end_num, 8),
            ("Receiver name", item.lb_receiver_name, 25),
            ("Receiver telephone", item.lb_tel_num, 20),
            ("Comment", item.lb_comment, 60)
        );
    }

    private static string? FirstLengthError(
        params (string Name, string? Value, int Maximum)[] values
    )
    {
        foreach (var (name, value, maximum) in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maximum)
                return $"{name} cannot exceed {maximum} characters.";
        }

        return null;
    }

    private bool HasGlobalLogbookScope() =>
        HasRole("Administrator") || HasRole("Admin") || HasRole("System Administrator") || HasRole("SystemAdministrator");

    private bool HasRole(string role) => User.Claims
        .Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase))
        .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        .Any(value => string.Equals(value.Trim(), role, StringComparison.OrdinalIgnoreCase));

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

public sealed class LogbookVehicleOptionDto
{
    public int VmfCode { get; set; }
    public string? FleetNumber { get; set; }
    public string? RegistrationNumber { get; set; }
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
