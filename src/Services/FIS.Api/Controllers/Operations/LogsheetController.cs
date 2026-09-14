using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize(Roles = "Reports")]
[Route("api/[controller]")]
public class LogsheetController : BaseApiController
{
    private static readonly int[] LegacyLogsheetManagerUserCodes = [279, 47, 38];

    private readonly ILogsheetRepository _repository;
    private readonly IContractRepository _contractRepository;
    private readonly ILogger<LogsheetController> _logger;

    public LogsheetController(
        ILogsheetRepository repository,
        IContractRepository contractRepository,
        ILogger<LogsheetController> logger
    )
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Logsheet>>> GetAll()
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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] int? vmfCode = null,
        [FromQuery] string? requisition = null
    )
    {
        try
        {
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

    [HttpGet("vehicle/{vmfCode}/contracts")]
    public async Task<ActionResult<IEnumerable<LogsheetContractOptionDto>>> GetVehicleContracts(
        int vmfCode
    )
    {
        if (vmfCode <= 0)
            return BadRequest(new { error = "A vehicle is required." });

        try
        {
            // Log_Entry_B2.aspx lists the vehicle's historical contract
            // choices. The entry action subsequently verifies the selected
            // period; it never auto-selects a contract.
            var contracts = await _contractRepository.GetContractsByVehicleAsync(vmfCode);
            return Ok(
                contracts
                    .Where(contract => contract.start_date.Date < DateTime.Today)
                    .OrderByDescending(contract => contract.start_date)
                    .ThenByDescending(contract => contract.contract_code)
                    .Select(contract =>
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
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet creation rule rejected the request");
            return LegacyLogsheetConflict();
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
        if (!CanManageLegacyLogsheets())
            return Forbid();

        try
        {
            if (id != item.log_code)
                return BadRequest();
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!CanManageLegacyLogsheets())
            return Forbid();

        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet delete rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
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
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet creation rule rejected the entry request");
            return LegacyLogsheetConflict();
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
        if (!CanManageLegacyLogsheets())
            return Forbid();

        try
        {
            var validationError = ValidateEntry(request);
            if (validationError != null)
                return BadRequest(new { message = validationError });

            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = $"Logsheet entry with code {id} not found" });

            existing.vmf_code = request.VmfCode;
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
                LogCode = id,
                Message = "Logsheet entry updated successfully",
            };
            return Ok(result);
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet update rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
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
        if (!CanManageLegacyLogsheets())
            return Forbid();

        try
        {
            var logsheet = await _repository.GetByIdAsync(id);
            if (logsheet == null)
                return NotFound(new { message = $"Logsheet entry with code {id} not found" });

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return Ok(new { message = "Logsheet entry deleted successfully", id });
        }
        catch (SqlException ex) when (IsLegacyLogsheetBusinessRule(ex))
        {
            _logger.LogInformation(ex, "Legacy logsheet delete rule rejected logsheet {LogCode}", id);
            return LegacyLogsheetConflict();
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

    private bool CanManageLegacyLogsheets() =>
        LegacyLogsheetManagerUserCodes.Contains(GetCurrentUserId());

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

    #region Reports

    /// <summary>
    /// Get logsheet reports menu
    /// </summary>
    [HttpGet("reports/menu")]
    public ActionResult<LogsheetReportMenuDto> GetReportsMenu()
    {
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
        try
        {
            var vehicleLogsheets = await _repository.GetByVehicleAsync(request.VmfCode);

            var filteredLogsheets = vehicleLogsheets
                .Where(l => !l.is_deleted)
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
        try
        {
            if (string.IsNullOrWhiteSpace(request.RequisitionNumber))
                return BadRequest(new { message = "Requisition number is required" });

            var allLogsheets = await _repository.GetAllAsync();
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
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
        try
        {
            var allLogsheets = await _repository.GetAllAsync();

            // Filter by site code (department) and date range
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
                .Where(l => l.site_code == request.DepartmentCode)
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
        try
        {
            var allLogsheets = await _repository.GetAllAsync();

            // Filter by date range (captured in this period)
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
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
        try
        {
            var allLogsheets = await _repository.GetAllAsync();

            // Filter by date range and calculate total km per class code
            var filteredLogsheets = allLogsheets
                .Where(l => !l.is_deleted)
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
