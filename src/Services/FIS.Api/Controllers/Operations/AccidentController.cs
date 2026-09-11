using System.Data;
using System.Globalization;
using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/accidents")]
[Authorize]
public class AccidentController : BaseApiController
{
    private readonly IAccidentRepository _repository;
    private readonly FisDbContext _context;
    private readonly ILogger<AccidentController> _logger;

    public AccidentController(
        IAccidentRepository repository,
        FisDbContext context,
        ILogger<AccidentController> logger
    )
    {
        _repository = repository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Accident>>> GetAll()
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

    /// <summary>
    /// Paged operational maintenance list. Legacy reports keep their own
    /// complete-result endpoints so that print and export behavior is unchanged.
    /// </summary>
    [HttpGet("maintenance")]
    public async Task<IActionResult> GetMaintenancePage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string searchType = "GP",
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? locationCode = null
    )
    {
        if (!HasAccidentRole())
        {
            return Forbid();
        }

        var normalizedSearchType = searchType.Trim().ToUpperInvariant();
        if (normalizedSearchType is not ("GG" or "GP"))
        {
            return BadRequest(new { error = "Search type must be GG or GP." });
        }

        try
        {
            var result = await _repository.GetMaintenancePageAsync(
                new AccidentMaintenancePageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    normalizedSearchType,
                    searchTerm ?? string.Empty,
                    locationCode
                )
            );
            return Ok(
                new
                {
                    data = result.Data,
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalRecords = result.TotalRecords,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged accident maintenance records");
            return StatusCode(
                500,
                "An error occurred while retrieving accident maintenance records"
            );
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Accident>> GetById(int id)
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

    private bool HasAccidentRole()
    {
        if (User.IsInRole("Accidents"))
        {
            return true;
        }

        return User.Claims.Any(claim =>
            (
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            && claim
                .Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
                .Any(role => string.Equals(role, "Accidents", StringComparison.OrdinalIgnoreCase))
        );
    }

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<Dictionary<string, string>>>> GetAccidentTypes()
    {
        try
        {
            var rows = await ExecuteReportQueryAsync(
                """
                SELECT [acc_type_code], [acc_type_description]
                FROM [dbo].[acc_type]
                ORDER BY [acc_type_code]
                """
            );
            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident types");
            return StatusCode(500);
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Accident>>> GetByVehicle(int vmfCode)
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

    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<Accident>>> GetByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            return Ok(await _repository.GetByDateRangeAsync(startDate, endDate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("claims-summary")]
    public async Task<ActionResult<AccidentClaimsSummary>> GetClaimsSummary()
    {
        try
        {
            return Ok(await _repository.GetClaimsSummaryAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("reports")]
    public async Task<ActionResult<IEnumerable<AccidentReport>>> GetRecentReports()
    {
        try
        {
            return Ok(await _repository.GetRecentReportsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/last-gg-reference")]
    public async Task<
        ActionResult<IEnumerable<AccidentLastGgReferenceRow>>
    > GetLastGgReferenceReport()
    {
        try
        {
            return Ok(await _repository.GetLastGgReferenceReportAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving last GG reference report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/all")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetAllAccidentsReport(
        [FromQuery] string mode = "2002-current"
    )
    {
        var normalizedMode = mode.Trim().ToLowerInvariant() switch
        {
            "2002-current" or "radionou" or "current" => "2002-current",
            "1999-2001" or "radioou" or "middle" => "1999-2001",
            "before-1999" or "radiobou" or "before" => "before-1999",
            _ => string.Empty,
        };
        if (normalizedMode.Length == 0)
        {
            return BadRequest(
                new { error = "Mode must be 2002-current, 1999-2001, or before-1999." }
            );
        }

        try
        {
            return Ok(await _repository.GetAllAccidentsReportAsync(normalizedMode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all accident report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/garage-detail")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetGarageAccidentsReport(
        [FromQuery] string mode = "jhb"
    )
    {
        var normalizedMode = mode.Trim().ToLowerInvariant() switch
        {
            "jhb" or "radiojhb" => "jhb",
            "pta" or "radiopta" => "pta",
            "all" or "radioall" => "all",
            _ => string.Empty,
        };
        if (normalizedMode.Length == 0)
        {
            return BadRequest(new { error = "Mode must be jhb, pta, or all." });
        }

        try
        {
            return Ok(await _repository.GetGarageAccidentsReportAsync(normalizedMode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving garage accident report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/driver")]
    public async Task<ActionResult<IEnumerable<AccidentDriverReportRow>>> GetDriverReport(
        [FromQuery] string searchTerm,
        [FromQuery] string mode = "name"
    )
    {
        var searchById = mode.Equals("id", StringComparison.OrdinalIgnoreCase);
        if (!searchById && !mode.Equals("name", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Mode must be name or id." });
        }

        try
        {
            return Ok(await _repository.GetDriverReportAsync(searchTerm, searchById));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident driver report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/vehicle")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetVehicleReport(
        [FromQuery] string searchTerm,
        [FromQuery] string mode = "registration"
    )
    {
        var normalizedMode = mode.Trim().ToLowerInvariant();
        var searchByFleet = normalizedMode is "fleet" or "gg" or "radiogg";
        var validRegistrationMode = normalizedMode is "registration" or "gp" or "radiogp";
        if (!searchByFleet && !validRegistrationMode)
        {
            return BadRequest(new { error = "Mode must be registration or fleet." });
        }

        try
        {
            return Ok(await _repository.GetVehicleReportAsync(searchTerm, searchByFleet));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident vehicle report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/outstanding-documents")]
    public async Task<
        ActionResult<IEnumerable<AccidentOutstandingDocumentLookupRow>>
    > GetOutstandingDocumentLookup(
        [FromQuery] string searchTerm,
        [FromQuery] string mode = "registration"
    )
    {
        if (searchTerm?.Trim().Length > 8)
        {
            return BadRequest(new { error = "Vehicle number must be 8 characters or fewer." });
        }

        var normalizedMode = mode.Trim().ToLowerInvariant();
        var searchByFleet = normalizedMode is "fleet" or "gg" or "radiogg";
        var validRegistrationMode = normalizedMode is "registration" or "gp" or "radiogp";
        if (!searchByFleet && !validRegistrationMode)
        {
            return BadRequest(new { error = "Mode must be registration or fleet." });
        }

        try
        {
            return Ok(
                await _repository.GetOutstandingDocumentLookupAsync(
                    searchTerm ?? string.Empty,
                    searchByFleet
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving outstanding accident document lookup");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/outstanding-documents/{accidentCode:int}")]
    public async Task<ActionResult<AccidentOutstandingDocumentReport>> GetOutstandingDocumentReport(
        int accidentCode
    )
    {
        if (accidentCode <= 0)
        {
            return BadRequest(new { error = "Accident code must be a positive integer." });
        }

        try
        {
            var report = await _repository.GetOutstandingDocumentReportAsync(accidentCode);
            return report is null ? NotFound() : Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving outstanding accident document report for {AccidentCode}",
                accidentCode
            );
            return StatusCode(500);
        }
    }

    [HttpGet("reports/inspection-letter")]
    public async Task<
        ActionResult<IEnumerable<AccidentOutstandingDocumentLookupRow>>
    > GetInspectionLetterLookup(
        [FromQuery] string searchTerm,
        [FromQuery] string mode = "registration"
    )
    {
        if (searchTerm?.Trim().Length > 8)
        {
            return BadRequest(new { error = "Vehicle number must be 8 characters or fewer." });
        }

        var normalizedMode = mode.Trim().ToLowerInvariant();
        var searchByFleet = normalizedMode is "fleet" or "gg" or "radiogg";
        var validRegistrationMode = normalizedMode is "registration" or "gp" or "radiogp";
        if (!searchByFleet && !validRegistrationMode)
        {
            return BadRequest(new { error = "Mode must be registration or fleet." });
        }

        try
        {
            return Ok(
                await _repository.GetInspectionLetterLookupAsync(
                    searchTerm ?? string.Empty,
                    searchByFleet
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inspection letter lookup");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/inspection-letter/{accidentCode:int}")]
    public async Task<ActionResult<AccidentOutstandingDocumentReport>> GetInspectionLetterReport(
        int accidentCode
    )
    {
        if (accidentCode <= 0)
        {
            return BadRequest(new { error = "Accident code must be a positive integer." });
        }

        try
        {
            var report = await _repository.GetInspectionLetterReportAsync(accidentCode);
            return report is null ? NotFound() : Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving inspection letter for {AccidentCode}",
                accidentCode
            );
            return StatusCode(500);
        }
    }

    [HttpGet("reports/private-vehicle")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetPrivateVehicleReport(
        [FromQuery] string searchTerm,
        [FromQuery] string mode = "third-party"
    )
    {
        var normalizedMode = mode.Trim().ToLowerInvariant();
        var searchByDescription =
            normalizedMode is "description" or "capture-description" or "private-description";
        var validThirdPartyMode =
            normalizedMode is "third-party" or "thirdparty" or "regno" or "private";
        if (!searchByDescription && !validThirdPartyMode)
        {
            return BadRequest(new { error = "Mode must be third-party or description." });
        }

        try
        {
            return Ok(
                await _repository.GetPrivateVehicleReportAsync(searchTerm, searchByDescription)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private vehicle accident report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/period")]
    public async Task<ActionResult<IEnumerable<AccidentPeriodReportRow>>> GetPeriodReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string status = "open"
    )
    {
        if (endDate < startDate)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        var normalizedStatus = status.Trim().ToLowerInvariant();
        if (normalizedStatus is not ("open" or "closed" or "close"))
        {
            return BadRequest(new { error = "Status must be open or closed." });
        }

        try
        {
            return Ok(
                await _repository.GetPeriodReportAsync(
                    departmentNumber ?? string.Empty,
                    startDate,
                    endDate,
                    normalizedStatus is "closed" or "close"
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident period report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/department-period")]
    public async Task<
        ActionResult<IEnumerable<AccidentVehicleReportRow>>
    > GetDepartmentPeriodReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        if (endDate < startDate)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        try
        {
            return Ok(
                await _repository.GetDepartmentPeriodReportAsync(
                    departmentNumber ?? string.Empty,
                    startDate,
                    endDate
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident department period report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/department-period-vip")]
    public async Task<
        ActionResult<IEnumerable<AccidentVehicleReportRow>>
    > GetDepartmentPeriodVipReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string mode = "all"
    )
    {
        if (endDate < startDate)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        var normalizedMode = mode.Trim().ToLowerInvariant() switch
        {
            "all" or "radioall" => "all",
            "vip" or "radiovip" => "vip",
            "pool" or "radiopool" => "pool",
            "permanent" or "radioperm" => "permanent",
            _ => string.Empty,
        };
        if (normalizedMode.Length == 0)
        {
            return BadRequest(new { error = "Mode must be all, vip, pool, or permanent." });
        }

        try
        {
            return Ok(
                await _repository.GetDepartmentPeriodVipReportAsync(
                    departmentNumber ?? string.Empty,
                    startDate,
                    endDate,
                    normalizedMode
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident department period hire-type report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/department-month")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetDepartmentMonthReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] string garage = "jhb",
        [FromQuery] string period = "month",
        [FromQuery] int? year = null,
        [FromQuery] int? month = null
    )
    {
        var normalizedGarage = garage.Trim().ToLowerInvariant() switch
        {
            "jhb" or "radiojhb" => "jhb",
            "pta" or "radiopta" => "pta",
            "all" or "radioall" => "all",
            _ => string.Empty,
        };
        if (normalizedGarage.Length == 0)
        {
            return BadRequest(new { error = "Garage must be jhb, pta, or all." });
        }

        var normalizedPeriod = period.Trim().ToLowerInvariant() switch
        {
            "month" or "radiomon" => "month",
            "year" or "radioyear" => "year",
            "02/03" or "radiof23" => "02/03",
            "01/02" or "radioy12" => "01/02",
            _ => string.Empty,
        };
        if (normalizedPeriod.Length == 0)
        {
            return BadRequest(new { error = "Period must be month, year, 02/03, or 01/02." });
        }

        if (
            normalizedPeriod is "month" or "year"
            && (!year.HasValue || year.Value is < 1 or > 9999)
        )
        {
            return BadRequest(
                new { error = "Year must be between 1 and 9999 for the selected period." }
            );
        }

        if (normalizedPeriod == "month" && (!month.HasValue || month.Value is < 1 or > 12))
        {
            return BadRequest(
                new { error = "Month must be between 1 and 12 for a monthly report." }
            );
        }

        try
        {
            return Ok(
                await _repository.GetDepartmentMonthReportAsync(
                    departmentNumber ?? string.Empty,
                    normalizedGarage,
                    normalizedPeriod,
                    year,
                    month
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident department month report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/department-finyear")]
    public async Task<
        ActionResult<IEnumerable<AccidentVehicleReportRow>>
    > GetDepartmentFinancialYearReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] string? financialYear,
        [FromQuery] string garage = "jhb"
    )
    {
        var normalizedGarage = garage.Trim().ToLowerInvariant() switch
        {
            "jhb" or "radiojhb" => "jhb",
            "pta" or "radiopta" => "pta",
            "all" or "radioall" => "all",
            _ => string.Empty,
        };
        if (normalizedGarage.Length == 0)
        {
            return BadRequest(new { error = "Garage must be jhb, pta, or all." });
        }

        var normalizedFinancialYear = financialYear?.Trim() ?? string.Empty;
        if (normalizedFinancialYear.Length == 0 || normalizedFinancialYear.Length > 5)
        {
            return BadRequest(
                new { error = "Book / Financial Year must be between 1 and 5 characters." }
            );
        }

        try
        {
            return Ok(
                await _repository.GetDepartmentFinancialYearReportAsync(
                    departmentNumber ?? string.Empty,
                    normalizedGarage,
                    normalizedFinancialYear
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident department financial year report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/accident-costs-finyear")]
    public async Task<
        ActionResult<IEnumerable<AccidentVehicleReportRow>>
    > GetAccidentCostsFinancialYearReport([FromQuery] string? financialYear)
    {
        var normalizedFinancialYear = financialYear?.Trim() ?? string.Empty;
        if (normalizedFinancialYear.Length == 0 || normalizedFinancialYear.Length > 5)
        {
            return BadRequest(new { error = "Financial Year must be between 1 and 5 characters." });
        }

        try
        {
            return Ok(
                await _repository.GetAccidentCostsFinancialYearReportAsync(normalizedFinancialYear)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident costs financial year report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/duplicates")]
    public async Task<
        ActionResult<IEnumerable<AccidentVehicleReportRow>>
    > GetDuplicateAccidentsReport([FromQuery] string garage = "all")
    {
        var normalizedGarage = garage.Trim().ToLowerInvariant() switch
        {
            "jhb" or "radiojhb" => "jhb",
            "pta" or "radiopta" => "pta",
            "all" or "radioall" => "all",
            _ => string.Empty,
        };
        if (normalizedGarage.Length == 0)
        {
            return BadRequest(new { error = "Garage must be jhb, pta, or all." });
        }

        try
        {
            return Ok(await _repository.GetDuplicateAccidentsReportAsync(normalizedGarage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving duplicate accident report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/new-accidents")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetNewAccidentsReport(
        [FromQuery] string mode = "all"
    )
    {
        var normalizedMode = mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("all" or "call" or "garage" or "confirm"))
        {
            return BadRequest(new { error = "Mode must be all, call, garage, or confirm." });
        }

        try
        {
            return Ok(await _repository.GetNewAccidentsReportAsync(normalizedMode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving new accident report");
            return StatusCode(500);
        }
    }

    [HttpGet("outstanding-claims")]
    public async Task<ActionResult<IEnumerable<AccidentOutstandingClaim>>> GetOutstandingClaims()
    {
        try
        {
            return Ok(await _repository.GetOutstandingClaimsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<AccidentStatistics>> GetStatistics(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null
    )
    {
        try
        {
            return Ok(await _repository.GetStatisticsAsync(fromDate, toDate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Accident>> Create([FromBody] Accident item)
    {
        try
        {
            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.accident_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Accident>> Update(int id, [FromBody] Accident item)
    {
        try
        {
            if (id != item.accident_code)
                return BadRequest();
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("hq/{id}")]
    public async Task<ActionResult<Accident>> UpdateHq(int id, [FromBody] Accident item)
    {
        try
        {
            if (id != item.accident_code)
                return BadRequest();
            return Ok(await _repository.UpdateHqAsync(item, GetCurrentUserId()));
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100",
        Justification = "SQL text is defined in controller constants and user inputs are parameterized."
    )]
    private async Task<List<Dictionary<string, string>>> ExecuteReportQueryAsync(
        string sql,
        params (string name, object value)[] parameters
    )
    {
        var result = new List<Dictionary<string, string>>();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            foreach (var parameter in parameters)
            {
                var dbParam = command.CreateParameter();
                dbParam.ParameterName = parameter.name;
                dbParam.Value = parameter.value ?? DBNull.Value;
                command.Parameters.Add(dbParam);
            }

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var key = reader.GetName(i);
                    string value;
                    if (reader.IsDBNull(i))
                    {
                        value = string.Empty;
                    }
                    else
                    {
                        value =
                            Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)
                            ?? string.Empty;
                    }
                    row[key] = value;
                }

                result.Add(row);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }
}
