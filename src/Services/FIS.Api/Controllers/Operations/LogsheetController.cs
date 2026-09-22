using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LogsheetController : BaseApiController
{
    // Log_menu.aspx exposes edit/delete only to these legacy operator
    // profiles. Modern editing is deliberately broader because correcting
    // daily kilometre captures is an operational requirement; destructive
    // deletion remains restricted to these profiles and administrators.
    private static readonly int[] LegacyLogsheetManagerUserCodes = [279, 47, 38];

    private readonly ILogsheetRepository _repository;
    private readonly IContractRepository _contractRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<LogsheetController> _logger;

    public LogsheetController(
        ILogsheetRepository repository,
        IContractRepository contractRepository,
        ISiteRepository siteRepository,
        FisDbContext context,
        ILogger<LogsheetController> logger
    )
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _siteRepository = siteRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Logsheet>>> GetAll()
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            return Ok(FilterByAllowedSites(await _repository.GetAllAsync(), allowedSites).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] int? vmfCode = null,
        [FromQuery] string? requisition = null
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            if (allowedSites is not null)
            {
                var boundedPageSize = Math.Clamp(pageSize, 1, 100);
                var all = FilterByAllowedSites(await _repository.GetAllAsync(), allowedSites)
                    .Where(item => !vmfCode.HasValue || item.vmf_code == vmfCode.Value)
                    .Where(item =>
                        string.IsNullOrWhiteSpace(requisition)
                        || string.Equals(
                            item.rek_num,
                            requisition.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .OrderByDescending(item => item.month)
                    .ThenByDescending(item => item.log_code)
                    .ToList();
                var totalPages = Math.Max(
                    1,
                    (int)Math.Ceiling(all.Count / (double)boundedPageSize)
                );
                var boundedPage = Math.Min(Math.Max(1, page), totalPages);
                return Ok(
                    new
                    {
                        items = all.Skip((boundedPage - 1) * boundedPageSize)
                            .Take(boundedPageSize)
                            .ToList(),
                        page = boundedPage,
                        pageSize = boundedPageSize,
                        total = all.Count,
                        totalPages,
                    }
                );
            }

            var result = await _repository.GetPageAsync(
                new LogsheetPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    vmfCode is > 0 ? vmfCode : null,
                    string.IsNullOrWhiteSpace(requisition) ? null : requisition.Trim()
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
            _logger.LogError(ex, "Error paging logsheets");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Logsheet>> GetById(int id)
    {
        if (!HasLogsheetAccess())
            return Forbid();

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

    [HttpGet("vehicle/{vmfCode}/contracts")]
    public async Task<ActionResult<IEnumerable<LogsheetContractOptionDto>>> GetVehicleContracts(
        int vmfCode
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        if (vmfCode <= 0)
            return BadRequest(new { error = "A vehicle is required." });

        try
        {
            // Log_Entry_B2.aspx lists the vehicle's historical contract
            // choices (start_date < today). When that leftover query is empty
            // it falls back to permanent-hire contracts (contract_type = 'A').
            // DEV_SEL_VehicleContracts belongs to Log_Entry_B3.aspx and is
            // not mapped onto this B2 path.
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var scoped = contracts
                .Where(contract =>
                    allowedSites is null || allowedSites.Contains(contract.site_code)
                )
                .ToList();
            var historical = scoped
                .Where(contract => contract.start_date.Date < DateTime.Today)
                .OrderByDescending(contract => contract.start_date)
                .ThenByDescending(contract => contract.contract_code)
                .ToList();
            if (historical.Count == 0)
            {
                historical = scoped
                    .Where(contract =>
                        string.Equals(contract.contract_type, "A", StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderByDescending(contract => contract.start_date)
                    .ThenByDescending(contract => contract.contract_code)
                    .ToList();
            }

            return Ok(
                historical.Select(contract =>
                    new LogsheetContractOptionDto
                    {
                        ContractCode = contract.contract_code,
                        SiteCode = contract.site_code,
                        SiteDescription = contract.Site?.description,
                        StartDate = contract.start_date,
                        EndDate = contract.end_date,
                    }
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading contracts for logsheet vehicle {VmfCode}", vmfCode);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Logsheet>> Create([FromBody] Logsheet item)
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var contractFailure = await ApplyLegacySelectedContractAsync(
                item,
                item.contract_code
            );
            if (contractFailure is not null)
                return contractFailure;
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.log_code }, created);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("legacy procedure", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy logsheet insert procedure is unavailable or incompatible");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy logsheet insert procedure is unavailable or incompatible. No direct-DML fallback was run.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet creation rule rejected the request");
            return LegacyLogsheetConflict();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy logsheet insert workflow is unavailable");
            return LegacyLogsheetUnavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Logsheet>> Update(int id, [FromBody] Logsheet item)
    {
        if (!HasLogsheetAccess() || !CanEditLegacyLogsheets())
            return Forbid();

        try
        {
            if (id != item.log_code)
                return BadRequest();
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound();
            if (!await IsSiteAllowedAsync(existing.site_code))
                return Forbid();
            if (item.vmf_code != existing.vmf_code)
            {
                return BadRequest(
                    new { error = "The vehicle for an existing logsheet cannot be changed." }
                );
            }
            var contractFailure = await ApplyLegacySelectedContractAsync(
                item,
                item.contract_code
            );
            if (contractFailure is not null)
                return contractFailure;
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet update rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy logsheet update workflow is unavailable for {LogCode}", id);
            return LegacyLogsheetUnavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!HasLogsheetAccess() || !CanDeleteLegacyLogsheets())
            return Forbid();

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
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet delete rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy logsheet delete workflow is unavailable for {LogCode}", id);
            return LegacyLogsheetUnavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    #region Specialized Operations

    /// <summary>
    /// Get logsheet menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<LogsheetMenuDto> GetMenu()
    {
        if (!HasLogsheetAccess())
            return Forbid();

        var menu = new LogsheetMenuDto
        {
            Options = new List<string> { "Enter", "Edit", "Delete", "Reports", "Help" },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Get logsheet help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<LogsheetHelpDto> GetHelp()
    {
        var help = new LogsheetHelpDto
        {
            Title = "Logsheet Management Help",
            Description = "Enter and manage vehicle logsheet entries",
        };
        return Ok(help);
    }

    /// <summary>
    /// Create new logsheet entry
    /// </summary>
    [HttpPost("entry")]
    public async Task<ActionResult<LogsheetEntryResultDto>> CreateEntry(
        [FromBody] LogsheetEntryDto request
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var validationError = ValidateEntry(request);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var logsheet = new Logsheet
            {
                vmf_code = request.VmfCode,
                start_odo = request.StartOdometer,
                end_odo = request.EndOdometer,
                month = request.Month,
                site_code = request.SiteCode,
                rek_num = request.RequisitionNumber,
                days_used = request.DaysUsed,
                bund_num = request.BundleNumber,
                contract_code = request.ContractCode,
            };

            var contractFailure = await ApplyLegacySelectedContractAsync(
                logsheet,
                request.ContractCode
            );
            if (contractFailure is not null)
                return contractFailure;

            var created = await _repository.CreateAsync(logsheet, GetCurrentUserId());

            var result = new LogsheetEntryResultDto
            {
                Success = true,
                LogCode = created.log_code,
                Message = "Logsheet entry created successfully",
            };
            return Ok(result);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("legacy procedure", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy logsheet insert procedure is unavailable or incompatible");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy logsheet insert procedure is unavailable or incompatible. No direct-DML fallback was run.",
                    source = "legacy-procedure-required",
                }
            );
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet creation rule rejected the entry request");
            return LegacyLogsheetConflict();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy logsheet insert workflow is unavailable");
            return LegacyLogsheetUnavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating logsheet entry");
            return StatusCode(500, "Error creating logsheet entry");
        }
    }

    /// <summary>
    /// Edit existing logsheet entry
    /// </summary>
    [HttpPut("edit/{id}")]
    public async Task<ActionResult<LogsheetEntryResultDto>> EditEntry(
        int id,
        [FromBody] LogsheetEntryDto request
    )
    {
        if (!HasLogsheetAccess() || !CanEditLegacyLogsheets())
            return Forbid();

        try
        {
            var validationError = ValidateEntry(request);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Logsheet entry with code {id} not found" });
            if (!await IsSiteAllowedAsync(existing.site_code))
                return Forbid();
            if (request.VmfCode != existing.vmf_code)
            {
                return BadRequest(
                    new { message = "The vehicle for an existing logsheet cannot be changed." }
                );
            }

            existing.start_odo = request.StartOdometer;
            existing.end_odo = request.EndOdometer;
            existing.month = request.Month;
            existing.site_code = request.SiteCode;
            existing.rek_num = request.RequisitionNumber;
            existing.days_used = request.DaysUsed;
            existing.bund_num = request.BundleNumber;

            var contractFailure = await ApplyLegacySelectedContractAsync(
                existing,
                request.ContractCode ?? existing.contract_code
            );
            if (contractFailure is not null)
                return contractFailure;

            var updated = await _repository.UpdateAsync(existing, GetCurrentUserId());

            var result = new LogsheetEntryResultDto
            {
                Success = true,
                // A posted legacy row is kept immutable by the INSTEAD OF
                // UPDATE trigger; the edited values are stored in a new child
                // logsheet. Return that leaf code so the caller does not keep
                // reopening the superseded parent row.
                LogCode = updated.log_code,
                Message = "Logsheet entry updated successfully",
            };
            return Ok(result);
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet update rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy logsheet update workflow is unavailable for {LogCode}", id);
            return LegacyLogsheetUnavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error editing logsheet entry: {Id}", id);
            return StatusCode(500, "Error editing logsheet entry");
        }
    }

    /// <summary>
    /// Delete logsheet entry
    /// </summary>
    [HttpDelete("entry/{id}")]
    public async Task<ActionResult> DeleteEntry(int id)
    {
        if (!HasLogsheetAccess() || !CanDeleteLegacyLogsheets())
            return Forbid();

        try
        {
            var logsheet = await _repository.GetByIdAsync(id);
            if (logsheet == null)
                return NotFound(new { message = $"Logsheet entry with code {id} not found" });
            if (!await IsSiteAllowedAsync(logsheet.site_code))
                return Forbid();

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return Ok(new { message = "Logsheet entry deleted successfully", id });
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet delete rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Legacy logsheet delete workflow is unavailable for {LogCode}", id);
            return LegacyLogsheetUnavailable();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting logsheet entry: {Id}", id);
            return StatusCode(500, "Error deleting logsheet entry");
        }
    }

    #endregion

    private static string? ValidateEntry(LogsheetEntryDto request)
    {
        if (request.VmfCode <= 0)
            return "A vehicle is required.";
        if (
            double.IsNaN(request.StartOdometer)
            || double.IsInfinity(request.StartOdometer)
            || request.StartOdometer < 0
        )
            return "Start odometer must be a non-negative number.";
        if (
            double.IsNaN(request.EndOdometer)
            || double.IsInfinity(request.EndOdometer)
            || request.EndOdometer < request.StartOdometer
        )
            return "End odometer must be greater than or equal to start odometer.";
        if (request.Month == default)
            return "A logsheet month is required.";
        if (
            string.IsNullOrWhiteSpace(request.RequisitionNumber)
            || request.RequisitionNumber.Length > 10
        )
            return "Requisition number is required and must be 10 characters or fewer.";
        if (request.DaysUsed is < 0)
            return "Days used cannot be negative.";
        if (request.BundleNumber is < 0)
            return "Batch number cannot be negative.";
        if (request.ContractCode is not > 0)
            return "Select the legacy contract that covers this logsheet entry.";
        return null;
    }

    private async Task<ActionResult?> ApplyLegacySelectedContractAsync(
        Logsheet logsheet,
        int? contractCode
    )
    {
        if (contractCode is not > 0)
        {
            return BadRequest(
                new
                {
                    error = "A selected legacy contract is required. The logsheet entry flow cannot infer or auto-select a contract.",
                }
            );
        }

        var contract = await _contractRepository.GetByIdAsync(contractCode.Value);
        if (contract is null)
            return BadRequest(new { error = "The selected contract was not found." });
        if (!await IsSiteAllowedAsync(contract.site_code))
            return Forbid();
        if (contract.vmf_code != logsheet.vmf_code)
            return BadRequest(
                new { error = "The selected contract does not belong to the selected vehicle." }
            );

        var entryDate = logsheet.month.Date;
        var endDate = contract.end_date?.Date;
        if (endDate == new DateTime(1900, 1, 1))
            endDate = DateTime.Today;
        if (entryDate < contract.start_date.Date || (endDate.HasValue && entryDate > endDate.Value))
        {
            return BadRequest(
                new { error = "The logsheet month must fall within the selected contract period." }
            );
        }

        // Log_Entry_ACT1(B).aspx derives both columns from the selected
        // contract/site record. Never accept a caller-supplied substitute.
        var departmentCode = contract.Site?.Depatrment_code;
        if (departmentCode is not > 0)
        {
            return Conflict(
                new { error = "The selected contract has no valid legacy site and department." }
            );
        }

        logsheet.contract_code = contract.contract_code;
        logsheet.site_code = contract.site_code;
        logsheet.department_code = departmentCode.Value;
        return null;
    }

    private bool CanEditLegacyLogsheets() =>
        HasGlobalLogsheetScope()
        || HasRole("Logsheets")
        // RPT_Editlogsform.aspx is exposed from the Reports branch of the
        // legacy menu and does not use the three-user restriction applied to
        // the older Log_Edit1.aspx path.
        || HasRole("Reports")
        || LegacyLogsheetManagerUserCodes.Contains(GetCurrentUserId());

    private bool CanDeleteLegacyLogsheets() =>
        HasGlobalLogsheetScope() || LegacyLogsheetManagerUserCodes.Contains(GetCurrentUserId());

    private async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalLogsheetScope())
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

    private async Task<bool> IsSiteAllowedAsync(short siteCode)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        return allowedSites is null || allowedSites.Contains(siteCode);
    }

    private static IEnumerable<Logsheet> FilterByAllowedSites(
        IEnumerable<Logsheet> records,
        IReadOnlySet<short>? allowedSites
    ) => allowedSites is null
        ? records
        : records.Where(item => allowedSites.Contains(item.site_code));

    private bool HasGlobalLogsheetScope() =>
        HasRole("Administrator")
        || HasRole("Admin")
        || HasRole("System Administrator")
        || HasRole("SystemAdministrator");

    private bool HasRole(string role) => User.Claims
        .Where(claim =>
            claim.Type == System.Security.Claims.ClaimTypes.Role
            || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
        )
        .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        .Any(value => string.Equals(value.Trim(), role, StringComparison.OrdinalIgnoreCase));

    private static bool IsLegacyLogsheetBusinessRule(SqlException exception)
    {
        if (exception.Number != 50000)
            return false;

        var message = exception.Message;
        return message.Contains("TRG_INS_LogsheetJournalDetailRecord", StringComparison.OrdinalIgnoreCase)
            || message.Contains("TRG_UPD_LogsheetJournalDetailRecord", StringComparison.OrdinalIgnoreCase)
            || message.Contains("TRG_UPD_LogsheetJounalDetailRecord", StringComparison.OrdinalIgnoreCase)
            || message.Contains("TRG_DEL_Logsheet", StringComparison.OrdinalIgnoreCase)
            || message.Contains("TRG_INS_UPD_Logsheet_Check_Overlap", StringComparison.OrdinalIgnoreCase)
            || message.Contains(
                "TRG_INS_UPD_Logsheet_CheckOverlappingOpenELsTrip",
                StringComparison.OrdinalIgnoreCase
            )
            || message.Contains("You are not allowed to delete logsheets", StringComparison.OrdinalIgnoreCase);
    }

    private ConflictObjectResult LegacyLogsheetConflict() =>
        Conflict(
            new
            {
                error = "The legacy database rejected this logsheet. Check the vehicle contract, site, date, requisition, and odometer range before retrying.",
            }
        );

    private ObjectResult LegacyLogsheetUnavailable() =>
        StatusCode(
            StatusCodes.Status503ServiceUnavailable,
            new
            {
                error = "The legacy logsheet procedure or trigger workflow is unavailable. No direct-DML fallback was run.",
                source = "legacy-procedure-required",
            }
        );

    private bool HasLogsheetAccess() =>
        HasGlobalLogsheetScope() || HasRole("Logsheets") || HasRole("Reports");

    #region Reports

    /// <summary>
    /// Get logsheet reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<LogsheetReportMenuDto> GetReportsMenu()
    {
        if (!HasLogsheetAccess())
            return Forbid();

        var menu = new LogsheetReportMenuDto
        {
            Reports = new List<string>
            {
                "One Vehicle",
                "One Requisition",
                "Department Period",
                "Captured",
                "Total KM per Class",
            },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Generate logsheet report for one vehicle
    /// </summary>
    [HttpPost("reports/one-vehicle")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportOneVehicle(
        [FromBody] LogsheetOneVehicleRequestDto request
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var vehicleLogsheets = await _repository.GetByVehicleAsync(request.VmfCode);
            var allowedSites = await ResolveAllowedSiteCodesAsync();

            var filteredLogsheets = FilterByAllowedSites(vehicleLogsheets, allowedSites)
                .Where(l => l.month >= request.StartDate && l.month <= request.EndDate)
                .OrderByDescending(l => l.month)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "OneVehicle",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
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
                "Error generating logsheet report for vehicle: {VmfCode}",
                request.VmfCode
            );
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate logsheet report for one requisition
    /// </summary>
    [HttpPost("reports/one-requisition")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportOneRequisition(
        [FromBody] LogsheetOneRequisitionRequestDto request
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.RequisitionNumber))
                return BadRequest(new { message = "Requisition number is required" });

            var allLogsheets = await _repository.GetAllAsync();
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var filteredLogsheets = FilterByAllowedSites(allLogsheets, allowedSites)
                .Where(l =>
                    l.rek_num != null
                    && l.rek_num.Equals(
                        request.RequisitionNumber,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                .OrderByDescending(l => l.month)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "OneRequisition",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
                GeneratedDate = DateTime.UtcNow,
            };
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error generating logsheet report for requisition: {RequisitionNumber}",
                request.RequisitionNumber
            );
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate logsheet report by department and period
    /// </summary>
    [HttpPost("reports/department-period")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportDepartmentPeriod(
        [FromBody] LogsheetDepartmentPeriodRequestDto request
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var allLogsheets = await _repository.GetAllAsync();
            var allowedSites = await ResolveAllowedSiteCodesAsync();

            // Legacy RPT_outstanding2 filters by the logsheet's department
            // column. Do not compare a department code with site_code.
            var filteredLogsheets = FilterByAllowedSites(allLogsheets, allowedSites)
                .Where(l => l.department_code == request.DepartmentCode)
                .Where(l => l.month >= request.StartDate && l.month <= request.EndDate)
                .OrderByDescending(l => l.month)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "DepartmentPeriod",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
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
                "Error generating logsheet report by department period: Department={DepartmentCode}, Start={StartDate}, End={EndDate}",
                request.DepartmentCode,
                request.StartDate,
                request.EndDate
            );
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate captured logsheets report
    /// </summary>
    [HttpPost("reports/captured")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportCaptured(
        [FromBody] LogsheetCapturedRequestDto request
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var allLogsheets = await _repository.GetAllAsync();
            var allowedSites = await ResolveAllowedSiteCodesAsync();

            // Filter by date range (captured in this period)
            var filteredLogsheets = FilterByAllowedSites(allLogsheets, allowedSites)
                .Where(l =>
                    l.date_created >= request.StartDate && l.date_created <= request.EndDate
                )
                .OrderByDescending(l => l.date_created)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "Captured",
                Data = filteredLogsheets.Cast<object>().ToList(),
                RecordCount = filteredLogsheets.Count,
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
                "Error generating captured logsheets report: Start={StartDate}, End={EndDate}",
                request.StartDate,
                request.EndDate
            );
            return StatusCode(500, "Error generating report");
        }
    }

    /// <summary>
    /// Generate total kilometers per class code report
    /// </summary>
    [HttpPost("reports/total-km-per-class-code")]
    public async Task<ActionResult<LogsheetReportDto>> GetReportTotalKmPerClass(
        [FromBody] LogsheetKmPerClassRequestDto request
    )
    {
        if (!HasLogsheetAccess())
            return Forbid();

        try
        {
            var allLogsheets = await _repository.GetAllAsync();
            var allowedSites = await ResolveAllowedSiteCodesAsync();

            // Filter by date range and calculate total km per class code
            var filteredLogsheets = FilterByAllowedSites(allLogsheets, allowedSites)
                .Where(l => l.month >= request.StartDate && l.month <= request.EndDate)
                .Where(l => l.Vehicle != null) // Ensure vehicle navigation property is loaded
                .ToList();

            // Group by vehicle type code and sum kilometers
            var kmByClassCode = filteredLogsheets
                .GroupBy(l => l.Vehicle?.type_code ?? 0)
                .Select(g => new
                {
                    ClassCode = g.Key,
                    TotalKilometers = g.Sum(l => l.end_odo - l.start_odo),
                    VehicleCount = g.Select(l => l.vmf_code).Distinct().Count(),
                    RecordCount = g.Count(),
                })
                .OrderByDescending(x => x.TotalKilometers)
                .ToList();

            var report = new LogsheetReportDto
            {
                ReportType = "TotalKmPerClass",
                Data = kmByClassCode.Cast<object>().ToList(),
                RecordCount = kmByClassCode.Count,
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
                "Error generating total km per class code report: Start={StartDate}, End={EndDate}",
                request.StartDate,
                request.EndDate
            );
            return StatusCode(500, "Error generating report");
        }
    }

    #endregion
}

#region Logsheet DTOs
public class LogsheetMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class LogsheetHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public class LogsheetEntryDto
{
    public int VmfCode { get; set; }
    public double StartOdometer { get; set; }
    public double EndOdometer { get; set; }
    public DateTime Month { get; set; }
    public short SiteCode { get; set; }
    public string? RequisitionNumber { get; set; }
    public int? DaysUsed { get; set; }
    public int? BundleNumber { get; set; }
    public int? ContractCode { get; set; }
}

public class LogsheetEntryResultDto
{
    public bool Success { get; set; }
    public int LogCode { get; set; }
    public string Message { get; set; } = "";
}

public class LogsheetContractOptionDto
{
    public int ContractCode { get; set; }
    public short SiteCode { get; set; }
    public string? SiteDescription { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class LogsheetReportMenuDto
{
    public List<string> Reports { get; set; } = new();
}

public class LogsheetOneVehicleRequestDto
{
    public int VmfCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class LogsheetOneRequisitionRequestDto
{
    public string RequisitionNumber { get; set; } = "";
}

public class LogsheetDepartmentPeriodRequestDto
{
    public int DepartmentCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class LogsheetCapturedRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class LogsheetKmPerClassRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class LogsheetReportDto
{
    public string ReportType { get; set; } = "";
    public List<object> Data { get; set; } = new();
    public int RecordCount { get; set; }
    public DateTime GeneratedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
#endregion
