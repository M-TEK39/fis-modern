using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

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

    public AccidentController(IAccidentRepository repository, FisDbContext context, ILogger<AccidentController> logger)
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

    [HttpGet("types")]
    public async Task<ActionResult<IEnumerable<Dictionary<string, string>>>> GetAccidentTypes()
    {
        try
        {
            var rows = await ExecuteReportQueryAsync("""
                SELECT [acc_type_code], [acc_type_description]
                FROM [dbo].[acc_type]
                ORDER BY [acc_type_code]
                """);
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

    [HttpGet("reports/all")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetAllAccidentsReport(
        [FromQuery] string mode = "2002-current")
    {
        var normalizedMode = mode.Trim().ToLowerInvariant() switch
        {
            "2002-current" or "radionou" or "current" => "2002-current",
            "1999-2001" or "radioou" or "middle" => "1999-2001",
            "before-1999" or "radiobou" or "before" => "before-1999",
            _ => string.Empty
        };
        if (normalizedMode.Length == 0)
        {
            return BadRequest(new { error = "Mode must be 2002-current, 1999-2001, or before-1999." });
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
        [FromQuery] string mode = "jhb")
    {
        var normalizedMode = mode.Trim().ToLowerInvariant() switch
        {
            "jhb" or "radiojhb" => "jhb",
            "pta" or "radiopta" => "pta",
            "all" or "radioall" => "all",
            _ => string.Empty
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
        [FromQuery] string mode = "name")
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
        [FromQuery] string mode = "registration")
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

    [HttpGet("reports/private-vehicle")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetPrivateVehicleReport(
        [FromQuery] string searchTerm,
        [FromQuery] string mode = "third-party")
    {
        var normalizedMode = mode.Trim().ToLowerInvariant();
        var searchByDescription = normalizedMode is "description" or "capture-description" or "private-description";
        var validThirdPartyMode = normalizedMode is "third-party" or "thirdparty" or "regno" or "private";
        if (!searchByDescription && !validThirdPartyMode)
        {
            return BadRequest(new { error = "Mode must be third-party or description." });
        }

        try
        {
            return Ok(await _repository.GetPrivateVehicleReportAsync(searchTerm, searchByDescription));
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
        [FromQuery] string status = "open")
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
            return Ok(await _repository.GetPeriodReportAsync(
                departmentNumber ?? string.Empty,
                startDate,
                endDate,
                normalizedStatus is "closed" or "close"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident period report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/department-period")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetDepartmentPeriodReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (endDate < startDate)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        try
        {
            return Ok(await _repository.GetDepartmentPeriodReportAsync(
                departmentNumber ?? string.Empty,
                startDate,
                endDate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident department period report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/department-period-vip")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetDepartmentPeriodVipReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string mode = "all")
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
            _ => string.Empty
        };
        if (normalizedMode.Length == 0)
        {
            return BadRequest(new { error = "Mode must be all, vip, pool, or permanent." });
        }

        try
        {
            return Ok(await _repository.GetDepartmentPeriodVipReportAsync(
                departmentNumber ?? string.Empty,
                startDate,
                endDate,
                normalizedMode));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accident department period hire-type report");
            return StatusCode(500);
        }
    }

    [HttpGet("reports/period-status")]
    public async Task<ActionResult<IEnumerable<Dictionary<string, string>>>> GetPeriodStatusReport(
        [FromQuery] string? departmentNumber,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string status = "open")
    {
        if (endDate < startDate)
        {
            return BadRequest(new { error = "End date must be on or after start date." });
        }

        var sql = status.Equals("close", StringComparison.OrdinalIgnoreCase)
            ? @"
SELECT
    ISNULL(vm.registration_number, '') AS registration_number,
    ISNULL(vm.fleet_number, '') AS fleet_number,
    CASE
        WHEN vm.location_code = 1 THEN 'JHB'
        WHEN vm.location_code = 2 THEN 'PTA'
        ELSE ''
    END AS garage,
    ISNULL(CONVERT(varchar(10), a.occurence_date, 120), '') AS occurence_date,
    ISNULL(s.Department_number, '') AS department_number,
    ISNULL(s.description, '') AS site_description,
    ISNULL(t.type_description, '') AS hire_type,
    ISNULL(at.acc_type_description, '') AS accident_type_description,
    ISNULL(a.driver_name, '') AS driver_name,
    ISNULL(a.transoffic_name, '') AS transoffic_name,
    ISNULL(a.Call_Refer, '') AS call_refer,
    ISNULL(CONVERT(varchar(32), a.cost_of_repair), '') AS cost_of_repair,
    ISNULL(CONVERT(varchar(10), a.file_close_date, 120), '') AS file_close_date
FROM accident a
INNER JOIN vehicle_master vm ON vm.vmf_code = a.vmf_code
LEFT JOIN site s ON s.site_code = a.driver_site_code
LEFT JOIN type t ON t.type_code = vm.type_code
LEFT JOIN acc_type at ON at.acc_type_code = a.acc_type_code
WHERE ISNULL(a.is_deleted, 0) = 0
  AND a.occurence_date >= @startDate
  AND a.occurence_date <= @endDate
  AND a.file_close_date IS NOT NULL
  AND (@departmentNumber = '' OR s.Department_number LIKE '%' + @departmentNumber + '%')
ORDER BY s.Department_number, vm.fleet_number"
            : @"
SELECT
    ISNULL(vm.registration_number, '') AS registration_number,
    ISNULL(vm.fleet_number, '') AS fleet_number,
    ISNULL(CONVERT(varchar(10), a.occurence_date, 120), '') AS occurence_date,
    ISNULL(s.Department_number, '') AS department_number,
    ISNULL(s.description, '') AS site_description,
    ISNULL(t.type_description, '') AS hire_type,
    ISNULL(at.acc_type_description, '') AS accident_type_description,
    ISNULL(a.driver_name, '') AS driver_name,
    ISNULL(a.transoffic_name, '') AS transoffic_name,
    ISNULL(a.Call_Refer, '') AS call_refer,
    ISNULL(CONVERT(varchar(32), a.cost_of_repair), '') AS cost_of_repair,
    ISNULL(CONVERT(varchar(10), a.file_close_date, 120), '') AS file_close_date
FROM accident a
INNER JOIN vehicle_master vm ON vm.vmf_code = a.vmf_code
LEFT JOIN site s ON s.site_code = a.driver_site_code
LEFT JOIN type t ON t.type_code = vm.type_code
LEFT JOIN acc_type at ON at.acc_type_code = a.acc_type_code
WHERE ISNULL(a.is_deleted, 0) = 0
  AND a.occurence_date >= @startDate
  AND a.occurence_date <= @endDate
  AND a.file_close_date IS NULL
  AND (@departmentNumber = '' OR s.Department_number LIKE '%' + @departmentNumber + '%')
ORDER BY s.Department_number, vm.fleet_number";

        var rows = await ExecuteReportQueryAsync(sql, ("@departmentNumber", departmentNumber ?? string.Empty), ("@startDate", startDate.Date), ("@endDate", endDate.Date));
        return Ok(rows);
    }

    [HttpGet("reports/duplicates")]
    public async Task<ActionResult<IEnumerable<Dictionary<string, string>>>> GetDuplicateAccidentsReport([FromQuery] string garage = "all")
    {
        var sql = @"
WITH ordered AS
(
    SELECT
        a.accident_code,
        a.vmf_code,
        ISNULL(vm.fleet_number, '') AS fleet_number,
        ISNULL(vm.registration_number, '') AS registration_number,
        ISNULL(CONVERT(varchar(10), a.occurence_date, 120), '') AS occurence_date,
        ISNULL(CONVERT(varchar(5), a.occurence_time, 108), '') AS occurence_time,
        ISNULL(a.occurence_place, '') AS occurence_place,
        ISNULL(a.fin_year, '') AS fin_year,
        ISNULL(a.description, '') AS description,
        ISNULL(a.trip_author, '') AS trip_author,
        ISNULL(a.driver_name, '') AS driver_name,
        ISNULL(a.driver_employ_number, '') AS driver_employ_number,
        ISNULL(s.Department_number, '') AS department_number,
        ISNULL(a.transoffic_name, '') AS transoffic_name,
        ISNULL(a.transoffic_tel, '') AS transoffic_tel,
        ISNULL(a.hq_reference, '') AS hq_reference,
        ISNULL(a.gg_reference, '') AS gg_reference,
        ISNULL(a.case_number, '') AS case_number,
        ISNULL(CONVERT(varchar(32), a.cost_of_repair), '') AS cost_of_repair,
        ISNULL(a.driver_fault, '') AS driver_fault,
        ISNULL(CONVERT(varchar(16), a.death), '') AS death,
        ISNULL(CONVERT(varchar(16), a.injured), '') AS injured,
        ISNULL(a.third_party_regno, '') AS third_party_regno,
        ISNULL(a.third_party_owner, '') AS third_party_owner,
        ISNULL(CONVERT(varchar(32), a.third_party_claim), '') AS third_party_claim,
        ISNULL(CONVERT(varchar(32), a.claim_against_dept), '') AS claim_against_dept,
        ISNULL(CONVERT(varchar(10), a.file_close_date, 120), '') AS file_close_date,
        ISNULL(a.notes, '') AS notes
    FROM accident a
    INNER JOIN vehicle_master vm ON vm.vmf_code = a.vmf_code
    LEFT JOIN site s ON s.site_code = a.driver_site_code
    WHERE ISNULL(a.is_deleted, 0) = 0
      AND (
            @garage = 'all'
            OR (@garage = 'jhb' AND vm.location_code = 1)
            OR (@garage = 'pta' AND vm.location_code = 2)
      )
),
dups AS
(
    SELECT vmf_code, occurence_date
    FROM ordered
    GROUP BY vmf_code, occurence_date
    HAVING COUNT(*) > 1
)
SELECT o.*
FROM ordered o
INNER JOIN dups d ON d.vmf_code = o.vmf_code AND d.occurence_date = o.occurence_date
ORDER BY o.fleet_number, o.occurence_date, o.accident_code";

        var rows = await ExecuteReportQueryAsync(sql, ("@garage", garage.Trim().ToLowerInvariant()));
        return Ok(rows);
    }

    [HttpGet("reports/new-accidents")]
    public async Task<ActionResult<IEnumerable<AccidentVehicleReportRow>>> GetNewAccidentsReport([FromQuery] string mode = "all")
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100", Justification = "SQL text is defined in controller constants and user inputs are parameterized.")]
    private async Task<List<Dictionary<string, string>>> ExecuteReportQueryAsync(string sql, params (string name, object value)[] parameters)
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
                        value = Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
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
